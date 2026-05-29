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

// Flatten 18mm plate parts for OpenNest.
//
// For each input Brep:
//   1. Find the largest planar face (the "broad side" of the plate).
//   2. Build a transform that maps that face's plane to WorldXY.
//   3. Output the outer outline curve in 2D (ready for OpenNest Geometry input).
//   4. Output the flat Brep, original face plane, and forward/inverse transforms.
//
// Use xforms_back later to reverse-transform the cut/marked parts back into
// their original 3D positions for assembly preview.

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<Brep> parts,
    double flatness_tol,
    ref object outlines,
    ref object inner_holes,
    ref object flat_parts,
    ref object orig_planes,
    ref object xforms_to_xy,
    ref object xforms_back,
    ref object info)
  {
    List<Curve>     outlinesOut = new List<Curve>();
    DataTree<Curve> holesTree   = new DataTree<Curve>();
    List<Brep>      flatOut     = new List<Brep>();
    List<Plane>     planesOut   = new List<Plane>();
    List<Transform> xfFwdOut    = new List<Transform>();
    List<Transform> xfBackOut   = new List<Transform>();
    List<string>    infoList    = new List<string>();

    outlines      = outlinesOut;
    inner_holes   = holesTree;
    flat_parts    = flatOut;
    orig_planes   = planesOut;
    xforms_to_xy  = xfFwdOut;
    xforms_back   = xfBackOut;
    info          = infoList;

    if (parts == null || parts.Count == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No parts");
      return;
    }
    if (flatness_tol <= 0) flatness_tol = 0.01;

    for (int i = 0; i < parts.Count; i++)
    {
      Brep brep = parts[i];
      GH_Path path = new GH_Path(i);

      if (brep == null || !brep.IsValid)
      {
        infoList.Add("p" + i.ToString() + ": invalid");
        continue;
      }

      // --- Find largest planar face ---
      int bestIdx = -1;
      double bestArea = -1.0;
      Plane bestFacePlane = Plane.Unset;

      for (int f = 0; f < brep.Faces.Count; f++)
      {
        BrepFace face = brep.Faces[f];
        Plane fp;
        if (!face.TryGetPlane(out fp, flatness_tol)) continue;

        // Use single-face brep for accurate trimmed area
        Brep single = face.DuplicateFace(false);
        if (single == null) continue;
        double area = single.GetArea();

        if (area > bestArea)
        {
          bestArea = area;
          bestIdx = f;

          // Outward-facing plane: flip if face orientation reversed
          if (face.OrientationIsReversed)
            bestFacePlane = new Plane(fp.Origin, fp.XAxis, -fp.YAxis);
          else
            bestFacePlane = fp;
        }
      }

      if (bestIdx < 0)
      {
        infoList.Add("p" + i.ToString() + ": no planar face found");
        continue;
      }

      // --- Build transform to WorldXY ---
      Transform xfFwd = Transform.PlaneToPlane(bestFacePlane, Plane.WorldXY);
      Transform xfBack;
      bool hasInv = xfFwd.TryGetInverse(out xfBack);
      if (!hasInv) xfBack = Transform.Identity;

      // Apply to brep
      Brep flatBrep = brep.DuplicateBrep();
      flatBrep.Transform(xfFwd);

      // --- Extract loops of the now-flat face ---
      BrepFace flatFace = flatBrep.Faces[bestIdx];

      Curve outerCrv = null;
      List<Curve> innerCrvs = new List<Curve>();

      for (int li = 0; li < flatFace.Loops.Count; li++)
      {
        BrepLoop loop = flatFace.Loops[li];
        Curve loopCrv = loop.To3dCurve();
        if (loopCrv == null) continue;

        // Project to XY for clean 2D
        Curve projected = Curve.ProjectToPlane(loopCrv, Plane.WorldXY);
        if (projected != null) loopCrv = projected;

        if (loop.LoopType == BrepLoopType.Outer)
          outerCrv = loopCrv;
        else if (loop.LoopType == BrepLoopType.Inner)
          innerCrvs.Add(loopCrv);
      }

      if (outerCrv == null)
      {
        infoList.Add("p" + i.ToString() + ": no outer loop");
        continue;
      }

      outlinesOut.Add(outerCrv);
      for (int k = 0; k < innerCrvs.Count; k++) holesTree.Add(innerCrvs[k], path);
      flatOut.Add(flatBrep);
      planesOut.Add(bestFacePlane);
      xfFwdOut.Add(xfFwd);
      xfBackOut.Add(xfBack);

      infoList.Add(
        "p" + i.ToString() +
        ": face=" + bestIdx.ToString() +
        " area=" + bestArea.ToString("F0") +
        " holes=" + innerCrvs.Count.ToString()
      );
    }

    outlines      = outlinesOut;
    inner_holes   = holesTree;
    flat_parts    = flatOut;
    orig_planes   = planesOut;
    xforms_to_xy  = xfFwdOut;
    xforms_back   = xfBackOut;
    info          = infoList;
  }
}
