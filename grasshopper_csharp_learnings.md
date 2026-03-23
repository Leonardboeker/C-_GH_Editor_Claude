# Grasshopper C# Scripting -- Key Learnings

## 1. Script Structure

Every GH C# script in Rhino 8 follows this exact structure.
Never include the class wrapper or RunScript signature manually -- GH provides it.
Only paste the body:

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
  private void RunScript(List<Plane> planes, double angle, ref object out_planes)
  {
    // your code here
  }
}
```

---

## 2. Critical Rules -- Never Break These

### No German or Non-ASCII Characters
**EVER.** Not in comments, not in variable names, not anywhere.
IronPython and the Rhino 8 C# compiler crash on any non-ASCII character.
This includes: ae/oe/ue umlauts, eszett, em-dashes, en-dashes, arrows, and any special symbol.

```csharp
// WRONG
// Berechne die Mittellinie zwischen zwei Punkten

// CORRECT
// Calculate midline between two points
```

### No German Comments at All
Write comments in English only, or write no comments at all.
A script with zero comments is better than one with German comments.

### No `out var` Declarations
Always declare the out variable type on a separate line before the method call.
The Rhino 8 Script Editor uses Roslyn and may support `out var` in recent builds,
but older Rhino 8 installs do not. Explicit declaration is universally safe.

```csharp
// WRONG - prohibited
// bool ok = curve.ClosestPoint(pt, out var t);

// CORRECT - explicit out declaration
double t;
bool ok = curve.ClosestPoint(pt, out t);
```

### No Pattern Matching, Records, or Switch Expressions
Stick to classic C# control flow. These C# 8+ features may not compile
in all Rhino 8 Script Editor versions:
- `is` pattern matching (`if (obj is Point3d p)`) -- use explicit cast + null check
- `switch` expressions -- use classic `switch` statements
- record types -- use classes or structs
- tuple deconstruction (`var (a, b) = ...`) -- use explicit variable assignment

```csharp
// WRONG - pattern matching
// if (geo is Curve c) { ... }

// CORRECT - explicit cast
Curve c = geo as Curve;
if (c != null) { /* use c */ }
```

### String Interpolation Is Safe
String interpolation ($"text {variable}") works in Rhino 8 (Roslyn compiler).
However, keep all interpolated strings ASCII-only.

```csharp
// OK in Rhino 8
Print($"Found {count} points");

// WRONG - non-ASCII inside interpolation
// Print($"Punkt {i} ist {distance:F2}m entfernt");
```

---

## 3. Output Parameters

All outputs use `ref object`:

```csharp
private void RunScript(List<Plane> planes, double angle, ref object out_planes)
```

Assign at the end:
```csharp
out_planes = result;
```

Never forget to assign -- an unassigned output gives null silently.

### Multiple Outputs

Each output is a separate `ref object` parameter. Name them with the `out_` prefix
or short descriptive names matching the GH component output nicknames:

```csharp
private void RunScript(
  List<Point3d> points,
  List<Curve> curves,
  ref object out_points,
  ref object out_curves,
  ref object out_count)
{
  if (points == null) return;
  if (curves == null) return;

  List<Point3d> resultPts = new List<Point3d>();
  List<Curve> resultCrvs = new List<Curve>();

  // ... processing ...

  // Assign ALL outputs at the end, never inside a loop
  out_points = resultPts;
  out_curves = resultCrvs;
  out_count = resultPts.Count;
}
```

### Output Assignment Rules

1. **Assign once at the end** -- never assign `ref object` outputs inside a loop
2. **Every code path must assign** -- if you `return` early from a guard, the output stays null
3. **Lists auto-convert** -- `List<Point3d>` assigned to `ref object` automatically becomes a GH list
4. **DataTree outputs** -- assign `DataTree<T>` directly to `ref object`
5. **Single values** -- assign directly: `out_value = 42.0;`

### Default Values Before Guards

To prevent null outputs on early return, assign defaults before guards:

```csharp
private void RunScript(List<Plane> planes, ref object out_planes, ref object out_count)
{
  // Defaults -- downstream components get these if guards trigger
  out_planes = new List<Plane>();
  out_count = 0;

  if (planes == null || planes.Count == 0) return;

  // ... actual processing ...
  out_planes = result;
  out_count = result.Count;
}
```

---

## 4. Input Access Types

Right-click on any input to set access mode:

| Mode | Use when |
|---|---|
| Item | single value |
| List | flat list |
| Tree | DataTree (e.g. KUKAprc Analysis outputs) |

For KUKAprc Analysis outputs always use **Tree access**:
```csharp
private void RunScript(DataTree<object> axis_values, ...)
```

### Access Mode Reference Table

| Access Mode | C# Parameter Type | When to Use | Example Input |
|---|---|---|---|
| Item Access | `double`, `int`, `string`, `Point3d`, `Curve` | Single value per execution | Number Slider, one Point |
| List Access | `List<double>`, `List<Point3d>`, `List<Curve>` | Flat list of values | Series, multiple Points |
| Tree Access | `DataTree<object>` | Branched data, plugin outputs | KUKAprc Analysis, Entwine |

### Type Casting from Tree Access

Tree access always gives `DataTree<object>`. Cast values inside the loop:

```csharp
private void RunScript(DataTree<object> tree, ref object A)
{
  if (tree == null || tree.BranchCount == 0) return;

  List<double> values = new List<double>();

  for (int i = 0; i < tree.BranchCount; i++)
  {
    List<object> branch = tree.Branch(i);
    for (int j = 0; j < branch.Count; j++)
    {
      // Method 1: TryParse (safest for numbers)
      double val;
      if (double.TryParse(branch[j].ToString(), out val))
      {
        values.Add(val);
      }
    }
  }
  A = values;
}
```

### Casting Geometry from Tree Access

For geometry types, use the GH wrapper's `.Value` property:

```csharp
// For Point3d from DataTree<object>
GH_Point ghPt = branch[j] as GH_Point;
if (ghPt != null)
{
  Point3d pt = ghPt.Value;
}

// For Curve from DataTree<object>
GH_Curve ghCrv = branch[j] as GH_Curve;
if (ghCrv != null)
{
  Curve crv = ghCrv.Value;
}

// For Plane from DataTree<object>
GH_Plane ghPln = branch[j] as GH_Plane;
if (ghPln != null)
{
  Plane pln = ghPln.Value;
}
```

### Common Casting Types

| GH Wrapper | `.Value` Type | Use For |
|---|---|---|
| `GH_Number` | `double` | Numbers (alternative to TryParse) |
| `GH_Integer` | `int` | Integers |
| `GH_Point` | `Point3d` | Points |
| `GH_Curve` | `Curve` | Curves |
| `GH_Plane` | `Plane` | Planes |
| `GH_Surface` | `Surface` | Surfaces |
| `GH_Brep` | `Brep` | Breps |
| `GH_Vector` | `Vector3d` | Vectors |
| `GH_String` | `string` | Strings |
| `GH_Boolean` | `bool` | Booleans |

---

## 5. DataTree Iteration (Rhino 8)

`AllData()` does not work in Rhino 8. Use path iteration:

```csharp
foreach (GH_Path path in tree.Paths)
{
  List<object> branch = tree.Branch(path);
  foreach (object v in branch)
  {
    double val;
    if (double.TryParse(v.ToString(), out val))
    {
      // use val
    }
  }
}
```

---

## 6. Never Use While Loops for Geometry

While loops can cause infinite loops and crash Rhino.
Always use for loops with a pre-calculated count:

```csharp
// WRONG - can crash
while (y <= y_max)
{
  y += step;
}

