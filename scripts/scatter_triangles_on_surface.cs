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

// Scatter random triangles on a surface (e.g. an open tube).
//
// For each triangle:
//   - Random UV position on the surface
//   - Random size between size_min and size_max
//   - Random rotation around the surface normal
//   - 3 random corner angles around the center (gives non-equilateral shapes)
//   - Corners are projected onto the surface so triangles hug the form
//
// Optional min_spacing rejects triangles whose center is too close to an
// already-placed triangle's center (anti-overlap).
//
// Outputs:
//   triangles : closed Polyline curves (for visualization or Pipe with caps)
//   edges     : individual Line segments (3 per triangle) -- wire to Pipe
//               for the structural look
//   centers   : center point of each triangle
//   info      : statistics

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    Surface surface,
    int count,
    double size_min,
    double size_max,
    double min_spacing,
    bool follow_surface,
    int random_seed,
    int max_attempts,
    ref object triangles,
    ref object edges,
    ref object centers,
    ref object info)
  {
    List<Curve>   triList = new List<Curve>();
    List<Line>    edgeList = new List<Line>();
    List<Point3d> centerList = new List<Point3d>();
    List<string>  infoList = new List<string>();

    triangles = triList;
    edges     = edgeList;
    centers   = centerList;
    info      = infoList;

    if (surface == null)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No surface");
      return;
    }
    if (count <= 0) count = 30;
    if (size_min <= 0) size_min = 10.0;
    if (size_max <= size_min) size_max = size_min * 2.0;
    if (max_attempts <= 0) max_attempts = count * 30;
    if (min_spacing < 0) min_spacing = 0;

    // Reparam surface to 0..1
    Surface srf = surface.Duplicate() as Surface;
    srf.SetDomain(0, new Interval(0, 1));
    srf.SetDomain(1, new Interval(0, 1));

    Random rng = new Random(random_seed);
    int placed = 0;
    int attempts = 0;

    for (int iter = 0; iter < max_attempts; iter++)
    {
      if (placed >= count) break;
      attempts++;

      // Random UV
      double u = rng.NextDouble();
      double v = rng.NextDouble();

      // Tangent frame at (u, v)
      Plane frame;
      if (!srf.FrameAt(u, v, out frame)) continue;
      Point3d center = frame.Origin;

      // Min-spacing rejection
      if (min_spacing > 0)
      {
        bool tooClose = false;
        for (int k = 0; k < centerList.Count; k++)
        {
          if (centerList[k].DistanceTo(center) < min_spacing)
          {
            tooClose = true;
            break;
          }
        }
        if (tooClose) continue;
      }

      // Random size + random base rotation
      double size = size_min + rng.NextDouble() * (size_max - size_min);
      double baseAngle = rng.NextDouble() * Math.PI * 2.0;

      // 3 random angles around center, then sort
      double a1 = rng.NextDouble() * 2.0 * Math.PI / 3.0;
      double a2 = 2.0 * Math.PI / 3.0 + rng.NextDouble() * 2.0 * Math.PI / 3.0;
      double a3 = 4.0 * Math.PI / 3.0 + rng.NextDouble() * 2.0 * Math.PI / 3.0;
      double[] angles = new double[] { a1, a2, a3 };

      // Slightly varied radii per corner for less-uniform shape
      double r1 = size * (0.85 + rng.NextDouble() * 0.30);
      double r2 = size * (0.85 + rng.NextDouble() * 0.30);
      double r3 = size * (0.85 + rng.NextDouble() * 0.30);
      double[] radii = new double[] { r1, r2, r3 };

      // Build 3 corners in tangent plane
      Point3d[] corners = new Point3d[3];
      for (int k = 0; k < 3; k++)
      {
        double ang = baseAngle + angles[k];
        double r = radii[k];
        Point3d p = center + frame.XAxis * (r * Math.Cos(ang))
                           + frame.YAxis * (r * Math.Sin(ang));

        if (follow_surface)
        {
          // Snap each corner back to the surface
          double cu, cv;
          if (srf.ClosestPoint(p, out cu, out cv))
            p = srf.PointAt(cu, cv);
        }
        corners[k] = p;
      }

      // Build closed polyline triangle
      Polyline poly = new Polyline();
      poly.Add(corners[0]);
      poly.Add(corners[1]);
      poly.Add(corners[2]);
      poly.Add(corners[0]);
      triList.Add(new PolylineCurve(poly));

      // 3 edges as separate Lines (for piping)
      edgeList.Add(new Line(corners[0], corners[1]));
      edgeList.Add(new Line(corners[1], corners[2]));
      edgeList.Add(new Line(corners[2], corners[0]));

      centerList.Add(center);
      placed++;
    }

    infoList.Add("placed: " + placed + " / requested: " + count + " (attempts: " + attempts + ")");
    infoList.Add("size range: " + size_min.ToString("F1") + " .. " + size_max.ToString("F1"));
    infoList.Add("min_spacing: " + min_spacing.ToString("F1"));
    infoList.Add("follow_surface: " + follow_surface);
    infoList.Add("seed: " + random_seed);

    triangles = triList;
    edges     = edgeList;
    centers   = centerList;
    info      = infoList;
  }
}
