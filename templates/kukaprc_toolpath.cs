// Template: KUKAprc Toolpath
// Inputs: toolpathCurve (Item), workPlane (Item), divisions (Item, int),
//         approachHeight (Item, 50), approachSpeed (Item, 200), workSpeed (Item, 100)
// Outputs: planes, speeds
//
// Copy-paste starting point for scripts that generate robot toolpaths.
// Outputs plane list and matching speed list for KUKAprc.
// KUKAprc wiring: Connect planes to Core Movements input, speeds to Speed input.

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
    Curve toolpathCurve,
    Plane workPlane,
    int divisions,
    double approachHeight,
    double approachSpeed,
    double workSpeed,
    ref object planes,
    ref object speeds)
  {
    // Defaults
    planes = new List<Plane>();
    speeds = new List<double>();

    // Guards
    if (toolpathCurve == null)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No toolpath curve connected");
      return;
    }
    if (divisions < 2) divisions = 10;
    if (approachHeight <= 0) approachHeight = 50.0;
    if (approachSpeed <= 0) approachSpeed = 200.0;
    if (workSpeed <= 0) workSpeed = 100.0;

    // Divide curve into points
    Point3d[] divPts;
    double[] parameters = toolpathCurve.DivideByCount(divisions, true, out divPts);
    if (parameters == null || divPts == null)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "Curve division failed");
      return;
    }

    List<Plane> outPlanes = new List<Plane>();
    List<double> outSpeeds = new List<double>();

    // Approach plane (above first point)
    Plane approachPlane = new Plane(workPlane);
    approachPlane.Transform(
      Transform.Translation(workPlane.ZAxis * approachHeight));
    outPlanes.Add(approachPlane);
    outSpeeds.Add(approachSpeed);

    // Work planes along curve
    for (int i = 0; i < divPts.Length; i++)
    {
      Vector3d tangent = toolpathCurve.TangentAt(parameters[i]);
      tangent.Unitize();

      // Build plane: origin at point, Z along work plane normal
      Vector3d zAxis = workPlane.ZAxis;
      Vector3d yAxis = Vector3d.CrossProduct(zAxis, tangent);
      yAxis.Unitize();
      if (yAxis.Length < 1e-10)
      {
        yAxis = workPlane.YAxis;
      }
      Vector3d xAxis = Vector3d.CrossProduct(yAxis, zAxis);
      xAxis.Unitize();

      Plane toolPlane = new Plane(divPts[i], xAxis, yAxis);
      outPlanes.Add(toolPlane);
      outSpeeds.Add(workSpeed);
    }

    // Retract plane (above last point)
    Plane retractPlane = new Plane(workPlane);
    retractPlane.Origin = divPts[divPts.Length - 1];
    retractPlane.Transform(
      Transform.Translation(workPlane.ZAxis * approachHeight));
    outPlanes.Add(retractPlane);
    outSpeeds.Add(approachSpeed);

    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      outPlanes.Count.ToString() + " planes, " + outSpeeds.Count.ToString() + " speeds");

    // CRITICAL: plane count must equal speed count
    planes = outPlanes;
    speeds = outSpeeds;
  }
}
