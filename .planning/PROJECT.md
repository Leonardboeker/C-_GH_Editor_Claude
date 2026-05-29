# GH C# Editor — Rhino/Grasshopper Scripting Toolkit

## What This Is

A local reference toolkit that ensures every Rhino/Grasshopper C# script written by Claude Code works correctly on the first run. It consists of a comprehensive knowledge base of rules and patterns, a CLAUDE.md that auto-loads those rules each session, and reusable component templates for the three main use cases: geometry processing, KUKAprc toolpaths, and DataTree/list operations.

## Core Value

Every C# script Claude writes for this project must compile and run correctly in Rhino 8 Grasshopper on the first try — no runtime crashes, no silent nulls, no type errors.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Comprehensive `grasshopper_csharp_learnings.md` — all rules, patterns, and gotchas for Rhino 8 C# scripting
- [ ] `CLAUDE.md` — auto-loaded per-session instructions that enforce the knowledge base rules
- [ ] Component templates for geometry processing (curves, surfaces, breps, transforms, intersections)
- [ ] Component templates for KUKAprc toolpaths (axis values, speeds, planes, external axes)
- [ ] Component templates for DataTree and list operations (iteration, branching, filtering)
- [ ] GSD workflow integration — use `/gsd:` commands to plan and execute scripting tasks

### Out of Scope

- Full Grasshopper plugin (.gha) development — that requires a separate Visual Studio project
- Python scripting — C# only in this folder
- Rhino 7 legacy compatibility — Rhino 8 only

## Context

- **Environment**: Rhino 8, Grasshopper C# Script component (new editor, full class shown)
- **Key constraint**: No non-ASCII characters anywhere — compiler crashes on German/special chars
- **Primary use cases**: KUKAprc robot toolpath generation, geometry processing, DataTree manipulation
- **Existing knowledge**: `grasshopper_csharp_learnings.md` from `C:\Users\leona\Downloads\` — starting point, needs expansion
- **Workflow goal**: Ask Claude → get working code → paste into GH → runs immediately

## Constraints

- **Compiler**: Rhino 8 C# Script uses a restricted C# version — no `out var`, limited LINQ, no async
- **ASCII only**: Any non-ASCII char (including German umlauts, em-dashes) crashes the compiler
- **No Tailwind/web**: This is desktop tool scripting, not web dev
- **RhinoCommon**: Must use Rhino.Geometry / Grasshopper.Kernel namespaces, no external NuGet packages

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Rhino 8 only | New Script component is the standard going forward | — Pending |
| No Python | C# is 10-100x faster, no GH_Plane wrapper issues | — Pending |
| GSD for scripting tasks | Structured planning even for small scripts prevents rework | — Pending |

---
*Last updated: 2026-03-23 after initial project setup*
