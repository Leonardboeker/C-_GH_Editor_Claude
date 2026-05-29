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

// Surface growth-pattern triangulation for Galapagos.
//
// 1. Poisson-disk sampling in UV space (organic, irregular distribution)
// 2. Optional Lloyd relaxation for smoother spacing
// 3. Maps to 3D via surface.PointAt(u, v)
// 4. Outputs points + UV pairs + a single fitness number for Galapagos
//
// For closed (tube) surfaces set wrap_u = true so points seamlessly wrap.
//
// Wiring with Galapagos:
//   Sliders -> target_count, min_dist, curvature_bias, relax_iter, random_seed
//   surface -> surface input
//   out_points_3d -> Delaunay Mesh (native GH) -> mesh
//   out_fitness -> Galapagos fitness (set to MINIMIZE)

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    Surface surface,
    int target_count,
    double min_dist,
    double curvature_bias,
    int relax_iter,
    int random_seed,
    bool wrap_u,
    ref object out_points_3d,
    ref object out_points_uv,
    ref object out_fitness,
    ref object out_edge_lengths,
    ref object info)
  {
    List<Point3d> pts3d = new List<Point3d>();
    List<Point3d> ptsUv = new List<Point3d>();
    List<double>  edges = new List<double>();
    List<string>  infoList = new List<string>();
    double fitness = 999999.0;

    out_points_3d = pts3d;
    out_points_uv = ptsUv;
    out_edge_lengths = edges;
    out_fitness = fitness;
    info = infoList;

    if (surface == null)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No surface");
      return;
    }
    if (target_count < 4) target_count = 4;
    if (min_dist <= 0) min_dist = 0.05;
    if (min_dist > 0.5) min_dist = 0.5;
    if (curvature_bias < 0) curvature_bias = 0;
    if (curvature_bias > 1) curvature_bias = 1;
    if (relax_iter < 0) relax_iter = 0;

    // Reparam surface to 0..1 in both directions
    Surface srf = surface.Duplicate() as Surface;
    srf.SetDomain(0, new Interval(0, 1));
    srf.SetDomain(1, new Interval(0, 1));

    Random rng = new Random(random_seed);

    // --- Step 1: Poisson disk sampling in UV (with wrap support) ---
    List<Point2d> uvPts = PoissonDisk(rng, target_count, min_dist, wrap_u, srf, curvature_bias);

    // --- Step 2: Lloyd relaxation (optional) ---
    for (int it = 0; it < relax_iter; it++)
    {
      uvPts = LloydStep(uvPts, min_dist, wrap_u);
    }

    // --- Step 3: Map UV -> 3D ---
    for (int i = 0; i < uvPts.Count; i++)
    {
      double u = ClampWrap(uvPts[i].X, wrap_u);
      double v = Math.Max(0, Math.Min(1, uvPts[i].Y));
      Point3d p3 = srf.PointAt(u, v);
      pts3d.Add(p3);
      ptsUv.Add(new Point3d(u, v, 0));
    }

    // --- Step 4: Compute nearest-neighbor distances for fitness ---
    // Lower stdev = more uniform spacing
    double[] nnDist = new double[uvPts.Count];
    double meanDist = 0;
    for (int i = 0; i < uvPts.Count; i++)
    {
      double minD = double.MaxValue;
      for (int j = 0; j < uvPts.Count; j++)
      {
        if (i == j) continue;
        double d = WrapDistUV(uvPts[i], uvPts[j], wrap_u);
        if (d < minD) minD = d;
      }
      nnDist[i] = minD;
      meanDist += minD;
      edges.Add(minD);
    }
    meanDist /= uvPts.Count;

    double variance = 0;
    for (int i = 0; i < nnDist.Length; i++)
    {
      double d = nnDist[i] - meanDist;
      variance += d * d;
    }
    variance /= uvPts.Count;
    double stdev = Math.Sqrt(variance);

    // Coefficient of variation: stdev / mean (scale-invariant)
    double cv = (meanDist > 1e-10) ? stdev / meanDist : 999999.0;

    // Penalty if too few points placed (Poisson failed to reach target)
    double countPenalty = 0;
    int countDelta = target_count - uvPts.Count;
    if (countDelta > 0) countPenalty = countDelta * 0.5;

    fitness = cv + countPenalty;

    // NaN guard for Galapagos
    if (double.IsNaN(fitness) || double.IsInfinity(fitness)) fitness = 999999.0;

    infoList.Add("requested: " + target_count + " | placed: " + uvPts.Count);
    infoList.Add("min_dist: " + min_dist.ToString("F3") + " | relax_iter: " + relax_iter);
    infoList.Add("mean nn-dist: " + meanDist.ToString("F4"));
    infoList.Add("stdev nn-dist: " + stdev.ToString("F4"));
    infoList.Add("coefficient of variation: " + cv.ToString("F4"));
    infoList.Add("FITNESS (minimize): " + fitness.ToString("F4"));

    out_points_3d = pts3d;
    out_points_uv = ptsUv;
    out_edge_lengths = edges;
    out_fitness = fitness;
    info = infoList;
  }

  // --- Poisson disk sampling in UV ---
  private List<Point2d> PoissonDisk(Random rng, int target, double minDist, bool wrap, Surface srf, double curvBias)
  {
    List<Point2d> result = new List<Point2d>();
    int maxAttempts = target * 30;
    int attempts = 0;

    // Seed first point
    result.Add(new Point2d(rng.NextDouble(), rng.NextDouble()));

    for (int iter = 0; iter < maxAttempts; iter++)
    {
      if (result.Count >= target) break;
      attempts++;

      // Sample candidate
      double u = rng.NextDouble();
      double v = rng.NextDouble();

      // Curvature bias: bias toward areas of high curvature
      if (curvBias > 0)
      {
        double k = SurfaceCurvature(srf, u, v);
        // Reject low-curvature samples with probability proportional to bias
        double acceptThresh = (1.0 - curvBias) + curvBias * Math.Min(1.0, k * 0.5);
        if (rng.NextDouble() > acceptThresh) continue;
      }

      Point2d cand = new Point2d(u, v);
      bool ok = true;
      for (int j = 0; j < result.Count; j++)
      {
        if (WrapDistUV(cand, result[j], wrap) < minDist)
        {
          ok = false;
          break;
        }
      }
      if (ok) result.Add(cand);
    }
    return result;
  }

  // Approximate surface curvature at (u,v): magnitude of mean curvature
  private double SurfaceCurvature(Surface srf, double u, double v)
  {
    SurfaceCurvature sc = srf.CurvatureAt(u, v);
    if (sc == null) return 0;
    return Math.Abs(sc.Mean);
  }

  // --- Lloyd relaxation step: move each point toward centroid of its
  // ad-hoc "Voronoi cell" approximated via k-nearest neighbors ---
  private List<Point2d> LloydStep(List<Point2d> pts, double minDist, bool wrap)
  {
    List<Point2d> next = new List<Point2d>();
    int k = 6; // approximate neighborhood size

    for (int i = 0; i < pts.Count; i++)
    {
      // Find k nearest
      List<KeyValuePair<int, double>> dists = new List<KeyValuePair<int, double>>();
      for (int j = 0; j < pts.Count; j++)
      {
        if (i == j) continue;
        dists.Add(new KeyValuePair<int, double>(j, WrapDistUV(pts[i], pts[j], wrap)));
      }
      dists.Sort((a, b) => a.Value.CompareTo(b.Value));

      // Centroid of k nearest
      double cx = pts[i].X, cy = pts[i].Y;
      int n = Math.Min(k, dists.Count);
      for (int m = 0; m < n; m++)
      {
        Point2d nb = pts[dists[m].Key];
        // Handle wrap by choosing nearest copy
        double dx = nb.X - pts[i].X;
        if (wrap)
        {
          if (dx > 0.5) dx -= 1.0;
          else if (dx < -0.5) dx += 1.0;
        }
        cx += pts[i].X + dx;
        cy += nb.Y;
      }
      cx /= (n + 1);
      cy /= (n + 1);

      // Move slightly toward centroid (damped, factor 0.3)
      double newU = pts[i].X * 0.7 + cx * 0.3;
      double newV = pts[i].Y * 0.7 + cy * 0.3;

      newU = ClampWrap(newU, wrap);
      newV = Math.Max(0, Math.Min(1, newV));

      next.Add(new Point2d(newU, newV));
    }
    return next;
  }

  private double ClampWrap(double v, bool wrap)
  {
    if (wrap)
    {
      v = v % 1.0;
      if (v < 0) v += 1.0;
      return v;
    }
    return Math.Max(0, Math.Min(1, v));
  }

  private double WrapDistUV(Point2d a, Point2d b, bool wrapU)
  {
    double dx = a.X - b.X;
    if (wrapU)
    {
      if (dx > 0.5) dx -= 1.0;
      else if (dx < -0.5) dx += 1.0;
    }
    double dy = a.Y - b.Y;
    return Math.Sqrt(dx * dx + dy * dy);
  }
}