// CORRECT
int line_count = (int)Math.Floor((y_max - y_min) / step) + 1;
for (int i = 0; i < line_count; i++)
{
  double y = Math.Min(y_min + i * step, y_max);
}
```

---

## 7. Null and Range Guards

Always guard inputs at the top of RunScript:

```csharp
if (planes == null || planes.Count == 0) return;
if (tool_diameter <= 0) return;
if (stepover <= 0) return;
if (x_min >= x_max) return;
```

This prevents runtime errors and Rhino crashes.

---

## 8. Plane Construction

Always use explicit XAxis/YAxis construction:

```csharp
Plane pln = new Plane(origin, xAxis, yAxis);
```

Never construct from just a point -- it creates an undefined plane.

Copying a plane:
```csharp
Plane copy = new Plane(existingPlane);
```

Transforming a plane:
```csharp
Transform xform = Transform.Rotation(angleRad, axis, center);
pln.Transform(xform);
```

---

## 9. RhinoMath for Angle Conversion

Always use RhinoMath -- never Math.PI manually:

```csharp
double angleRad = RhinoMath.ToRadians(angleDeg);
```

---

## 10. Helper Methods

Extract repeated logic into private helper methods inside the class:

```csharp
private Point3d PointOn(Plane pln, double x, double y, double z)
{
  return pln.Origin
    + pln.XAxis * x
    + pln.YAxis * y
    + pln.ZAxis * z;
}
```

Call anywhere in RunScript:
```csharp
planes.Add(new Plane(PointOn(origin, x0, y0, z0), origin.XAxis, origin.YAxis));
```

---

## 11. GH_Plane Wrapper Problem (Python only)

In Python scripts GH passes planes as GH_Plane wrappers, not rg.Plane.
Unpack with:
```python
if hasattr(pln, 'Value'):
    pln = pln.Value
```

In C# this problem does not exist -- types are handled natively.
**This is one of the main reasons to prefer C# over Python.**

---

## 12. DataTree Output

When outputting a DataTree from C#:

```csharp
DataTree<double> tree = new DataTree<double>();
GH_Path path = new GH_Path(0, i);
tree.Add(value, path);
out_tree = tree;
```

For simple cases -- just output a List and let GH handle it:
```csharp
List<double> result = new List<double>();
result.Add(value);
out_result = result;
```

---

## 13. Speeds Always Parallel to Planes

When building toolpaths always output a speed list with exactly the same count as the plane list:

```csharp
List<Plane> planes = new List<Plane>();
List<double> speeds = new List<double>();

planes.Add(somePlane);
speeds.Add(someSpeed); // always add speed at the same time as plane
```

---

## 14. Galapagos Integration

For Galapagos optimization:
- Genome inputs must be **Number Sliders** or **Integer Sliders** directly -- not Remote Receivers
- Fitness output must be a **single number** -- use Mass Addition on flattened inputs
- NaN in fitness breaks Galapagos -- always guard against it:

```csharp
double total = col * 100.0 + reach * 50.0 + sing * 1.0;
fitness = double.IsNaN(total) ? 999999.0 : total;
```

- For binary choices (e.g. 180 or -180): use Integer Slider 0/1, map in C#:
```csharp
angle = (toggle == 0) ? -180.0 : 180.0;
```

---

## 15. C# vs Python -- When to Use Which

| Use Case | Tool |
|---|---|
| Geometry, planes, transforms | C# |
| Toolpath generation | C# |
| Performance-critical loops | C# |
| JSON parsing, API calls | Python |
| External libraries (numpy, requests) | Python |
| KUKAprc / RhinoCommon | C# |

C# is compiled -- 10-100x faster than Python for geometry operations.
C# has no GH_Plane wrapper issues.
C# has full Intellisense support in Visual Studio / Rider.

---

## 16. Surface Evaluation

To get a plane at UV position on a surface:

```csharp
Surface reparamed = srf.Duplicate() as Surface;
reparamed.SetDomain(0, new Interval(0, 1));
reparamed.SetDomain(1, new Interval(0, 1));

Point3d pt;
Vector3d[] derivs;
reparamed.Evaluate(u, v, 1, out pt, out derivs);

Vector3d xAxis = derivs[0];
xAxis.Unitize();
Vector3d yAxis = derivs[1];
yAxis.Unitize();

Plane result = new Plane(pt, xAxis, yAxis);
```

Do NOT use `out var derivs` -- it does not work in this C# version.

---

## 17. Vector Operations

```csharp
// Cross product
Vector3d perp = Vector3d.CrossProduct(ab, Vector3d.ZAxis);

// Unitize (normalize)
perp.Unitize();

// Check if result is valid
if (perp.Length < 0.001) { /* fallback */ }

// Point offset along vector
Point3d newPt = origin + vector * distance;
```

---

## 18. Complete Minimal Template

Copy this as starting point for every new component:

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
  private void RunScript(List<Plane> planes, double value, ref object out_planes)
  {
    if (planes == null || planes.Count == 0) return;

    List<Plane> result = new List<Plane>();

    foreach (Plane pln in planes)
    {
      result.Add(pln);
    }

    out_planes = result;
  }
}
```

---

## 19. Rhino 8 Script Editor vs Legacy

Rhino 8 replaced the legacy C# Script component editor with a new Script Editor.

| Feature | Legacy (Rhino 7) | New (Rhino 8) |
|---|---|---|
| Compiler | CodeDom (.NET Framework 4.x) | Roslyn (.NET 7, .NET 8 on 8.20+) |
| Editor mode | Body-only (no class visible) | Full class visible (SDK-Mode) |
| Script modes | Body-only | SDK-Mode (full class) and Script-Mode (global scope) |
| String interpolation | No | Yes |
| Async/await | No | Yes (Rhino 8.12+) |
| Target framework | .NET Framework 4.8 | .NET 7.0 (8.0 on Rhino 8.20+) |
| Auto-referenced assemblies | RhinoCommon, Grasshopper | RhinoCommon, Grasshopper, System.* |

### SDK-Mode vs Script-Mode

**SDK-Mode (use this always):** Full class with `GH_ScriptInstance` base class.
The Script Editor shows the entire class including `using` statements, class declaration,
and `RunScript` method. This is the project standard.

**Script-Mode:** Code runs in global scope without a class wrapper.
Simpler for quick tests but lacks helper method support and is harder to debug.
Do not use Script-Mode for production scripts.

### GH_ScriptInstance Available Members

Inside SDK-Mode, the following properties and methods are available on `this`:

```csharp
// Properties available in RunScript
RhinoDocument          // Rhino.RhinoDoc - the active document
GrasshopperDocument    // GH_Document - the GH document owning this script
Component              // IGH_Component - the script component itself
Iteration              // int - how many times RunScript was called this solve

// Methods
Print(string text)                          // Output to the [out] parameter
Print(string format, params object[] args)  // Formatted output to [out]
Reflect(object obj)                         // Reflect object members to [out]
```

### Important: Always Output Full Class

When Claude writes a C# script for Grasshopper, always output the complete
SDK-Mode class including `using` statements, class declaration, and `RunScript`.
The user pastes the entire class into the new Script Editor.
Never output just the method body -- the new editor expects a full class.

---

## 20. Curve Operations

Common curve methods from RhinoCommon. All return values must be null-checked.

