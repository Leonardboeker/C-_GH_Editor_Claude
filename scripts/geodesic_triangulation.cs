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

// Geodesic-style triangulation on a tube/surface with variable triangle sizes.
// All nodes connected via shared edges. Closed seam when wrap_u = true.
// Clean boundaries when boundary_count > 0.
//
// Algorithm:
//   1. Optional: seed boundary points evenly along V=0 and V=1 (top & bottom)
//   2. Variable-radius Poisson disk in interior (3D distance for rejection)
//   3. If wrap_u: duplicate all points at U+1 (creates 2-tile strip)
//   4. Bowyer-Watson Delaunay on all points
//   5. Keep triangles anchored in the central strip; map duplicates back;
//      deduplicate by sorted vertex triple
//   6. Build mesh + extract unique edges
//
// Outputs:
//   mesh, tri_curves, unique_edges, nodes, info

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    Surface surface,
    int target_count,
    double size_min,
    double size_max,
    bool wrap_u,
    int boundary_count,
    int random_seed,
    int max_attempts,
    ref object mesh,
    ref object tri_curves,
    ref object unique_edges,
    ref object nodes,
    ref object info)
  {
    List<Curve>   triCurves = new List<Curve>();
    List<Line>    edgeList = new List<Line>();
    List<Point3d> nodePts = new List<Point3d>();
    List<string>  infoList = new List<string>();
    Mesh outMesh = new Mesh();
    mesh = outMesh; tri_curves = triCurves; unique_edges = edgeList;
    nodes = nodePts; info = infoList;

    if (surface == null)
    { this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No surface"); return; }
    if (target_count < 4) target_count = 50;
    if (size_min <= 0) size_min = 30.0;
    if (size_max <= size_min) size_max = size_min * 2.0;
    if (max_attempts <= 0) max_attempts = target_count * 30;
    if (boundary_count < 0) boundary_count = 0;

    Surface srf = surface.Duplicate() as Surface;
    srf.SetDomain(0, new Interval(0, 1));
    srf.SetDomain(1, new Interval(0, 1));

    Random rng = new Random(random_seed);

    List<Point2d> uvPts = new List<Point2d>();
    List<Point3d> pts3d = new List<Point3d>();
    List<double>  sizes = new List<double>();

    // --- 1. Boundary seeding along V=0 and V=1 ---
    double seedSize = (size_min + size_max) * 0.5;
    if (boundary_count > 0)
    {
      for (int i = 0; i < boundary_count; i++)
      {
        double u = (double)i / boundary_count;     // 0..1 excluding 1 (so wrap is clean)
        // bottom V=0
        uvPts.Add(new Point2d(u, 0.0));
        pts3d.Add(srf.PointAt(u, 0.0));
        sizes.Add(seedSize);
        // top V=1
        uvPts.Add(new Point2d(u, 1.0));
        pts3d.Add(srf.PointAt(u, 1.0));
        sizes.Add(seedSize);
      }
    }

    // --- 2. Poisson disk for interior ---
    int attempts = 0;
    for (int iter = 0; iter < max_attempts; iter++)
    {
      if (uvPts.Count >= target_count) break;
      attempts++;

      double u = rng.NextDouble();
      // bias interior away from exact boundary (since boundary may be seeded)
      double v = 0.02 + rng.NextDouble() * 0.96;
      Point3d p = srf.PointAt(u, v);
      double ownSize = size_min + rng.NextDouble() * (size_max - size_min);

      bool ok = true;
      for (int j = 0; j < pts3d.Count; j++)
      {
        double need = (ownSize + sizes[j]) * 0.5;
        if (p.DistanceTo(pts3d[j]) < need) { ok = false; break; }
      }
      if (ok)
      {
        uvPts.Add(new Point2d(u, v));
        pts3d.Add(p);
        sizes.Add(ownSize);
      }
    }

    if (uvPts.Count < 3)
    {
      infoList.Add("ERROR: only " + uvPts.Count + " points placed");
      info = infoList; return;
    }

    // --- 3. Triangulate (with U-wrap if needed) ---
    List<int[]> triangles = wrap_u
      ? DelaunayUWrap(uvPts)
      : BowyerWatson(uvPts);

    // --- 4. Build mesh & polylines ---
    for (int i = 0; i < pts3d.Count; i++) outMesh.Vertices.Add(pts3d[i]);
    for (int t = 0; t < triangles.Count; t++)
    {
      int a = triangles[t][0];
      int b = triangles[t][1];
      int c = triangles[t][2];
      outMesh.Faces.AddFace(a, b, c);

      Polyline poly = new Polyline();
      poly.Add(pts3d[a]);
      poly.Add(pts3d[b]);
      poly.Add(pts3d[c]);
      poly.Add(pts3d[a]);
      triCurves.Add(new PolylineCurve(poly));
    }
    outMesh.Normals.ComputeNormals();
    outMesh.Compact();

    // --- 5. Unique edges ---
    HashSet<long> seen = new HashSet<long>();
    for (int t = 0; t < triangles.Count; t++)
    {
      AddEdge(triangles[t][0], triangles[t][1], pts3d, edgeList, seen);
      AddEdge(triangles[t][1], triangles[t][2], pts3d, edgeList, seen);
      AddEdge(triangles[t][2], triangles[t][0], pts3d, edgeList, seen);
    }

    for (int i = 0; i < pts3d.Count; i++) nodePts.Add(pts3d[i]);

    infoList.Add("nodes: " + uvPts.Count + " (target " + target_count + ", boundary " + (boundary_count * 2) + ")");
    infoList.Add("triangles: " + triangles.Count);
    infoList.Add("unique edges: " + edgeList.Count);
    infoList.Add("size range: " + size_min.ToString("F1") + " .. " + size_max.ToString("F1") + " mm");
    infoList.Add("wrap_u: " + wrap_u + " | seed: " + random_seed);

    mesh = outMesh; tri_curves = triCurves; unique_edges = edgeList;
    nodes = nodePts; info = infoList;
  }

  private void AddEdge(int a, int b, List<Point3d> pts, List<Line> edges, HashSet<long> seen)
  {
    int lo = Math.Min(a, b);
    int hi = Math.Max(a, b);
    long key = ((long)lo << 32) | (uint)hi;
    if (seen.Contains(key)) return;
    seen.Add(key);
    edges.Add(new Line(pts[lo], pts[hi]));
  }

  // Delaunay with U-wrap: duplicate strip at U+1, triangulate doubled set,
  // keep only triangles anchored in central strip, dedupe by sorted vertex triple.
  private List<int[]> DelaunayUWrap(List<Point2d> origPts)
  {
    int N = origPts.Count;
    List<Point2d> doubled = new List<Point2d>(origPts);
    for (int i = 0; i < N; i++)
      doubled.Add(new Point2d(origPts[i].X + 1.0, origPts[i].Y));

    List<int[]> allTris = BowyerWatson(doubled);
    List<int[]> result = new List<int[]>();
    HashSet<long> seen = new HashSet<long>();

    for (int t = 0; t < allTris.Count; t++)
    {
      int[] tri = allTris[t];
      // Skip if all three vertices are in the right strip (exact duplicate of a left-strip tri)
      if (tri[0] >= N && tri[1] >= N && tri[2] >= N) continue;

      // Map duplicates back to originals
      int a = tri[0] >= N ? tri[0] - N : tri[0];
      int b = tri[1] >= N ? tri[1] - N : tri[1];
      int c = tri[2] >= N ? tri[2] - N : tri[2];

      // Skip degenerate (collapse after mapping)
      if (a == b || b == c || a == c) continue;

      // Dedupe by sorted triple
      int s1 = a, s2 = b, s3 = c;
      if (s1 > s2) { int tmp = s1; s1 = s2; s2 = tmp; }
      if (s2 > s3) { int tmp = s2; s2 = s3; s3 = tmp; }
      if (s1 > s2) { int tmp = s1; s1 = s2; s2 = tmp; }
      long key = ((long)s1 * 1000000L + s2) * 1000000L + s3;
      if (seen.Contains(key)) continue;
      seen.Add(key);

      result.Add(new int[] { a, b, c });
    }
    return result;
  }

  // --- Bowyer-Watson Delaunay in 2D ---
  private List<int[]> BowyerWatson(List<Point2d> pts)
  {
    int n = pts.Count;
    double minX = double.MaxValue, minY = double.MaxValue;
    double maxX = double.MinValue, maxY = double.MinValue;
    for (int i = 0; i < n; i++)
    {
      if (pts[i].X < minX) minX = pts[i].X;
      if (pts[i].Y < minY) minY = pts[i].Y;
      if (pts[i].X > maxX) maxX = pts[i].X;
      if (pts[i].Y > maxY) maxY = pts[i].Y;
    }
    double dmax = Math.Max(maxX - minX, maxY - minY) * 10.0;
    double midX = (minX + maxX) * 0.5;
    double midY = (minY + maxY) * 0.5;

    List<Point2d> work = new List<Point2d>(pts);
    int sv0 = work.Count; work.Add(new Point2d(midX - 20 * dmax, midY - dmax));
    int sv1 = work.Count; work.Add(new Point2d(midX, midY + 20 * dmax));
    int sv2 = work.Count; work.Add(new Point2d(midX + 20 * dmax, midY - dmax));

    List<int[]> tris = new List<int[]>();
    tris.Add(new int[] { sv0, sv1, sv2 });

    for (int p = 0; p < n; p++)
    {
      Point2d pt = work[p];
      List<int> bad = new List<int>();
      for (int t = 0; t < tris.Count; t++)
      {
        if (InCircumcircle(pt, work[tris[t][0]], work[tris[t][1]], work[tris[t][2]]))
          bad.Add(t);
      }

      List<int[]> polygon = new List<int[]>();
      for (int bi = 0; bi < bad.Count; bi++)
      {
        int[] tri = tris[bad[bi]];
        int[][] edges = new int[][]
        {
          new int[] { tri[0], tri[1] },
          new int[] { tri[1], tri[2] },
          new int[] { tri[2], tri[0] }
        };
        for (int e = 0; e < 3; e++)
        {
          int a = edges[e][0], b = edges[e][1];
          bool shared = false;
          for (int bj = 0; bj < bad.Count; bj++)
          {
            if (bi == bj) continue;
            if (TriangleHasEdge(tris[bad[bj]], a, b)) { shared = true; break; }
          }
          if (!shared) polygon.Add(new int[] { a, b });
        }
      }

      bad.Sort();
      for (int bi = bad.Count - 1; bi >= 0; bi--) tris.RemoveAt(bad[bi]);

      for (int e = 0; e < polygon.Count; e++)
        tris.Add(new int[] { polygon[e][0], polygon[e][1], p });
    }

    List<int[]> result = new List<int[]>();
    for (int t = 0; t < tris.Count; t++)
    {
      int[] tr = tris[t];
      if (tr[0] >= n || tr[1] >= n || tr[2] >= n) continue;
      result.Add(tr);
    }
    return result;
  }

  private bool TriangleHasEdge(int[] tri, int a, int b)
  {
    int matches = 0;
    for (int k = 0; k < 3; k++) if (tri[k] == a || tri[k] == b) matches++;
    return matches >= 2;
  }

  private bool InCircumcircle(Point2d p, Point2d a, Point2d b, Point2d c)
  {
    double ax = a.X - p.X, ay = a.Y - p.Y;
    double bx = b.X - p.X, by = b.Y - p.Y;
    double cx = c.X - p.X, cy = c.Y - p.Y;
    double d = (ax * ax + ay * ay) * (bx * cy - cx * by)
             - (bx * bx + by * by) * (ax * cy - cx * ay)
             + (cx * cx + cy * cy) * (ax * by - bx * ay);
    double orient = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    return (orient > 0) ? d > 0 : d < 0;
  }
}
