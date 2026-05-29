# Roadmap: GH C# Editor Toolkit

## Overview

Build a comprehensive knowledge base, auto-load rules, and component templates so that every C# script Claude writes for Rhino 8 Grasshopper works correctly on the first run — no debugging, no crashes, no re-runs.

## Phases

- [x] **Phase 1: Knowledge Base** - Expand grasshopper_csharp_learnings.md with all patterns needed for working Rhino 8 C# scripts (completed 2026-03-23)
- [x] **Phase 2: CLAUDE.md and GSD Integration** - Auto-load rules each session and integrate GSD workflow for scripting tasks (completed 2026-03-23)
- [x] **Phase 3: Component Templates** - Create ready-to-paste templates for geometry, KUKAprc, DataTree, and Galapagos components (completed 2026-03-23)

## Phase Details

### Phase 1: Knowledge Base
**Goal**: Produce a comprehensive `grasshopper_csharp_learnings.md` covering every pattern Claude needs to write working Rhino 8 C# scripts first try — compiler rules, geometry ops, KUKAprc patterns, DataTree iteration, debugging, and performance.
**Depends on**: Nothing (first phase)
**Requirements**: KB-01, KB-02, KB-03, KB-04, KB-05, KB-06, KB-07, KB-08
**Success Criteria** (what must be TRUE):
  1. `grasshopper_csharp_learnings.md` covers all 8 KB requirements including compiler rules, geometry, KUKAprc, and DataTree patterns
  2. Every code example is syntactically valid for Rhino 8 C# Script component (no `out var`, no While loops, ASCII only)
  3. All templates are copy-paste ready and include null guards and correct output assignment
**Plans**: 5 plans

Plans:
- [x] 01-01: Expand compiler and Rhino 8 script component rules
- [x] 01-02: Add geometry operation patterns (curves, surfaces, breps, intersections, vectors)
- [x] 01-03: Add KUKAprc and DataTree patterns (axis input, speed lists, path iteration)
- [x] 01-04: Add debugging and error handling patterns (AddRuntimeMessage, runtime guards)
- [x] 01-05: Add performance patterns and embed all component templates

### Phase 2: CLAUDE.md and GSD Integration
**Goal**: Auto-load the knowledge base rules every session and document the GSD workflow for managing scripting tasks.
**Depends on**: Phase 1
**Requirements**: CL-01, CL-02, CL-03
**Success Criteria** (what must be TRUE):
  1. Opening this folder in Claude Code auto-loads all C# scripting rules via CLAUDE.md
  2. CLAUDE.md specifies correct output format (full class body, English comments only)
  3. GSD commands are documented for scripting task management
**Plans**: 3 plans

Plans:
- [x] 02-01-PLAN.md — Create CLAUDE.md with auto-load rules (CL-01) and output format (CL-02)
- [x] 02-02-PLAN.md — Append GSD workflow guidance section to CLAUDE.md (CL-03)
- [x] 02-03-PLAN.md — Verify file placement and CLAUDE.md completeness (all CL requirements)

### Phase 3: Component Templates
**Goal**: Create standalone, ready-to-paste component templates for the four main use cases (geometry, KUKAprc, DataTree, Galapagos), extracted from the knowledge base into the templates/ folder, with cross-references back to grasshopper_csharp_learnings.md.
**Depends on**: Phase 2
**Requirements**: TPL-01, TPL-02, TPL-03, TPL-04
**Success Criteria** (what must be TRUE):
  1. Each template compiles in Rhino 8 C# Script component without modification
  2. Templates include English-only comments and correct parameter structure
  3. Templates are referenced from grasshopper_csharp_learnings.md
**Plans**: 3 plans

Plans:
- [x] 03-01-PLAN.md — Create geometry processing + KUKAprc toolpath templates (TPL-01, TPL-02)
- [x] 03-02-PLAN.md — Create DataTree processing + Galapagos fitness templates (TPL-03, TPL-04)
- [x] 03-03-PLAN.md — Add cross-reference links from knowledge base to template files

---

## Milestone v2.0: Geometric Stone Panel Fabrication Workflow

Building a complete GH scripting pipeline from a drawn polyhedron to production-ready `.hop` CNC files for 19mm Spanplatten, using the DYNESTIC Post-Processor plugin.

**Depends on:** Phase 3 (miter_angle_calculator.cs template exists)

---

### Phase 4: Panel Volume Generator
**Goal:** For each face polyline of the stone, extrude a correct 19mm Brep solid in the outward normal direction and identify the outer face (largest face) for downstream use.
**Depends on:** Phase 3 (uses the same face polylines as miter_angle_calculator)
**Requirements:** PNL-01, PNL-02
**Success Criteria:**
  1. Each input polyline produces one closed Brep solid extruded 19mm outward
  2. The outer face (original polyline face, largest flat surface) is identified and output as a plane + boundary curve per panel
  3. Script follows all grasshopper_csharp_learnings.md rules — compiles and runs first try

Plans:
- [ ] 04-01: Write panel_volume_generator.cs template

### Phase 5: Panel Labeler
**Goal:** Assign sequential names (prefix + number), gradient colors, and embedded metadata to a list of panel Breps. Display label text at the center of each outer face.
**Depends on:** Phase 4
**Requirements:** LBL-01, LBL-02, LBL-03, LBL-04
**Success Criteria:**
  1. Each panel gets a name (e.g. "P_01") stored in brep.UserDictionary["PartName"]
  2. Gradient color is assigned per panel (index-based interpolation across input color list)
  3. Label position = centroid of the largest face of each panel (outer face)
  4. Output includes: named/colored Breps, label points, label strings, raw names list

Plans:
- [ ] 05-01: Write panel_labeler.cs template

### Phase 6: Grid Layout
**Goal:** Place all panels flat on the XY plane in a grid, outer face facing DOWN (towards -Z), so each panel is ready for CNC machining orientation.
**Depends on:** Phase 5
**Requirements:** GRD-01, GRD-02
**Success Criteria:**
  1. Each panel is oriented so its outer face (largest face) faces -Z (towards the CNC table)
  2. Panels are placed in a column-configurable grid with user-defined spacing
  3. Output includes transformed Breps AND flat outline curves (outer face boundary at Z=0) for HopPart

Plans:
- [ ] 06-01: Write panel_grid_layout.cs template

### Phase 7: DYNESTIC Export Bridge
**Goal:** For a single selected panel, extract all data needed to wire directly into the DYNESTIC Post-Processor plugin components (HopPart → HopPartExport → .hop file).
**Depends on:** Phase 6
**Requirements:** HOP-01, HOP-02
**Success Criteria:**
  1. Script outputs the outer-face boundary curve (closed, at Z=0) ready for HopPart `outline` input
  2. Script outputs dx, dy, dz (from bounding box) and partName (from UserDictionary) for HopExport header
  3. Script can select a panel by index from the grid layout list

Plans:
- [ ] 07-01: Write panel_to_hop_bridge.cs template

---
*Roadmap created: 2026-03-23*
*v2.0 milestone added: 2026-04-11*