### Curve.ClosestPoint

Returns the parameter `t` on the curve nearest to a test point.

```csharp
double t;
bool success = curve.ClosestPoint(testPoint, out t);
if (success)
{
  Point3d closestPt = curve.PointAt(t);
  Vector3d tangent = curve.TangentAt(t);
}
```

### Curve.DivideByCount

Divides curve into N equal segments. Returns parameters AND points.

```csharp
Point3d[] divPts;
double[] parameters = curve.DivideByCount(segmentCount, true, out divPts);
if (parameters == null || divPts == null)
{
  this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Division failed");
  return;
}
// divPts.Length == segmentCount + 1 (includes both endpoints)
// parameters[i] is the curve parameter at divPts[i]
```

### Curve.DivideByLength

Divides curve by a fixed chord length. Point count depends on curve length.

```csharp
Point3d[] divPts;
double[] parameters = curve.DivideByLength(segmentLength, true, out divPts);
if (parameters == null)
{
  this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Division failed");
  return;
}
// Point count = floor(curveLength / segmentLength) + 1 approximately
```

### Curve.Offset

Returns `Curve[]` (array), never a single curve. Kinks and self-intersections produce multiple segments.

```csharp
Curve[] offsetCurves = curve.Offset(
  Plane.WorldXY,                    // offset plane
  distance,                          // positive = one side, negative = other
  0.01,                              // tolerance (or doc.ModelAbsoluteTolerance)
  CurveOffsetCornerStyle.Sharp);     // Sharp, Round, Smooth, Chamfer, None

if (offsetCurves != null && offsetCurves.Length > 0)
{
  // Simple case: take first result
  Curve single = offsetCurves[0];

  // Complex case: join all segments
  Curve[] joined = Curve.JoinCurves(offsetCurves, 0.01);
}
```

### Curve.JoinCurves (Static Method)

Joins multiple curves into fewer continuous curves.

```csharp
Curve[] joined = Curve.JoinCurves(inputCurves, 0.01);
if (joined != null)
{
  // joined.Length may be less than inputCurves count
  // Check joined[0].IsClosed if you need a closed result
}
```

### Curve.Trim

Trims curve to a parameter subdomain. Returns null if domain is invalid.

```csharp
Curve trimmed = curve.Trim(t0, t1);
if (trimmed != null)
{
  // trimmed runs from parameter t0 to t1
}
```

### Curve.Extend

Extends curve from one or both ends by a length.

```csharp
Curve extended = curve.Extend(
  CurveEnd.Both,                   // CurveEnd.Start, .End, or .Both
  extensionLength,
  CurveExtensionStyle.Line);       // Line, Arc, or Smooth
```

### Curve Properties Quick Reference

| Property/Method | Returns | Notes |
|---|---|---|
| curve.GetLength() | double | Total arc length |
| curve.PointAtStart | Point3d | Start point |
| curve.PointAtEnd | Point3d | End point |
| curve.PointAt(t) | Point3d | Point at parameter t |
| curve.TangentAt(t) | Vector3d | Tangent vector at t |
| curve.CurvatureAt(t) | Vector3d | Curvature vector at t |
| curve.Domain | Interval | Parameter domain (min, max) |
| curve.IsClosed | bool | True if start == end |
| curve.IsPlanar() | bool | True if curve lies in a plane |
| curve.Degree | int | NURBS degree |

---

## 21. Brep Operations

Breps (Boundary Representations) are the most common solid geometry type in Rhino. A Brep has Faces, Edges, and Vertices.

### Brep.ClosestPoint (Extended Overload)

Returns closest point, surface parameters, component index, and surface normal. Always use this when you need more than just the point.

```csharp
Point3d closestPt;
ComponentIndex ci;
double s, t;
Vector3d normal;
bool found = brep.ClosestPoint(
  testPoint,
  out closestPt,
  out ci,           // which face/edge/vertex
  out s,            // surface U parameter
  out t,            // surface V parameter
  double.MaxValue,  // maximum search distance
  out normal);      // surface normal at closest point

if (found)
{
  // closestPt is the closest point on the brep
  // ci.ComponentIndexType tells you if it hit a Face, Edge, or Vertex
  // s, t are the UV parameters on the face (if ci is a face)
  // normal is the outward-facing surface normal
}
```

### Brep.ClosestPoint (Simple Overload)

Use when you only need the point.

```csharp
Point3d closestPt = brep.ClosestPoint(testPoint);
// No success check -- returns the input point on failure (rare)
```

### Brep.IsPointInside

Tests if a point is inside a closed, manifold brep. Only valid for closed solids.

```csharp
// IMPORTANT: Check that the brep is solid first
if (!brep.IsSolid)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning, "Brep is not solid -- cannot test containment");
  return;
}

bool inside = brep.IsPointInside(testPoint, RhinoMath.SqrtEpsilon, false);
// Third param: strictlyIn (false = on boundary counts as inside)
```

### BrepFace Normal at a Point

Gets the surface normal at the closest point on a specific face. Must check OrientationIsReversed.

```csharp
BrepFace face = brep.Faces[faceIndex];
double u, v;
if (face.ClosestPoint(testPoint, out u, out v))
{
  Vector3d normal = face.NormalAt(u, v);
  if (face.OrientationIsReversed)
    normal.Reverse();
  // normal now points outward from the brep
}
```

### Iterating Brep Faces

```csharp
for (int i = 0; i < brep.Faces.Count; i++)
{
  BrepFace face = brep.Faces[i];
  Interval uDom = face.Domain(0);
  Interval vDom = face.Domain(1);
  double uMid = uDom.Mid;
  double vMid = vDom.Mid;
  Point3d center = face.PointAt(uMid, vMid);
  Vector3d normal = face.NormalAt(uMid, vMid);
  if (face.OrientationIsReversed) normal.Reverse();
}
```

### Brep Properties Quick Reference

| Property/Method | Returns | Notes |
|---|---|---|
| brep.Faces | BrepFaceList | All faces of the brep |
| brep.Edges | BrepEdgeList | All edges |
| brep.Vertices | BrepVertexList | All vertices |
| brep.IsSolid | bool | True if closed manifold |
| brep.IsManifold | bool | True if manifold |
| brep.GetArea() | double | Total surface area |
| brep.GetVolume() | double | Volume (only valid if IsSolid) |
| brep.GetBoundingBox(true) | BoundingBox | World-axis bounding box |

---

## 22. Intersection Patterns

All intersection methods live in `Rhino.Geometry.Intersect.Intersection` (static class). Add `using Rhino.Geometry.Intersect;` to use them.

### Intersection.CurveCurve

Returns CurveIntersections (a collection of IntersectionEvent objects). Do NOT treat as Point3d array.

```csharp
using Rhino.Geometry.Intersect;

CurveIntersections events = Intersection.CurveCurve(
  curveA, curveB,
  0.001,   // intersection tolerance
  0.0);    // overlap tolerance

if (events != null)
{
  List<Point3d> intersectionPts = new List<Point3d>();
  for (int i = 0; i < events.Count; i++)
  {
    IntersectionEvent ev = events[i];
    if (ev.IsOverlap)
    {
      // Overlap region: ev.PointA and ev.PointA2 are endpoints
      // ev.OverlapA is the Interval on curveA
    }
    else
    {
      // Transverse intersection: single point
      intersectionPts.Add(ev.PointA);
      // ev.ParameterA = parameter on curveA
      // ev.ParameterB = parameter on curveB
    }
  }
}
```

