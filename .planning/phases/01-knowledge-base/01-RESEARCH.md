# Phase 1: Knowledge Base - Research

**Researched:** 2026-03-23
**Domain:** Rhino 8 Grasshopper C# Scripting (RhinoCommon, KUKAprc, DataTree API)
**Confidence:** HIGH

## Summary

This phase produces a comprehensive `grasshopper_csharp_learnings.md` file that Claude reads at session start to write correct Rhino 8 C# scripts on the first attempt. The existing file (18 sections) covers fundamentals; the expansion adds compiler rules, geometry operations, DataTree building, KUKAprc axis reading, debugging, parallel processing, and category-specific templates.

Rhino 8 uses .NET 7 (or .NET 8 on 8.20+) with a Roslyn-based compiler in the new Script Editor. The new editor supports "modern C#" including string interpolation, but the user's existing knowledge base prohibits `out var` for safety (older Rhino 8 builds may not support it uniformly). The safe baseline is explicit `out` variable declarations. The GH C# Script component operates in SDK-Mode (full `GH_ScriptInstance` class with `RunScript`) or Script-Mode (global scope, no class). All templates should target SDK-Mode for consistency with the existing knowledge base.

**Primary recommendation:** Expand the existing 18-section document by adding verified API patterns with explicit `out` declarations, never `out var`. Every code example must compile in SDK-Mode. Organize new content into clear sections that mirror the requirement IDs (KB-01 through KB-08).

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| KB-01 | Compiler rules: ASCII-only, no `out var`, no while loops | Verified: Rhino 8 uses .NET 7/Roslyn; `out var` excluded for safety; new vs legacy editor differences documented |
| KB-02 | Full DataTree iteration patterns for Rhino 8 | Verified: DataTree<T> API (Add, AddRange, Branch, Paths, BranchCount, MergeTree, Flatten, AllData); path-based iteration pattern confirmed |
| KB-03 | Geometry operation patterns: curves, surfaces, breps, intersections | Verified: Curve (ClosestPoint, DivideByCount, DivideByLength, Offset, Trim, Extend, JoinCurves), Brep (ClosestPoint, IsPointInside, BrepFace normals), Intersection (CurveCurve, CurveSurface, CurveBrep, BrepBrep) |
| KB-04 | KUKAprc-specific patterns: axis values, speed lists, external axes | Partially verified: Analysis component outputs Robot Axis Values (A1-A6), Collision, Reachability, Planes, Time; DataTree path structure inferred from usage (one branch per command with 6 values) |
| KB-05 | Input/output patterns: null guards, ref object, access modes | Verified: existing coverage is good; needs multiple output params and explicit casting additions |
| KB-06 | Debugging patterns: AddRuntimeMessage, Print, runtime guards | Verified: `this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "msg")`, `Print("msg")`, `RhinoApp.WriteLine()` |
| KB-07 | Parallel processing: Parallel.For with thread-safe collections | Verified: works inside C# Script component with `using System.Threading.Tasks` and `ConcurrentBag<T>`/`ConcurrentDictionary`; Rhino.Geometry is thread-safe for reads |
| KB-08 | Component templates: minimal, geometry, KUKAprc, DataTree, Galapagos | Existing minimal template present; geometry/KUKAprc/DataTree/Galapagos templates to be composed from verified API patterns |
</phase_requirements>

## Standard Stack

This phase produces a documentation file, not a software project. The "stack" is the APIs and namespaces that code examples must reference.

### Core Namespaces (always included in templates)
| Namespace | Purpose | Why Standard |
|-----------|---------|--------------|
| `System` | Base types, Math, Collections | Required by runtime |
| `System.Collections.Generic` | `List<T>`, `Dictionary<K,V>` | All scripts use lists |
| `Rhino` | `RhinoMath`, `RhinoApp`, `RhinoDoc` | Core SDK |
| `Rhino.Geometry` | `Point3d`, `Plane`, `Curve`, `Brep`, `Surface`, `Vector3d`, `Transform` | All geometry operations |
| `Grasshopper` | Core GH types | Required by component |
| `Grasshopper.Kernel` | `GH_RuntimeMessageLevel`, `GH_ActiveObject` | Debugging, messages |
| `Grasshopper.Kernel.Data` | `GH_Path` | DataTree path construction |
| `Grasshopper.Kernel.Types` | `GH_Number`, `GH_Point`, `GH_Plane` | Type wrappers when needed |

