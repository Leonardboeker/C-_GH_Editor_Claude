using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

// Persistent shaded preview via DisplayConduit.
// Shows geometry regardless of GH selection state, preview toggles, or
// component activity. Survives mode switches.
//
// Inputs:
//   breps       : List<Brep>     -- geometry to draw
//   color       : Color          -- diffuse base color (default mouse grey)
//   shine       : double 0..1    -- specular intensity (0=matte, 1=plastic)
//   transparency: double 0..1    -- 0=opaque, 1=invisible
//   two_sided   : bool           -- render back faces too
//   enabled     : bool           -- master on/off switch
//
// Output:
//   info        : status string

public class Script_Instance : GH_ScriptInstance
{
  // Conduit persists across script solves
  private static PreviewConduit _conduit;

  private void RunScript(
    List<Brep> breps,
    Color color,
    double shine,
    double transparency,
    bool two_sided,
    bool enabled,
    ref object info)
  {
    if (_conduit == null) _conduit = new PreviewConduit();

    if (!enabled || breps == null || breps.Count == 0)
    {
      _conduit.Enabled = false;
      info = "preview off";
      if (RhinoDoc.ActiveDoc != null) RhinoDoc.ActiveDoc.Views.Redraw();
      return;
    }

    // Defaults
    if (color.A == 0) color = Color.FromArgb(180, 180, 180);
    if (shine < 0) shine = 0;
    if (shine > 1) shine = 1;
    if (transparency < 0) transparency = 0;
    if (transparency > 1) transparency = 1;

    // Mesh breps once, cache
    MeshingParameters mp = MeshingParameters.QualityRenderMesh;
    List<Mesh> meshes = new List<Mesh>();
    BoundingBox bb = BoundingBox.Empty;
    int meshed = 0;

    for (int i = 0; i < breps.Count; i++)
    {
      Brep b = breps[i];
      if (b == null || !b.IsValid) continue;

      Mesh[] subMeshes = Mesh.CreateFromBrep(b, mp);
      if (subMeshes == null || subMeshes.Length == 0) continue;

      Mesh joined = new Mesh();
      for (int j = 0; j < subMeshes.Length; j++)
      {
        if (subMeshes[j] != null) joined.Append(subMeshes[j]);
      }
      joined.Normals.ComputeNormals();
      joined.Compact();

      meshes.Add(joined);
      bb.Union(joined.GetBoundingBox(true));
      meshed++;
    }

    // Build display material: diffuse, specular, shine
    DisplayMaterial mat = new DisplayMaterial(color, transparency);
    mat.Diffuse = color;
    // Specular: lighter shade of diffuse for plastic-ish highlights
    int sR = Math.Min(255, color.R + (int)(80 * shine));
    int sG = Math.Min(255, color.G + (int)(80 * shine));
    int sB = Math.Min(255, color.B + (int)(80 * shine));
    mat.Specular = Color.FromArgb(sR, sG, sB);
    mat.Shine = shine;
    mat.IsTwoSided = two_sided;
    // Subtle emission so it never goes pitch black in low-light angles
    int eR = (int)(color.R * 0.08);
    int eG = (int)(color.G * 0.08);
    int eB = (int)(color.B * 0.08);
    mat.Emission = Color.FromArgb(eR, eG, eB);

    _conduit.Meshes = meshes.ToArray();
    _conduit.Material = mat;
    _conduit.BBox = bb;
    _conduit.Enabled = true;

    info = "previewing " + meshed.ToString() + " breps, color RGB(" +
           color.R.ToString() + "," + color.G.ToString() + "," + color.B.ToString() +
           "), shine=" + shine.ToString("F2") +
           ", transparency=" + transparency.ToString("F2");

    if (RhinoDoc.ActiveDoc != null) RhinoDoc.ActiveDoc.Views.Redraw();
  }

  private class PreviewConduit : DisplayConduit
  {
    public Mesh[] Meshes;
    public DisplayMaterial Material;
    public BoundingBox BBox;

    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
    {
      if (BBox.IsValid) e.IncludeBoundingBox(BBox);
    }

    protected override void PostDrawObjects(DrawEventArgs e)
    {
      if (Meshes == null || Material == null) return;
      for (int i = 0; i < Meshes.Length; i++)
      {
        Mesh m = Meshes[i];
        if (m != null && m.IsValid)
        {
          e.Display.DrawMeshShaded(m, Material);
        }
      }
    }
  }
}