### IntersectionEvent Properties

| Property | Type | Description |
|---|---|---|
| PointA | Point3d | Intersection point on curve A |
| PointB | Point3d | Intersection point on curve B (same as PointA for transverse) |
| ParameterA | double | Parameter on curve A |
| ParameterB | double | Parameter on curve B |
| IsOverlap | bool | True if curves overlap (not just cross) |
| OverlapA | Interval | Parameter range of overlap on curve A |
| OverlapB | Interval | Parameter range of overlap on curve B |
| PointA2 | Point3d | End point of overlap on curve A (if IsOverlap) |
| PointB2 | Point3d | End point of overlap on curve B (if IsOverlap) |

### Intersection.CurveSurface

Same return type as CurveCurve, same iteration pattern.

```csharp
CurveIntersections events = Intersection.CurveSurface(
  curve, surface,
  0.001,   // tolerance
  0.0);    // overlap tolerance

if (events != null)
{
  for (int i = 0; i < events.Count; i++)
  {
    Point3d pt = events[i].PointA;
    double curveParam = events[i].ParameterA;
  }
}
```

### Intersection.CurveBrep

Different return type: two separate arrays (overlap curves AND intersection points).

```csharp
Curve[] overlapCurves;
Point3d[] intersectionPoints;
bool success = Intersection.CurveBrep(
  curve, brep, 0.001,
  out overlapCurves,
  out intersectionPoints);

// IMPORTANT: Check both arrays for null independently
if (intersectionPoints != null && intersectionPoints.Length > 0)
{
  for (int i = 0; i < intersectionPoints.Length; i++)
  {
    Point3d pt = intersectionPoints[i];
  }
}
if (overlapCurves != null && overlapCurves.Length > 0)
{
  for (int i = 0; i < overlapCurves.Length; i++)
  {
    Curve crv = overlapCurves[i];
  }
}
```

### Intersection.BrepBrep

Returns intersection curves and points where two breps meet.

```csharp
Curve[] intersectionCurves;
Point3d[] intersectionPoints;
bool success = Intersection.BrepBrep(
  brepA, brepB, 0.001,
  out intersectionCurves,
  out intersectionPoints);

if (success && intersectionCurves != null)
{
  // intersectionCurves are the curves where surfaces intersect
  // intersectionPoints are isolated tangent points (rare)
}
```

### Intersection.LinePlane

Quick line-plane intersection test.

```csharp
double lineParam;
bool success = Intersection.LinePlane(line, plane, out lineParam);
if (success)
{
  Point3d hitPt = line.PointAt(lineParam);
}
```

### Intersection.PlanePlanePlane

Find the point where three planes meet.

```csharp
Point3d meetPt;
bool success = Intersection.PlanePlanePlane(planeA, planeB, planeC, out meetPt);
```

### Intersection Method Summary

| Method | Returns | Out Params | Use Case |
|---|---|---|---|
| CurveCurve | CurveIntersections | none | Two curves crossing |
| CurveSurface | CurveIntersections | none | Curve hitting a surface |
| CurveBrep | bool | Curve[], Point3d[] | Curve hitting a solid |
| BrepBrep | bool | Curve[], Point3d[] | Two solids intersecting |
| LinePlane | bool | double (line param) | Line-plane hit test |
| PlanePlanePlane | bool | Point3d | Three-plane meet point |

---

## 23. Transform Operations

Transforms modify geometry in place (for mutable types) or return new geometry. For planes and points, Transform modifies in place. For curves and breps, use .DuplicateXxx() first to keep the original.

### Creating Transforms

```csharp
// Translation
Transform move = Transform.Translation(new Vector3d(dx, dy, dz));
Transform move2 = Transform.Translation(Vector3d.ZAxis * height);

// Rotation (angle in radians, axis, center point)
Transform rotate = Transform.Rotation(
  RhinoMath.ToRadians(angleDeg),
  Vector3d.ZAxis,
  centerPoint);

// Rotation from one direction to another
Transform reorient = Transform.Rotation(
  fromDirection,    // Vector3d
  toDirection,      // Vector3d
  centerPoint);     // Point3d

// Uniform scale
Transform scale = Transform.Scale(centerPoint, factor);

// Non-uniform scale
Transform scaleNU = Transform.Scale(
  Plane.WorldXY,    // scale relative to this plane
  xFactor, yFactor, zFactor);

// Mirror across a plane
Transform mirror = Transform.Mirror(mirrorPlane);

// Plane-to-plane orientation (most useful for robotics)
Transform orient = Transform.PlaneToPlane(sourcePlane, targetPlane);
```

### Applying Transforms

```csharp
// Points (value type) -- returns new point
Point3d movedPt = new Point3d(pt);
movedPt.Transform(xform);

// Planes (value type) -- modifies in place
Plane pln = new Plane(originalPlane);
pln.Transform(xform);

// Curves (reference type) -- duplicate first to keep original
Curve movedCurve = curve.DuplicateCurve();
movedCurve.Transform(xform);

// Breps (reference type) -- duplicate first to keep original
Brep movedBrep = brep.DuplicateBrep();
movedBrep.Transform(xform);
```

### Combining Transforms

Multiply transforms to chain them. Order matters (right to left application).

```csharp
// First rotate, then move (multiply in reverse order)
Transform combined = move * rotate;

// Apply the combined transform once
Plane result = new Plane(sourcePlane);
result.Transform(combined);
```

### PlaneToPlane (Most Common for Robotics)

Maps geometry from one coordinate frame to another. Essential for KUKAprc toolpath generation.

```csharp
// Build geometry at origin
Plane local = Plane.WorldXY;
Point3d localPt = new Point3d(10, 0, 5);

// Orient to each target plane
for (int i = 0; i < targetPlanes.Count; i++)
{
  Transform orient = Transform.PlaneToPlane(local, targetPlanes[i]);
  Point3d worldPt = new Point3d(localPt);
  worldPt.Transform(orient);
  result.Add(worldPt);
}
```

### Transform Quick Reference

| Method | Parameters | Use Case |
|---|---|---|
| Translation(Vector3d) | Direction + distance | Move geometry |
| Rotation(angle, axis, center) | Radians, Vector3d, Point3d | Rotate around axis |
| Rotation(from, to, center) | Vector3d, Vector3d, Point3d | Align one direction to another |
| Scale(center, factor) | Point3d, double | Uniform scale |
| Scale(plane, x, y, z) | Plane, 3 doubles | Non-uniform scale |
| Mirror(plane) | Plane | Reflect across plane |
| PlaneToPlane(from, to) | Plane, Plane | Reorient coordinate frame |

---

## 24. DataTree Building and Manipulation

DataTree<T> is Grasshopper's branching data structure. Each branch has a GH_Path (an array of integers) and contains a List<T>. Understanding path construction is critical for correct GH data flow.

### Building a DataTree from Scratch

```csharp
DataTree<Point3d> tree = new DataTree<Point3d>();

for (int i = 0; i < rowCount; i++)
{
  GH_Path path = new GH_Path(i);          // Path: {i}
  List<Point3d> branchData = new List<Point3d>();

  for (int j = 0; j < colCount; j++)
  {
    branchData.Add(new Point3d(i, j, 0));
  }

  tree.AddRange(branchData, path);         // Add entire list to branch
}

out_tree = tree;
```

### Multi-Level Paths

Paths can have multiple indices for nested grouping.

