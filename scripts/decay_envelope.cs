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

// Apply a decay envelope to a tree of values.
// Each value multiplied by a factor interpolating from decay_start (first
// index) to decay_end (last index), shaped by curve_power.
//
//   factor(t) = decay_start + (decay_end - decay_start) * t^curve_power
//   where t = index / (count - 1)
//
// curve_power:
//   1.0  = linear ramp
//   >1   = holds high at start, drops late (ease-out)
//   <1   = drops fast at start, levels low (ease-in)
//
// Use case: damped sine wave -- first peak full, each next smaller.
// Insert AFTER your Remap Numbers (so envelope scales the actual mm
// amplitude, not the raw 0..1 graph mapper output).

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    DataTree<double> values,
    double decay_start,
    double decay_end,
    double curve_power,
    bool reverse,
    ref object out_values,
    ref object out_factors,
    ref object info)
  {
    DataTree<double> outVals = new DataTree<double>();
    DataTree<double> outFacs = new DataTree<double>();
    List<string> infoList = new List<string>();
    out_values  = outVals;
    out_factors = outFacs;
    info        = infoList;

    if (values == null || values.BranchCount == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No values");
      return;
    }
    if (curve_power <= 0) curve_power = 1.0;

    for (int b = 0; b < values.BranchCount; b++)
    {
      GH_Path path = values.Path(b);
      List<double> branch = values.Branch(b);
      int n = branch.Count;
      if (n == 0) continue;

      for (int i = 0; i < n; i++)
      {
        double t = (n > 1) ? (double)i / (n - 1) : 0.0;
        if (reverse) t = 1.0 - t;
        double factor = decay_start + (decay_end - decay_start) * Math.Pow(t, curve_power);
        outVals.Add(branch[i] * factor, path);
        outFacs.Add(factor, path);
      }
    }

    infoList.Add("decay: " + decay_start.ToString("F2") + " -> " + decay_end.ToString("F2"));
    infoList.Add("power: " + curve_power.ToString("F2"));
    infoList.Add("reverse: " + reverse.ToString());
    infoList.Add("branches: " + values.BranchCount.ToString());

    out_values  = outVals;
    out_factors = outFacs;
    info        = infoList;
  }
}
