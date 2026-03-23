// Template: DataTree Processing
// Inputs: inputTree (Tree access -- right-click input to set), factor (Item, default 1.0)
// Outputs: out_tree, out_counts
//
// Copy-paste starting point for scripts that read, transform, and output DataTrees.
// Preserves input tree structure in output.
// IMPORTANT: The inputTree parameter MUST be set to Tree access mode in GH
// (right-click the input -> select "Tree Access").

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
    DataTree<object> inputTree,
    double factor,
    ref object out_tree,
    ref object out_counts)
  {
    // Defaults
    out_tree = new DataTree<double>();
    out_counts = new List<int>();

    // Guards
    if (inputTree == null || inputTree.BranchCount == 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No data in input tree");
      return;
    }

    DataTree<double> resultTree = new DataTree<double>();
    List<int> branchCounts = new List<int>();

    // Process each branch
    for (int i = 0; i < inputTree.BranchCount; i++)
    {
      GH_Path path = inputTree.Path(i);
      List<object> branch = inputTree.Branch(i);
      List<double> newBranch = new List<double>();

      for (int j = 0; j < branch.Count; j++)
      {
        double val;
        if (double.TryParse(branch[j].ToString(), out val))
        {
          newBranch.Add(val * factor);
        }
        else
        {
          this.Component.AddRuntimeMessage(
            GH_RuntimeMessageLevel.Warning,
            "Cannot parse value at branch " + i.ToString() + " index " + j.ToString());
        }
      }

      resultTree.AddRange(newBranch, path);  // Preserve original path
      branchCounts.Add(newBranch.Count);
    }

    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      "Processed " + inputTree.BranchCount.ToString() + " branches, "
      + resultTree.DataCount.ToString() + " total items");

    // Output
    out_tree = resultTree;
    out_counts = branchCounts;
  }
}