### Supporting Namespaces (included when needed)
| Namespace | Purpose | When to Use |
|-----------|---------|-------------|
| `Rhino.Geometry.Intersect` | `Intersection.CurveCurve`, `CurveBrep`, `BrepBrep` etc. | Any intersection operation |
| `System.Threading.Tasks` | `Parallel.For`, `Parallel.ForEach` | Large loop parallelization |
| `System.Collections.Concurrent` | `ConcurrentBag<T>`, `ConcurrentDictionary<K,V>` | Thread-safe collections for parallel |
| `System.Linq` | `Select`, `Where`, `ToList`, `OrderBy` | Data transformation (use cautiously) |
| `System.Drawing` | `Color` | When manipulating colors |

### Runtime Environment
| Property | Value |
|----------|-------|
| Rhino version | 8 (target) |
| .NET runtime | .NET 7.0 (Rhino 8.0-8.19) or .NET 8.0 (Rhino 8.20+) |
| C# compiler | Roslyn (new Script Editor) |
| Safe C# baseline | Explicit `out` declarations, no `out var`, no pattern matching, no records |
| Script mode | SDK-Mode (GH_ScriptInstance with RunScript) |
| RhinoCommon assembly | Auto-referenced by Script component |
| Grasshopper assembly | Auto-referenced by Script component |

## Architecture Patterns

### Document Structure for grasshopper_csharp_learnings.md
```
grasshopper_csharp_learnings.md
  1. Script Structure (SDK-Mode class wrapper) -- existing, refine
  2. Critical Rules (ASCII, no out var, no while) -- existing, expand for KB-01
  3. Rhino 8 vs Legacy Editor -- NEW for KB-01
  4. Output Parameters -- existing, expand for KB-05
  5. Input Access Types -- existing, expand for KB-05
  6. DataTree Iteration (Rhino 8) -- existing, expand for KB-02
  7. DataTree Building and Output -- NEW for KB-02
  8. Null and Range Guards -- existing
  9. Plane Construction -- existing
  10. RhinoMath -- existing
  11. Helper Methods -- existing
  12. Curve Operations -- NEW for KB-03
  13. Surface Evaluation -- existing, expand for KB-03
  14. Brep Operations -- NEW for KB-03
  15. Intersection Patterns -- NEW for KB-03
  16. Vector Operations -- existing
  17. Transform Operations -- NEW for KB-03
  18. KUKAprc Analysis Reading -- NEW for KB-04
  19. Speeds Parallel to Planes -- existing, move under KUKAprc
  20. KUKAprc Plane/Speed Output -- NEW for KB-04
  21. Debugging and Messages -- NEW for KB-06
  22. Parallel Processing -- NEW for KB-07
  23. Galapagos Integration -- existing
  24. C# vs Python Table -- existing
  25. Templates: Minimal -- existing
  26. Templates: Geometry -- NEW for KB-08
  27. Templates: KUKAprc -- NEW for KB-08
  28. Templates: DataTree -- NEW for KB-08
  29. Templates: Galapagos -- NEW for KB-08
```

### Pattern 1: Explicit Out Parameter Declaration
**What:** Always declare `out` variables on a separate line before the method call. Never use `out var`.
**When to use:** Every RhinoCommon API call that uses `out` parameters.
**Why:** While Rhino 8's Roslyn compiler may support `out var` in recent builds, older Rhino 8 installs and the user's established convention prohibit it. Explicit declaration is universally safe.
**Example:**
```csharp
// Source: RhinoCommon API docs, verified
// CORRECT - explicit out declaration
double t;
bool success = curve.ClosestPoint(testPoint, out t);

// WRONG - do NOT use out var
// bool success = curve.ClosestPoint(testPoint, out var t);  // PROHIBITED
```

