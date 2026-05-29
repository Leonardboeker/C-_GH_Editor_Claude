# User Object Konfigurationen

Alle C#-Komponenten als User Object speichern. Pro Komponente:

1. C# Script Komponente auf Canvas ziehen
2. Doppelklick → den **Code-Block** aus der jeweiligen Section unten reinkopieren → OK
3. Inputs nach Tabelle konfigurieren (Rechtsklick auf Input → Type Hint + Access)
4. Inputs/Outputs umbenennen (Rechtsklick → Rename) wie in Tabelle
5. Komponente selektieren → **File → Create User Object...**
6. Felder aus diesem Dokument abtippen
7. Icon-Datei aus `scripts/icons/<name>.png` ins Icon-Feld droppen
8. OK → erscheint dauerhaft unter **Category → Sub-Category**

Alle Category: **Leo**

---

# ANIMATION

## assembly_animation

**User Object Properties:**
- **Name:** Assembly Animation
- **Nickname:** Assembly
- **Description:** Interpolates Breps between start (nest) and end (assembled) positions over t (0..1). With stagger, easing, snake order. Use with GH Animate.
- **Sub-Category:** Animation
- **Panel Section:** 1
- **Icon:** `scripts/icons/assembly_animation.png`

**Inputs:**
- `parts_start` (List, Brep)
- `parts_end` (List, Brep)
- `t` (Item, double)
- `stagger` (Item, double)
- `easing` (Item, double)
- `reverse` (Item, bool)
- `order` (List, int)

**Outputs:** `out_parts`, `info`

```csharp
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
    List<Brep> parts_start,
    List<Brep> parts_end,
    double t,
    double stagger,
    double easing,
    bool reverse,
    List<int> order,
    ref object out_parts,
    ref object info)
  {
    List<Brep>   outBreps = new List<Brep>();
    List<string> infoList = new List<string>();
    out_parts = outBreps;
    info      = infoList;

    if (parts_start == null || parts_end == null) return;
    int n = Math.Min(parts_start.Count, parts_end.Count);
    if (n == 0) return;

    if (easing <= 0) easing = 1.0;
    if (stagger < 0) stagger = 0;
    if (stagger > 1) stagger = 1;
    if (t < 0) t = 0; if (t > 1) t = 1;

    double effT = reverse ? 1.0 - t : t;
    double dur  = 1.0 - stagger;

    int[] assemblyOrder = new int[n];
    if (order != null && order.Count == n)
    {
      for (int k = 0; k < n; k++) assemblyOrder[k] = order[k];
    }
    else
    {
      for (int k = 0; k < n; k++) assemblyOrder[k] = k;
    }
    int[] posOfIndex = new int[n];
    for (int pos = 0; pos < n; pos++) posOfIndex[assemblyOrder[pos]] = pos;

    int skipped = 0;
    for (int i = 0; i < n; i++)
    {
      Brep startB = parts_start[i];
      Brep endB   = parts_end[i];
      if (startB == null || endB == null) { skipped++; continue; }

      int pos = posOfIndex[i];
      double tStart = (n > 1) ? ((double)pos / (n - 1)) * stagger : 0.0;
      double localT;
      if (effT <= tStart) localT = 0;
      else if (effT >= tStart + dur || dur <= 0) localT = 1;
      else localT = (effT - tStart) / dur;

      double easedT = (Math.Abs(easing - 1.0) > 1e-6) ? Math.Pow(localT, easing) : localT;

      bool okS, okE;
      Plane planeS = BuildRefPlane(startB, out okS);
      Plane planeE = BuildRefPlane(endB,   out okE);
      if (!okS || !okE)
      {
        outBreps.Add(startB.DuplicateBrep());
        skipped++;
        continue;
      }

      Plane planeNow = LerpPlane(planeS, planeE, easedT);
      Transform xf = Transform.PlaneToPlane(planeS, planeNow);

      Brep dup = startB.DuplicateBrep();
      dup.Transform(xf);
      outBreps.Add(dup);
    }

    infoList.Add("t=" + t.ToString("F3") + " effT=" + effT.ToString("F3"));
    infoList.Add("parts=" + outBreps.Count.ToString() + " skipped=" + skipped.ToString());
    infoList.Add("stagger=" + stagger.ToString("F2") + " easing=" + easing.ToString("F2"));

    out_parts = outBreps;
    info      = infoList;
  }

  private Plane BuildRefPlane(Brep b, out bool ok)
  {
    ok = false;
    if (b == null || b.Vertices.Count < 3) return Plane.WorldXY;

    Point3d p0 = b.Vertices[0].Location;
    Point3d p1 = b.Vertices[1].Location;
    Point3d p2 = b.Vertices[2].Location;

    Vector3d x = p1 - p0;
    if (x.Length < 1e-9) return Plane.WorldXY;
    x.Unitize();

    Vector3d y = p2 - p0;
    y -= x * (y * x);
    if (y.Length < 1e-9) return Plane.WorldXY;
    y.Unitize();

    ok = true;
    return new Plane(p0, x, y);
  }

  private Plane LerpPlane(Plane a, Plane b, double t)
  {
    Quaternion qa = Quaternion.Rotation(Plane.WorldXY, a);
    Quaternion qb = Quaternion.Rotation(Plane.WorldXY, b);
    Quaternion q  = Quaternion.Slerp(qa, qb, t);

    Point3d origin = new Point3d(
      a.Origin.X + (b.Origin.X - a.Origin.X) * t,
      a.Origin.Y + (b.Origin.Y - a.Origin.Y) * t,
      a.Origin.Z + (b.Origin.Z - a.Origin.Z) * t);

    Plane result;
    if (!q.GetRotation(out result)) result = a;
    result.Origin = origin;
    return result;
  }
}
```

---

## helix_camera

**User Object Properties:**
- **Name:** Helix Camera
- **Nickname:** HelixCam
- **Description:** Animates camera on a helix path around cam_center. Sets viewport camera on each solve, outputs preview polyline.
- **Sub-Category:** Animation
- **Panel Section:** 1
- **Icon:** `scripts/icons/helix_camera.png`

**Inputs:**
- `t` (Item, double)
- `cam_center` (Item, Point3d)
- `look_at` (Item, Point3d)
- `radius_start` / `radius_end` (Item, double)
- `height_start` / `height_end` (Item, double)
- `rotation_deg` / `start_angle_deg` (Item, double)
- `view_name` (Item, string)
- `apply_camera` (Item, bool)
- `preview_segments` (Item, int)

**Outputs:** `cam_location`, `cam_target`, `helix_preview`, `info`

```csharp
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

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    double t,
    Point3d cam_center,
    Point3d look_at,
    double radius_start,
    double radius_end,
    double height_start,
    double height_end,
    double rotation_deg,
    double start_angle_deg,
    string view_name,
    bool apply_camera,
    int preview_segments,
    ref object cam_location,
    ref object cam_target,
    ref object helix_preview,
    ref object info)
  {
    List<string> infoList = new List<string>();
    info = infoList;

    if (t < 0) t = 0;
    if (t > 1) t = 1;
    if (preview_segments <= 0) preview_segments = 60;
    if (rotation_deg == 0) rotation_deg = 360;

    double startRad = start_angle_deg * Math.PI / 180.0;

    Point3d camLoc = HelixPoint(t, cam_center, radius_start, radius_end,
                                height_start, height_end, rotation_deg, startRad);
    Point3d target = look_at;
    if (look_at.X == 0 && look_at.Y == 0 && look_at.Z == 0) target = cam_center;
    cam_location = camLoc;
    cam_target   = target;

    Polyline poly = new Polyline();
    for (int i = 0; i <= preview_segments; i++)
    {
      double ti = (double)i / preview_segments;
      poly.Add(HelixPoint(ti, cam_center, radius_start, radius_end,
                          height_start, height_end, rotation_deg, startRad));
    }
    helix_preview = new PolylineCurve(poly);

    if (apply_camera)
    {
      RhinoDoc doc = RhinoDoc.ActiveDoc;
      if (doc != null)
      {
        RhinoView view = FindView(doc, view_name);
        if (view != null)
        {
          view.ActiveViewport.SetCameraLocations(target, camLoc);
          Vector3d up = Vector3d.ZAxis;
          Vector3d viewDir = target - camLoc;
          double dotZ = Math.Abs(viewDir.X * 0 + viewDir.Y * 0 + viewDir.Z * 1) / Math.Max(viewDir.Length, 1e-9);
          if (dotZ > 0.999) up = Vector3d.YAxis;
          view.ActiveViewport.CameraUp = up;
          view.Redraw();
          infoList.Add("camera applied to '" + view.ActiveViewport.Name + "'");
        }
        else { infoList.Add("WARNING: view not found"); }
      }
    }

    infoList.Add("t=" + t.ToString("F3"));
    infoList.Add("cam: (" + camLoc.X.ToString("F1") + ", " + camLoc.Y.ToString("F1") + ", " + camLoc.Z.ToString("F1") + ")");
    infoList.Add("target: (" + target.X.ToString("F1") + ", " + target.Y.ToString("F1") + ", " + target.Z.ToString("F1") + ")");
    infoList.Add("radius: " + radius_start.ToString("F1") + " -> " + radius_end.ToString("F1"));
    infoList.Add("height: " + height_start.ToString("F1") + " -> " + height_end.ToString("F1"));
    infoList.Add("rotation: " + rotation_deg.ToString("F1") + " deg");

    info = infoList;
  }

  private Point3d HelixPoint(
    double t, Point3d center,
    double r0, double r1,
    double h0, double h1,
    double rotDeg, double startRad)
  {
    double angle = startRad + t * rotDeg * Math.PI / 180.0;
    double radius = r0 + (r1 - r0) * t;
    double height = h0 + (h1 - h0) * t;
    return new Point3d(
      center.X + Math.Cos(angle) * radius,
      center.Y + Math.Sin(angle) * radius,
      center.Z + height);
  }

  private RhinoView FindView(RhinoDoc doc, string name)
  {
    if (!string.IsNullOrEmpty(name))
    {
      RhinoView[] views = doc.Views.GetViewList(true, false);
      for (int i = 0; i < views.Length; i++)
      {
        if (views[i].ActiveViewport.Name == name) return views[i];
      }
    }
    return doc.Views.ActiveView;
  }
}
```

