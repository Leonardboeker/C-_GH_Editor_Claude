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

// Helix-camera driver. Computes camera position on a helix path around
// cam_center and applies it to a named (or active) viewport.
//
// At t=0: camera at (radius_start * X, 0, height_start) -- angle = 0
// At t=1: camera at angle = rotation_deg, radius = radius_end, height = height_end
//
// Outputs cam_location, cam_target, and a preview polyline of the full
// helix path so you can see the camera trajectory before running Animate.
//
// Side effect: SetCameraLocation on the chosen viewport whenever the
// script solves. Drive t with a slider, run GH Animate -> each frame
// triggers a solve -> camera moves along the helix.

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

    // Current camera position from t (helix orbits cam_center)
    Point3d camLoc = HelixPoint(t, cam_center, radius_start, radius_end,
                                height_start, height_end, rotation_deg, startRad);
    // look_at is the FIXED target the camera always points at
    // (if not provided / default Origin, fall back to cam_center)
    Point3d target = look_at;
    if (look_at.X == 0 && look_at.Y == 0 && look_at.Z == 0) target = cam_center;
    cam_location = camLoc;
    cam_target   = target;

    // Preview polyline of the full helix
    Polyline poly = new Polyline();
    for (int i = 0; i <= preview_segments; i++)
    {
      double ti = (double)i / preview_segments;
      poly.Add(HelixPoint(ti, cam_center, radius_start, radius_end,
                          height_start, height_end, rotation_deg, startRad));
    }
    helix_preview = new PolylineCurve(poly);

    // Apply to viewport
    if (apply_camera)
    {
      RhinoDoc doc = RhinoDoc.ActiveDoc;
      if (doc != null)
      {
        RhinoView view = FindView(doc, view_name);
        if (view != null)
        {
          // Atomic camera set: both target and location at once
          view.ActiveViewport.SetCameraLocations(target, camLoc);
          // Lock up vector to world Z so view never tilts/rolls
          // (unless camera is exactly above/below target -> use Y as fallback)
          Vector3d up = Vector3d.ZAxis;
          Vector3d viewDir = target - camLoc;
          double dotZ = Math.Abs(viewDir.X * 0 + viewDir.Y * 0 + viewDir.Z * 1) / Math.Max(viewDir.Length, 1e-9);
          if (dotZ > 0.999) up = Vector3d.YAxis;
          view.ActiveViewport.CameraUp = up;
          view.Redraw();
          infoList.Add("camera applied to '" + view.ActiveViewport.Name + "'");
        }
        else
        {
          infoList.Add("WARNING: view not found");
        }
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
