// Template: Geometry Processing
// Inputs: curve (Item), points (List), tolerance (Item, default 0.01)
// Outputs: out_points, out_distances, out_count
//
// Copy-paste starting point for scripts that process curves, surfaces,
// or breps. Includes null guards, curve operations, and distance filtering.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    Curve curve,
    List<Point3d> points,
    double tolerance,
    ref object out_points,
    ref object out_distances,
    ref object out_count)
  {
    // Defaults
    out_points = new List<Point3d>();
    out_distances = new List<double>();
    out_count = 0;

    // Guards
    if (curve == null)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No curve connected");
      return;
    }
    if (points == null || points.Count == 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No points connected");
      return;
    }
    if (tolerance <= 0) tolerance = 0.01;

    // Processing
    List<Point3d> resultPts = new List<Point3d>();
    List<double> resultDist = new List<double>();
    int skipped = 0;

    for (int i = 0; i < points.Count; i++)
    {
      double t;
      if (!curve.ClosestPoint(points[i], out t))
      {
        skipped++;
        continue;
      }
      Point3d closestPt = curve.PointAt(t);
      double dist = points[i].DistanceTo(closestPt);
      resultPts.Add(closestPt);
      resultDist.Add(dist);
    }

    if (skipped > 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Warning,
        skipped.ToString() + " points failed ClosestPoint");
    }

    // Output
    out_points = resultPts;
    out_distances = resultDist;
    out_count = resultPts.Count;
  }
}
