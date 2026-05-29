---
phase: 04-panel-volume
verified: 2026-04-11T00:00:00Z
status: passed
score: 10/10 must-haves verified
gaps: []
---

# Phase 4: Panel Volume Generator — Verification Report

**Phase Goal:** For each face polyline of a geometric stone shape, extrude a correct 19mm Brep solid in the outward normal direction and identify the outer face (largest face) for downstream use.
**Verified:** 2026-04-11
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| #  | Truth                                                                 | Status     | Evidence                                   |
|----|-----------------------------------------------------------------------|------------|--------------------------------------------|
| 1  | File `panel_volume_generator.cs` exists in templates/                | VERIFIED   | File read, 173 lines, non-empty            |
| 2  | Accepts `List<Polyline> faces` and `double thickness` inputs          | VERIFIED   | RunScript signature lines 37-38            |
| 3  | Uses `Brep.CreateFromExtrusion` to produce one Brep per face          | VERIFIED   | Line 115                                   |
| 4  | Outward normal via cross product + centroid flip check                | VERIFIED   | Lines 95-110: CrossProduct + dot-product flip |
| 5  | Uses `AreaMassProperties.Compute` to find largest face                | VERIFIED   | Lines 128-134 (loop), line 154 (centroid)  |
| 6  | Outputs `outerPlanes` (List<Plane>) and `outerCurves` (List<Curve>)  | VERIFIED   | Lines 6-7 header, 41-42 signature, 159-160 |
| 7  | Outputs `outerCentroids` (List<Point3d>) for downstream labeling     | VERIFIED   | Lines 7, 43, 161, 169                      |
| 8  | No non-ASCII characters                                               | VERIFIED   | Python byte scan: 0 bytes > 127            |
| 9  | No `out var` usage (pre-declared out variables only)                  | VERIFIED   | Line 147: `Plane outerFacePlane; bool planeOk = outerFace.TryGetPlane(out outerFacePlane);` — no `out var` pattern found |
| 10 | No LINQ, no while loops                                               | VERIFIED   | No matches for `while(`, `.Where(`, `.Select(`, `using System.Linq` |

**Score:** 10/10 truths verified

---

### Required Artifacts

| Artifact                                    | Expected                              | Status     | Details                          |
|---------------------------------------------|---------------------------------------|------------|----------------------------------|
| `templates/panel_volume_generator.cs`       | Complete GH C# Script_Instance class  | VERIFIED   | 173 lines, fully implemented     |

---

### Key Link Verification

| From                         | To                              | Via                                         | Status  | Details                               |
|------------------------------|---------------------------------|---------------------------------------------|---------|---------------------------------------|
| `faces` input                | per-face Brep extrusion         | `Brep.CreateFromExtrusion` line 115         | WIRED   | Profile curve built from poly line 114 |
| face normal computation      | outward direction               | CrossProduct + centroid dot-product flip 95-110 | WIRED | flip guard at line 107               |
| extruded Brep faces          | largest face identification     | `AreaMassProperties.Compute` loop 126-135   | WIRED   | index tracked, BrepFace extracted 143  |
| `outerFace`                  | `outerPlanes` output            | `TryGetPlane` line 147                      | WIRED   | fallback to WorldXY if fails          |
| `outerFace`                  | `outerCurves` output            | `OuterLoop.To3dCurve()` line 151            | WIRED   | boundary curve extracted              |
| `outerFace`                  | `outerCentroids` output         | `AreaMassProperties.Compute` line 154       | WIRED   | centroid extracted, fallback to faceCentroid |

---

### Requirements Coverage

| Requirement | Description                                                                              | Status     | Evidence                                              |
|-------------|------------------------------------------------------------------------------------------|------------|-------------------------------------------------------|
| PNL-01      | Panel volume generator accepts face polylines + thickness, outputs one closed 19mm Brep per face | SATISFIED  | RunScript signature + Brep.CreateFromExtrusion + MakeValidForV2 |
| PNL-02      | Outer face identified per panel, output as plane + boundary curve                        | SATISFIED  | AreaMassProperties loop + TryGetPlane + OuterLoop.To3dCurve + centroid output |

---

### Anti-Patterns Found

None. Scan results:
- No TODO/FIXME/PLACEHOLDER comments
- No `return null` / `return {}` stubs (null returns are only in guard-continue paths, not final output)
- No LINQ usage
- No while loops
- No `out var`
- No non-ASCII characters
- All 5 outputs assigned from populated lists after the per-face loop (lines 166-170)

---

### Human Verification Required

#### 1. Outward Normal Correctness in Practice

**Test:** Connect a convex stone shape (e.g. cube or irregular hexahedron) with known face polylines into the component. Inspect extrusion direction visually in Rhino viewport.
**Expected:** All Brep panels extrude away from the stone interior; no panel penetrates the stone body.
**Why human:** The cross-product + centroid-flip logic is geometrically correct, but edge-case polyline winding (CW vs CCW depending on view) can silently produce inward extrusions that only a visual check catches.

#### 2. Largest-Face == Original Polyline Face

**Test:** Use a non-square polygon face (e.g. a narrow triangle) where side faces of the extrusion could approach the face area. Verify `outerCurves` matches the input polyline boundary, not a side wall.
**Expected:** The boundary curve returned matches the original face polyline shape.
**Why human:** For thin/elongated polygons, a tall side face could exceed the polygon face area, causing the wrong face to be identified as "outer." Requires hands-on geometry testing.

---

### Gaps Summary

No gaps. All 10 must-haves verified against actual file content. Both PNL-01 and PNL-02 are satisfied. Two items are flagged for human verification as correctness checks under edge-case geometry — these do not block the phase but should be confirmed before downstream phases (labeler, grid layout) depend on the outer-face outputs.

---

_Verified: 2026-04-11_
_Verifier: Claude (gsd-verifier)_
