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

// Assembly animation interpolator. Drive `t` with a slider, then use
// Grasshopper Animate (right-click slider) to capture frames.
//
// parts_start[i] and parts_end[i] must be the SAME brep in two positions
// (same vertex topology). If you used the flatten_plates_for_nesting
// script, wire:
//   parts_start = flat_parts (from flatten output)
//   parts_end   = original 3D parts (input to flatten)
//
// t        : 0..1 animation parameter
// stagger  : 0 = all parts move together, 1 = each part fully sequential
// easing   : 1 = linear, 2 = ease-out (slow end), 0.5 = ease-in
// reverse  : true = play backwards (assembled -> nested)
// order    : list of indices defining assembly order (empty = 0..n-1)

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<Brep> parts_start,
    List<Brep> parts_end,
    double t,
    double stagger,
    double easing,
    bool reverse,
    List<int> order,
    ref object out_parts,
    ref object info)
  {
    List<Brep>   outBreps = new List<Brep>();
    List<string> infoList = new List<string>();
    out_parts = outBreps;
    info      = infoList;

    if (parts_start == null || parts_end == null) return;
    int n = Math.Min(parts_start.Count, parts_end.Count);
    if (n == 0) return;

    if (easing <= 0) easing = 1.0;
    if (stagger < 0) stagger = 0;
    if (stagger > 1) stagger = 1;
    if (t < 0) t = 0; if (t > 1) t = 1;

    double effT = reverse ? 1.0 - t : t;
    double dur  = 1.0 - stagger;

    // Build the assembly order array
    int[] assemblyOrder = new int[n];
    if (order != null && order.Count == n)
    {
      for (int k = 0; k < n; k++) assemblyOrder[k] = order[k];
    }
    else
    {
      for (int k = 0; k < n; k++) assemblyOrder[k] = k;
    }
    // Reverse-map: original index -> assembly position
    int[] posOfIndex = new int[n];
    for (int pos = 0; pos < n; pos++) posOfIndex[assemblyOrder[pos]] = pos;

    int skipped = 0;
    for (int i = 0; i < n; i++)
    {
      Brep startB = parts_start[i];
      Brep endB   = parts_end[i];
      if (startB == null || endB == null) { skipped++; continue; }

      // Per-part local t based on assembly position
      int pos = posOfIndex[i];
      double tStart = (n > 1) ? ((double)pos / (n - 1)) * stagger : 0.0;
      double localT;
      if (effT <= tStart) localT = 0;
      else if (effT >= tStart + dur || dur <= 0) localT = 1;
      else localT = (effT - tStart) / dur;

      double easedT = (Math.Abs(easing - 1.0) > 1e-6) ? Math.Pow(localT, easing) : localT;

      // Reference planes from 3 corresponding vertices
      bool okS, okE;
      Plane planeS = BuildRefPlane(startB, out okS);
      Plane planeE = BuildRefPlane(endB,   out okE);
      if (!okS || !okE)
      {
        outBreps.Add(startB.DuplicateBrep());
        skipped++;
        continue;
      }

      Plane planeNow = LerpPlane(planeS, planeE, easedT);
      Transform xf = Transform.PlaneToPlane(planeS, planeNow);

      Brep dup = startB.DuplicateBrep();
      dup.Transform(xf);
      outBreps.Add(dup);
    }

    infoList.Add("t=" + t.ToString("F3") + " effT=" + effT.ToString("F3"));
    infoList.Add("parts=" + outBreps.Count.ToString() + " skipped=" + skipped.ToString());
    infoList.Add("stagger=" + stagger.ToString("F2") + " easing=" + easing.ToString("F2"));

    out_parts = outBreps;
    info      = infoList;
  }

  private Plane BuildRefPlane(Brep b, out bool ok)
  {
    ok = false;
    if (b == null || b.Vertices.Count < 3) return Plane.WorldXY;

    Point3d p0 = b.Vertices[0].Location;
    Point3d p1 = b.Vertices[1].Location;
    Point3d p2 = b.Vertices[2].Location;

    Vector3d x = p1 - p0;
    if (x.Length < 1e-9) return Plane.WorldXY;
    x.Unitize();

    Vector3d y = p2 - p0;
    y -= x * (y * x);
    if (y.Length < 1e-9) return Plane.WorldXY;
    y.Unitize();

    ok = true;
    return new Plane(p0, x, y);
  }

  private Plane LerpPlane(Plane a, Plane b, double t)
  {
    Quaternion qa = Quaternion.Rotation(Plane.WorldXY, a);
    Quaternion qb = Quaternion.Rotation(Plane.WorldXY, b);
    Quaternion q  = Quaternion.Slerp(qa, qb, t);

    Point3d origin = new Point3d(
      a.Origin.X + (b.Origin.X - a.Origin.X) * t,
      a.Origin.Y + (b.Origin.Y - a.Origin.Y) * t,
      a.Origin.Z + (b.Origin.Z - a.Origin.Z) * t);

    Plane result;
    if (!q.GetRotation(out result)) result = a;
    result.Origin = origin;
    return result;
  }
}
