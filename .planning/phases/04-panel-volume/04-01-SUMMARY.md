---
phase: 04-panel-volume
plan: "01"
subsystem: templates
tags: [grasshopper, csharp, brep, extrusion, panel-fabrication]
dependency_graph:
  requires: []
  provides: [templates/panel_volume_generator.cs]
  affects: [phase-05-labeler, phase-06-grid-layout]
tech_stack:
  added: []
  patterns: [Guard-Default-Work-Output, PolyCentroid helper, Brep.CreateFromExtrusion, AreaMassProperties.Compute, BrepFace.TryGetPlane, OuterLoop.To3dCurve]
key_files:
  created:
    - templates/panel_volume_generator.cs
  modified: []
decisions:
  - Largest-area face used to identify the original polyline face (outer face) -- extrusion side faces and end cap are always smaller than the input polygon face
  - Stone centroid approximated as average of all face centroids -- sufficient for outward-normal flip logic without requiring full mesh or Brep input
  - PolyCentroid helper uses 1e-6 tolerance (hardcoded) rather than doc tolerance -- consistent with plan spec and avoids RhinoDoc dependency in helper
  - extruded.MakeValidForV2() called to ensure Brep is a valid closed solid before face analysis
metrics:
  duration: 4min
  completed: "2026-04-11"
  tasks_completed: 1
  files_created: 1
requirements_satisfied: [PNL-01, PNL-02]
---

# Phase 4 Plan 01: Panel Volume Generator Summary

**One-liner:** Closed 19mm Brep extrusion per face polyline with outward-normal flip logic and largest-area outer-face identification via AreaMassProperties.

## What Was Built

`templates/panel_volume_generator.cs` -- a complete Grasshopper C# `Script_Instance` that:

1. Takes a list of closed planar polylines (stone face outlines) and a thickness value (default 19mm)
2. Approximates the stone centroid as the average of all face centroids
3. For each face: computes a face normal via cross product of the first two edges, flips it to point away from the stone centroid if needed, then extrudes with `Brep.CreateFromExtrusion`
4. Identifies the outer face (original polyline face) as the Brep face with the largest area using `AreaMassProperties.Compute`
5. Extracts the outer face plane (`BrepFace.TryGetPlane`), boundary curve (`OuterLoop.To3dCurve`), and centroid
6. Outputs: `panels` (List<Brep>), `outerPlanes` (List<Plane>), `outerCurves` (List<Curve>), `outerCentroids` (List<Point3d>), `report` (List<string>)

## Tasks

| Task | Name | Status | Files |
|------|------|--------|-------|
| 1 | Write panel_volume_generator.cs | Complete | templates/panel_volume_generator.cs |

## Verification Results

| Check | Result |
|-------|--------|
| File exists and non-empty | PASS |
| No non-ASCII characters | PASS |
| No `out var` in code | PASS |
| No `while` loops | PASS |
| `Brep.CreateFromExtrusion` present | PASS (line 115) |
| `AreaMassProperties.Compute` present (x2) | PASS (lines 128, 154) |
| `TryGetPlane` present | PASS (line 147) |
| `OuterLoop.To3dCurve` present | PASS (line 151) |
| `AddRuntimeMessage` present | PASS (line 55) |
| Defaults before guards | PASS (lines 46-50) |
| Result lists declared before loop | PASS (lines 78-82) |
| All 5 outputs assigned after loop | PASS (lines 166-170) |

## Deviations from Plan

None -- plan executed exactly as written.

## Known Stubs

None -- all outputs are wired and populated per panel.

## Self-Check: PASSED

- `D:\Projekte\SynologyDrive\55_C#_GH_Editor\templates\panel_volume_generator.cs` exists and is non-empty
- All acceptance criteria from 04-01-PLAN.md verified via grep checks above