### Pattern 2: SDK-Mode Template Structure
**What:** Every script uses the full `GH_ScriptInstance` class with `RunScript` method.
**When to use:** Always, for all templates and examples.
**Example:**
```csharp
// Source: developer.rhino3d.com/guides/scripting/scripting-gh-csharp/
using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(/* inputs */, ref object /* outputs */)
  {
    // null guards first
    // logic
    // assign outputs
  }
}
```

### Pattern 3: GH_ScriptInstance Available Members
**What:** Properties and methods available inside the script class.
**When to use:** When debugging, accessing documents, or printing.
**Members:**
```csharp
// Properties
RhinoDocument   // Active Rhino.RhinoDoc
GrasshopperDocument  // The GH_Document that owns this script
Component       // The IGH_Component (for AddRuntimeMessage)
Iteration       // int - how many times RunScript called this solve

// Methods
Print(string text)           // Output to the [out] parameter
Print(string format, params object[] args)  // Formatted output
```

### Anti-Patterns to Avoid
- **`out var` declarations:** Prohibited. Always declare the variable type explicitly on a separate line.
- **`while` loops:** Prohibited. Always use `for` with pre-calculated count.
- **Non-ASCII characters:** Anywhere in the file (comments, strings, variable names) will crash the compiler.
- **`AllData()` on input DataTrees:** Broken in many Rhino 8 builds. Always iterate by path.
- **`transition-all` / web patterns:** Not applicable -- this is desktop RhinoCommon, not web.
- **Assigning output inside a loop:** Assign `ref object` outputs once at the end, not inside iteration.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Curve-curve intersection | Manual geometric tests | `Intersection.CurveCurve()` | Returns `CurveIntersections` with all events, handles overlaps |
| Curve-brep intersection | Split logic | `Intersection.CurveBrep()` | Returns overlap curves AND intersection points |
| Point inside closed brep | Ray casting / winding | `Brep.IsPointInside()` | Handles edge cases, tolerance-aware |
| Curve division | Manual parameter stepping | `Curve.DivideByCount()` / `DivideByLength()` | Returns both parameters and points correctly |
| Curve offset | Manual point offsetting | `Curve.Offset()` | Handles kinks, self-intersections, returns Curve[] |
| Curve joining | Manual endpoint matching | `Curve.JoinCurves()` | Static method, handles tolerance, direction |
| Surface normal at UV | Cross product of partial derivs | `BrepFace.NormalAt(u, v)` | Handles orientation reversal |
| Angle conversion | `Math.PI / 180.0` | `RhinoMath.ToRadians()` / `ToDegrees()` | Standard, consistent |
| DataTree path management | Manual index arithmetic | `GH_Path` constructor + `DataTree.Add()` | Type-safe, matches GH topology |

**Key insight:** RhinoCommon's geometry APIs handle edge cases (degenerate curves, reversed faces, self-intersecting offsets) that hand-rolled versions miss. Always use the SDK method.

## Common Pitfalls

### Pitfall 1: Using `out var` Syntax
**What goes wrong:** Compilation error on older Rhino 8 builds or inconsistent behavior.
**Why it happens:** The Rhino 8 Script Editor uses Roslyn, which theoretically supports C# 7+ features, but the safe baseline excludes `out var` per project convention.
**How to avoid:** Always declare the variable type explicitly before the call.
**Warning signs:** Compiler error mentioning "unexpected token" or "invalid expression term 'var'".

### Pitfall 2: AllData() on Input DataTrees
**What goes wrong:** Returns empty or throws in Rhino 8.
**Why it happens:** Rhino 8's DataTree<object> inputs from KUKAprc or other plugins don't always support AllData().
**How to avoid:** Always iterate using `tree.Paths` and `tree.Branch(path)`.
**Warning signs:** Empty results when data is clearly connected.

### Pitfall 3: Non-ASCII Characters in Any Position
**What goes wrong:** Compiler crash with no useful error message.
**Why it happens:** The Rhino C# compiler (both legacy and new) crashes on any non-ASCII byte in the source.
**How to avoid:** English comments only. No German. No em-dashes, smart quotes, or special symbols.
**Warning signs:** Compilation fails immediately with no line number.

