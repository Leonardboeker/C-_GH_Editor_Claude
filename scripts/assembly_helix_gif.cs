using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Rhino;
using Rhino.DocObjects;
using Rhino.Display;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

// One-click assembly + helix-camera animation + GIF export.
//
// On rising edge of `run`:
//   For each frame f in 0..frame_count-1:
//     t_total = f / (frame_count - 1)
//     t_assembly = min(t_total / assembly_complete_at, 1)  // assembly done early
//     1. Interpolate parts at t_assembly
//     2. Bake parts as temporary objects in Rhino
//     3. Set camera on helix: angle, radius, height all interpolated
//     4. Capture viewport to PNG
//     5. Delete temp objects
//   Call ffmpeg to combine PNGs into GIF.
//
// Inputs: parts_start, parts_end must have matching topology (same Breps,
// different positions). Use flatten_plates_for_nesting outputs:
//   parts_start = flat_parts, parts_end = original 3D parts.
//
// ffmpeg must be installed and on PATH, or specify ffmpeg_path explicitly.

public class Script_Instance : GH_ScriptInstance
{
  private bool _lastRun = false;

  private void RunScript(
    List<Brep> parts_start,
    List<Brep> parts_end,
    bool run,
    int frame_count,
    int fps,
    int width,
    int height,
    Point3d cam_center,
    double radius_start,
    double radius_end,
    double height_start,
    double height_end,
    double rotation_deg,
    double assembly_complete_at,
    double easing,
    string output_folder,
    string gif_name,
    string ffmpeg_path,
    string view_name,
    ref object out_status,
    ref object out_frames,
    ref object out_gif_path)
  {
    List<string> status = new List<string>();
    List<string> frames = new List<string>();
    string gifPath = "";
    out_status = status;
    out_frames = frames;
    out_gif_path = gifPath;

    if (parts_start == null || parts_end == null)
    {
      status.Add("ERROR: no parts");
      out_status = status; return;
    }
    int n = Math.Min(parts_start.Count, parts_end.Count);
    if (n == 0)
    {
      status.Add("ERROR: empty part lists");
      out_status = status; return;
    }

    // Defaults
    if (frame_count <= 0) frame_count = 120;
    if (fps <= 0) fps = 30;
    if (width <= 0) width = 1280;
    if (height <= 0) height = 720;
    if (radius_end <= 0) radius_end = radius_start;
    if (rotation_deg == 0) rotation_deg = 360;
    if (assembly_complete_at <= 0) assembly_complete_at = 0.7;
    if (assembly_complete_at > 1) assembly_complete_at = 1.0;
    if (easing <= 0) easing = 1.0;
    if (string.IsNullOrEmpty(output_folder)) output_folder = Path.Combine(Path.GetTempPath(), "gh_animation");
    if (string.IsNullOrEmpty(gif_name)) gif_name = "assembly.gif";
    if (string.IsNullOrEmpty(ffmpeg_path)) ffmpeg_path = "ffmpeg";
    if (!gif_name.EndsWith(".gif")) gif_name += ".gif";

    // Live status
    status.Add("parts=" + n.ToString());
    status.Add("frame_count=" + frame_count.ToString() + " fps=" + fps.ToString());
    status.Add("camera: radius " + radius_start.ToString("F1") + "->" + radius_end.ToString("F1") +
               " height " + height_start.ToString("F1") + "->" + height_end.ToString("F1") +
               " rotation " + rotation_deg.ToString("F1") + " deg");
    status.Add("assembly complete at t=" + assembly_complete_at.ToString("F2"));
    status.Add("output: " + output_folder);

    // Only run on rising edge
    if (!run || _lastRun) { _lastRun = run; out_status = status; return; }
    _lastRun = run;

    RhinoDoc doc = RhinoDoc.ActiveDoc;
    if (doc == null) { status.Add("ERROR: no active RhinoDoc"); out_status = status; return; }

    // Get target view
    RhinoView view = null;
    if (!string.IsNullOrEmpty(view_name))
    {
      RhinoView[] views = doc.Views.GetViewList(true, false);
      for (int i = 0; i < views.Length; i++)
      {
        if (views[i].ActiveViewport.Name == view_name) { view = views[i]; break; }
      }
    }
    if (view == null) view = doc.Views.ActiveView;
    if (view == null) { status.Add("ERROR: no view"); out_status = status; return; }

    // Make sure output folder exists
    if (!Directory.Exists(output_folder)) Directory.CreateDirectory(output_folder);

    // Build reference planes once (start state only)
    Plane[] planesStart = new Plane[n];
    Plane[] planesEnd   = new Plane[n];
    bool[]  okPlanes    = new bool[n];
    for (int i = 0; i < n; i++)
    {
      bool oS, oE;
      planesStart[i] = BuildRefPlane(parts_start[i], out oS);
      planesEnd[i]   = BuildRefPlane(parts_end[i],   out oE);
      okPlanes[i]    = oS && oE;
    }

    // Capture initial camera so we can restore after
    Point3d savedCamLoc = view.ActiveViewport.CameraLocation;
    Point3d savedCamTgt = view.ActiveViewport.CameraTarget;
    Vector3d savedCamUp = view.ActiveViewport.CameraUp;

    Stopwatch sw = Stopwatch.StartNew();
    List<Guid> tempIds = new List<Guid>();

    try
    {
      for (int f = 0; f < frame_count; f++)
      {
        double tTotal = (frame_count > 1) ? (double)f / (frame_count - 1) : 0;

        // Assembly time: completes early, then holds at 1
        double tAssembly = tTotal / assembly_complete_at;
        if (tAssembly > 1.0) tAssembly = 1.0;
        double tEased = (Math.Abs(easing - 1.0) > 1e-6) ? Math.Pow(tAssembly, easing) : tAssembly;

        // Interpolate parts and bake temp
        tempIds.Clear();
        for (int i = 0; i < n; i++)
        {
          if (parts_start[i] == null) continue;
          Brep dup;
          if (!okPlanes[i])
          {
            dup = parts_start[i].DuplicateBrep();
          }
          else
          {
            Plane pNow = LerpPlane(planesStart[i], planesEnd[i], tEased);
            Transform xf = Transform.PlaneToPlane(planesStart[i], pNow);
            dup = parts_start[i].DuplicateBrep();
            dup.Transform(xf);
          }
          Guid id = doc.Objects.AddBrep(dup);
          if (id != Guid.Empty) tempIds.Add(id);
        }

        // Helix camera
        double angleRad = tTotal * rotation_deg * Math.PI / 180.0;
        double radius   = radius_start + (radius_end - radius_start) * tTotal;
        double zHeight  = height_start + (height_end - height_start) * tTotal;
        Point3d camLoc = new Point3d(
          cam_center.X + Math.Cos(angleRad) * radius,
          cam_center.Y + Math.Sin(angleRad) * radius,
          cam_center.Z + zHeight);
        view.ActiveViewport.SetCameraLocation(camLoc, false);
        view.ActiveViewport.SetCameraTarget(cam_center, false);

        view.Redraw();
        RhinoApp.Wait();

        // Capture
        string framePath = Path.Combine(output_folder, string.Format("frame_{0:D4}.png", f));
        Bitmap bmp = view.CaptureToBitmap(new System.Drawing.Size(width, height));
        if (bmp != null)
        {
          bmp.Save(framePath, System.Drawing.Imaging.ImageFormat.Png);
          bmp.Dispose();
          frames.Add(framePath);
        }

        // Cleanup temp objects
        for (int k = 0; k < tempIds.Count; k++)
        {
          doc.Objects.Delete(tempIds[k], true);
        }
      }
    }
    catch (Exception ex)
    {
      status.Add("ERROR during capture: " + ex.Message);
    }
    finally
    {
      // Restore camera
      view.ActiveViewport.SetCameraLocations(savedCamTgt, savedCamLoc);
      view.ActiveViewport.CameraUp = savedCamUp;
      view.Redraw();
    }

    sw.Stop();
    status.Add("captured " + frames.Count.ToString() + " frames in " + (sw.ElapsedMilliseconds / 1000.0).ToString("F1") + " s");

    // ffmpeg: png sequence -> palette -> gif
    try
    {
      gifPath = Path.Combine(output_folder, gif_name);
      string palettePath = Path.Combine(output_folder, "palette.png");

      string framePattern = Path.Combine(output_folder, "frame_%04d.png");

      // Pass 1: palette
      RunFfmpeg(ffmpeg_path,
        "-y -framerate " + fps + " -i \"" + framePattern + "\" -vf \"fps=" + fps + ",palettegen\" \"" + palettePath + "\"",
        status);

      // Pass 2: gif using palette
      RunFfmpeg(ffmpeg_path,
        "-y -framerate " + fps + " -i \"" + framePattern + "\" -i \"" + palettePath + "\" -filter_complex \"fps=" + fps + " [x]; [x][1:v] paletteuse\" \"" + gifPath + "\"",
        status);

      if (File.Exists(gifPath))
      {
        status.Add("GIF created: " + gifPath);
      }
      else
      {
        status.Add("ffmpeg ran but GIF not found");
      }
    }
    catch (Exception ex)
    {
      status.Add("ffmpeg error: " + ex.Message);
    }

    out_status   = status;
    out_frames   = frames;
    out_gif_path = gifPath;
  }

  private void RunFfmpeg(string exe, string args, List<string> status)
  {
    ProcessStartInfo psi = new ProcessStartInfo();
    psi.FileName = exe;
    psi.Arguments = args;
    psi.UseShellExecute = false;
    psi.CreateNoWindow = true;
    psi.RedirectStandardError = true;
    psi.RedirectStandardOutput = true;
    Process p = Process.Start(psi);
    p.WaitForExit();
    if (p.ExitCode != 0)
    {
      string err = p.StandardError.ReadToEnd();
      status.Add("ffmpeg exit " + p.ExitCode.ToString() + ": " + (err.Length > 200 ? err.Substring(0, 200) : err));
    }
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