```csharp
DataTree<double> tree = new DataTree<double>();

for (int group = 0; group < groupCount; group++)
{
  for (int item = 0; item < itemCount; item++)
  {
    GH_Path path = new GH_Path(group, item);  // Path: {group;item}
    tree.Add(value, path);                      // Add single value
  }
}
```

### Path Construction Reference

```csharp
// Single-level path
GH_Path p1 = new GH_Path(0);            // {0}
GH_Path p2 = new GH_Path(3);            // {3}

// Multi-level paths
GH_Path p3 = new GH_Path(0, 1);         // {0;1}
GH_Path p4 = new GH_Path(2, 0, 5);      // {2;0;5}

// From existing path + append
GH_Path p5 = new GH_Path(existingPath);
p5 = p5.AppendElement(newIndex);         // Returns new path (immutable)

// From integer array
int[] indices = new int[] { 1, 2, 3 };
GH_Path p6 = new GH_Path(indices);      // {1;2;3}
```

### Safe DataTree Iteration (Rhino 8 Pattern)

NEVER use AllData() on input trees. Always iterate by path:

```csharp
// Pattern 1: By branch index (most common)
for (int i = 0; i < tree.BranchCount; i++)
{
  GH_Path path = tree.Path(i);
  List<object> branch = tree.Branch(i);
  for (int j = 0; j < branch.Count; j++)
  {
    // process branch[j]
  }
}

// Pattern 2: By path (when you need the path object)
for (int i = 0; i < tree.BranchCount; i++)
{
  GH_Path path = tree.Path(i);
  List<object> branch = tree.Branch(path);
  // path.Indices gives int[] of the path components
}
```

### Mirroring Input Tree Structure

When transforming a DataTree, preserve the original path structure:

```csharp
private void RunScript(DataTree<object> inputTree, ref object A)
{
  if (inputTree == null || inputTree.BranchCount == 0) return;

  DataTree<Point3d> outputTree = new DataTree<Point3d>();

  for (int i = 0; i < inputTree.BranchCount; i++)
  {
    GH_Path path = inputTree.Path(i);        // Keep same path
    List<object> branch = inputTree.Branch(i);
    List<Point3d> newBranch = new List<Point3d>();

    for (int j = 0; j < branch.Count; j++)
    {
      GH_Point ghPt = branch[j] as GH_Point;
      if (ghPt != null)
      {
        Point3d pt = ghPt.Value;
        // Transform the point
        newBranch.Add(new Point3d(pt.X * 2, pt.Y * 2, pt.Z));
      }
    }

    outputTree.AddRange(newBranch, path);    // Same path structure
  }

  A = outputTree;
}
```

### DataTree Operations

```csharp
// Merge two trees (branches with same paths are combined)
DataTree<double> combined = new DataTree<double>(treeA);
combined.MergeTree(treeB);

// Flatten (all data into single branch {0})
DataTree<double> flat = new DataTree<double>(tree);
flat.Flatten();

// Get all data as flat list (use ONLY on trees you built yourself, not inputs)
List<double> allData = flat.AllData();

// Check if tree has a specific path
bool exists = tree.PathExists(new GH_Path(0, 1));

// Get branch count and item count
int branches = tree.BranchCount;
int totalItems = tree.DataCount;
```

### DataTree Properties and Methods Reference

| Property/Method | Returns | Notes |
|---|---|---|
| tree.BranchCount | int | Number of branches |
| tree.DataCount | int | Total items across all branches |
| tree.Path(i) | GH_Path | Path of branch at index i |
| tree.Paths | IList<GH_Path> | All paths (for foreach iteration) |
| tree.Branch(i) | List<T> | Data in branch at index i |
| tree.Branch(path) | List<T> | Data at specific path |
| tree.Add(item, path) | void | Add single item to path |
| tree.AddRange(list, path) | void | Add list of items to path |
| tree.PathExists(path) | bool | Check if path exists |
| tree.MergeTree(other) | void | Merge another tree in |
| tree.Flatten() | void | Collapse all branches to {0} |
| tree.Clear() | void | Remove all data |

### Filtering DataTree Branches

```csharp
// Keep only branches that have at least N items
DataTree<double> filtered = new DataTree<double>();

for (int i = 0; i < tree.BranchCount; i++)
{
  GH_Path path = tree.Path(i);
  List<double> branch = tree.Branch(i);
  if (branch.Count >= minItems)
  {
    filtered.AddRange(branch, path);
  }
}
```

---

## 25. KUKAprc Patterns

KUKAprc is the Grasshopper plugin for KUKA robot programming. These patterns cover the most common C# scripting tasks with KUKAprc.

### Reading Axis Values from Analysis Component

The KUKAprc Analysis component outputs "Robot Axis Values" as a DataTree. Structure: one branch per command, 6 values per branch (A1 through A6). Input must be set to Tree access mode.

```csharp
private void RunScript(
  DataTree<object> axisValues,
  ref object A1, ref object A2, ref object A3,
  ref object A4, ref object A5, ref object A6)
{
  if (axisValues == null || axisValues.BranchCount == 0)
  {
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Warning, "No axis values connected");
    return;
  }

  List<double> a1 = new List<double>();
  List<double> a2 = new List<double>();
  List<double> a3 = new List<double>();
  List<double> a4 = new List<double>();
  List<double> a5 = new List<double>();
  List<double> a6 = new List<double>();

  for (int i = 0; i < axisValues.BranchCount; i++)
  {
    List<object> branch = axisValues.Branch(i);
    if (branch.Count < 6)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Warning,
        "Branch " + i.ToString() + " has fewer than 6 values");
      continue;
    }

    double val;
    if (double.TryParse(branch[0].ToString(), out val)) a1.Add(val);
    if (double.TryParse(branch[1].ToString(), out val)) a2.Add(val);
    if (double.TryParse(branch[2].ToString(), out val)) a3.Add(val);
    if (double.TryParse(branch[3].ToString(), out val)) a4.Add(val);
    if (double.TryParse(branch[4].ToString(), out val)) a5.Add(val);
    if (double.TryParse(branch[5].ToString(), out val)) a6.Add(val);
  }

  A1 = a1; A2 = a2; A3 = a3;
  A4 = a4; A5 = a5; A6 = a6;
}
```

### KUKAprc Analysis Component Outputs

| Output | Type | Description |
|---|---|---|
| Robot Axis Values | DataTree (6 per branch) | Joint angles A1-A6 in degrees |
| Collision | List<bool> or DataTree | Collision detected per command |
| Reachability | List<bool> or DataTree | Target reachable per command |
| Planes | List<Plane> | TCP planes per command |
| Time | List<double> | Time in seconds per command |

### Speed List Parallel to Planes

When building KUKAprc toolpaths, the speed list must have exactly the same count as the plane list. Always add speed and plane together:

```csharp
private void RunScript(
  List<Plane> inputPlanes,
  double approachSpeed,
  double workSpeed,
  double retractSpeed,
  ref object planes,
  ref object speeds)
{
  if (inputPlanes == null || inputPlanes.Count == 0) return;

  List<Plane> outPlanes = new List<Plane>();
  List<double> outSpeeds = new List<double>();

  // Approach: first plane at approach speed
  outPlanes.Add(inputPlanes[0]);
  outSpeeds.Add(approachSpeed);

  // Work: all planes at work speed
  for (int i = 0; i < inputPlanes.Count; i++)
  {
    outPlanes.Add(inputPlanes[i]);
    outSpeeds.Add(workSpeed);
  }

  // Retract: last plane lifted, at retract speed
  Plane retractPlane = new Plane(inputPlanes[inputPlanes.Count - 1]);
  retractPlane.Transform(Transform.Translation(Vector3d.ZAxis * 50));
  outPlanes.Add(retractPlane);
  outSpeeds.Add(retractSpeed);

  // CRITICAL: outPlanes.Count must equal outSpeeds.Count
  planes = outPlanes;
  speeds = outSpeeds;
}
```

