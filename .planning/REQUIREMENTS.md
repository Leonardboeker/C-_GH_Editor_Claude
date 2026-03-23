# Requirements: GH C# Editor Toolkit

**Defined:** 2026-03-23
**Core Value:** Every C# script Claude writes runs correctly in Rhino 8 Grasshopper on the first try

## v1 Requirements

### Knowledge Base

- [x] **KB-01**: `grasshopper_csharp_learnings.md` covers all critical compiler rules (ASCII-only, no `out var`, no While loops)
- [x] **KB-02**: Full DataTree iteration patterns for Rhino 8 (path-based, not `AllData()`)
- [x] **KB-03**: Geometry operation patterns: curves, surfaces, breps, intersections, transforms
- [x] **KB-04**: KUKAprc-specific patterns: reading axis values from DataTree, speed lists, external axes
- [x] **KB-05**: Input/output patterns: null guards, ref object outputs, access mode table
- [x] **KB-06**: Debugging patterns: `AddRuntimeMessage`, `Print`, runtime error prevention
- [x] **KB-07**: Parallel processing patterns with `System.Threading.Tasks.Parallel.For`
- [x] **KB-08**: Complete minimal template and category-specific templates embedded in the doc

### Auto-Load Rules (CLAUDE.md)

- [x] **CL-01**: `CLAUDE.md` in project root instructs Claude to read `grasshopper_csharp_learnings.md` before writing any C# code
- [x] **CL-02**: `CLAUDE.md` specifies output format: full class body only (no wrapper), English comments only
- [x] **CL-03**: `CLAUDE.md` instructs use of `/gsd:` workflow for any scripting task

### Component Templates

- [ ] **TPL-01**: Geometry processing template (curve ops, surface eval, brep intersection)
- [ ] **TPL-02**: KUKAprc toolpath template (plane list output, speed list, DataTree axis input)
- [ ] **TPL-03**: DataTree processing template (path iteration, branching output, filtering)
- [ ] **TPL-04**: Galapagos fitness template (NaN guard, single number output)

## v2 Requirements

### Extended Knowledge

- **KB-09**: RobotComponents library patterns (alternative to KUKAprc)
- **KB-10**: Anemone / loop component integration patterns
- **KB-11**: Human UI integration patterns

### Snippet Library

- **SNP-01**: Saved, tested script snippets organized by category
- **SNP-02**: Quick-copy format for pasting directly into GH Script editor

## Out of Scope

| Feature | Reason |
|---------|--------|
| .gha plugin development | Requires separate VS project, out of scope for inline scripting |
| Python scripts | C# only — Python has GH_Plane wrapper issues and is slower |
| Rhino 7 legacy | New Script component (Rhino 8) is the target |
| Web/API scripts | Desktop geometry tooling only |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| KB-01 | Phase 1 | Complete (01-01) |
| KB-02-KB-04 | Phase 1 | Pending |
| KB-05 | Phase 1 | Complete (01-01) |
| KB-06–KB-08 | Phase 1 | Pending |
| CL-01–CL-03 | Phase 2 | Pending |
| TPL-01–TPL-04 | Phase 3 | Pending |

**Coverage:**
- v1 requirements: 16 total
- Mapped to phases: 16
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-23*
*Last updated: 2026-03-23 after plan 01-01 completion*
