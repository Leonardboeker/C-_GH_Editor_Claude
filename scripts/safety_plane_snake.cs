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

// Safety plane traversal for 3 DataTree<Plane> inputs with custom order
// and per-branch speed modulation.
//
// Order:
//   Phase 1: A[0], C[0], A[1], C[1], ..., A[last], C[last]
//   Phase 2: B[0], B[lastB], B[1], B[lastB-1], B[2], B[lastB-2], ...
//
// Snake flip per visit position. Safety plane lift at branch start/end.
//
// Speed modulation:
//   - b_edge_slow_factor : multiplier applied to ALL cut speeds in B[0]
//     and B[lastB] branches (e.g. 0.5 = half speed for these two branches)
//   - start_boost_factor : extra speed multiplier at the first position of
//     every branch (1.0 = doubles cut_speed), fades linearly to 0 over
//     start_boost_count positions
//   - start_boost_count  : how many positions the boost fades over
//
// Per-plane speed = cut_speed * branch_mul * boost_mul

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    DataTree<Plane> planes_a,
    DataTree<Plane> planes_b,
    DataTree<Plane> planes_c,
    double safety_z,
    double cut_speed,
    double safe_speed,
    double b_edge_slow_factor,
    double start_boost_factor,
    int start_boost_count,
    ref object out_planes,
    ref object out_speeds,
    ref object out_motion,
    ref object out_visit,
    ref object out_order,
    ref object info)
  {
    List<Plane>  resultPlanes = new List<Plane>();
    List<double> resultSpeeds = new List<double>();
    List<string> resultMotion = new List<string>();
    List<string> visitLog     = new List<string>();
    List<string> orderLog     = new List<string>();
    List<string> infoList     = new List<string>();

    out_planes = resultPlanes;
    out_speeds = resultSpeeds;
    out_motion = resultMotion;
    out_visit  = visitLog;
    out_order  = orderLog;
    info       = infoList;

    if (safety_z   <= 0) safety_z   = 50.0;
    if (cut_speed  <= 0) cut_speed  = 100.0;
    if (safe_speed <= 0) safe_speed = 200.0;
    if (b_edge_slow_factor <= 0) b_edge_slow_factor = 1.0;
    if (start_boost_factor < 0)  start_boost_factor = 0.0;
    if (start_boost_count  < 0)  start_boost_count  = 0;

    // --- Build the global visit list ---
    List<int> visitTree   = new List<int>();
    List<int> visitBranch = new List<int>();

    int lenA = (planes_a != null) ? planes_a.BranchCount : 0;
    int lenB = (planes_b != null) ? planes_b.BranchCount : 0;
    int lenC = (planes_c != null) ? planes_c.BranchCount : 0;

    // Phase 1: A asc + C asc interleaved
    int pairs = Math.Min(lenA, lenC);
    for (int i = 0; i < pairs; i++)
    {
      visitTree.Add(0); visitBranch.Add(i);
      visitTree.Add(2); visitBranch.Add(i);
    }
    for (int i = pairs; i < lenA; i++) { visitTree.Add(0); visitBranch.Add(i); }
    for (int i = pairs; i < lenC; i++) { visitTree.Add(2); visitBranch.Add(i); }

    // Phase 2: B outside-in, starting LOW
    for (int p = 0; p < lenB; p++)
    {
      int idx = (p % 2 == 0) ? (p / 2) : (lenB - 1 - (p - 1) / 2);
      visitTree.Add(1); visitBranch.Add(idx);
    }

    DataTree<Plane>[] trees = new DataTree<Plane>[] { planes_a, planes_b, planes_c };
    string[] names = new string[] { "A", "B", "C" };

    // Log order
    List<string> orderStrs = new List<string>();
    for (int v = 0; v < visitTree.Count; v++)
      orderStrs.Add(names[visitTree[v]] + visitBranch[v].ToString());
    orderLog.Add(string.Join(" -> ", orderStrs.ToArray()));

    // --- Emit ---
    int cutCount = 0;
    for (int v = 0; v < visitTree.Count; v++)
    {
      int treeIdx = visitTree[v];
      DataTree<Plane> tree = trees[treeIdx];
      int b = visitBranch[v];
      if (tree == null || b < 0 || b >= tree.BranchCount) continue;

      List<Plane> branch = new List<Plane>(tree.Branch(b));
      if (branch.Count == 0) continue;

      // Snake flip
      if (v % 2 == 1) branch.Reverse();

      // Per-branch speed multiplier
      double branchMul = 1.0;
      bool isBEdge = (treeIdx == 1) && (b == 0 || b == lenB - 1);
      if (isBEdge) branchMul = b_edge_slow_factor;

      Plane first = branch[0];
      Plane last  = branch[branch.Count - 1];
      Plane safeIn  = LiftZ(first, safety_z);
      Plane safeOut = LiftZ(last,  safety_z);

      string tag = names[treeIdx] + b.ToString() + "_pos" + v.ToString();
      if (isBEdge) tag += "_SLOW";

      // Enter
      resultPlanes.Add(safeIn);
      resultSpeeds.Add(safe_speed);
      resultMotion.Add("Joint");
      visitLog.Add(tag + "_in");

      // Cut planes with start-of-branch boost
      for (int i = 0; i < branch.Count; i++)
      {
        double boostMul = 1.0;
        if (start_boost_count > 0 && i < start_boost_count)
        {
          double tBoost = 1.0 - (double)i / (double)start_boost_count;
          boostMul = 1.0 + start_boost_factor * tBoost;
        }
        double speed = cut_speed * branchMul * boostMul;

        resultPlanes.Add(branch[i]);
        resultSpeeds.Add(speed);
        resultMotion.Add("Linear");
        visitLog.Add(tag);
        cutCount++;
      }

      // Exit
      resultPlanes.Add(safeOut);
      resultSpeeds.Add(safe_speed);
      resultMotion.Add("Joint");
      visitLog.Add(tag + "_out");
    }

    infoList.Add("A=" + lenA.ToString() + " B=" + lenB.ToString() + " C=" + lenC.ToString());
    infoList.Add("visits=" + visitTree.Count.ToString() + " cut_planes=" + cutCount.ToString());
    infoList.Add("safety_z=" + safety_z.ToString("F1"));
    infoList.Add("cut_speed=" + cut_speed.ToString("F1") + " mm/s");
    infoList.Add("b_edge_slow_factor=" + b_edge_slow_factor.ToString("F2"));
    infoList.Add("start_boost: +" + start_boost_factor.ToString("F2") + " over " + start_boost_count.ToString() + " positions");

    // Sample speeds at pos 0..start_boost_count for verification
    if (start_boost_count > 0 && start_boost_factor > 0)
    {
      List<string> previewSpeeds = new List<string>();
      for (int i = 0; i <= start_boost_count; i++)
      {
        double tBoost = (i < start_boost_count) ? 1.0 - (double)i / (double)start_boost_count : 0.0;
        double mul = 1.0 + start_boost_factor * tBoost;
        previewSpeeds.Add((cut_speed * mul).ToString("F1"));
      }
      infoList.Add("first " + (start_boost_count + 1).ToString() + " speeds (normal branch): " +
                   string.Join(", ", previewSpeeds.ToArray()) + " mm/s");
    }

    out_planes = resultPlanes;
    out_speeds = resultSpeeds;
    out_motion = resultMotion;
    out_visit  = visitLog;
    out_order  = orderLog;
    info       = infoList;
  }

  private Plane LiftZ(Plane p, double dz)
  {
    Plane lifted = new Plane(p);
    lifted.Origin = new Point3d(p.Origin.X, p.Origin.Y, p.Origin.Z + dz);
    return lifted;
  }
}
