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

// Compare two groups of closed curves by area + perimeter signature.
// Rotation- and translation-invariant. Greedy 1-to-1 matching.
//
// Outputs:
//   only_in_a   : curves from A with no match in B (missing in B)
//   only_in_b   : curves from B with no match in A (extras / duplicates)
//   matched_a   : A-side of matched pairs
//   matched_b   : B-side of matched pairs (parallel to matched_a)
//   idx_only_a  : indices of unmatched A items in original list
//   idx_only_b  : indices of unmatched B items in original list
//   info        : summary

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<Curve> group_a,
    List<Curve> group_b,
    double tol_area,
    double tol_length,
    ref object only_in_a,
    ref object only_in_b,
    ref object matched_a,
    ref object matched_b,
    ref object idx_only_a,
    ref object idx_only_b,
    ref object info)
  {
    List<Curve>  onlyA    = new List<Curve>();
    List<Curve>  onlyB    = new List<Curve>();
    List<Curve>  matA     = new List<Curve>();
    List<Curve>  matB     = new List<Curve>();
    List<int>    idxOnlyA = new List<int>();
    List<int>    idxOnlyB = new List<int>();
    List<string> infoList = new List<string>();

    only_in_a  = onlyA;
    only_in_b  = onlyB;
    matched_a  = matA;
    matched_b  = matB;
    idx_only_a = idxOnlyA;
    idx_only_b = idxOnlyB;
    info       = infoList;

    if (group_a == null) group_a = new List<Curve>();
    if (group_b == null) group_b = new List<Curve>();
    if (tol_area   <= 0) tol_area   = 10.0;  // mm^2
    if (tol_length <= 0) tol_length = 1.0;   // mm

    // Signatures
    double[] areaA = new double[group_a.Count];
    double[] lenA  = new double[group_a.Count];
    for (int i = 0; i < group_a.Count; i++)
    {
      areaA[i] = CurveArea(group_a[i]);
      lenA[i]  = (group_a[i] != null) ? group_a[i].GetLength() : 0;
    }

    double[] areaB = new double[group_b.Count];
    double[] lenB  = new double[group_b.Count];
    for (int j = 0; j < group_b.Count; j++)
    {
      areaB[j] = CurveArea(group_b[j]);
      lenB[j]  = (group_b[j] != null) ? group_b[j].GetLength() : 0;
    }

    // Greedy 1-to-1 matching
    bool[] usedB = new bool[group_b.Count];
    int[]  match = new int[group_a.Count];
    for (int i = 0; i < group_a.Count; i++) match[i] = -1;

    for (int i = 0; i < group_a.Count; i++)
    {
      int bestJ = -1;
      double bestScore = double.MaxValue;
      for (int j = 0; j < group_b.Count; j++)
      {
        if (usedB[j]) continue;
        double dA = Math.Abs(areaA[i] - areaB[j]);
        double dL = Math.Abs(lenA[i]  - lenB[j]);
        if (dA > tol_area)   continue;
        if (dL > tol_length) continue;
        double score = dA / Math.Max(tol_area, 1.0)
                     + dL / Math.Max(tol_length, 1.0);
        if (score < bestScore) { bestScore = score; bestJ = j; }
      }
      if (bestJ >= 0) { match[i] = bestJ; usedB[bestJ] = true; }
    }

    for (int i = 0; i < group_a.Count; i++)
    {
      if (match[i] < 0)
      {
        onlyA.Add(group_a[i]);
        idxOnlyA.Add(i);
      }
      else
      {
        matA.Add(group_a[i]);
        matB.Add(group_b[match[i]]);
      }
    }
    for (int j = 0; j < group_b.Count; j++)
    {
      if (!usedB[j])
      {
        onlyB.Add(group_b[j]);
        idxOnlyB.Add(j);
      }
    }

    infoList.Add("A: " + group_a.Count.ToString() + ", B: " + group_b.Count.ToString());
    infoList.Add("Matched: " + matA.Count.ToString());
    infoList.Add("Only in A (missing in B): " + onlyA.Count.ToString());
    infoList.Add("Only in B (extra/duplicates): " + onlyB.Count.ToString());

    only_in_a  = onlyA;
    only_in_b  = onlyB;
    matched_a  = matA;
    matched_b  = matB;
    idx_only_a = idxOnlyA;
    idx_only_b = idxOnlyB;
    info       = infoList;
  }

  private double CurveArea(Curve crv)
  {
    if (crv == null) return 0;
    AreaMassProperties amp = AreaMassProperties.Compute(crv);
    return (amp != null) ? Math.Abs(amp.Area) : 0;
  }
}
