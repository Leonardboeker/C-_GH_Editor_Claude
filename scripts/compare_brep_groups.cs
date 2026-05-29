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

// Compare two groups of Breps by volume + surface area signature.
// Rotation- and translation-invariant. Greedy 1-to-1 matching.
//
// Outputs:
//   only_in_a   : Breps from A with no match in B (missing in B)
//   only_in_b   : Breps from B with no match in A (extras / duplicates)
//   matched_a   : A-side of matched pairs
//   matched_b   : B-side of matched pairs (parallel to matched_a)
//   idx_only_a  : indices of unmatched A items
//   idx_only_b  : indices of unmatched B items
//   info        : summary + per-A signature

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<Brep> group_a,
    List<Brep> group_b,
    double tol_vol,
    double tol_area,
    ref object only_in_a,
    ref object only_in_b,
    ref object matched_a,
    ref object matched_b,
    ref object idx_only_a,
    ref object idx_only_b,
    ref object info)
  {
    List<Brep>   onlyA    = new List<Brep>();
    List<Brep>   onlyB    = new List<Brep>();
    List<Brep>   matA     = new List<Brep>();
    List<Brep>   matB     = new List<Brep>();
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

    if (group_a == null) group_a = new List<Brep>();
    if (group_b == null) group_b = new List<Brep>();
    if (tol_vol  <= 0) tol_vol  = 100.0;  // mm^3
    if (tol_area <= 0) tol_area = 50.0;   // mm^2

    // Signatures
    double[] volA  = new double[group_a.Count];
    double[] areaA = new double[group_a.Count];
    for (int i = 0; i < group_a.Count; i++)
    {
      volA[i]  = BrepVolume(group_a[i]);
      areaA[i] = BrepArea(group_a[i]);
    }

    double[] volB  = new double[group_b.Count];
    double[] areaB = new double[group_b.Count];
    for (int j = 0; j < group_b.Count; j++)
    {
      volB[j]  = BrepVolume(group_b[j]);
      areaB[j] = BrepArea(group_b[j]);
    }

    // Greedy 1-to-1 matching by combined score
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
        double dV = Math.Abs(volA[i]  - volB[j]);
        double dA = Math.Abs(areaA[i] - areaB[j]);
        if (dV > tol_vol)  continue;
        if (dA > tol_area) continue;
        double score = dV / Math.Max(tol_vol, 1.0)
                     + dA / Math.Max(tol_area, 1.0);
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
    infoList.Add("---");
    for (int i = 0; i < group_a.Count; i++)
    {
      infoList.Add(
        "A[" + i.ToString() + "] vol=" + volA[i].ToString("F0") +
        " area=" + areaA[i].ToString("F0") +
        (match[i] >= 0 ? " -> B[" + match[i].ToString() + "]" : " -> NO MATCH")
      );
    }

    only_in_a  = onlyA;
    only_in_b  = onlyB;
    matched_a  = matA;
    matched_b  = matB;
    idx_only_a = idxOnlyA;
    idx_only_b = idxOnlyB;
    info       = infoList;
  }

  private double BrepVolume(Brep b)
  {
    if (b == null || !b.IsSolid) return 0;
    VolumeMassProperties vmp = VolumeMassProperties.Compute(b);
    return (vmp != null) ? Math.Abs(vmp.Volume) : 0;
  }

  private double BrepArea(Brep b)
  {
    if (b == null) return 0;
    AreaMassProperties amp = AreaMassProperties.Compute(b);
    return (amp != null) ? Math.Abs(amp.Area) : 0;
  }
}