---

## make_gif

**User Object Properties:**
- **Name:** Make GIF
- **Nickname:** GIF
- **Description:** Builds animated GIF from PNG frames via ffmpeg. Supports boomerang with hold. Auto-detects sequence pattern.
- **Sub-Category:** Animation
- **Panel Section:** 2
- **Icon:** `scripts/icons/make_gif.png`

**Inputs:**
- `input_folder` / `output_gif` (Item, string)
- `fps` (Item, int)
- `file_pattern` / `ffmpeg_path` (Item, string)
- `width` (Item, int)
- `boomerang` (Item, bool)
- `hold_sec` (Item, double)
- `run` (Item, bool)

**Outputs:** `out_gif_path`, `out_log`

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  private bool _lastRun = false;

  private void RunScript(
    string input_folder,
    string output_gif,
    int fps,
    string file_pattern,
    string ffmpeg_path,
    int width,
    bool boomerang,
    double hold_sec,
    bool run,
    ref object out_gif_path,
    ref object out_log)
  {
    List<string> log = new List<string>();
    out_log = log;
    out_gif_path = "";

    if (fps <= 0) fps = 30;
    if (string.IsNullOrEmpty(file_pattern)) file_pattern = "*.png";
    if (string.IsNullOrEmpty(ffmpeg_path)) ffmpeg_path = "ffmpeg";
    if (width <= 0) width = -1;
    if (hold_sec < 0) hold_sec = 0;

    if (!run || _lastRun) { _lastRun = run; out_log = log; return; }
    _lastRun = run;

    if (string.IsNullOrEmpty(input_folder) || !Directory.Exists(input_folder))
    {
      log.Add("ERROR: input_folder missing");
      out_log = log; return;
    }
    if (string.IsNullOrEmpty(output_gif))
      output_gif = Path.Combine(input_folder, "out.gif");
    if (!output_gif.EndsWith(".gif")) output_gif += ".gif";

    string inputArg;
    int frameCount = 0;
    if (file_pattern.Contains("*"))
    {
      string ext = Path.GetExtension(file_pattern);
      if (string.IsNullOrEmpty(ext)) ext = ".png";
      string[] files = Directory.GetFiles(input_folder, "*" + ext);
      if (files.Length == 0)
      {
        log.Add("ERROR: no " + ext + " files in " + input_folder);
        out_log = log; return;
      }
      Array.Sort(files);
      frameCount = files.Length;
      Match m = Regex.Match(Path.GetFileName(files[0]), @"^(.*?)(\d+)(\.\w+)$");
      if (!m.Success)
      {
        log.Add("ERROR: cannot detect number pattern in '" + Path.GetFileName(files[0]) + "'");
        out_log = log; return;
      }
      string prefix = m.Groups[1].Value;
      int digitCount = m.Groups[2].Value.Length;
      string suffix = m.Groups[3].Value;
      int startNum = int.Parse(m.Groups[2].Value);
      string seqPattern = prefix + "%0" + digitCount + "d" + suffix;
      inputArg = "-start_number " + startNum + " -i \"" + Path.Combine(input_folder, seqPattern) + "\"";
      log.Add("detected: " + seqPattern + " start=" + startNum + " count=" + frameCount);
    }
    else
    {
      inputArg = "-i \"" + Path.Combine(input_folder, file_pattern) + "\"";
    }

    string palettePath = Path.Combine(input_folder, "_palette.png");
    string holdFilter = (hold_sec > 0)
      ? ",tpad=stop_duration=" + hold_sec.ToString("F2").Replace(",", ".") + ":stop_mode=clone"
      : "";
    string scaleFilter = (width > 0) ? ",scale=" + width + ":-1:flags=lanczos" : "";

    string filterPrep;
    string mainTag;
    if (boomerang)
    {
      filterPrep =
        "[0:v]split[fwd][rev_src];" +
        "[rev_src]reverse,trim=start_frame=1,setpts=PTS-STARTPTS[rev];" +
        "[fwd]null" + holdFilter + "[held];" +
        "[held][rev]concat=n=2:v=1,fps=" + fps + scaleFilter;
      mainTag = "[main]";
    }
    else
    {
      filterPrep = "[0:v]null" + holdFilter + ",fps=" + fps + scaleFilter;
      mainTag = "[main]";
    }

    Stopwatch sw = Stopwatch.StartNew();

    string args1 = "-y -framerate " + fps + " " + inputArg +
                   " -filter_complex \"" + filterPrep + ",palettegen[pal]\"" +
                   " -map \"[pal]\" \"" + palettePath + "\"";
    log.Add("pass 1 palette");
    int code1 = RunProcess(ffmpeg_path, args1, log);
    if (code1 != 0)
    {
      log.Add("ERROR: palette failed (code " + code1 + ")");
      out_log = log; return;
    }

    string args2 = "-y -framerate " + fps + " " + inputArg + " -i \"" + palettePath + "\"" +
                   " -filter_complex \"" + filterPrep + mainTag + ";" + mainTag + "[1:v]paletteuse[out]\"" +
                   " -map \"[out]\" -loop 0 \"" + output_gif + "\"";
    log.Add("pass 2 gif");
    int code2 = RunProcess(ffmpeg_path, args2, log);

    sw.Stop();

    if (code2 == 0 && File.Exists(output_gif))
    {
      FileInfo fi = new FileInfo(output_gif);
      log.Add("DONE: " + output_gif + " (" + (fi.Length / 1024).ToString() + " KB, " + (sw.ElapsedMilliseconds / 1000.0).ToString("F1") + " s)");
      out_gif_path = output_gif;
    }
    else { log.Add("ERROR: gif failed (code " + code2 + ")"); }

    try { if (File.Exists(palettePath)) File.Delete(palettePath); } catch { }

    out_log = log;
  }

  private int RunProcess(string exe, string args, List<string> log)
  {
    try
    {
      ProcessStartInfo psi = new ProcessStartInfo();
      psi.FileName = exe;
      psi.Arguments = args;
      psi.UseShellExecute = false;
      psi.CreateNoWindow = true;
      psi.RedirectStandardError = true;
      psi.RedirectStandardOutput = true;
      Process p = Process.Start(psi);
      string stderr = p.StandardError.ReadToEnd();
      p.WaitForExit();
      if (p.ExitCode != 0 && stderr.Length > 0)
      {
        string snippet = stderr.Length > 400 ? stderr.Substring(stderr.Length - 400) : stderr;
        log.Add("stderr: " + snippet);
      }
      return p.ExitCode;
    }
    catch (Exception ex)
    {
      log.Add("exception: " + ex.Message);
      return -1;
    }
  }
}
```

---

## nice_preview

**User Object Properties:**
- **Name:** Nice Preview
- **Nickname:** Preview
- **Description:** Persistent shaded Brep preview via DisplayConduit. Survives selection state and display modes. Custom color, shine, transparency.
- **Sub-Category:** Animation
- **Panel Section:** 2
- **Icon:** `scripts/icons/nice_preview.png`

**Inputs:**
- `breps` (List, Brep)
- `color` (Item, Color)
- `shine` / `transparency` (Item, double)
- `two_sided` / `enabled` (Item, bool)

**Outputs:** `info`

```csharp
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

