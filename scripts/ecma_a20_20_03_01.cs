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

// ECMA A20.20.03.01 -- Reverse Tuck End parametric die generator.
// Port of the IronPython script for Grasshopper C#.
// Outputs cut and crease curves as separate lists so you can bake
// them onto different layers / colors.

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    double L,
    double P,
    double A,
    double S,
    double inc,
    double ganc,
    double chamf,
    double taper,
    double fessura_l,
    double fessura_p,
    double nurbs_k,
    ref object cut,
    ref object crease,
    ref object outline_bbox,
    ref object info)
  {
    List<Curve> cutList = new List<Curve>();
    List<Curve> creaseList = new List<Curve>();
    List<string> infoList = new List<string>();
    cut = cutList;
    crease = creaseList;
    outline_bbox = null;
    info = infoList;

    // Defaults (match original Python script)
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

    // Validation
    if (P < inc + 3)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "P (" + P.ToString("F1") + ") < inc+3mm (" + (inc + 3).ToString("F1") + ") -- overlap on fold");
      return;
    }
    if (A <= 2 * S)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "A must be > 2*S (" + (2 * S).ToString("F1") + ")");
      return;
    }

    // ---- DERIVED COORDS ----
    double body_h  = A - 2 * S;
    double orecchio = 2 * S;

    // X panels
    double x0 = 0.0;
    double x1 = inc;                 // glue tab | P1
    double x2 = x1 + L - S;          // P1 | P2  (P1 = L-S)
    double x3 = x2 + P;              // P2 | P3  (P2 = P)
    double x4 = x3 + L;              // P3 | P4  (P3 = L)
    double x5 = x4 + P - S;          // right edge (P4 = P-S)

    // Y body
    double y_bot   = 0.0;
    double y_top   = body_h;
    double y_bot_t = y_bot - S;
    double y_top_t = y_top + S;

    // Dust flap depth
    double dust_d = Math.Min((P + ganc - S) / 2.0, L / 2.0);

    // Tuck bottom (from P3)
    double tuck_b_fold = y_bot_t - P + orecchio;
    double tuck_b_ear  = y_bot_t - P;
    double tuck_b_tip  = tuck_b_fold - ganc;

    // Tuck top (from P1)
    double tuck_t_fold = y_top_t + P - orecchio;
    double tuck_t_ear  = y_top_t + P;
    double tuck_t_tip  = tuck_t_fold + ganc;

    double cx = (x3 + x4) / 2.0;
    double y_inc_top = y_top - S;
    double y_inc_bot = y_bot;

    // =========================================================
    // 1. VERTICAL CREASES
    // =========================================================
    AddLine(creaseList, x1, y_bot, x1, y_inc_top);
    AddLine(creaseList, x2, y_bot, x2, y_top);
    AddLine(creaseList, x3, y_bot, x3, y_top);
    AddLine(creaseList, x4, y_bot, x4, y_top);

    // =========================================================
    // 2. BOTTOM HORIZONTAL CREASES
    // =========================================================
    AddLine(creaseList, x2 + S, y_bot,   x3, y_bot);
    AddLine(creaseList, x3,     y_bot_t, cx, y_bot_t);
    AddLine(creaseList, cx,     y_bot_t, x4, y_bot_t);
    AddLine(creaseList, x4,     y_bot,   x5, y_bot);

    // =========================================================
    // 3. TOP HORIZONTAL CREASES
    // =========================================================
    AddLine(creaseList, x1,     y_top_t, x2,     y_top_t);
    AddLine(creaseList, x2,     y_top,   x3 - S, y_top);
    AddLine(creaseList, x4 + S, y_top,   x5,     y_top);

    // =========================================================
    // 4. TUCK FOLD CREASES
    // =========================================================
    AddLine(creaseList, x3 + fessura_l, tuck_b_fold, cx,             tuck_b_fold);
    AddLine(creaseList, cx,             tuck_b_fold, x4 - fessura_l, tuck_b_fold);

    double tuck_t_slot_l = x1 + fessura_l;
    double tuck_t_slot_r = x2 - fessura_l;
    AddLine(creaseList, tuck_t_slot_l, tuck_t_fold, tuck_t_slot_r, tuck_t_fold);

    // =========================================================
    // 5. CUT -- BODY EDGES
    // =========================================================
    AddLine(cutList, x1, y_bot, x2, y_bot);
    AddLine(cutList, x3, y_top, x4, y_top);
    AddLine(cutList, x5, y_bot, x5, y_top);

    // =========================================================
    // 6. CUT -- DISCHARGES (S = thickness)
    // =========================================================
    AddLine(cutList, x2, y_bot,   x2 + S, y_bot);
    AddLine(cutList, x3, y_bot,   x3,     y_bot_t);
    AddLine(cutList, x4, y_bot_t, x4,     y_bot);
    AddLine(cutList, x2, y_top,   x2,     y_top_t);
    AddLine(cutList, x3 - S, y_top, x3,   y_top);
    AddLine(cutList, x4, y_top,   x4 + S, y_top);

    // =========================================================
    // 7. CUT -- GLUE TAB (incollatura)
    // =========================================================
    AddLine(cutList, x1, y_inc_bot,         x0, y_inc_bot + chamf);
    AddLine(cutList, x0, y_inc_bot + chamf, x0, y_inc_top - chamf);
    AddLine(cutList, x0, y_inc_top - chamf, x1, y_inc_top);

    // =========================================================
    // 8. CUT -- DUST FLAP BOTTOM P2
    // =========================================================
    double df_b2_ox = x2 + S;
    AddLine(cutList, df_b2_ox, y_bot,                    df_b2_ox, y_bot - chamf - taper);
    AddLine(cutList, df_b2_ox, y_bot - chamf - taper,    df_b2_ox + chamf, y_bot - 2 * chamf - taper);
    AddLine(cutList, df_b2_ox + chamf, y_bot - 2 * chamf - taper,
                     df_b2_ox + chamf + taper, y_bot - dust_d);

    AddLine(cutList, x3, y_bot,         x3 - chamf, y_bot - chamf);
    AddLine(cutList, x3 - chamf, y_bot - chamf,   x3 - chamf, y_bot - dust_d);

    AddLine(cutList, df_b2_ox + chamf + taper, y_bot - dust_d,
                     x3 - chamf, y_bot - dust_d);

    // =========================================================
    // 9. CUT -- DUST FLAP BOTTOM P4
    // =========================================================
    AddLine(cutList, x4, y_bot,           x4 + chamf, y_bot - chamf);
    AddLine(cutList, x4 + chamf, y_bot - chamf, x4 + chamf, y_bot - dust_d);

    AddLine(cutList, x5, y_bot,                       x5, y_bot - chamf - taper);
    AddLine(cutList, x5, y_bot - chamf - taper,       x5 - chamf, y_bot - 2 * chamf - taper);
    AddLine(cutList, x5 - chamf, y_bot - 2 * chamf - taper,
                     x5 - chamf - taper, y_bot - dust_d);

    AddLine(cutList, x4 + chamf, y_bot - dust_d,
                     x5 - chamf - taper, y_bot - dust_d);

    // =========================================================
    // 10. CUT -- DUST FLAP TOP P2
    // =========================================================
    AddLine(cutList, x2, y_top,             x2 + chamf, y_top + chamf);
    AddLine(cutList, x2 + chamf, y_top + chamf, x2 + chamf, y_top + dust_d);

    double df_t2_ix = x3 - S;
    AddLine(cutList, df_t2_ix, y_top,                       df_t2_ix, y_top + chamf + taper);
    AddLine(cutList, df_t2_ix, y_top + chamf + taper,       df_t2_ix - chamf, y_top + 2 * chamf + taper);
    AddLine(cutList, df_t2_ix - chamf, y_top + 2 * chamf + taper,
                     df_t2_ix - chamf - taper, y_top + dust_d);

    AddLine(cutList, x2 + chamf, y_top + dust_d,
                     df_t2_ix - chamf - taper, y_top + dust_d);

    // =========================================================
    // 11. CUT -- DUST FLAP TOP P4
    // =========================================================
    double df_t4_ix = x4 + S;
    AddLine(cutList, df_t4_ix, y_top,                       df_t4_ix, y_top + chamf + taper);
    AddLine(cutList, df_t4_ix, y_top + chamf + taper,       df_t4_ix + chamf, y_top + 2 * chamf + taper);
    AddLine(cutList, df_t4_ix + chamf, y_top + 2 * chamf + taper,
                     df_t4_ix + chamf + taper, y_top + dust_d);

    AddLine(cutList, x5, y_top,             x5 - chamf, y_top + chamf);
    AddLine(cutList, x5 - chamf, y_top + chamf, x5 - chamf, y_top + dust_d);

    AddLine(cutList, df_t4_ix + chamf + taper, y_top + dust_d,
                     x5 - chamf, y_top + dust_d);

    // =========================================================
    // 12. CUT -- TUCK BOTTOM (from P3)
    // =========================================================
    AddLine(cutList, x3, y_bot_t, x3, tuck_b_ear);
    AddLine(cutList, x4, y_bot_t, x4, tuck_b_ear);

    AddLine(cutList, x3, tuck_b_ear, x3 + fessura_l, tuck_b_ear);
    AddLine(cutList, x3 + fessura_l, tuck_b_ear,
                     x3 + fessura_l, tuck_b_ear + fessura_p);

    AddLine(cutList, x4, tuck_b_ear, x4 - fessura_l, tuck_b_ear);
    AddLine(cutList, x4 - fessura_l, tuck_b_ear,
                     x4 - fessura_l, tuck_b_ear + fessura_p);

    double tip_b_l = x3 + fessura_l + orecchio;
    double tip_b_r = x4 - fessura_l - orecchio;
    AddLine(cutList, tip_b_l, tuck_b_tip, tip_b_r, tuck_b_tip);

    double ctrl_b_y = tuck_b_ear + (tuck_b_tip - tuck_b_ear) * nurbs_k;
    AddNurbs2(cutList, x3, tuck_b_ear, x3, ctrl_b_y, tip_b_l, tuck_b_tip);
    AddNurbs2(cutList, x4, tuck_b_ear, x4, ctrl_b_y, tip_b_r, tuck_b_tip);

    // =========================================================
    // 13. CUT -- TUCK TOP (from P1)
    // =========================================================
    AddLine(cutList, x1, y_inc_top, x1, y_top_t);
    AddLine(cutList, x2, y_top_t,   x2, tuck_t_ear);
    AddLine(cutList, x1, y_top_t,   x1, tuck_t_ear);

    AddLine(cutList, x1, tuck_t_ear, x1 + fessura_l, tuck_t_ear);
    AddLine(cutList, x1 + fessura_l, tuck_t_ear,
                     x1 + fessura_l, tuck_t_ear - fessura_p);

    AddLine(cutList, x2, tuck_t_ear, x2 - fessura_l, tuck_t_ear);
    AddLine(cutList, x2 - fessura_l, tuck_t_ear,
                     x2 - fessura_l, tuck_t_ear - fessura_p);

    double tip_t_l = x1 + fessura_l + orecchio;
    double tip_t_r = x2 - fessura_l - orecchio;
    AddLine(cutList, tip_t_l, tuck_t_tip, tip_t_r, tuck_t_tip);

    double ctrl_t_y = tuck_t_ear + (tuck_t_tip - tuck_t_ear) * nurbs_k;
    AddNurbs2(cutList, x1, tuck_t_ear, x1, ctrl_t_y, tip_t_l, tuck_t_tip);
    AddNurbs2(cutList, x2, tuck_t_ear, x2, ctrl_t_y, tip_t_r, tuck_t_tip);

    // =========================================================
    // OUTLINE BBOX
    // =========================================================
    Rectangle3d bbox = new Rectangle3d(
      Plane.WorldXY,
      new Point3d(0, tuck_b_tip, 0),
      new Point3d(x5, tuck_t_tip, 0));
    outline_bbox = bbox.ToNurbsCurve();

    // =========================================================
    // INFO
    // =========================================================
    infoList.Add("ECMA A20.20.03.01 Reverse Tuck End");
    infoList.Add("External:  L=" + L.ToString("F2") + "  P=" + P.ToString("F2") + "  A=" + A.ToString("F2"));
    infoList.Add("Thickness: S=" + S.ToString("F2"));
    infoList.Add("Internal:  L=" + (L - 3 * S).ToString("F2") + "  P=" + (P - 2 * S).ToString("F2") + "  A=" + (A - 4 * S).ToString("F2"));
    infoList.Add("Body H (between P2/P4 creases): " + body_h.ToString("F2") + " mm");
    infoList.Add("Dust flap depth: " + dust_d.ToString("F2") + " mm  (max " + (L / 2.0).ToString("F1") + ")");
    infoList.Add("Tuck height: P+ganc-S = " + (P + ganc - S).ToString("F1") + " mm");
    infoList.Add("Panels: inc=" + inc.ToString("F1") +
                 "  P1=" + (L - S).ToString("F1") +
                 "  P2=" + (P - 2 * S).ToString("F1") +
                 "  P3=" + L.ToString("F1") +
                 "  P4=" + (P - S).ToString("F1"));
    infoList.Add("Total bbox: " + x5.ToString("F1") + " x " + (tuck_t_tip - tuck_b_tip).ToString("F1") + " mm");
    infoList.Add("Cut curves: " + cutList.Count.ToString() + " | Crease curves: " + creaseList.Count.ToString());

    cut = cutList;
    crease = creaseList;
    info = infoList;
  }

  private void AddLine(List<Curve> list, double ax, double ay, double bx, double by)
  {
    Point3d a = new Point3d(ax, ay, 0);
    Point3d b = new Point3d(bx, by, 0);
    if (a.DistanceTo(b) < 1e-9) return;
    list.Add(new LineCurve(a, b));
  }

  private void AddNurbs2(
    List<Curve> list,
    double p0x, double p0y,
    double cx,  double cy,
    double p2x, double p2y)
  {
    List<Point3d> pts = new List<Point3d>();
    pts.Add(new Point3d(p0x, p0y, 0));
    pts.Add(new Point3d(cx,  cy,  0));
    pts.Add(new Point3d(p2x, p2y, 0));
    NurbsCurve nc = NurbsCurve.Create(false, 2, pts);
    if (nc != null) list.Add(nc);
  }
}
