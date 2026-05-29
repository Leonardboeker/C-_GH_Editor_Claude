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

// Robot planner: per-curve enter / cut / exit with full transit_z lift
// between EVERY curve. Use for outline / boundary cuts where each curve
// is a separate cut and the tool must be fully retracted between them.
//
// Per curve:
//   transit_z plane above first sample   -> Joint
//   safe_z above first sample            -> Joint
//   first sample                         -> Linear (plunge)
//   [cuts following tangent direction]   -> Linear at cut_speed
//   safe_z above last sample             -> Linear (retract)
//   transit_z plane above last sample    -> Joint
//
// Plane orientation per sample:
//   origin = sample point
//   X axis = curve tangent projected perpendicular to tool_dir
//   Z axis = tool_dir
// So as the curve turns, the planes follow the tangent.

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<Curve> curves,
    double point_step,
    double cut_speed,
    double safe_speed,
    double safe_z,
    double transit_z,
    Vector3d tool_dir,
    double wrist_limit_deg,
    ref object out_planes,
    ref object out_speeds,
    ref object out_motion,
    ref object out_visit)
  {
    List<Plane>  resultPlanes = new List<Plane>();
    List<double> resultSpeeds = new List<double>();
    List<string> resultMotion = new List<string>();
    List<int>    visitLog     = new List<int>();
    out_planes = resultPlanes;
    out_speeds = resultSpeeds;
    out_motion = resultMotion;
    out_visit  = visitLog;

    if (curves == null || curves.Count == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No curves");
      return;
    }
    if (point_step <= 0) point_step = 5.0;
    if (cut_speed <= 0) cut_speed = 100;
    if (safe_speed <= 0) safe_speed = 200;
    if (safe_z <= 0) safe_z = 30;
    if (tool_dir.Length < 1e-10) tool_dir = Vector3d.ZAxis;
    tool_dir.Unitize();

    double wristLimitRad = wrist_limit_deg > 0
      ? wrist_limit_deg * Math.PI / 180.0
      : double.MaxValue;

    for (int c = 0; c < curves.Count; c++)
    {
      Curve crv = curves[c];
      if (crv == null || !crv.IsValid) continue;

      List<Plane> cuts = SampleCurveToPlanes(crv, point_step, tool_dir);
      if (cuts.Count < 2) continue;

      Plane first = cuts[0];
      Plane last  = cuts[cuts.Count - 1];

      // ENTER: transit -> safe lift -> plunge
      Plane transitIn = MakeTransitPlane(first, transit_z);
      Plane firstLift = LiftPlaneWorldZ(first, safe_z);

      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                transitIn, safe_speed, "Joint", c);
      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                firstLift, safe_speed, "Joint", c);
      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                first, safe_speed, "Linear", c);

      // CUT with optional wrist relief
      double accumAng = 0.0;
      Plane prev = first;

      for (int j = 1; j < cuts.Count; j++)
      {
        Plane curr = cuts[j];
        double dAng = SignedAngleAroundZ(prev.XAxis, curr.XAxis, prev.ZAxis);
        accumAng += Math.Abs(dAng);

        if (accumAng > wristLimitRad)
        {
          Plane prevLift = LiftPlaneWorldZ(prev, safe_z);
          Plane currLift = LiftPlaneWorldZ(curr, safe_z);
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                    prevLift, safe_speed, "Linear", c);
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                    currLift, safe_speed, "Joint", c);
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                    curr, safe_speed, "Linear", c);
          accumAng = 0.0;
        }
        else
        {
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                    curr, cut_speed, "Linear", c);
        }
        prev = curr;
      }

      // EXIT: retract -> transit
      Plane lastLift   = LiftPlaneWorldZ(last, safe_z);
      Plane transitOut = MakeTransitPlane(last, transit_z);

      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                lastLift, safe_speed, "Linear", c);
      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                transitOut, safe_speed, "Joint", c);
    }

    out_planes = resultPlanes;
    out_speeds = resultSpeeds;
    out_motion = resultMotion;
    out_visit  = visitLog;

    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
      resultPlanes.Count.ToString() + " planes from " +
      curves.Count.ToString() + " curves");
  }

  private List<Plane> SampleCurveToPlanes(Curve crv, double step, Vector3d toolDir)
  {
    List<Plane> result = new List<Plane>();
    if (crv == null) return result;

    Point3d[] pts;
    double[] tParams = crv.DivideByLength(step, true, out pts);
    if (tParams == null || pts == null || pts.Length < 2) return result;

    for (int i = 0; i < pts.Length; i++)
    {
      Vector3d tangent = crv.TangentAt(tParams[i]);
      if (tangent.Length < 1e-10) continue;
      tangent.Unitize();

      // X = tangent projected perpendicular to toolDir
      Vector3d xAxis = tangent - toolDir * (tangent * toolDir);
      if (xAxis.Length < 1e-10) xAxis = Vector3d.XAxis;
      xAxis.Unitize();

      Vector3d yAxis = Vector3d.CrossProduct(toolDir, xAxis);
      yAxis.Unitize();

      result.Add(new Plane(pts[i], xAxis, yAxis));
    }

    return result;
  }

  private void AddRecord(
    List<Plane> p, List<double> s, List<string> m, List<int> v,
    Plane plane, double speed, string motion, int branch)
  {
    p.Add(plane);
    s.Add(speed);
    m.Add(motion);
    v.Add(branch);
  }

  private Plane LiftPlaneWorldZ(Plane p, double dist)
  {
    Plane lifted = new Plane(p);
    lifted.Origin = new Point3d(p.Origin.X, p.Origin.Y, p.Origin.Z + dist);
    return lifted;
  }

  private Plane MakeTransitPlane(Plane p, double transit_z)
  {
    Point3d o = new Point3d(p.Origin.X, p.Origin.Y, transit_z);
    return new Plane(o, Vector3d.XAxis, Vector3d.YAxis);
  }

  private double SignedAngleAroundZ(Vector3d a, Vector3d b, Vector3d axis)
  {
    Vector3d aProj = a - axis * (a * axis);
    Vector3d bProj = b - axis * (b * axis);
    if (!aProj.Unitize() || !bProj.Unitize()) return 0.0;
    double dot = aProj * bProj;
    if (dot > 1.0) dot = 1.0; else if (dot < -1.0) dot = -1.0;
    double ang = Math.Acos(dot);
    Vector3d cross = Vector3d.CrossProduct(aProj, bProj);
    if (cross * axis < 0) ang = -ang;
    return ang;
  }
}