public class Script_Instance : GH_ScriptInstance
{
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

    if (color.A == 0) color = Color.FromArgb(180, 180, 180);
    if (shine < 0) shine = 0;
    if (shine > 1) shine = 1;
    if (transparency < 0) transparency = 0;
    if (transparency > 1) transparency = 1;

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

    DisplayMaterial mat = new DisplayMaterial(color, transparency);
    mat.Diffuse = color;
    int sR = Math.Min(255, color.R + (int)(80 * shine));
    int sG = Math.Min(255, color.G + (int)(80 * shine));
    int sB = Math.Min(255, color.B + (int)(80 * shine));
    mat.Specular = Color.FromArgb(sR, sG, sB);
    mat.Shine = shine;
    mat.IsTwoSided = two_sided;
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
```

---

# ROBOT

## safety_plane_snake

**User Object Properties:**
- **Name:** Safety Plane Snake
- **Nickname:** Snake
- **Description:** Robot traversal for 3 plane trees with custom order (A asc + C asc, then B outside-in) and snake flip. Safety lift between branches. Speed modulation per branch start.
- **Sub-Category:** Robot
- **Panel Section:** 1
- **Icon:** `scripts/icons/safety_plane_snake.png`

**Inputs:**
- `planes_a` / `planes_b` / `planes_c` (Tree, Plane)
- `safety_z` / `cut_speed` / `safe_speed` (Item, double)
- `b_edge_slow_factor` / `start_boost_factor` (Item, double)
- `start_boost_count` (Item, int)

**Outputs:** `out_planes`, `out_speeds`, `out_motion`, `out_visit`, `out_order`, `info`

```csharp
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

    List<int> visitTree   = new List<int>();
    List<int> visitBranch = new List<int>();

    int lenA = (planes_a != null) ? planes_a.BranchCount : 0;
    int lenB = (planes_b != null) ? planes_b.BranchCount : 0;
    int lenC = (planes_c != null) ? planes_c.BranchCount : 0;

    int pairs = Math.Min(lenA, lenC);
    for (int i = 0; i < pairs; i++)
    {
      visitTree.Add(0); visitBranch.Add(i);
      visitTree.Add(2); visitBranch.Add(i);
    }
    for (int i = pairs; i < lenA; i++) { visitTree.Add(0); visitBranch.Add(i); }
    for (int i = pairs; i < lenC; i++) { visitTree.Add(2); visitBranch.Add(i); }

    for (int p = 0; p < lenB; p++)
    {
      int idx = (p % 2 == 0) ? (p / 2) : (lenB - 1 - (p - 1) / 2);
      visitTree.Add(1); visitBranch.Add(idx);
    }

    DataTree<Plane>[] trees = new DataTree<Plane>[] { planes_a, planes_b, planes_c };
    string[] names = new string[] { "A", "B", "C" };

    List<string> orderStrs = new List<string>();
    for (int v = 0; v < visitTree.Count; v++)
      orderStrs.Add(names[visitTree[v]] + visitBranch[v].ToString());
    orderLog.Add(string.Join(" -> ", orderStrs.ToArray()));

