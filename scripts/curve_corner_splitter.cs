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
    List<Curve> curves,
    double angle_deg,
    double sample_step,
    double min_segment,
    ref object segments,
    ref object info)
  {
    DataTree<Curve> segTree = new DataTree<Curve>();
    List<string> infoList = new List<string>();
    segments = segTree;
    info = infoList;

    if (curves == null || curves.Count == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No curves connected");
      return;
    }
    if (angle_deg <= 0 || angle_deg >= 180) angle_deg = 80.0;
    if (min_segment < 0) min_segment = 0.0;

    double tol = (RhinoDoc.ActiveDoc != null) ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;

    for (int ci = 0; ci < curves.Count; ci++)
    {
      Curve crv = curves[ci];
      GH_Path path = new GH_Path(ci);

      if (crv == null || !crv.IsValid)
      {
        infoList.Add("c" + ci.ToString() + ": invalid");
        continue;
      }

      // --- Build sample (param, point) list ---
      List<double> tList = new List<double>();
      List<Point3d> ptList = new List<Point3d>();

      Polyline poly = new Polyline();
      bool gotPoly = false;
      if (sample_step <= 0.001) gotPoly = crv.TryGetPolyline(out poly);

      if (sample_step > 0.001)
      {
        Point3d[] dPts;
        double[] dP = crv.DivideByLength(sample_step, true, out dPts);
        if (dP != null && dPts != null)
        {
          for (int k = 0; k < dP.Length; k++) { tList.Add(dP[k]); ptList.Add(dPts[k]); }
        }
      }
      else if (gotPoly)
      {
        for (int k = 0; k < poly.Count; k++)
        {
          double t;
          if (crv.ClosestPoint(poly[k], out t)) { tList.Add(t); ptList.Add(poly[k]); }
        }
      }
      else
      {
        Point3d[] dPts;
        double[] dP = crv.DivideByCount(1000, true, out dPts);
        if (dP != null && dPts != null)
        {
          for (int k = 0; k < dP.Length; k++) { tList.Add(dP[k]); ptList.Add(dPts[k]); }
        }
      }

      // Treat near-closed curves as closed
      bool isClosed = crv.IsClosed;
      if (!isClosed && ptList.Count >= 2 && ptList[0].DistanceTo(ptList[ptList.Count - 1]) < tol * 10)
        isClosed = true;

      int n = tList.Count;

      // Strip duplicate endpoint for closed sample lists
      if (isClosed && n > 1 && ptList[n - 1].DistanceTo(ptList[0]) < tol * 10)
        n--;

      if (n < 3)
      {
        infoList.Add("c" + ci.ToString() + ": too few pts (" + n.ToString() + ")");
        segTree.Add(crv.DuplicateCurve(), path);
        continue;
      }

      // --- Interior angle at each vertex ---
      // 0 = needle tip, 90 = right angle, 180 = straight
      double[] ang = new double[n];
      for (int i = 0; i < n; i++)
      {
        int ip   = isClosed ? (i - 1 + n) % n : (i > 0     ? i - 1 : -1);
        int inxt = isClosed ? (i + 1) % n     : (i < n - 1 ? i + 1 : -1);

        if (ip < 0 || inxt < 0) { ang[i] = 180.0; continue; }

        Vector3d vIn  = ptList[i]    - ptList[ip];
        Vector3d vOut = ptList[inxt] - ptList[i];

        if (vIn.Length < 1e-10 || vOut.Length < 1e-10) { ang[i] = 180.0; continue; }
        vIn.Unitize();
        vOut.Unitize();

        double d = -(vIn.X * vOut.X + vIn.Y * vOut.Y + vIn.Z * vOut.Z);
        d = Math.Max(-1.0, Math.Min(1.0, d));
        ang[i] = RhinoMath.ToDegrees(Math.Acos(d));
      }

      // --- Find all candidate indices (interior angle < threshold) ---
      List<int> cand = new List<int>();
      for (int i = 0; i < n; i++)
        if (ang[i] < angle_deg) cand.Add(i);

      if (cand.Count == 0)
      {
        infoList.Add("c" + ci.ToString() + ": 0 corners");
        segTree.Add(crv.DuplicateCurve(), path);
        continue;
      }

      // --- Group consecutive candidates and pick sharpest per group ---
      List<List<int>> groups = new List<List<int>>();
      List<int> cur = new List<int>();
      cur.Add(cand[0]);
      for (int i = 1; i < cand.Count; i++)
      {
        if (cand[i] == cand[i - 1] + 1) cur.Add(cand[i]);
        else { groups.Add(cur); cur = new List<int>(); cur.Add(cand[i]); }
      }
      groups.Add(cur);

      // Wrap-merge first and last groups for closed curves
      if (isClosed && groups.Count >= 2)
      {
        List<int> first = groups[0];
        List<int> last = groups[groups.Count - 1];
        if (first[0] == 0 && last[last.Count - 1] == n - 1)
        {
          for (int k = 0; k < first.Count; k++) last.Add(first[k]);
          groups.RemoveAt(0);
        }
      }

      List<int> bestIdx = new List<int>();
      for (int g = 0; g < groups.Count; g++)
      {
        int best = groups[g][0];
        for (int k = 1; k < groups[g].Count; k++)
          if (ang[groups[g][k]] < ang[best]) best = groups[g][k];
        bestIdx.Add(best);
      }

      // --- Convert to params, sort ---
      List<double> cParams = new List<double>();
      for (int k = 0; k < bestIdx.Count; k++) cParams.Add(tList[bestIdx[k]]);
      cParams.Sort();

      // --- Merge corners closer than min_segment in arc-length ---
      if (min_segment > 0 && cParams.Count >= 2)
      {
        List<double> mp = new List<double>();
        mp.Add(cParams[0]);
        for (int k = 1; k < cParams.Count; k++)
        {
          double subLen = ArcLengthBetween(crv, mp[mp.Count - 1], cParams[k]);
          if (subLen >= min_segment) mp.Add(cParams[k]);
        }
        if (isClosed && mp.Count >= 2)
        {
          double total = crv.GetLength();
          double interior = ArcLengthBetween(crv, mp[0], mp[mp.Count - 1]);
          double wrap = total - interior;
          if (wrap < min_segment) mp.RemoveAt(mp.Count - 1);
        }
        cParams = mp;
      }

      infoList.Add("c" + ci.ToString() + ": " + cParams.Count.ToString() + " corners");

      if (cParams.Count == 0)
      {
        segTree.Add(crv.DuplicateCurve(), path);
        continue;
      }

      // Nudge params away from seam
      if (isClosed)
      {
        Interval dom = crv.Domain;
        double eps = dom.Length * 0.0001;
        for (int k = 0; k < cParams.Count; k++)
        {
          if (Math.Abs(cParams[k] - dom.Min) < eps) cParams[k] = dom.Min + eps;
          if (Math.Abs(cParams[k] - dom.Max) < eps) cParams[k] = dom.Max - eps;
        }
      }

      Curve[] pieces = crv.Split(cParams);

      if (pieces == null || pieces.Length == 0)
      {
        segTree.Add(crv.DuplicateCurve(), path);
        infoList[infoList.Count - 1] += " (split failed)";
        continue;
      }

      int added = 0;
      for (int i = 0; i < pieces.Length; i++)
      {
        if (pieces[i] == null) continue;
        if (pieces[i].GetLength() >= min_segment)
        {
          segTree.Add(pieces[i], path);
          added++;
        }
      }

      infoList[infoList.Count - 1] += " -> " + added.ToString() + " segs";
    }

    segments = segTree;
    info = infoList;
  }

  private double ArcLengthBetween(Curve crv, double t0, double t1)
  {
    if (t0 > t1) { double tmp = t0; t0 = t1; t1 = tmp; }
    Curve sub = crv.Trim(t0, t1);
    if (sub == null) return 0;
    return sub.GetLength();
  }
}