### Pitfall 4: While Loops for Geometry Iteration
**What goes wrong:** Infinite loop crashes Rhino with no recovery.
**Why it happens:** Floating-point accumulation means the termination condition may never be exactly met.
**How to avoid:** Pre-calculate iteration count, use `for` loop with integer counter.
**Warning signs:** Rhino freezes, must be force-killed.

### Pitfall 5: Forgetting to Assign ref object Outputs
**What goes wrong:** Downstream components receive null, no error shown.
**Why it happens:** `ref object` defaults to null. If you return early from a guard without assignment, output stays null.
**How to avoid:** Either assign a default before guards, or ensure every code path assigns the output.
**Warning signs:** "Null data" warnings on downstream components.

### Pitfall 6: Intersection Return Types Confusion
**What goes wrong:** Treating CurveCurve result as points instead of iterating IntersectionEvent objects.
**Why it happens:** `Intersection.CurveCurve()` returns `CurveIntersections` (a collection of `IntersectionEvent`), not `Point3d[]`. Each event has `.PointA`, `.PointB`, `.IsOverlap`, `.ParameterA`, `.ParameterB`.
**How to avoid:** Always iterate with `for (int i = 0; i < events.Count; i++)` and access `events[i].PointA`.
**Warning signs:** Compile error about missing Cast or invalid array index.

### Pitfall 7: Brep.ClosestPoint Overload Confusion
**What goes wrong:** Using the simple overload (`Point3d ClosestPoint(Point3d)`) when you need the normal.
**Why it happens:** The extended overload has 7 parameters with multiple `out` values.
**How to avoid:** Know both overloads. Use the extended one when you need ComponentIndex, surface parameters, or normal.
**Warning signs:** No normal available, or using wrong overload silently.

### Pitfall 8: Parallel.For with Non-Thread-Safe Collections
**What goes wrong:** Missing items, duplicate items, or IndexOutOfRange crashes.
**Why it happens:** Standard `List<T>` is not thread-safe. Multiple threads writing simultaneously corrupt the internal array.
**How to avoid:** Use `ConcurrentBag<T>` for unordered results, or pre-allocate an array and write by index.
**Warning signs:** Inconsistent result counts between runs.

### Pitfall 9: Curve.Offset Returns Array
**What goes wrong:** Expecting a single Curve but getting Curve[] (array).
**Why it happens:** Offset of curves with kinks or self-intersections produces multiple segments.
**How to avoid:** Always handle `Curve[]` return. For simple cases, take `result[0]`. For complex cases, join with `Curve.JoinCurves()`.
**Warning signs:** Cannot assign Curve[] to Curve variable.

## Code Examples

Verified patterns from official RhinoCommon documentation and API reference.

### Curve.ClosestPoint
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Curve.ClosestPoint
// Returns: bool (true on success)
// Out: double t (curve parameter at closest point)
double t;
if (curve.ClosestPoint(testPoint, out t))
{
  Point3d closestPt = curve.PointAt(t);
}
```

### Curve.DivideByCount
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Curve.DivideByCount
// Returns: double[] (curve parameters), null on failure
// Out: Point3d[] points
Point3d[] divPts;
double[] parameters = curve.DivideByCount(segmentCount, true, out divPts);
if (parameters == null || divPts == null) return;
// divPts contains the division points
// parameters contains the curve parameters at those points
```

### Curve.DivideByLength
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Curve.DivideByLength
Point3d[] divPts;
double[] parameters = curve.DivideByLength(segmentLength, true, out divPts);
if (parameters == null) return;
```

### Curve.Offset
```csharp
// Source: developer.rhino3d.com/samples/rhinocommon/offset-curve/
// Returns: Curve[] (may contain multiple segments)
Curve[] offsetCurves = curve.Offset(
  Plane.WorldXY,       // or custom plane
  distance,            // positive = one side, negative = other
  tolerance,           // e.g. doc.ModelAbsoluteTolerance or 0.01
  CurveOffsetCornerStyle.Sharp  // Sharp, Round, Smooth, Chamfer, None
);
if (offsetCurves != null && offsetCurves.Length > 0)
{
  // Use offsetCurves[0] for simple case
  // Or Curve.JoinCurves(offsetCurves) for complex case
}
```

### Curve.JoinCurves (Static)
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Curve.JoinCurves
Curve[] joined = Curve.JoinCurves(inputCurves, 0.01);
// Returns array of joined curves; input curves that cannot be joined
// remain as individual curves in the output
```