### External Axes (KUKAprc Pro)

KUKAprc Pro supports external axes (E1-E6) for linear tracks and turntables. External axis values may appear as additional values in the Analysis DataTree (positions 6-11 for E1-E6) or as separate outputs depending on the version.

```csharp
// Reading external axes (if present in Analysis output)
// Check branch.Count > 6 to detect external axes
if (branch.Count >= 12)
{
  // Positions 6-11 are E1-E6
  double e1Val;
  if (double.TryParse(branch[6].ToString(), out e1Val))
  {
    e1.Add(e1Val);
  }
  // ... repeat for E2-E6 at indices 7-11
}
```

NOTE: External axis DataTree structure may vary between KUKAprc versions. Wire a Panel to the Analysis output first to verify the exact structure before writing extraction code.

### Building Toolpath Planes

Common pattern: build planes in a local coordinate frame, then orient to the robot's work position using PlaneToPlane.

```csharp
private void RunScript(
  Curve toolpathCurve,
  Plane workPlane,
  int divisionCount,
  Vector3d toolDirection,
  ref object planes,
  ref object speeds)
{
  if (toolpathCurve == null) return;

  // Divide curve into points
  Point3d[] divPts;
  double[] parameters = toolpathCurve.DivideByCount(divisionCount, true, out divPts);
  if (parameters == null) return;

  List<Plane> outPlanes = new List<Plane>();
  List<double> outSpeeds = new List<double>();

  for (int i = 0; i < divPts.Length; i++)
  {
    // Get tangent for X direction
    Vector3d tangent = toolpathCurve.TangentAt(parameters[i]);
    tangent.Unitize();

    // Build tool plane: Z = tool direction, X = tangent
    Vector3d yAxis = Vector3d.CrossProduct(toolDirection, tangent);
    yAxis.Unitize();
    Vector3d xAxis = Vector3d.CrossProduct(yAxis, toolDirection);
    xAxis.Unitize();

    Plane localPlane = new Plane(divPts[i], xAxis, yAxis);

    // Orient from world to work position
    Transform orient = Transform.PlaneToPlane(Plane.WorldXY, workPlane);
    localPlane.Transform(orient);

    outPlanes.Add(localPlane);
    outSpeeds.Add(100.0);  // mm/s -- always add speed with plane
  }

  planes = outPlanes;
  speeds = outSpeeds;
}
```

### KUKAprc Checklist

Before sending planes to KUKAprc:

1. Plane count == Speed count -- always, no exceptions
2. Z-axis points along tool direction -- KUKAprc uses Z as tool axis
3. No duplicate consecutive planes -- causes zero-distance moves
4. Check reachability first -- wire to Analysis before running on robot
5. Units are millimeters -- Rhino model must be in mm
6. Angles are degrees -- axis values from Analysis are in degrees

---

## 26. Debugging and Runtime Messages

### AddRuntimeMessage

The primary way to communicate errors and warnings from a C# Script component. Messages appear as colored bubbles on the component in the GH canvas. Access via `this.Component` inside SDK-Mode RunScript:

```csharp
// Error (red) -- component stops, downstream receives null
this.Component.AddRuntimeMessage(
  GH_RuntimeMessageLevel.Error, "Input curve is null");

// Warning (orange) -- component runs, output may be partial
this.Component.AddRuntimeMessage(
  GH_RuntimeMessageLevel.Warning, "3 of 10 points were out of range");

// Remark (gray) -- informational, no visual alarm
this.Component.AddRuntimeMessage(
  GH_RuntimeMessageLevel.Remark, "Processing 42 points");
```

### Message Level Guide

| Level | Color | When to Use | Component Behavior |
|---|---|---|---|
| Error | Red | Input missing, impossible operation, unrecoverable | Stops, outputs null |
| Warning | Orange | Partial failure, skipped items, degraded result | Runs, outputs partial |
| Remark | Gray | Statistics, progress info, non-critical notes | Runs normally |

### When to Use Each Level

**Error** -- use when the script CANNOT produce any meaningful output:

```csharp
if (curve == null)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "No curve connected");
  return;
}
```

**Warning** -- use when the script CAN run but some inputs are problematic:

```csharp
int skipped = 0;
for (int i = 0; i < points.Count; i++)
{
  double t;
  if (!curve.ClosestPoint(points[i], out t))
  {
    skipped++;
    continue;
  }
  result.Add(curve.PointAt(t));
}
if (skipped > 0)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning,
    skipped.ToString() + " of " + points.Count.ToString() + " points failed ClosestPoint");
}
```

**Remark** -- use for counts, timing, or diagnostic info:

```csharp
this.Component.AddRuntimeMessage(
  GH_RuntimeMessageLevel.Remark,
  "Processed " + result.Count.ToString() + " items in " + branches.ToString() + " branches");
```

### Print() for Debug Output

Print() writes to the component's special [out] text output parameter. Useful during development; remove or reduce for production scripts.

```csharp
// Simple text
Print("Curve length: " + curve.GetLength().ToString("F2"));

// Formatted (like string.Format)
Print("Point {0}: ({1:F2}, {2:F2}, {3:F2})", i, pt.X, pt.Y, pt.Z);

// Loop diagnostics
for (int i = 0; i < 5 && i < points.Count; i++)
{
  Print("points[" + i.ToString() + "] = " + points[i].ToString());
}
```

### RhinoApp.WriteLine() for Command History

Writes to Rhino's command-line history window. Useful for logging that persists across component updates (Print() gets overwritten each solve).

```csharp
Rhino.RhinoApp.WriteLine("DEBUG: RunScript iteration " + Iteration.ToString());
Rhino.RhinoApp.WriteLine("DEBUG: Tree has " + tree.BranchCount.ToString() + " branches");
```

### Debugging Workflow

When a script produces unexpected results, follow this sequence:

**Step 1 - Check inputs first.** Print input types and counts:

```csharp
Print("Input type: " + (inputObj == null ? "null" : inputObj.GetType().ToString()));
Print("List count: " + myList.Count.ToString());
Print("Tree branches: " + tree.BranchCount.ToString());
```

**Step 2 - Check intermediate values.** Print values at each processing step:

```csharp
Print("After division: " + divPts.Length.ToString() + " points");
Print("After filtering: " + filtered.Count.ToString() + " remain");
```

**Step 3 - Check output assignment.** Verify you are assigning the correct variable:

```csharp
Print("Output list has " + result.Count.ToString() + " items");
out_result = result;  // Make sure this matches
```

**Step 4 - Check DataTree structure.** When working with trees:

```csharp
for (int i = 0; i < Math.Min(3, tree.BranchCount); i++)
{
  GH_Path path = tree.Path(i);
  Print("Branch " + path.ToString() + ": " + tree.Branch(i).Count.ToString() + " items");
}
```

### Timing for Performance

```csharp
System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
sw.Start();

// ... expensive operation ...

sw.Stop();
Print("Operation took " + sw.ElapsedMilliseconds.ToString() + " ms");
```

---

## 27. Runtime Error Prevention

