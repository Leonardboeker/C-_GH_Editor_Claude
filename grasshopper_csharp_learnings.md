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