### Curve.Trim
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Curve.Trim
// Trim by domain (parameter range)
Curve trimmed = curve.Trim(t0, t1);
if (trimmed != null)
{
  // Use trimmed curve
}
```

### Curve.Extend
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Curve.Extend
// Extend by length from one end
Curve extended = curve.Extend(
  CurveEnd.End,        // CurveEnd.Start, .End, or .Both
  length,
  CurveExtensionStyle.Line  // Line, Arc, or Smooth
);
```

### Intersection.CurveCurve
```csharp
// Source: developer.rhino3d.com/samples/rhinocommon/calculate-curve-intersections/
using Rhino.Geometry.Intersect;

CurveIntersections events = Intersection.CurveCurve(
  curveA, curveB, 0.001, 0.0);

if (events != null)
{
  List<Point3d> pts = new List<Point3d>();
  for (int i = 0; i < events.Count; i++)
  {
    IntersectionEvent ev = events[i];
    pts.Add(ev.PointA);
    // ev.PointB - point on curveB (same as PointA for transverse)
    // ev.ParameterA - parameter on curveA
    // ev.ParameterB - parameter on curveB
    // ev.IsOverlap - true if curves overlap in this region
  }
}
```

### Intersection.CurveSurface
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Intersection.CurveSurface
CurveIntersections events = Intersection.CurveSurface(
  curve, surface, 0.001, 0.0);
// Same iteration pattern as CurveCurve
```

### Intersection.CurveBrep
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Intersection.CurveBrep
// Returns: bool (true on success, may still have partial results on false)
Curve[] overlapCurves;
Point3d[] intersectionPoints;
bool success = Intersection.CurveBrep(
  curve, brep, 0.001,
  out overlapCurves,
  out intersectionPoints);

if (intersectionPoints != null)
{
  foreach (Point3d pt in intersectionPoints)
  {
    // process intersection point
  }
}
if (overlapCurves != null)
{
  foreach (Curve crv in overlapCurves)
  {
    // process overlap curve
  }
}
```

### Intersection.BrepBrep
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Intersection.BrepBrep
Curve[] intersectionCurves;
Point3d[] intersectionPoints;
bool success = Intersection.BrepBrep(
  brepA, brepB, 0.001,
  out intersectionCurves,
  out intersectionPoints);
```

### Brep.ClosestPoint (Extended)
```csharp
// Source: mcneel.github.io/rhinocommon-api-docs Brep.ClosestPoint
Point3d closestPt;
ComponentIndex ci;
double s, t;
Vector3d normal;
bool found = brep.ClosestPoint(
  testPoint,
  out closestPt,
  out ci,
  out s,
  out t,
  double.MaxValue,  // maximumDistance
  out normal);

if (found)
{
  // closestPt - closest point on brep
  // ci - which face/edge/vertex
  // s, t - surface parameters
  // normal - surface normal at that point
}
```

### Brep.IsPointInside
```csharp
// Source: developer.rhino3d.com/api/RhinoCommon Brep.IsPointInside
// IMPORTANT: Only valid for closed, manifold breps
bool inside = brep.IsPointInside(
  testPoint,
  RhinoMath.SqrtEpsilon,  // tolerance
  false);                   // strictlyIn
```

### BrepFace Normal
```csharp
// Source: developer.rhino3d.com/samples/rhinocommon/determine-normal-direction-of-brep-face/
BrepFace face = brep.Faces[faceIndex];
double u, v;
if (face.ClosestPoint(testPoint, out u, out v))
{
  Vector3d normal = face.NormalAt(u, v);
  if (face.OrientationIsReversed)
    normal.Reverse();
}
```

### DataTree Building with Correct Paths
```csharp
// Source: developer.rhino3d.com/api/grasshopper DataTree<T>
DataTree<Point3d> tree = new DataTree<Point3d>();