Defensive patterns that prevent crashes and null outputs. Every RunScript should use these guards appropriate to its inputs.

### Null Geometry Guards

```csharp
// Single geometry input
if (curve == null)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "No curve connected");
  return;
}

// List input -- check for null list AND empty list
if (points == null || points.Count == 0)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "No points connected");
  return;
}

// DataTree input
if (tree == null || tree.BranchCount == 0)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "No data in tree");
  return;
}
```

### Numeric Guards

```csharp
// Division by zero
if (Math.Abs(divisor) < 1e-10)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "Divisor is zero");
  return;
}

// Negative or zero where positive required
if (radius <= 0)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "Radius must be positive");
  return;
}

// NaN guard (critical for Galapagos fitness)
if (double.IsNaN(value) || double.IsInfinity(value))
{
  value = 999999.0;  // Safe fallback
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning, "NaN detected, using fallback");
}
```

### Range and Index Guards

```csharp
// Index bounds
if (index < 0 || index >= list.Count)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error,
    "Index " + index.ToString() + " out of range (0-" + (list.Count - 1).ToString() + ")");
  return;
}

// Matching list lengths (e.g., planes and speeds)
if (planes.Count != speeds.Count)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error,
    "Plane count (" + planes.Count.ToString() + ") != speed count (" + speeds.Count.ToString() + ")");
  return;
}

// Minimum list length
if (points.Count < 3)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "Need at least 3 points");
  return;
}
```

### Geometry Validity Checks

```csharp
// Curve validity
if (!curve.IsValid)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning, "Input curve is invalid");
  return;
}

// Brep solidity (required for IsPointInside)
if (!brep.IsSolid)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning, "Brep is not solid");
  return;
}

// Vector length (avoid division by zero in Unitize)
if (vec.Length < 1e-10)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning, "Vector is zero-length");
  return;
}
vec.Unitize();

// Plane validity
if (plane.XAxis.Length < 1e-10 || plane.YAxis.Length < 1e-10)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "Degenerate plane (zero-length axis)");
  return;
}
```

### Safe Type Casting

```csharp
// Safe cast with null check (preferred over direct cast)
Curve crv = obj as Curve;
if (crv == null)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning,
    "Item " + i.ToString() + " is not a Curve (type: " + obj.GetType().Name + ")");
  continue;
}

// For GH wrapper types from DataTree<object>
GH_Point ghPt = branch[j] as GH_Point;
if (ghPt == null)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning,
    "Branch " + i.ToString() + " item " + j.ToString() + " is not a Point");
  continue;
}
Point3d pt = ghPt.Value;
```

### Method Return Value Checks

Many RhinoCommon methods return null or false on failure. Always check before using the result.

```csharp
// Curve.ClosestPoint
double t;
if (!curve.ClosestPoint(testPoint, out t))
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning, "ClosestPoint failed for point " + i.ToString());
  continue;
}

// Curve.DivideByCount
Point3d[] divPts;
double[] parameters = curve.DivideByCount(count, true, out divPts);
if (parameters == null || divPts == null)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Error, "Curve division failed");
  return;
}

// Curve.Offset
Curve[] offsetResult = curve.Offset(plane, dist, tol, cornerStyle);
if (offsetResult == null || offsetResult.Length == 0)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Warning, "Curve offset produced no result");
  return;
}

// Intersection.CurveCurve
CurveIntersections events = Intersection.CurveCurve(crvA, crvB, 0.001, 0.0);
if (events == null || events.Count == 0)
{
  this.Component.AddRuntimeMessage(
    GH_RuntimeMessageLevel.Remark, "No intersections found");
}
```

### Complete Guard Pattern Template

Standard opening for any RunScript with common input types:

```csharp
private void RunScript(
  Curve curve, List<Point3d> points, double value,
  ref object out_result, ref object out_count)
{
  // Defaults (prevent null outputs on early return)
  out_result = new List<Point3d>();
  out_count = 0;

  // Null guards
  if (curve == null)
  {
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Error, "No curve connected");
    return;
  }
  if (points == null || points.Count == 0)
  {
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Error, "No points connected");
    return;
  }
  if (!curve.IsValid)
  {
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Warning, "Curve is invalid");
    return;
  }

  // Processing
  List<Point3d> result = new List<Point3d>();
  int skipped = 0;

  for (int i = 0; i < points.Count; i++)
  {
    double t;
    if (!curve.ClosestPoint(points[i], out t))
    {
      skipped++;
      continue;
    }
    result.Add(curve.PointAt(t));
  }

  if (skipped > 0)
  {
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Warning,
      skipped.ToString() + " points failed");
  }

  // Output assignment
  out_result = result;
  out_count = result.Count;
}
```

---

## 28. Parallel Processing

Use `System.Threading.Tasks.Parallel.For` to speed up independent operations on large collections. Works inside the C# Script component on Rhino 8 (.NET 7+).

### When to Use Parallel Processing

Use `Parallel.For` when:
- Processing 100+ items independently
- Each item does significant work (geometry ops, not simple math)
- Order of results does not matter OR you pre-allocate by index

Do NOT use when:
- Items depend on each other
- Collection is small (<50 items)
- You need to modify shared state without thread-safe collections

### Pattern 1: Pre-Allocated Array (Ordered Results)

Safest pattern. Each thread writes to its own index.

```csharp
using System.Threading.Tasks;

// Pre-allocate result array with same size as input
Point3d[] results = new Point3d[points.Count];

Parallel.For(0, points.Count, i =>
{
  // Each thread gets its own index -- no shared writes
  double t;
  curve.ClosestPoint(points[i], out t);
  results[i] = curve.PointAt(t);
});

// Convert to list for GH output
out_points = new List<Point3d>(results);
```

### Pattern 2: ConcurrentBag (Unordered Results)

Use when result order does not matter.

```csharp
using System.Threading.Tasks;
using System.Collections.Concurrent;

ConcurrentBag<Point3d> bag = new ConcurrentBag<Point3d>();

Parallel.For(0, points.Count, i =>
{
  double t;
  if (curve.ClosestPoint(points[i], out t))
  {
    bag.Add(curve.PointAt(t));
  }
  // Items that fail ClosestPoint are simply not added
});

out_points = bag.ToList();
// WARNING: bag.ToList() order is NOT the same as input order
```

### Pattern 3: ConcurrentDictionary (Keyed Results)

Use when you need to track which input produced which output.

```csharp
using System.Threading.Tasks;
using System.Collections.Concurrent;

ConcurrentDictionary<int, double> distances = new ConcurrentDictionary<int, double>();

Parallel.For(0, points.Count, i =>
{
  double t;
  if (curve.ClosestPoint(points[i], out t))
  {
    Point3d closest = curve.PointAt(t);
    double dist = points[i].DistanceTo(closest);
    distances.TryAdd(i, dist);
  }
});

// Reconstruct ordered list
List<double> result = new List<double>();
for (int i = 0; i < points.Count; i++)
{
  double val;
  if (distances.TryGetValue(i, out val))
  {
    result.Add(val);
  }
  else
  {
    result.Add(-1.0);  // Sentinel for failed items
  }
}
out_distances = result;
```

### Thread Safety Rules