    int cutCount = 0;
    for (int v = 0; v < visitTree.Count; v++)
    {
      int treeIdx = visitTree[v];
      DataTree<Plane> tree = trees[treeIdx];
      int b = visitBranch[v];
      if (tree == null || b < 0 || b >= tree.BranchCount) continue;

      List<Plane> branch = new List<Plane>(tree.Branch(b));
      if (branch.Count == 0) continue;

      if (v % 2 == 1) branch.Reverse();

      double branchMul = 1.0;
      bool isBEdge = (treeIdx == 1) && (b == 0 || b == lenB - 1);
      if (isBEdge) branchMul = b_edge_slow_factor;

      Plane first = branch[0];
      Plane last  = branch[branch.Count - 1];
      Plane safeIn  = LiftZ(first, safety_z);
      Plane safeOut = LiftZ(last,  safety_z);

      string tag = names[treeIdx] + b.ToString() + "_pos" + v.ToString();
      if (isBEdge) tag += "_SLOW";

      resultPlanes.Add(safeIn);
      resultSpeeds.Add(safe_speed);
      resultMotion.Add("Joint");
      visitLog.Add(tag + "_in");

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
```

---

## robot_planner_per_curve

**User Object Properties:**
- **Name:** Robot Planner Per Curve
- **Nickname:** PerCurve
- **Description:** Per-curve enter/cut/exit with full transit_z lift between every curve. Planes follow tangent direction.
- **Sub-Category:** Robot
- **Panel Section:** 1
- **Icon:** `scripts/icons/robot_planner_per_curve.png`

**Inputs:**
- `curves` (List, Curve)
- `point_step` / `cut_speed` / `safe_speed` / `safe_z` / `transit_z` (Item, double)
- `tool_dir` (Item, Vector3d)
- `wrist_limit_deg` (Item, double)

**Outputs:** `out_planes`, `out_speeds`, `out_motion`, `out_visit`

```csharp
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
    double point_step,
    double cut_speed,
    double safe_speed,
    double safe_z,
    double transit_z,
    Vector3d tool_dir,
    double wrist_limit_deg,
    ref object out_planes,
    ref object out_speeds,
    ref object out_motion,
    ref object out_visit)
  {
    List<Plane>  resultPlanes = new List<Plane>();
    List<double> resultSpeeds = new List<double>();
    List<string> resultMotion = new List<string>();
    List<int>    visitLog     = new List<int>();
    out_planes = resultPlanes;
    out_speeds = resultSpeeds;
    out_motion = resultMotion;
    out_visit  = visitLog;

    if (curves == null || curves.Count == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No curves");
      return;
    }
    if (point_step <= 0) point_step = 5.0;
    if (cut_speed <= 0) cut_speed = 100;
    if (safe_speed <= 0) safe_speed = 200;
    if (safe_z <= 0) safe_z = 30;
    if (tool_dir.Length < 1e-10) tool_dir = Vector3d.ZAxis;
    tool_dir.Unitize();

    double wristLimitRad = wrist_limit_deg > 0
      ? wrist_limit_deg * Math.PI / 180.0
      : double.MaxValue;

    for (int c = 0; c < curves.Count; c++)
    {
      Curve crv = curves[c];
      if (crv == null || !crv.IsValid) continue;

      List<Plane> cuts = SampleCurveToPlanes(crv, point_step, tool_dir);
      if (cuts.Count < 2) continue;

      Plane first = cuts[0];
      Plane last  = cuts[cuts.Count - 1];

      Plane transitIn = MakeTransitPlane(first, transit_z);
      Plane firstLift = LiftPlaneWorldZ(first, safe_z);

      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                transitIn, safe_speed, "Joint", c);
      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                firstLift, safe_speed, "Joint", c);
      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                first, safe_speed, "Linear", c);

      double accumAng = 0.0;
      Plane prev = first;

      for (int j = 1; j < cuts.Count; j++)
      {
        Plane curr = cuts[j];
        double dAng = SignedAngleAroundZ(prev.XAxis, curr.XAxis, prev.ZAxis);
        accumAng += Math.Abs(dAng);

        if (accumAng > wristLimitRad)
        {
          Plane prevLift = LiftPlaneWorldZ(prev, safe_z);
          Plane currLift = LiftPlaneWorldZ(curr, safe_z);
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, prevLift, safe_speed, "Linear", c);
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, currLift, safe_speed, "Joint", c);
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, curr, safe_speed, "Linear", c);
          accumAng = 0.0;
        }
        else
        {
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, curr, cut_speed, "Linear", c);
        }
        prev = curr;
      }

      Plane lastLift   = LiftPlaneWorldZ(last, safe_z);
      Plane transitOut = MakeTransitPlane(last, transit_z);

      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                lastLift, safe_speed, "Linear", c);
      AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog,
                transitOut, safe_speed, "Joint", c);
    }

    out_planes = resultPlanes;
    out_speeds = resultSpeeds;
    out_motion = resultMotion;
    out_visit  = visitLog;
  }

  private List<Plane> SampleCurveToPlanes(Curve crv, double step, Vector3d toolDir)
  {
    List<Plane> result = new List<Plane>();
    if (crv == null) return result;
    Point3d[] pts;
    double[] tParams = crv.DivideByLength(step, true, out pts);
    if (tParams == null || pts == null || pts.Length < 2) return result;
    for (int i = 0; i < pts.Length; i++)
    {
      Vector3d tangent = crv.TangentAt(tParams[i]);
      if (tangent.Length < 1e-10) continue;
      tangent.Unitize();
      Vector3d xAxis = tangent - toolDir * (tangent * toolDir);
      if (xAxis.Length < 1e-10) xAxis = Vector3d.XAxis;
      xAxis.Unitize();
      Vector3d yAxis = Vector3d.CrossProduct(toolDir, xAxis);
      yAxis.Unitize();
      result.Add(new Plane(pts[i], xAxis, yAxis));
    }
    return result;
  }

  private void AddRecord(
    List<Plane> p, List<double> s, List<string> m, List<int> v,
    Plane plane, double speed, string motion, int branch)
  {
    p.Add(plane); s.Add(speed); m.Add(motion); v.Add(branch);
  }

  private Plane LiftPlaneWorldZ(Plane p, double dist)
  {
    Plane lifted = new Plane(p);
    lifted.Origin = new Point3d(p.Origin.X, p.Origin.Y, p.Origin.Z + dist);
    return lifted;
  }

  private Plane MakeTransitPlane(Plane p, double transit_z)
  {
    Point3d o = new Point3d(p.Origin.X, p.Origin.Y, transit_z);
    return new Plane(o, Vector3d.XAxis, Vector3d.YAxis);
  }

  private double SignedAngleAroundZ(Vector3d a, Vector3d b, Vector3d axis)
  {
    Vector3d aProj = a - axis * (a * axis);
    Vector3d bProj = b - axis * (b * axis);
    if (!aProj.Unitize() || !bProj.Unitize()) return 0.0;
    double dot = aProj * bProj;
    if (dot > 1.0) dot = 1.0; else if (dot < -1.0) dot = -1.0;
    double ang = Math.Acos(dot);
    Vector3d cross = Vector3d.CrossProduct(aProj, bProj);
    if (cross * axis < 0) ang = -ang;
    return ang;
  }
}
```

---

## robot_planner_grouped_curves

**User Object Properties:**
- **Name:** Robot Planner Grouped
- **Nickname:** Grouped
- **Description:** Robot path for DataTree<Curve> with per-group transit_z + per-curve safe_z. Snake reversal within group.
- **Sub-Category:** Robot
- **Panel Section:** 1
- **Icon:** `scripts/icons/robot_planner_grouped_curves.png`

**Inputs:**
- `groups` (Tree, Curve)
- `point_step` / `cut_speed` / `safe_speed` / `safe_z` / `transit_z` (Item, double)
- `tool_dir` (Item, Vector3d)
- `snake` (Item, bool)
- `wrist_limit_deg` (Item, double)

**Outputs:** `out_planes`, `out_speeds`, `out_motion`, `out_visit`

```csharp
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
    DataTree<Curve> groups,
    double point_step,
    double cut_speed,
    double safe_speed,
    double safe_z,
    double transit_z,
    Vector3d tool_dir,
    bool snake,
    double wrist_limit_deg,
    ref object out_planes,
    ref object out_speeds,
    ref object out_motion,
    ref object out_visit)
  {
    List<Plane>  resultPlanes = new List<Plane>();
    List<double> resultSpeeds = new List<double>();
    List<string> resultMotion = new List<string>();
    List<int>    visitLog     = new List<int>();
    out_planes = resultPlanes;
    out_speeds = resultSpeeds;
    out_motion = resultMotion;
    out_visit  = visitLog;

    if (groups == null || groups.BranchCount == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No groups");
      return;
    }
    if (point_step <= 0) point_step = 5.0;
    if (cut_speed <= 0) cut_speed = 100;
    if (safe_speed <= 0) safe_speed = 200;
    if (safe_z <= 0) safe_z = 30;
    if (tool_dir.Length < 1e-10) tool_dir = Vector3d.ZAxis;
    tool_dir.Unitize();

    double wristLimitRad = wrist_limit_deg > 0
      ? wrist_limit_deg * Math.PI / 180.0
      : double.MaxValue;

    int curveCount = 0;

    for (int b = 0; b < groups.BranchCount; b++)
    {
      List<Curve> curves = ToCurveList(groups.Branch(b));
      if (curves.Count == 0) continue;

      for (int c = 0; c < curves.Count; c++)
      {
        Curve crv = curves[c];
        if (crv == null || !crv.IsValid) continue;

        Curve workCrv = crv.DuplicateCurve();
        if (snake && (c % 2 == 1)) workCrv.Reverse();

        List<Plane> cuts = SampleCurveToPlanes(workCrv, point_step, tool_dir);
        if (cuts.Count < 2) continue;

        Plane first = cuts[0];
        Plane last  = cuts[cuts.Count - 1];

        if (c == 0)
        {
          Plane transitIn = MakeTransitPlane(first, transit_z);
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, transitIn, safe_speed, "Joint", b);
        }
        Plane firstLift = LiftPlaneWorldZ(first, safe_z);
        AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, firstLift, safe_speed, "Joint", b);
        AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, first, safe_speed, "Linear", b);

        double accumAng = 0.0;
        Plane prev = first;

        for (int j = 1; j < cuts.Count; j++)
        {
          Plane curr = cuts[j];
          double dAng = SignedAngleAroundZ(prev.XAxis, curr.XAxis, prev.ZAxis);
          accumAng += Math.Abs(dAng);

          if (accumAng > wristLimitRad)
          {
            Plane prevLift = LiftPlaneWorldZ(prev, safe_z);
            Plane currLift = LiftPlaneWorldZ(curr, safe_z);
            AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, prevLift, safe_speed, "Linear", b);
            AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, currLift, safe_speed, "Joint", b);
            AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, curr, safe_speed, "Linear", b);
            accumAng = 0.0;
          }
          else
          {
            AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, curr, cut_speed, "Linear", b);
          }
          prev = curr;
        }

        Plane lastLift = LiftPlaneWorldZ(last, safe_z);
        AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, lastLift, safe_speed, "Linear", b);

        if (c == curves.Count - 1)
        {
          Plane transitOut = MakeTransitPlane(last, transit_z);
          AddRecord(resultPlanes, resultSpeeds, resultMotion, visitLog, transitOut, safe_speed, "Joint", b);
        }

        curveCount++;
      }
    }

    out_planes = resultPlanes;
    out_speeds = resultSpeeds;
    out_motion = resultMotion;
    out_visit  = visitLog;
  }

  private List<Curve> ToCurveList(IList src)
  {
    List<Curve> result = new List<Curve>();
    if (src == null) return result;
    for (int i = 0; i < src.Count; i++)
    {
      object item = src[i];
      Curve c = item as Curve;
      if (c != null) { result.Add(c); continue; }
      GH_Curve gc = item as GH_Curve;
      if (gc != null) { result.Add(gc.Value); continue; }
    }
    return result;
  }

  private List<Plane> SampleCurveToPlanes(Curve crv, double step, Vector3d toolDir)
  {
    List<Plane> result = new List<Plane>();
    if (crv == null) return result;
    Point3d[] pts;
    double[] tParams = crv.DivideByLength(step, true, out pts);
    if (tParams == null || pts == null || pts.Length < 2) return result;
    for (int i = 0; i < pts.Length; i++)
    {
      Vector3d tangent = crv.TangentAt(tParams[i]);
      if (tangent.Length < 1e-10) continue;
      tangent.Unitize();
      Vector3d xAxis = tangent - toolDir * (tangent * toolDir);
      if (xAxis.Length < 1e-10) xAxis = Vector3d.XAxis;
      xAxis.Unitize();
      Vector3d yAxis = Vector3d.CrossProduct(toolDir, xAxis);
      yAxis.Unitize();
      result.Add(new Plane(pts[i], xAxis, yAxis));
    }
    return result;
  }

  private void AddRecord(
    List<Plane> p, List<double> s, List<string> m, List<int> v,
    Plane plane, double speed, string motion, int branch)
  {
    p.Add(plane); s.Add(speed); m.Add(motion); v.Add(branch);
  }

  private Plane LiftPlaneWorldZ(Plane p, double dist)
  {
    Plane lifted = new Plane(p);
    lifted.Origin = new Point3d(p.Origin.X, p.Origin.Y, p.Origin.Z + dist);
    return lifted;
  }

  private Plane MakeTransitPlane(Plane p, double transit_z)
  {
    Point3d o = new Point3d(p.Origin.X, p.Origin.Y, transit_z);
    return new Plane(o, Vector3d.XAxis, Vector3d.YAxis);
  }

  private double SignedAngleAroundZ(Vector3d a, Vector3d b, Vector3d axis)
  {
    Vector3d aProj = a - axis * (a * axis);
    Vector3d bProj = b - axis * (b * axis);
    if (!aProj.Unitize() || !bProj.Unitize()) return 0.0;
    double dot = aProj * bProj;
    if (dot > 1.0) dot = 1.0; else if (dot < -1.0) dot = -1.0;
    double ang = Math.Acos(dot);
    Vector3d cross = Vector3d.CrossProduct(aProj, bProj);
    if (cross * axis < 0) ang = -ang;
    return ang;
  }
}
```

---

# GEOMETRY

## curve_corner_splitter

**User Object Properties:**
- **Name:** Curve Corner Splitter
- **Nickname:** Corners
- **Description:** Splits closed polylines at outer corners (angle below threshold). Group-based detection, wrap-aware, min-segment merging.
- **Sub-Category:** Geometry
- **Panel Section:** 1
- **Icon:** `scripts/icons/curve_corner_splitter.png`

**Inputs:**
- `curves` (List, Curve)
- `angle_deg` / `sample_step` / `min_segment` (Item, double)

**Outputs:** `segments`, `info`

```csharp
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
      if (crv == null || !crv.IsValid) { infoList.Add("c" + ci.ToString() + ": invalid"); continue; }

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
          for (int k = 0; k < dP.Length; k++) { tList.Add(dP[k]); ptList.Add(dPts[k]); }
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
          for (int k = 0; k < dP.Length; k++) { tList.Add(dP[k]); ptList.Add(dPts[k]); }
      }

      bool isClosed = crv.IsClosed;
      if (!isClosed && ptList.Count >= 2 && ptList[0].DistanceTo(ptList[ptList.Count - 1]) < tol * 10) isClosed = true;
      int n = tList.Count;
      if (isClosed && n > 1 && ptList[n - 1].DistanceTo(ptList[0]) < tol * 10) n--;

      if (n < 3) { infoList.Add("c" + ci.ToString() + ": too few pts"); segTree.Add(crv.DuplicateCurve(), path); continue; }

      double[] ang = new double[n];
      for (int i = 0; i < n; i++)
      {
        int ip   = isClosed ? (i - 1 + n) % n : (i > 0     ? i - 1 : -1);
        int inxt = isClosed ? (i + 1) % n     : (i < n - 1 ? i + 1 : -1);
        if (ip < 0 || inxt < 0) { ang[i] = 180.0; continue; }
        Vector3d vIn  = ptList[i]    - ptList[ip];
        Vector3d vOut = ptList[inxt] - ptList[i];
        if (vIn.Length < 1e-10 || vOut.Length < 1e-10) { ang[i] = 180.0; continue; }
        vIn.Unitize(); vOut.Unitize();
        double d = -(vIn.X * vOut.X + vIn.Y * vOut.Y + vIn.Z * vOut.Z);
        d = Math.Max(-1.0, Math.Min(1.0, d));
        ang[i] = RhinoMath.ToDegrees(Math.Acos(d));
      }

      List<int> cand = new List<int>();
      for (int i = 0; i < n; i++) if (ang[i] < angle_deg) cand.Add(i);
      if (cand.Count == 0) { infoList.Add("c" + ci.ToString() + ": 0 corners"); segTree.Add(crv.DuplicateCurve(), path); continue; }

      List<List<int>> groups = new List<List<int>>();
      List<int> cur = new List<int>();
      cur.Add(cand[0]);
      for (int i = 1; i < cand.Count; i++)
      {
        if (cand[i] == cand[i - 1] + 1) cur.Add(cand[i]);
        else { groups.Add(cur); cur = new List<int>(); cur.Add(cand[i]); }
      }
      groups.Add(cur);

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

      List<double> cParams = new List<double>();
      for (int k = 0; k < bestIdx.Count; k++) cParams.Add(tList[bestIdx[k]]);
      cParams.Sort();

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
      if (cParams.Count == 0) { segTree.Add(crv.DuplicateCurve(), path); continue; }

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
      if (pieces == null || pieces.Length == 0) { segTree.Add(crv.DuplicateCurve(), path); continue; }

      int added = 0;
      for (int i = 0; i < pieces.Length; i++)
      {
        if (pieces[i] == null) continue;
        if (pieces[i].GetLength() >= min_segment) { segTree.Add(pieces[i], path); added++; }
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
```

---

## compare_curve_groups

**User Object Properties:**
- **Name:** Compare Curves
- **Nickname:** DiffCrv
- **Description:** Greedy 1-to-1 matching of two curve groups by area + perimeter signature. Finds missing/duplicate items.
- **Sub-Category:** Geometry
- **Panel Section:** 2
- **Icon:** `scripts/icons/compare_curve_groups.png`

**Inputs:**
- `group_a` / `group_b` (List, Curve)
- `tol_area` / `tol_length` (Item, double)

**Outputs:** `only_in_a`, `only_in_b`, `matched_a`, `matched_b`, `idx_only_a`, `idx_only_b`, `info`

```csharp
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

    only_in_a = onlyA; only_in_b = onlyB; matched_a = matA; matched_b = matB;
    idx_only_a = idxOnlyA; idx_only_b = idxOnlyB; info = infoList;

    if (group_a == null) group_a = new List<Curve>();
    if (group_b == null) group_b = new List<Curve>();
    if (tol_area   <= 0) tol_area   = 10.0;
    if (tol_length <= 0) tol_length = 1.0;

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
        double score = dA / Math.Max(tol_area, 1.0) + dL / Math.Max(tol_length, 1.0);
        if (score < bestScore) { bestScore = score; bestJ = j; }
      }
      if (bestJ >= 0) { match[i] = bestJ; usedB[bestJ] = true; }
    }

    for (int i = 0; i < group_a.Count; i++)
    {
      if (match[i] < 0) { onlyA.Add(group_a[i]); idxOnlyA.Add(i); }
      else { matA.Add(group_a[i]); matB.Add(group_b[match[i]]); }
    }
    for (int j = 0; j < group_b.Count; j++)
    {
      if (!usedB[j]) { onlyB.Add(group_b[j]); idxOnlyB.Add(j); }
    }

    infoList.Add("A: " + group_a.Count + ", B: " + group_b.Count);
    infoList.Add("Matched: " + matA.Count);
    infoList.Add("Only in A (missing in B): " + onlyA.Count);
    infoList.Add("Only in B (extra/duplicates): " + onlyB.Count);

    only_in_a = onlyA; only_in_b = onlyB; matched_a = matA; matched_b = matB;
    idx_only_a = idxOnlyA; idx_only_b = idxOnlyB; info = infoList;
  }

  private double CurveArea(Curve crv)
  {
    if (crv == null) return 0;
    AreaMassProperties amp = AreaMassProperties.Compute(crv);
    return (amp != null) ? Math.Abs(amp.Area) : 0;
  }
}
```

---

## compare_brep_groups

**User Object Properties:**
- **Name:** Compare Breps
- **Nickname:** DiffBrep
- **Description:** Greedy 1-to-1 matching of two Brep groups by volume + surface area signature. Rotation/translation invariant.
- **Sub-Category:** Geometry
- **Panel Section:** 2
- **Icon:** `scripts/icons/compare_brep_groups.png`

**Inputs:**
- `group_a` / `group_b` (List, Brep)
- `tol_vol` / `tol_area` (Item, double)

**Outputs:** `only_in_a`, `only_in_b`, `matched_a`, `matched_b`, `idx_only_a`, `idx_only_b`, `info`

```csharp
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

    only_in_a = onlyA; only_in_b = onlyB; matched_a = matA; matched_b = matB;
    idx_only_a = idxOnlyA; idx_only_b = idxOnlyB; info = infoList;

    if (group_a == null) group_a = new List<Brep>();
    if (group_b == null) group_b = new List<Brep>();
    if (tol_vol  <= 0) tol_vol  = 100.0;
    if (tol_area <= 0) tol_area = 50.0;

    double[] volA  = new double[group_a.Count];
    double[] areaA = new double[group_a.Count];
    for (int i = 0; i < group_a.Count; i++) { volA[i] = BrepVolume(group_a[i]); areaA[i] = BrepArea(group_a[i]); }

    double[] volB  = new double[group_b.Count];
    double[] areaB = new double[group_b.Count];
    for (int j = 0; j < group_b.Count; j++) { volB[j] = BrepVolume(group_b[j]); areaB[j] = BrepArea(group_b[j]); }

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
        double score = dV / Math.Max(tol_vol, 1.0) + dA / Math.Max(tol_area, 1.0);
        if (score < bestScore) { bestScore = score; bestJ = j; }
      }
      if (bestJ >= 0) { match[i] = bestJ; usedB[bestJ] = true; }
    }

    for (int i = 0; i < group_a.Count; i++)
    {
      if (match[i] < 0) { onlyA.Add(group_a[i]); idxOnlyA.Add(i); }
      else { matA.Add(group_a[i]); matB.Add(group_b[match[i]]); }
    }
    for (int j = 0; j < group_b.Count; j++)
    {
      if (!usedB[j]) { onlyB.Add(group_b[j]); idxOnlyB.Add(j); }
    }

    infoList.Add("A: " + group_a.Count + ", B: " + group_b.Count);
    infoList.Add("Matched: " + matA.Count);
    infoList.Add("Only in A (missing in B): " + onlyA.Count);
    infoList.Add("Only in B (extra/duplicates): " + onlyB.Count);

    only_in_a = onlyA; only_in_b = onlyB; matched_a = matA; matched_b = matB;
    idx_only_a = idxOnlyA; idx_only_b = idxOnlyB; info = infoList;
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
```

---

## flatten_plates_for_nesting

**User Object Properties:**
- **Name:** Flatten Plates
- **Nickname:** Flatten
- **Description:** Finds largest planar face of each Brep and lays it flat on WorldXY for OpenNest. Outputs outlines, inner holes, transforms.
- **Sub-Category:** Geometry
- **Panel Section:** 3
- **Icon:** `scripts/icons/flatten_plates_for_nesting.png`

**Inputs:**
- `parts` (List, Brep)
- `flatness_tol` (Item, double)

**Outputs:** `outlines`, `inner_holes`, `flat_parts`, `orig_planes`, `xforms_to_xy`, `xforms_back`, `info`

```csharp
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

    outlines = outlinesOut; inner_holes = holesTree; flat_parts = flatOut;
    orig_planes = planesOut; xforms_to_xy = xfFwdOut; xforms_back = xfBackOut; info = infoList;

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
      if (brep == null || !brep.IsValid) { infoList.Add("p" + i + ": invalid"); continue; }

      int bestIdx = -1;
      double bestArea = -1.0;
      Plane bestFacePlane = Plane.Unset;

      for (int f = 0; f < brep.Faces.Count; f++)
      {
        BrepFace face = brep.Faces[f];
        Plane fp;
        if (!face.TryGetPlane(out fp, flatness_tol)) continue;
        Brep single = face.DuplicateFace(false);
        if (single == null) continue;
        double area = single.GetArea();
        if (area > bestArea)
        {
          bestArea = area;
          bestIdx = f;
          if (face.OrientationIsReversed)
            bestFacePlane = new Plane(fp.Origin, fp.XAxis, -fp.YAxis);
          else
            bestFacePlane = fp;
        }
      }

      if (bestIdx < 0) { infoList.Add("p" + i + ": no planar face"); continue; }

      Transform xfFwd = Transform.PlaneToPlane(bestFacePlane, Plane.WorldXY);
      Transform xfBack;
      bool hasInv = xfFwd.TryGetInverse(out xfBack);
      if (!hasInv) xfBack = Transform.Identity;

      Brep flatBrep = brep.DuplicateBrep();
      flatBrep.Transform(xfFwd);

      BrepFace flatFace = flatBrep.Faces[bestIdx];
      Curve outerCrv = null;
      List<Curve> innerCrvs = new List<Curve>();

      for (int li = 0; li < flatFace.Loops.Count; li++)
      {
        BrepLoop loop = flatFace.Loops[li];
        Curve loopCrv = loop.To3dCurve();
        if (loopCrv == null) continue;
        Curve projected = Curve.ProjectToPlane(loopCrv, Plane.WorldXY);
        if (projected != null) loopCrv = projected;
        if (loop.LoopType == BrepLoopType.Outer) outerCrv = loopCrv;
        else if (loop.LoopType == BrepLoopType.Inner) innerCrvs.Add(loopCrv);
      }

      if (outerCrv == null) { infoList.Add("p" + i + ": no outer loop"); continue; }

      outlinesOut.Add(outerCrv);
      for (int k = 0; k < innerCrvs.Count; k++) holesTree.Add(innerCrvs[k], path);
      flatOut.Add(flatBrep);
      planesOut.Add(bestFacePlane);
      xfFwdOut.Add(xfFwd);
      xfBackOut.Add(xfBack);

      infoList.Add("p" + i + ": face=" + bestIdx + " area=" + bestArea.ToString("F0") + " holes=" + innerCrvs.Count);
    }

    outlines = outlinesOut; inner_holes = holesTree; flat_parts = flatOut;
    orig_planes = planesOut; xforms_to_xy = xfFwdOut; xforms_back = xfBackOut; info = infoList;
  }
}
```

---

# PACKAGING

## ecma_a20_20_03_01

**User Object Properties:**
- **Name:** ECMA Reverse Tuck
- **Nickname:** TuckEnd
- **Description:** ECMA A20.20.03.01 Reverse Tuck End -- parametric packaging die. Outputs cut + crease curves + bbox.
- **Sub-Category:** Packaging
- **Panel Section:** 1
- **Icon:** `scripts/icons/ecma_a20_20_03_01.png`

**Inputs:**
- `L` / `P` / `A` / `S` (Item, double) — Außenmaße + Materialstärke
- `inc` / `ganc` / `chamf` / `taper` / `fessura_l` / `fessura_p` / `nurbs_k` (Item, double)

**Outputs:** `cut`, `crease`, `outline_bbox`, `info`

```csharp
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
    double L, double P, double A, double S,
    double inc, double ganc, double chamf, double taper,
    double fessura_l, double fessura_p, double nurbs_k,
    ref object cut, ref object crease, ref object outline_bbox, ref object info)
  {
    List<Curve> cutList = new List<Curve>();
    List<Curve> creaseList = new List<Curve>();
    List<string> infoList = new List<string>();
    cut = cutList; crease = creaseList; outline_bbox = null; info = infoList;

    if (L  <= 0) L  = 50.0;
    if (P  <= 0) P  = 25.0;
    if (A  <= 0) A  = 80.0;
    if (S  <= 0) S  = 0.5;
    if (inc        <= 0) inc        = 18.0;
    if (ganc       <= 0) ganc       = 18.0;
    if (chamf      <= 0) chamf      = 3.0;
    if (taper      <= 0) taper      = 2.0;
    if (fessura_l  <= 0) fessura_l  = 7.0;
    if (fessura_p  <= 0) fessura_p  = 2.5;
    if (nurbs_k    <= 0) nurbs_k    = 0.635;

    if (P < inc + 3) { this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "P < inc+3mm"); return; }
    if (A <= 2 * S)  { this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A must be > 2*S"); return; }

    double body_h  = A - 2 * S;
    double orecchio = 2 * S;

    double x0 = 0.0;
    double x1 = inc;
    double x2 = x1 + L - S;
    double x3 = x2 + P;
    double x4 = x3 + L;
    double x5 = x4 + P - S;

    double y_bot = 0.0, y_top = body_h;
    double y_bot_t = y_bot - S, y_top_t = y_top + S;
    double dust_d = Math.Min((P + ganc - S) / 2.0, L / 2.0);
    double tuck_b_fold = y_bot_t - P + orecchio;
    double tuck_b_ear  = y_bot_t - P;
    double tuck_b_tip  = tuck_b_fold - ganc;
    double tuck_t_fold = y_top_t + P - orecchio;
    double tuck_t_ear  = y_top_t + P;
    double tuck_t_tip  = tuck_t_fold + ganc;
    double cx = (x3 + x4) / 2.0;
    double y_inc_top = y_top - S, y_inc_bot = y_bot;

    // CREASES
    AddLine(creaseList, x1, y_bot, x1, y_inc_top);
    AddLine(creaseList, x2, y_bot, x2, y_top);
    AddLine(creaseList, x3, y_bot, x3, y_top);
    AddLine(creaseList, x4, y_bot, x4, y_top);
    AddLine(creaseList, x2 + S, y_bot, x3, y_bot);
    AddLine(creaseList, x3, y_bot_t, cx, y_bot_t);
    AddLine(creaseList, cx, y_bot_t, x4, y_bot_t);
    AddLine(creaseList, x4, y_bot, x5, y_bot);
    AddLine(creaseList, x1, y_top_t, x2, y_top_t);
    AddLine(creaseList, x2, y_top, x3 - S, y_top);
    AddLine(creaseList, x4 + S, y_top, x5, y_top);
    AddLine(creaseList, x3 + fessura_l, tuck_b_fold, cx, tuck_b_fold);
    AddLine(creaseList, cx, tuck_b_fold, x4 - fessura_l, tuck_b_fold);
    AddLine(creaseList, x1 + fessura_l, tuck_t_fold, x2 - fessura_l, tuck_t_fold);

    // CUTS body
    AddLine(cutList, x1, y_bot, x2, y_bot);
    AddLine(cutList, x3, y_top, x4, y_top);
    AddLine(cutList, x5, y_bot, x5, y_top);
    AddLine(cutList, x2, y_bot, x2 + S, y_bot);
    AddLine(cutList, x3, y_bot, x3, y_bot_t);
    AddLine(cutList, x4, y_bot_t, x4, y_bot);
    AddLine(cutList, x2, y_top, x2, y_top_t);
    AddLine(cutList, x3 - S, y_top, x3, y_top);
    AddLine(cutList, x4, y_top, x4 + S, y_top);

    // Glue tab
    AddLine(cutList, x1, y_inc_bot, x0, y_inc_bot + chamf);
    AddLine(cutList, x0, y_inc_bot + chamf, x0, y_inc_top - chamf);
    AddLine(cutList, x0, y_inc_top - chamf, x1, y_inc_top);

    // Dust flap bot P2
    double df_b2_ox = x2 + S;
    AddLine(cutList, df_b2_ox, y_bot, df_b2_ox, y_bot - chamf - taper);
    AddLine(cutList, df_b2_ox, y_bot - chamf - taper, df_b2_ox + chamf, y_bot - 2 * chamf - taper);
    AddLine(cutList, df_b2_ox + chamf, y_bot - 2 * chamf - taper, df_b2_ox + chamf + taper, y_bot - dust_d);
    AddLine(cutList, x3, y_bot, x3 - chamf, y_bot - chamf);
    AddLine(cutList, x3 - chamf, y_bot - chamf, x3 - chamf, y_bot - dust_d);
    AddLine(cutList, df_b2_ox + chamf + taper, y_bot - dust_d, x3 - chamf, y_bot - dust_d);

    // Dust flap bot P4
    AddLine(cutList, x4, y_bot, x4 + chamf, y_bot - chamf);
    AddLine(cutList, x4 + chamf, y_bot - chamf, x4 + chamf, y_bot - dust_d);
    AddLine(cutList, x5, y_bot, x5, y_bot - chamf - taper);
    AddLine(cutList, x5, y_bot - chamf - taper, x5 - chamf, y_bot - 2 * chamf - taper);
    AddLine(cutList, x5 - chamf, y_bot - 2 * chamf - taper, x5 - chamf - taper, y_bot - dust_d);
    AddLine(cutList, x4 + chamf, y_bot - dust_d, x5 - chamf - taper, y_bot - dust_d);

    // Dust flap top P2
    AddLine(cutList, x2, y_top, x2 + chamf, y_top + chamf);
    AddLine(cutList, x2 + chamf, y_top + chamf, x2 + chamf, y_top + dust_d);
    double df_t2_ix = x3 - S;
    AddLine(cutList, df_t2_ix, y_top, df_t2_ix, y_top + chamf + taper);
    AddLine(cutList, df_t2_ix, y_top + chamf + taper, df_t2_ix - chamf, y_top + 2 * chamf + taper);
    AddLine(cutList, df_t2_ix - chamf, y_top + 2 * chamf + taper, df_t2_ix - chamf - taper, y_top + dust_d);
    AddLine(cutList, x2 + chamf, y_top + dust_d, df_t2_ix - chamf - taper, y_top + dust_d);

    // Dust flap top P4
    double df_t4_ix = x4 + S;
    AddLine(cutList, df_t4_ix, y_top, df_t4_ix, y_top + chamf + taper);
    AddLine(cutList, df_t4_ix, y_top + chamf + taper, df_t4_ix + chamf, y_top + 2 * chamf + taper);
    AddLine(cutList, df_t4_ix + chamf, y_top + 2 * chamf + taper, df_t4_ix + chamf + taper, y_top + dust_d);
    AddLine(cutList, x5, y_top, x5 - chamf, y_top + chamf);
    AddLine(cutList, x5 - chamf, y_top + chamf, x5 - chamf, y_top + dust_d);
    AddLine(cutList, df_t4_ix + chamf + taper, y_top + dust_d, x5 - chamf, y_top + dust_d);

    // Tuck bottom
    AddLine(cutList, x3, y_bot_t, x3, tuck_b_ear);
    AddLine(cutList, x4, y_bot_t, x4, tuck_b_ear);
    AddLine(cutList, x3, tuck_b_ear, x3 + fessura_l, tuck_b_ear);
    AddLine(cutList, x3 + fessura_l, tuck_b_ear, x3 + fessura_l, tuck_b_ear + fessura_p);
    AddLine(cutList, x4, tuck_b_ear, x4 - fessura_l, tuck_b_ear);
    AddLine(cutList, x4 - fessura_l, tuck_b_ear, x4 - fessura_l, tuck_b_ear + fessura_p);
    double tip_b_l = x3 + fessura_l + orecchio;
    double tip_b_r = x4 - fessura_l - orecchio;
    AddLine(cutList, tip_b_l, tuck_b_tip, tip_b_r, tuck_b_tip);
    double ctrl_b_y = tuck_b_ear + (tuck_b_tip - tuck_b_ear) * nurbs_k;
    AddNurbs2(cutList, x3, tuck_b_ear, x3, ctrl_b_y, tip_b_l, tuck_b_tip);
    AddNurbs2(cutList, x4, tuck_b_ear, x4, ctrl_b_y, tip_b_r, tuck_b_tip);

    // Tuck top
    AddLine(cutList, x1, y_inc_top, x1, y_top_t);
    AddLine(cutList, x2, y_top_t, x2, tuck_t_ear);
    AddLine(cutList, x1, y_top_t, x1, tuck_t_ear);
    AddLine(cutList, x1, tuck_t_ear, x1 + fessura_l, tuck_t_ear);
    AddLine(cutList, x1 + fessura_l, tuck_t_ear, x1 + fessura_l, tuck_t_ear - fessura_p);
    AddLine(cutList, x2, tuck_t_ear, x2 - fessura_l, tuck_t_ear);
    AddLine(cutList, x2 - fessura_l, tuck_t_ear, x2 - fessura_l, tuck_t_ear - fessura_p);
    double tip_t_l = x1 + fessura_l + orecchio;
    double tip_t_r = x2 - fessura_l - orecchio;
    AddLine(cutList, tip_t_l, tuck_t_tip, tip_t_r, tuck_t_tip);
    double ctrl_t_y = tuck_t_ear + (tuck_t_tip - tuck_t_ear) * nurbs_k;
    AddNurbs2(cutList, x1, tuck_t_ear, x1, ctrl_t_y, tip_t_l, tuck_t_tip);
    AddNurbs2(cutList, x2, tuck_t_ear, x2, ctrl_t_y, tip_t_r, tuck_t_tip);

    Rectangle3d bbox = new Rectangle3d(Plane.WorldXY,
      new Point3d(0, tuck_b_tip, 0), new Point3d(x5, tuck_t_tip, 0));
    outline_bbox = bbox.ToNurbsCurve();

    infoList.Add("ECMA A20.20.03.01 Reverse Tuck End");
    infoList.Add("External:  L=" + L.ToString("F2") + "  P=" + P.ToString("F2") + "  A=" + A.ToString("F2"));
    infoList.Add("Bbox: " + x5.ToString("F1") + " x " + (tuck_t_tip - tuck_b_tip).ToString("F1") + " mm");
    infoList.Add("Cut: " + cutList.Count + " | Crease: " + creaseList.Count);

    cut = cutList; crease = creaseList; info = infoList;
  }

  private void AddLine(List<Curve> list, double ax, double ay, double bx, double by)
  {
    Point3d a = new Point3d(ax, ay, 0);
    Point3d b = new Point3d(bx, by, 0);
    if (a.DistanceTo(b) < 1e-9) return;
    list.Add(new LineCurve(a, b));
  }

  private void AddNurbs2(List<Curve> list, double p0x, double p0y, double cx, double cy, double p2x, double p2y)
  {
    List<Point3d> pts = new List<Point3d>();
    pts.Add(new Point3d(p0x, p0y, 0));
    pts.Add(new Point3d(cx,  cy,  0));
    pts.Add(new Point3d(p2x, p2y, 0));
    NurbsCurve nc = NurbsCurve.Create(false, 2, pts);
    if (nc != null) list.Add(nc);
  }
}
```

---

# UTIL

## rotate_planes_by_value

**User Object Properties:**
- **Name:** Rotate Planes by Value
- **Nickname:** RotByVal
- **Description:** Rotates planes around chosen local axis by angle remapped from per-plane value. 3-point gradient across branches.
- **Sub-Category:** Util
- **Panel Section:** 1
- **Icon:** `scripts/icons/rotate_planes_by_value.png`

**Inputs:**
- `planes` (Tree, Plane)
- `values` (Tree, double)
- `axis` (Item, int) — wire Value List: X=0, Y=1, Z=2
- `angle_min_deg` / `angle_max_deg` (Item, double)
- `auto_range` (Item, bool)
- `value_min` / `value_max` (Item, double)
- `grad_start` / `grad_mid` / `grad_end` (Item, double)

**Outputs:** `rotated_planes`, `angles_deg`, `info`

```csharp
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
    DataTree<Plane> planes,
    DataTree<double> values,
    int axis,
    double angle_min_deg,
    double angle_max_deg,
    bool auto_range,
    double value_min,
    double value_max,
    double grad_start,
    double grad_mid,
    double grad_end,
    ref object rotated_planes,
    ref object angles_deg,
    ref object info)
  {
    DataTree<Plane>  outPlanes = new DataTree<Plane>();
    DataTree<double> outAngles = new DataTree<double>();
    List<string>     infoList  = new List<string>();
    rotated_planes = outPlanes; angles_deg = outAngles; info = infoList;

    if (planes == null || planes.BranchCount == 0) { this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No planes"); return; }
    if (values == null || values.BranchCount == 0) { this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No values"); return; }
    if (axis < 0 || axis > 2) axis = 2;

    double vmin, vmax;
    if (auto_range)
    {
      vmin = double.MaxValue; vmax = double.MinValue;
      for (int b = 0; b < values.BranchCount; b++)
      {
        List<double> br = values.Branch(b);
        for (int i = 0; i < br.Count; i++)
        {
          if (br[i] < vmin) vmin = br[i];
          if (br[i] > vmax) vmax = br[i];
        }
      }
      if (vmin >= vmax) { vmin = 0; vmax = 1; }
    }
    else { vmin = value_min; vmax = value_max; }

    double range = vmax - vmin;
    if (Math.Abs(range) < 1e-10) range = 1.0;

    string axisName = (axis == 0) ? "X" : (axis == 1) ? "Y" : "Z";
    int totalBranches = planes.BranchCount;

    for (int b = 0; b < totalBranches; b++)
    {
      GH_Path path = planes.Path(b);
      List<Plane> brPlanes = planes.Branch(b);

      List<double> brValues;
      if (b < values.BranchCount) brValues = values.Branch(b);
      else if (values.BranchCount == 1) brValues = values.Branch(0);
      else continue;

      double branchT = (totalBranches > 1) ? (double)b / (totalBranches - 1) : 0.5;
      double gradFactor;
      if (branchT <= 0.5)
      {
        double localT = branchT * 2.0;
        gradFactor = grad_start + localT * (grad_mid - grad_start);
      }
      else
      {
        double localT = (branchT - 0.5) * 2.0;
        gradFactor = grad_mid + localT * (grad_end - grad_mid);
      }

      for (int i = 0; i < brPlanes.Count; i++)
      {
        Plane p = brPlanes[i];
        double v;
        if (i < brValues.Count) v = brValues[i];
        else if (brValues.Count > 0) v = brValues[brValues.Count - 1];
        else v = 0;

        double t = (v - vmin) / range;
        double angleDegRaw = angle_min_deg + t * (angle_max_deg - angle_min_deg);
        double angleDeg = angleDegRaw * gradFactor;
        double angleRad = angleDeg * Math.PI / 180.0;

        Vector3d rotAxis;
        if (axis == 0)      rotAxis = p.XAxis;
        else if (axis == 1) rotAxis = p.YAxis;
        else                rotAxis = p.ZAxis;

        Transform xform = Transform.Rotation(angleRad, rotAxis, p.Origin);
        Plane rotated = new Plane(p);
        rotated.Transform(xform);

        outPlanes.Add(rotated, path);
        outAngles.Add(angleDeg, path);
      }
    }

    infoList.Add("axis: " + axisName);
    infoList.Add("value range: " + vmin.ToString("F3") + " .. " + vmax.ToString("F3"));
    infoList.Add("angle range: " + angle_min_deg.ToString("F1") + " .. " + angle_max_deg.ToString("F1") + " deg");
    infoList.Add("gradient: " + grad_start.ToString("F2") + " .. " + grad_mid.ToString("F2") + " .. " + grad_end.ToString("F2"));

    rotated_planes = outPlanes; angles_deg = outAngles; info = infoList;
  }
}
```

---

## group_segments_by_direction

**User Object Properties:**
- **Name:** Group by Direction
- **Nickname:** GroupDir
- **Description:** Bins curve segments by chord direction angle. For robot toolpath grouping to minimize axis-6 rotation.
- **Sub-Category:** Util
- **Panel Section:** 1
- **Icon:** `scripts/icons/group_segments_by_direction.png`

**Inputs:**
- `segments` (List, Curve)
- `bin_count` (Item, int)
- `use_orientation` (Item, bool)
- `work_plane` (Item, Plane)

**Outputs:** `grouped`, `bin_centers_deg`, `seg_angles_deg`, `info`

```csharp
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
    List<Curve> segments,
    int bin_count,
    bool use_orientation,
    Plane work_plane,
    ref object grouped,
    ref object bin_centers_deg,
    ref object seg_angles_deg,
    ref object info)
  {
    DataTree<Curve> outTree = new DataTree<Curve>();
    List<double> binCenters = new List<double>();
    List<double> segAngles = new List<double>();
    List<string> infoList = new List<string>();
    grouped = outTree; bin_centers_deg = binCenters; seg_angles_deg = segAngles; info = infoList;

    if (segments == null || segments.Count == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No segments");
      return;
    }
    if (bin_count <= 0) bin_count = 8;
    if (!work_plane.IsValid) work_plane = Plane.WorldXY;

    double maxAngle = use_orientation ? 180.0 : 360.0;
    double binWidth = maxAngle / bin_count;

    int[] segBin = new int[segments.Count];
    double[] segAng = new double[segments.Count];

    for (int i = 0; i < segments.Count; i++)
    {
      Curve crv = segments[i];
      if (crv == null) { segBin[i] = -1; segAng[i] = double.NaN; continue; }

      Point3d p0 = crv.PointAtStart;
      Point3d p1 = crv.PointAtEnd;
      Vector3d dir = p1 - p0;
      if (dir.Length < 1e-10)
      {
        double mid = (crv.Domain.Min + crv.Domain.Max) * 0.5;
        dir = crv.TangentAt(mid);
        if (dir.Length < 1e-10) { segBin[i] = -1; segAng[i] = double.NaN; continue; }
      }

      double dx = Vector3d.Multiply(dir, work_plane.XAxis);
      double dy = Vector3d.Multiply(dir, work_plane.YAxis);
      double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
      if (angle < 0) angle += 360.0;
      if (use_orientation && angle >= 180.0) angle -= 180.0;

      int idx = (int)Math.Floor(angle / binWidth);
      if (idx >= bin_count) idx = bin_count - 1;
      if (idx < 0) idx = 0;
      segBin[i] = idx;
      segAng[i] = angle;
    }

    for (int b = 0; b < bin_count; b++)
    {
      GH_Path path = new GH_Path(b);
      int count = 0;
      for (int i = 0; i < segments.Count; i++)
      {
        if (segBin[i] == b) { outTree.Add(segments[i], path); count++; }
      }
      double center = b * binWidth + binWidth * 0.5;
      binCenters.Add(center);
      infoList.Add("bin " + b + " [" + (b * binWidth).ToString("F0") + "-" + ((b + 1) * binWidth).ToString("F0") + " deg]: " + count + " segs");
    }

    for (int i = 0; i < segAng.Length; i++) segAngles.Add(segAng[i]);
    grouped = outTree; bin_centers_deg = binCenters; seg_angles_deg = segAngles; info = infoList;
  }
}
```

---

## decay_envelope

**User Object Properties:**
- **Name:** Decay Envelope
- **Nickname:** Decay
- **Description:** Multiplies values by a decay envelope (decay_start to decay_end with power curve). For damped sine modulation.
- **Sub-Category:** Util
- **Panel Section:** 2
- **Icon:** `scripts/icons/decay_envelope.png`

**Inputs:**
- `values` (Tree, double)
- `decay_start` / `decay_end` / `curve_power` (Item, double)
- `reverse` (Item, bool)

**Outputs:** `out_values`, `out_factors`, `info`

```csharp
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
    out_values = outVals; out_factors = outFacs; info = infoList;

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

    out_values = outVals; out_factors = outFacs; info = infoList;
  }
}
```

---

# Quick-Workflow pro Komponente (~30 Sek)

1. C# Script Komponente auf Canvas droppen
2. Doppelklick → Code aus dem entsprechenden Block oben reinkopieren → OK
3. Pro Input rechtsklick → **Type Hint** und **Access** wie in Tabelle
4. Inputs umbenennen (Rechtsklick → Rename) wie in Tabelle
5. Outputs umbenennen wie in Tabelle
6. Komponente selektieren → **File → Create User Object...**
7. Aus dieser Doku: Name, Nickname, Description, Sub-Category, Panel Section einfügen
8. Icon aus `scripts/icons/<name>.png` reindroppen
9. OK → fertig, erscheint im Tab **Leo → <Sub-Category>**