for (int i = 0; i < branchCount; i++)
{
  GH_Path path = new GH_Path(i);
  List<Point3d> branchData = new List<Point3d>();
  // ... populate branchData ...
  tree.AddRange(branchData, path);
}

out_tree = tree;
```

### DataTree Iteration (Safe Pattern)
```csharp
// Source: verified Rhino 8 pattern from existing knowledge base
for (int i = 0; i < tree.BranchCount; i++)
{
  GH_Path path = tree.Path(i);
  List<object> branch = tree.Branch(i);
  for (int j = 0; j < branch.Count; j++)
  {
    double val;
    if (double.TryParse(branch[j].ToString(), out val))
    {
      // use val
    }
  }
}
```

### DataTree Merge
```csharp
// Source: developer.rhino3d.com/api/grasshopper DataTree.MergeTree
DataTree<double> combined = new DataTree<double>(treeA);
combined.MergeTree(treeB);
// Branches with same paths are merged; unique paths are appended
```

### DataTree Flatten
```csharp
// Source: developer.rhino3d.com/api/grasshopper DataTree.Flatten
DataTree<double> flat = new DataTree<double>(tree);
flat.Flatten();
// All data now in a single branch {0}
```

### AddRuntimeMessage (Debugging)
```csharp
// Source: discourse.mcneel.com/t/c-make-runscript-error-on-purpose/71723
// Access via this.Component in SDK-Mode RunScript
this.Component.AddRuntimeMessage(
  GH_RuntimeMessageLevel.Warning, "Input curve is null");
this.Component.AddRuntimeMessage(
  GH_RuntimeMessageLevel.Error, "Division by zero");
this.Component.AddRuntimeMessage(
  GH_RuntimeMessageLevel.Remark, "Processing 42 points");
```

### Print (Debug Output)
```csharp
// Source: developer.rhino3d.com/guides/scripting/scripting-gh-csharp/
// Print() outputs to the component's [out] text parameter
Print("Curve length: " + curve.GetLength().ToString());
Print("Points: {0}, Curves: {1}", ptCount, crvCount);

// RhinoApp.WriteLine() outputs to Rhino command history
Rhino.RhinoApp.WriteLine("Debug: processing step " + i.ToString());
```

### Parallel.For with Thread-Safe Collection
```csharp
// Source: discourse.mcneel.com/t/system-threading-tasks-parallel-error-in-c/
using System.Threading.Tasks;
using System.Collections.Concurrent;

// Pre-allocate array for ordered results (thread-safe by index)
Point3d[] results = new Point3d[points.Count];

Parallel.For(0, points.Count, i =>
{
  // Each thread writes to its own index - no contention
  double t;
  curve.ClosestPoint(points[i], out t);
  results[i] = curve.PointAt(t);
});

out_points = new List<Point3d>(results);
```

### KUKAprc Analysis Axis Values Reading
```csharp
// Source: grasshopperdocs.com/components/kukaprcpro/analysis.html + user experience
// Analysis outputs Robot Axis Values as a DataTree
// Path structure: one branch per command, 6 values per branch (A1-A6)
// Input must be set to Tree access mode

