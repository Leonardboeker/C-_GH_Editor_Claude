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

// Build a plane at each circle's center with Z aligned to the circle's
// local normal (axis), not world Z.
//
// Inputs:
//   circles      : list of circles
//   flip_z       : if true, flip the Z direction (handy when normals
//                  point the "wrong" way for tool orientation)
//   align_x_to   : optional reference vector. If non-zero, the plane's
//                  X axis gets aligned to this projected into the plane.
//                  Otherwise the circle's natural X axis is used.
//
// Outputs:
//   planes       : plane per circle (origin = center, Z = circle axis)
//   centers      : center points (debug / reuse)
//   normals      : Z axis vectors (debug / reuse)
//   info

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<Circle> circles,
    bool flip_z,
    Vector3d align_x_to,
    ref object planes,
    ref object centers,
    ref object normals,
    ref object info)
  {
    List<Plane>    outPlanes  = new List<Plane>();
    List<Point3d>  outCenters = new List<Point3d>();
    List<Vector3d> outNormals = new List<Vector3d>();
    List<string>   infoList   = new List<string>();

    planes  = outPlanes;
    centers = outCenters;
    normals = outNormals;
    info    = infoList;

    if (circles == null || circles.Count == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No circles");
      return;
    }

    bool useRefX = (align_x_to.Length > 1e-10);

    for (int i = 0; i < circles.Count; i++)
    {
      Circle c = circles[i];
      Plane p = c.Plane;

      // Build plane axes
      Vector3d zAxis = p.ZAxis;
      if (flip_z) zAxis = -zAxis;
      zAxis.Unitize();

      Vector3d xAxis;
      if (useRefX)
      {
        // Project ref vector into the plane perpendicular to zAxis
        Vector3d r = align_x_to;
        double d = r * zAxis;
        xAxis = r - zAxis * d;
        if (xAxis.Length < 1e-10) xAxis = p.XAxis;
        xAxis.Unitize();
      }
      else
      {
        xAxis = p.XAxis;
        if (flip_z)
        {
          // Keep handedness consistent when Z flipped
          xAxis = -xAxis;
        }
        xAxis.Unitize();
      }

      Vector3d yAxis = Vector3d.CrossProduct(zAxis, xAxis);
      yAxis.Unitize();
      // Re-orthogonalize X just in case
      xAxis = Vector3d.CrossProduct(yAxis, zAxis);
      xAxis.Unitize();

      Plane local = new Plane(p.Origin, xAxis, yAxis);

      outPlanes.Add(local);
      outCenters.Add(p.Origin);
      outNormals.Add(zAxis);
    }

    infoList.Add("processed " + circles.Count + " circles");
    infoList.Add("flip_z: " + flip_z);
    infoList.Add("align_x_to: " + (useRefX ? align_x_to.ToString() : "(circle X)"));

    planes  = outPlanes;
    centers = outCenters;
    normals = outNormals;
    info    = infoList;
  }
}