1. RhinoCommon geometry is thread-safe for READS. You can call `curve.ClosestPoint()`, `brep.IsPointInside()`, etc. from multiple threads.
2. Do NOT create new Rhino document objects in parallel threads. `RhinoDoc.ActiveDoc` is not thread-safe.
3. Do NOT use standard `List<T>` for writes in parallel. Use `ConcurrentBag<T>`, `ConcurrentDictionary<K,V>`, or pre-allocated arrays.
4. Do NOT use `DataTree<T>` inside `Parallel.For`. DataTree is not thread-safe. Build the tree after parallel processing.
5. Input parameters must be List Access. `Parallel.For` needs the full list available inside RunScript.

### Building DataTree After Parallel Processing

```csharp
// Step 1: Parallel compute (array-based)
Point3d[] closestPts = new Point3d[points.Count];
double[] distances = new double[points.Count];

Parallel.For(0, points.Count, i =>
{
  double t;
  curve.ClosestPoint(points[i], out t);
  closestPts[i] = curve.PointAt(t);
  distances[i] = points[i].DistanceTo(closestPts[i]);
});

// Step 2: Build DataTree sequentially (NOT thread-safe)
DataTree<Point3d> ptTree = new DataTree<Point3d>();
DataTree<double> distTree = new DataTree<double>();

for (int i = 0; i < points.Count; i++)
{
  GH_Path path = new GH_Path(i);
  ptTree.Add(closestPts[i], path);
  distTree.Add(distances[i], path);
}

out_points = ptTree;
out_distances = distTree;
```

### Required Namespaces

```csharp
using System.Threading.Tasks;               // Parallel.For, Parallel.ForEach
using System.Collections.Concurrent;         // ConcurrentBag, ConcurrentDictionary
```

### Performance Notes

`Parallel.For` has startup overhead (~1-2ms). Not worth it for <50 items. Best speedup: geometry queries on large point sets (ClosestPoint, IsPointInside). The Script component may run slower than a compiled .gha for `Parallel.For` due to JIT overhead, but still faster than serial for large sets. Profile with Stopwatch (see Section 26) before and after parallelization.

---

## 29. Template -- Geometry Processing

Copy-paste starting point for scripts that process curves, surfaces, or breps. Includes null guards, curve operations, and intersection handling.

Standalone file: `templates/geometry_processing.cs`

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    Curve curve,
    List<Point3d> points,
    double tolerance,
    ref object out_points,
    ref object out_distances,
    ref object out_count)
  {
    // Defaults
    out_points = new List<Point3d>();
    out_distances = new List<double>();
    out_count = 0;

    // Guards
    if (curve == null)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No curve connected");
      return;
    }
    if (points == null || points.Count == 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No points connected");
      return;
    }
    if (tolerance <= 0) tolerance = 0.01;

    // Processing
    List<Point3d> resultPts = new List<Point3d>();
    List<double> resultDist = new List<double>();
    int skipped = 0;

    for (int i = 0; i < points.Count; i++)
    {
      double t;
      if (!curve.ClosestPoint(points[i], out t))
      {
        skipped++;
        continue;
      }
      Point3d closestPt = curve.PointAt(t);
      double dist = points[i].DistanceTo(closestPt);
      resultPts.Add(closestPt);
      resultDist.Add(dist);
    }

    if (skipped > 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Warning,
        skipped.ToString() + " points failed ClosestPoint");
    }

    // Output
    out_points = resultPts;
    out_distances = resultDist;
    out_count = resultPts.Count;
  }
}
```

Inputs to configure in GH: curve (Item), points (List), tolerance (Item, default 0.01).
Outputs: out_points, out_distances, out_count.

---

## 30. Template -- KUKAprc Toolpath

Copy-paste starting point for scripts that generate robot toolpaths. Outputs plane list and matching speed list for KUKAprc.

Standalone file: `templates/kukaprc_toolpath.cs`

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
```

Inputs: toolpathCurve (Item), workPlane (Item), divisions (Item, int), approachHeight (Item, 50), approachSpeed (Item, 200), workSpeed (Item, 100).
Outputs: planes, speeds.
KUKAprc wiring: Connect planes to KUKA|prc Core Movements input, speeds to Speed input.

---

## 31. Template -- DataTree Processing

Copy-paste starting point for scripts that read, transform, and output DataTrees. Preserves input tree structure in output.

Standalone file: `templates/datatree_processing.cs`

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
    DataTree<object> inputTree,
    double factor,
    ref object out_tree,
    ref object out_counts)
  {
    // Defaults
    out_tree = new DataTree<double>();
    out_counts = new List<int>();

    // Guards
    if (inputTree == null || inputTree.BranchCount == 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "No data in input tree");
      return;
    }

    DataTree<double> resultTree = new DataTree<double>();
    List<int> branchCounts = new List<int>();

    // Process each branch
    for (int i = 0; i < inputTree.BranchCount; i++)
    {
      GH_Path path = inputTree.Path(i);
      List<object> branch = inputTree.Branch(i);
      List<double> newBranch = new List<double>();

      for (int j = 0; j < branch.Count; j++)
      {
        double val;
        if (double.TryParse(branch[j].ToString(), out val))
        {
          newBranch.Add(val * factor);
        }
        else
        {
          this.Component.AddRuntimeMessage(
            GH_RuntimeMessageLevel.Warning,
            "Cannot parse value at branch " + i.ToString() + " index " + j.ToString());
        }
      }

      resultTree.AddRange(newBranch, path);  // Preserve original path
      branchCounts.Add(newBranch.Count);
    }

    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      "Processed " + inputTree.BranchCount.ToString() + " branches, "
      + resultTree.DataCount.ToString() + " total items");

    // Output
    out_tree = resultTree;
    out_counts = branchCounts;
  }
}
```

Inputs: inputTree (Tree access -- right-click to set), factor (Item, default 1.0).
Outputs: out_tree, out_counts.
Important: The inputTree parameter MUST be set to Tree access mode in GH.

---

## 32. Template -- Galapagos Fitness

Copy-paste starting point for Galapagos optimization fitness functions. Must output a single number. NaN protection is critical.

Standalone file: `templates/galapagos_fitness.cs`

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
    double gene1,
    double gene2,
    int geneInt,
    ref object fitness)
  {
    // Default (high penalty = bad)
    fitness = 999999.0;

    // Map integer gene to binary choice
    double angle = (geneInt == 0) ? -180.0 : 180.0;

    // Compute fitness components
    double distanceScore = Math.Abs(gene1 - 50.0);    // Minimize distance from target
    double angleScore = Math.Abs(gene2 - angle);       // Minimize angle error

    // Weighted combination (lower is better for minimization)
    double total = distanceScore * 100.0 + angleScore * 50.0;

    // CRITICAL: NaN guard -- Galapagos crashes on NaN
    if (double.IsNaN(total) || double.IsInfinity(total))
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Warning, "NaN detected in fitness");
      total = 999999.0;
    }

    fitness = total;
  }
}
```

Inputs: gene1 (Number Slider), gene2 (Number Slider), geneInt (Integer Slider 0-1).
Outputs: fitness (single number).

### Galapagos Setup Checklist

1. Genome: Connect Number Sliders or Integer Sliders DIRECTLY to inputs -- do not use intermediate components or Remote Receivers
2. Fitness: Connect the single fitness output to Galapagos fitness input
3. Direction: Set Galapagos to "Minimize" if lower fitness = better
4. NaN guard: Always include -- even if your formula "cannot" produce NaN, edge cases in geometry operations can
5. Integer genes for binary choices: Use Integer Slider 0-1, map in script
6. Penalty values: Use a large number (999999) not double.MaxValue (MaxValue can cause overflow in Galapagos internals)
