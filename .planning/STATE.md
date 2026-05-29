---
gsd_state_version: 1.0
milestone: v2.0
milestone_name: Geometric Stone Panel Fabrication Workflow
status: unknown
last_updated: "2026-04-11T22:53:26.736Z"
progress:
  total_phases: 7
  completed_phases: 4
  total_plans: 12
  completed_plans: 12
---

# STATE.md — GH C# Editor Toolkit

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-23)

**Core value:** Every C# script Claude writes runs correctly in Rhino 8 Grasshopper on the first try
**Current focus:** Phase 04 — Panel Volume Generator

## Current Phase

Phase 4: Panel Volume Generator — Plan 01 complete (panel_volume_generator.cs written).

## Completed Phases

- Phase 1: Knowledge Base -- 32 sections covering KB-01 through KB-08
- Phase 2: CLAUDE.md and GSD Integration -- auto-load rules, output format, GSD commands
- Phase 3: Component Templates -- 4 standalone templates with cross-references

## Decisions

- Replaced all non-ASCII characters in knowledge base with ASCII equivalents for strict compiler compliance
- Listed prohibited characters by description rather than showing actual non-ASCII chars
- All intersection examples show full IntersectionEvent iteration with IsOverlap check, not simplified point extraction
- Transform section distinguishes value types from reference types for Transform application
- DataTree iteration examples use for-loop with BranchCount, never AllData() on inputs
- KUKAprc external axes documented as version-dependent with Panel verification recommendation
- AllData() usage restricted to self-built trees only, with explicit warning comment
- Used this.Component.AddRuntimeMessage (SDK-Mode) consistently across all debugging examples
- Structured guard template with defaults-before-guards pattern to prevent null downstream outputs
- Three distinct parallel patterns (array, ConcurrentBag, ConcurrentDictionary) for different result-ordering needs
- All category templates use SDK-Mode (Script_Instance : GH_ScriptInstance) for consistency
- Galapagos penalty uses 999999 not double.MaxValue to prevent overflow in Galapagos internals
- [Phase 02]: Used explicit Read tool instruction instead of @import to avoid inlining 2348 lines into every session
- [Phase 02]: Wrapped Output Format in <important if> block for stronger C# code generation adherence
- [Phase 02]: Included 10-rule critical summary as fallback safety net if Claude skips reading the full knowledge base
- [Phase 02]: Used concise code-block format for GSD command reference with Grasshopper-specific examples
- [Phase 02]: Read-only verification confirms all CL requirements met -- no fixes needed
- [Phase 03]: Extracted templates verbatim from learnings.md sections 29-30 -- no modifications
- [Phase 03]: Extracted DataTree and Galapagos templates verbatim from learnings.md sections 31-32 with added header comments
- [Phase 03]: Cross-references use 'Standalone file:' pattern linking knowledge base sections 29-32 to templates/*.cs files
- [Phase 04-01]: Largest-area BrepFace used as outer-face proxy -- always the original polygon, smaller than extrusion side caps
- [Phase 04-01]: Stone centroid approximated as average of face centroids -- sufficient for outward-normal flip without full mesh input
- [Phase 04-01]: PolyCentroid helper uses 1e-6 hardcoded tolerance rather than doc tolerance to avoid RhinoDoc dependency in helper

## Performance Metrics

| Phase | Plan | Duration | Tasks | Files |
|-------|------|----------|-------|-------|
| 01 | 01 | 3min | 2 | 1 |
| 01 | 02 | 3min | 2 | 1 |
| 01 | 03 | 2min | 2 | 1 |
| 01 | 04 | 2min | 2 | 1 |
| 01 | 05 | 2min | 2 | 1 |
| 02 | 01 | 1min | 1 | 1 |
| 02 | 02 | 1min | 1 | 1 |
| 02 | 03 | 1min | 1 | 0 |
| 03 | 01 | 2min | 2 | 2 |
| 03 | 02 | 2min | 2 | 2 |
| 03 | 03 | 1min | 1 | 1 |
| 04 | 01 | 4min | 1 | 1 |

## Last Action

Completed 04-01-PLAN.md (panel_volume_generator.cs) -- 2026-04-11T23:00:00Z
Phase 4 Plan 01 complete. templates/panel_volume_generator.cs written.
