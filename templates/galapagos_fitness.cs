// Template: Galapagos Fitness
// Inputs: gene1 (Number Slider), gene2 (Number Slider), geneInt (Integer Slider 0-1)
// Outputs: fitness (single number)
//
// Copy-paste starting point for Galapagos optimization fitness functions.
// Must output a single number. NaN protection is critical.
//
// Galapagos Setup:
// 1. Genome: Connect Number/Integer Sliders DIRECTLY to inputs
//    (do not use intermediate components or Remote Receivers)
// 2. Fitness: Connect the single fitness output to Galapagos fitness input
// 3. Direction: Set Galapagos to "Minimize" if lower fitness = better
// 4. Penalty: Uses 999999 not double.MaxValue (MaxValue causes overflow)

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
    double gene1,
    double gene2,
    int geneInt,
    ref object fitness)
  {
    // Default (high penalty = bad)
    fitness = 999999.0;

    // Map integer gene to binary choice
    double angle = (geneInt == 0) ? -180.0 : 180.0;

    // Compute fitness components
    double distanceScore = Math.Abs(gene1 - 50.0);    // Minimize distance from target
    double angleScore = Math.Abs(gene2 - angle);       // Minimize angle error

    // Weighted combination (lower is better for minimization)
    double total = distanceScore * 100.0 + angleScore * 50.0;

    // CRITICAL: NaN guard -- Galapagos crashes on NaN
    if (double.IsNaN(total) || double.IsInfinity(total))
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Warning, "NaN detected in fitness");
      total = 999999.0;
    }

    fitness = total;
  }
}
