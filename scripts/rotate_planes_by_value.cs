using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

// Rotate each plane around a chosen local axis by an angle that is remapped
// from a per-plane value, with an optional 3-point gradient across branches.
//
// axis input (wire a Value List):
//   0 = X axis (pitch)
//   1 = Y axis (roll)
//   2 = Z axis (yaw / spin around tool)
//
// Mapping (per plane):
//   value_min -> angle_min_deg
//   value_max -> angle_max_deg
//   linear between
//
// Branch gradient (3-point, piecewise linear):
//   first branch       -> grad_start
//   middle branch      -> grad_mid
//   last branch        -> grad_end
// Final angle = remapped_angle * branch_gradient_factor
//
// Examples:
//   start=0, mid=0.5, end=1     -> simple ramp (off to full)
//   start=1, mid=0,   end=-1    -> V-shape, sign flip at middle (your case)
//   start=1, mid=1,   end=1     -> constant full (gradient disabled)

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    DataTree<Plane> planes,
    DataTree<double> values,
    int axis,
    double angle_min_deg,
    double angle_max_deg,
    bool auto_range,
    double value_min,
    double value_max,
    double grad_start,
    double grad_mid,
    double grad_end,
    ref object rotated_planes,
    ref object angles_deg,
    ref object info)
  {
    DataTree<Plane>  outPlanes = new DataTree<Plane>();
    DataTree<double> outAngles = new DataTree<double>();
    List<string>     infoList  = new List<string>();

    rotated_planes = outPlanes;
    angles_deg     = outAngles;
    info           = infoList;

    if (planes == null || planes.BranchCount == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No planes");
      return;
    }
    if (values == null || values.BranchCount == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No values");
      return;
    }
    if (axis < 0 || axis > 2) axis = 2;

    // Determine value range
    double vmin, vmax;
    if (auto_range)
    {
      vmin = double.MaxValue;
      vmax = double.MinValue;
      for (int b = 0; b < values.BranchCount; b++)
      {
        List<double> br = values.Branch(b);
        for (int i = 0; i < br.Count; i++)
        {
          if (br[i] < vmin) vmin = br[i];
          if (br[i] > vmax) vmax = br[i];
        }
      }
      if (vmin >= vmax) { vmin = 0; vmax = 1; }
    }
    else
    {
      vmin = value_min;
      vmax = value_max;
    }

    double range = vmax - vmin;
    if (Math.Abs(range) < 1e-10) range = 1.0;

    string axisName = (axis == 0) ? "X" : (axis == 1) ? "Y" : "Z";
    int totalBranches = planes.BranchCount;

    for (int b = 0; b < totalBranches; b++)
    {
      GH_Path path = planes.Path(b);
      List<Plane> brPlanes = planes.Branch(b);

      List<double> brValues;
      if (b < values.BranchCount) brValues = values.Branch(b);
      else if (values.BranchCount == 1) brValues = values.Branch(0);
      else continue;

      // 3-point gradient: start at 0, mid at 0.5, end at 1.0
      double branchT = (totalBranches > 1) ? (double)b / (totalBranches - 1) : 0.5;
      double gradFactor;
      if (branchT <= 0.5)
      {
        double localT = branchT * 2.0;            // 0..1 across first half
        gradFactor = grad_start + localT * (grad_mid - grad_start);
      }
      else
      {
        double localT = (branchT - 0.5) * 2.0;    // 0..1 across second half
        gradFactor = grad_mid + localT * (grad_end - grad_mid);
      }

      for (int i = 0; i < brPlanes.Count; i++)
      {
        Plane p = brPlanes[i];

        double v;
        if (i < brValues.Count) v = brValues[i];
        else if (brValues.Count > 0) v = brValues[brValues.Count - 1];
        else v = 0;

        double t = (v - vmin) / range;
        double angleDegRaw = angle_min_deg + t * (angle_max_deg - angle_min_deg);
        double angleDeg = angleDegRaw * gradFactor;
        double angleRad = angleDeg * Math.PI / 180.0;

        Vector3d rotAxis;
        if (axis == 0)      rotAxis = p.XAxis;
        else if (axis == 1) rotAxis = p.YAxis;
        else                rotAxis = p.ZAxis;

        Transform xform = Transform.Rotation(angleRad, rotAxis, p.Origin);
        Plane rotated = new Plane(p);
        rotated.Transform(xform);

        outPlanes.Add(rotated, path);
        outAngles.Add(angleDeg, path);
      }
    }

    infoList.Add("axis: " + axisName);
    infoList.Add("value range: " + vmin.ToString("F3") + " .. " + vmax.ToString("F3"));
    infoList.Add("angle range: " + angle_min_deg.ToString("F1") + " .. " + angle_max_deg.ToString("F1") + " deg");
    infoList.Add("gradient: " + grad_start.ToString("F2") + " .. " + grad_mid.ToString("F2") + " .. " + grad_end.ToString("F2"));
    infoList.Add("branches: " + totalBranches.ToString());

    rotated_planes = outPlanes;
    angles_deg     = outAngles;
    info           = infoList;
  }
}
