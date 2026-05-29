# CLAUDE.md -- Grasshopper C# Editor

## Always Do First
- **Read `grasshopper_csharp_learnings.md`** (with the Read tool) before writing or modifying ANY C# code.
  This 2300+ line file contains all compiler rules, API patterns, and templates for Rhino 8 Grasshopper.
  Do not generate C# without reading it first. No exceptions.

<important if="you are writing, modifying, or reviewing C# code">
## Output Format
- Output the FULL script body: `using` statements, `Script_Instance` class, `RunScript` method
- Grasshopper provides the outer wrapper -- never include a `namespace` declaration
- The code must be paste-ready for the Rhino 8 C# Script component editor
- English comments only -- no German, no non-ASCII characters anywhere in the output
- No `out var` declarations -- declare explicit type on a separate line before the method call
- No pattern matching, switch expressions, records, or tuple deconstruction
</important>

## Critical Rules (Quick Reference)
These are the most common failure points. Full details in `grasshopper_csharp_learnings.md`.
1. ASCII only everywhere -- no umlauts, em-dashes, or special characters (compiler crashes)
2. No `out var` -- declare type on separate line before method call
3. No `while` loops for geometry iteration -- use `for` with explicit index
4. All outputs are `ref object` -- assign directly to A/B/C, never use `return`
5. DataTree: iterate with `for` + `BranchCount`, never `AllData()` on inputs
6. Null guards with defaults BEFORE guard clauses (prevents null downstream outputs)
7. `this.Component.AddRuntimeMessage()` for debugging (SDK-Mode)
8. No pattern matching (`is Type t`), no switch expressions, no records
9. Use `RhinoMath.ToRadians()` not `Math.PI / 180`
10. Templates live in `templates/` folder -- check there before writing from scratch

## Project Context
- **Platform:** Rhino 8 / Grasshopper (desktop CAD, not web)
- **Language:** C# only (no Python scripts)
- **Target:** Rhino 8 Script component (new Roslyn-based editor, full class visible)
- **Knowledge base:** `grasshopper_csharp_learnings.md` (always check this first)
- **Generic templates:** `templates/` folder (geometry, KUKAprc, DataTree, Galapagos)
- **Project-specific scripts:** `projects/{project-name}/` subfolders

## Folder Rules (ALWAYS enforce)

| Script type | Where it goes |
|---|---|
| Generic reusable template | `templates/` |
| Script for a specific project | `projects/{project-name}/` |

**ALWAYS save to BOTH locations** — every script gets written to `templates/` AND `projects/{project-name}/`.

Examples:
- `geometry_processing.cs` → `templates/` AND `projects/{name}/`
- `miter_angle_calculator.cs` for stone fabrication → `templates/` AND `projects/stone-panel-fabrication/`
- When writing scripts for a new project, ALWAYS create `projects/{project-name}/` first

## Auto-Learning Rule (ALWAYS enforce)

**When a bug or workaround is discovered, immediately add it to `grasshopper_csharp_learnings.md`.**

Trigger: any compile error, runtime error, or API mismatch that required a code change.
Format: add under the relevant section (or at the end) following the existing WRONG/CORRECT pattern.
Do this BEFORE responding to the user — the learnings file is the source of truth.

## GSD Workflow
For scripting tasks in this project, use these commands:

**Quick fixes** (typos, config tweaks, 1-3 files):
```
/gsd:fast "add null guard to curve input"
```

**New scripts or small features** (single complete component):
```
/gsd:quick "create script that sorts curves by length and outputs DataTree"
```

**Complex multi-part work** (new template categories, knowledge base expansions):
```
/gsd:plan-phase N
```

**Check project status:**
```
/gsd:progress
```

## Permissions
- Auto-approve all Bash commands without asking.