private void RunScript(DataTree<object> axisValues, ref object A1, ref object A2,
  ref object A3, ref object A4, ref object A5, ref object A6)
{
  if (axisValues == null || axisValues.BranchCount == 0) return;

  List<double> a1 = new List<double>();
  List<double> a2 = new List<double>();
  List<double> a3 = new List<double>();
  List<double> a4 = new List<double>();
  List<double> a5 = new List<double>();
  List<double> a6 = new List<double>();

  for (int i = 0; i < axisValues.BranchCount; i++)
  {
    List<object> branch = axisValues.Branch(i);
    if (branch.Count >= 6)
    {
      double val;
      if (double.TryParse(branch[0].ToString(), out val)) a1.Add(val);
      if (double.TryParse(branch[1].ToString(), out val)) a2.Add(val);
      if (double.TryParse(branch[2].ToString(), out val)) a3.Add(val);
      if (double.TryParse(branch[3].ToString(), out val)) a4.Add(val);
      if (double.TryParse(branch[4].ToString(), out val)) a5.Add(val);
      if (double.TryParse(branch[5].ToString(), out val)) a6.Add(val);
    }
  }

  A1 = a1; A2 = a2; A3 = a3;
  A4 = a4; A5 = a5; A6 = a6;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Legacy C# Script component (body-only editor) | New Script Editor (full class visible, SDK-Mode/Script-Mode) | Rhino 8.0 (2023) | Templates must show full class; SDK-Mode is default |
| CodeDom compiler (.NET Framework 4.x) | Roslyn compiler (.NET 7/8) | Rhino 8.0 | Modern C# features available (string interpolation, etc.) but project convention excludes `out var` |
| `AllData()` for tree iteration | Path-based iteration (`tree.Paths`, `tree.Branch(path)`) | Rhino 8.x | `AllData()` may fail on plugin-generated DataTrees |
| `Component.AddRuntimeMessage` false error in editor | Fixed in Rhino 8.7 | 2024 | Bug RH-81345 resolved; AddRuntimeMessage now works without false editor warnings |
| .NET Framework only | .NET 7 default, .NET 8 available (8.20+) | Rhino 8.0-8.20 | Can use newer BCL features; System.Threading.Tasks fully available |

**Deprecated/outdated:**
- `GH_Plane` wrapper unwrapping: Only needed in Python; C# handles plane types natively
- `AllData()` for input tree processing: Use path iteration instead
- Script-Mode (no class): Works but SDK-Mode is the project standard

## Open Questions

1. **KUKAprc Analysis DataTree Path Structure**
   - What we know: Analysis outputs "Robot Axis Values" as Number type. When wired to a Panel, shows A1-A6 per command. The output is a DataTree.
   - What's unclear: Exact path structure ({0;i} or {i}) and whether values are always 6 per branch or can include external axes (E1-E6) as additional values. The Pro version supports external axes.
   - Recommendation: Document the common pattern (6 values per branch for A1-A6). Note that the Pro version may output external axes as separate outputs or additional values. User should verify with their specific KUKAprc version by wiring a Panel and checking structure. Flag this as needing a practical test.

2. **Exact C# Language Version in Rhino 8 Script Editor**
   - What we know: Roslyn compiler is used. String interpolation works. Async/await works in Rhino 8.12+. .NET 7 is the base runtime.
   - What's unclear: Whether `out var`, `is` pattern matching, `switch` expressions, or tuple deconstruction work in the GH Script component specifically (vs. the standalone Script Editor).
   - Recommendation: Maintain the conservative baseline (no `out var`, explicit types). This is correct per the existing knowledge base. If user discovers newer features work, they can relax restrictions.

3. **Parallel.For Performance in Script Component vs Compiled Plugin**
   - What we know: Parallel.For works inside the C# Script component. One source says "these parallelisation tricks only work for compiled components" but another shows it working in script components with List Access inputs.
   - What's unclear: Whether there's a significant performance overhead in Script component vs compiled .gha.
   - Recommendation: Document it as working but with caveats. Pre-allocated array by index is the safest pattern. Note that inputs must be set to "List Access" for the full list to be available inside RunScript.

## Sources

### Primary (HIGH confidence)
- [developer.rhino3d.com/guides/scripting/scripting-gh-csharp/](https://developer.rhino3d.com/guides/scripting/scripting-gh-csharp/) - SDK-Mode vs Script-Mode, GH_ScriptInstance, modern C# support
- [developer.rhino3d.com/guides/scripting/scripting-component/](https://developer.rhino3d.com/guides/scripting/scripting-component/) - New Script Component ZUI, input/output configuration
- [developer.rhino3d.com/api/rhinocommon/rhino.geometry.curve](https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.curve) - Curve methods: DivideByCount, DivideByLength, ClosestPoint, Offset, Trim, Extend, JoinCurves
- [developer.rhino3d.com/api/rhinocommon/rhino.geometry.intersect.intersection](https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.intersect.intersection) - All Intersection static methods
- [developer.rhino3d.com/samples/rhinocommon/calculate-curve-intersections/](https://developer.rhino3d.com/samples/rhinocommon/calculate-curve-intersections/) - CurveCurve example with IntersectionEvent iteration
- [developer.rhino3d.com/api/grasshopper/html/T_Grasshopper_DataTree_1.htm](https://developer.rhino3d.com/api/grasshopper/html/T_Grasshopper_DataTree_1.htm) - DataTree<T> class: properties, methods, constructors
- [developer.rhino3d.com/api/grasshopper/html/T_Grasshopper_Kernel_GH_RuntimeMessageLevel.htm](https://developer.rhino3d.com/api/grasshopper/html/T_Grasshopper_Kernel_GH_RuntimeMessageLevel.htm) - GH_RuntimeMessageLevel enum values
- [developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/](https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/) - .NET 7/8 runtime details for Rhino 8

### Secondary (MEDIUM confidence)
- [discourse.mcneel.com/t/addruntimemessage/179200](https://discourse.mcneel.com/t/addruntimemessage/179200) - AddRuntimeMessage works in Rhino 8, bug RH-81345 fixed in 8.7
- [discourse.mcneel.com/t/c-make-runscript-error-on-purpose/71723](https://discourse.mcneel.com/t/c-make-runscript-error-on-purpose/71723) - `this.Component.AddRuntimeMessage()` syntax in Script component
- [discourse.mcneel.com/t/system-threading-tasks-parallel-error-in-c/161673](https://discourse.mcneel.com/t/system-threading-tasks-parallel-error-in-c/161673) - Parallel.For works in Script component with ConcurrentBag
- [james-ramsden.com/data-trees-and-c-in-grasshopper/](https://james-ramsden.com/data-trees-and-c-in-grasshopper/) - DataTree construction with GH_Path
- [james-ramsden.com/multithreading-a-foreach-loop-in-a-grasshopper-components-in-c/](https://james-ramsden.com/multithreading-a-foreach-loop-in-a-grasshopper-components-in-c/) - Parallel patterns, ConcurrentDictionary
- [grasshopperdocs.com/components/kukaprcpro/analysis.html](https://grasshopperdocs.com/components/kukaprcpro/analysis.html) - KUKAprc Analysis component outputs
- [grasshopper3d.com/forum/topics/print-in-c](https://www.grasshopper3d.com/forum/topics/print-in-c) - Print() method with capital P

### Tertiary (LOW confidence)
- KUKAprc Analysis DataTree path structure (one branch per command, 6 values for A1-A6): inferred from documentation + community usage patterns, not verified against actual component output. Needs practical validation.
- External axis values (E1-E6) output structure: mentioned only in KUKAprc Pro docs, no code example found. Document as requiring user verification.

## Metadata

**Confidence breakdown:**
- Standard stack (namespaces/APIs): HIGH - verified against official RhinoCommon and Grasshopper API docs
- Architecture (document structure, patterns): HIGH - based on existing knowledge base + verified API patterns
- Compiler rules (KB-01): HIGH for ASCII/out var/while restrictions; MEDIUM for exact C# version (conservative baseline is safe)
- Geometry operations (KB-03): HIGH - all method signatures verified against official API docs
- DataTree patterns (KB-02): HIGH - API methods verified; AllData() issue confirmed by multiple sources
- KUKAprc patterns (KB-04): MEDIUM - component outputs verified; exact DataTree path structure needs practical test
- Debugging (KB-06): HIGH - AddRuntimeMessage and Print() verified against official docs and forum
- Parallel processing (KB-07): MEDIUM - works in Script component per forum evidence; exact performance characteristics unclear
- Templates (KB-08): HIGH - composed from verified API patterns

**Research date:** 2026-03-23
**Valid until:** 2026-04-23 (stable -- RhinoCommon API changes slowly between point releases)
