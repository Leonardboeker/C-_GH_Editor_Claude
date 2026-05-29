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

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<Curve> segments,
    int bin_count,
    bool use_orientation,
    Plane work_plane,
    ref object grouped,
    ref object bin_centers_deg,
    ref object seg_angles_deg,
    ref object info)
  {
    DataTree<Curve> outTree = new DataTree<Curve>();
    List<double> binCenters = new List<double>();
    List<double> segAngles = new List<double>();
    List<string> infoList = new List<string>();
    grouped = outTree;
    bin_centers_deg = binCenters;
    seg_angles_deg = segAngles;
    info = infoList;

    if (segments == null || segments.Count == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No segments");
      return;
    }
    if (bin_count <= 0) bin_count = 8;
    if (!work_plane.IsValid) work_plane = Plane.WorldXY;

    double maxAngle = use_orientation ? 180.0 : 360.0;
    double binWidth = maxAngle / bin_count;

    // Compute direction angle for each segment
    int[] segBin = new int[segments.Count];
    double[] segAng = new double[segments.Count];

    for (int i = 0; i < segments.Count; i++)
    {
      Curve crv = segments[i];
      if (crv == null) { segBin[i] = -1; segAng[i] = double.NaN; continue; }

      Point3d p0 = crv.PointAtStart;
      Point3d p1 = crv.PointAtEnd;
      Vector3d dir = p1 - p0;

      if (dir.Length < 1e-10)
      {
        // Degenerate chord: try midpoint tangent instead
        double mid = (crv.Domain.Min + crv.Domain.Max) * 0.5;
        dir = crv.TangentAt(mid);
        if (dir.Length < 1e-10) { segBin[i] = -1; segAng[i] = double.NaN; continue; }
      }

      // Project onto work_plane: get 2D coords in plane's local frame
      double dx = Vector3d.Multiply(dir, work_plane.XAxis);
      double dy = Vector3d.Multiply(dir, work_plane.YAxis);

      double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
      if (angle < 0) angle += 360.0;
      if (use_orientation && angle >= 180.0) angle -= 180.0;

      int idx = (int)Math.Floor(angle / binWidth);
      if (idx >= bin_count) idx = bin_count - 1;
      if (idx < 0) idx = 0;

      segBin[i] = idx;
      segAng[i] = angle;
    }

    // Build output tree (one branch per bin)
    for (int b = 0; b < bin_count; b++)
    {
      GH_Path path = new GH_Path(b);
      int count = 0;
      for (int i = 0; i < segments.Count; i++)
      {
        if (segBin[i] == b)
        {
          outTree.Add(segments[i], path);
          count++;
        }
      }

      double center = b * binWidth + binWidth * 0.5;
      binCenters.Add(center);

      infoList.Add(
        "bin " + b.ToString() + " [" +
        (b * binWidth).ToString("F0") + "-" +
        ((b + 1) * binWidth).ToString("F0") + " deg]: " +
        count.ToString() + " segs (center=" + center.ToString("F0") + ")"
      );
    }

    // Per-segment angle for debugging / display
    for (int i = 0; i < segAng.Length; i++) segAngles.Add(segAng[i]);

    grouped = outTree;
    bin_centers_deg = binCenters;
    seg_angles_deg = segAngles;
    info = infoList;
  }
}
