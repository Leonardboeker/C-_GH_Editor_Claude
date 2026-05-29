# Quick Task 260422-3wx: Falzleiste Hanging System Component - Context

**Gathered:** 2026-04-22
**Status:** Ready for planning
**Source:** Direct conversation with user (11 clarifying questions answered)

<domain>
## Task Boundary

Build a single parametric Rhino 8 Grasshopper C# Script component that generates a **Falzleiste** (German rebate-based French cleat / Aufhängeleiste) hanging system from a planar input surface.

NOT a mitered French cleat — uses rebate (Falz) cuts that interlock. Frame mounts on wall, mating cleats mount on hanging object and hook onto frame.

Single `.cs` file, paste-ready into Rhino 8 Script component.
</domain>

<decisions>
## Implementation Decisions (locked — do not revisit)

### Inputs (7 total — UI auto-generated on canvas)
- `S` (Surface): planar rectangular surface (wall panel). Required.
- `O` (double): offsetBorder — auto-generated **Number Slider**, default 50, range 0–500
- `T` (int): stripThickness — auto-generated **Value List** with items [10, 12, 16, 18, 19, 38], default 18
- `W` (double): stripWidth — auto-generated **Number Slider**, default 80, range 20–200
- `Fw` (double): falzWidthMate — auto-generated **Number Slider**, default 10, range 5–30
- `Fe` (double): falzExtraFrame — auto-generated **Number Slider**, default 4, range 0–10
- `Sh` (double): mateShorten — auto-generated **Number Slider**, default 20, range 0–100

### Outputs
- `A` (List<Brep>): FRAME = 5 Breps → 2 vertical sides + 3 horizontal cleats with top-back rebate
- `B` (List<Brep>): MATING = 3 Breps → horizontal cleats (2*Sh shorter), bottom-front rebate

### Coordinate convention
- Input surface treated as the **wall**.
- Long axis = **global Z** (vertical). Short axis = horizontal in surface plane.
- +Y direction = outward from wall (surface normal). Front = +Y (away from wall). Back = -Y side (wall-facing side).

### Geometry spec (locked)
1. **Surface rectangle extraction:** get bounding rectangle of input surface. Validate it is planar. W = horizontal extent, H = vertical (Z) extent.
2. **Frame outline:** inset surface edges by `O` on all 4 sides. Frame outer dims = (W - 2O) × (H - 2O).
3. **Two vertical SIDES:**
   - Located at left and right of frame outline.
   - In-plane horizontal width = `W_strip` (= 80 mm by default).
   - Vertical length = full frame height (H - 2O).
   - Out-of-wall depth = `T` (material thickness).
   - **No rebate on sides.**
4. **Three horizontal CLEATS between the sides:**
   - Length (X) = frame_width - 2*W_strip = (W - 2O) - 2*W_strip.
   - Vertical extent (Z) = W_strip (80 mm).
   - Out-of-wall depth (Y) = T.
   - **Bottom cleat:** lower Z edge flush with bottom of sides.
   - **Middle cleat:** upper Z edge = frame_vertical_center - W_strip (user-confirmed: upper_edge + cleat_width = frame center, so cleat extends below center).
   - **Top cleat:** upper Z edge = top of sides - W_strip (leaves an 80 mm empty zone above the top cleat — this is where the top mating cleat hangs when installed).
5. **Rebate on FRAME cleats (TOP-BACK):**
   - Cut region: Y ∈ [0, T/2], Z ∈ [Z_top - (Fw+Fe), Z_top], X = full cleat length.
   - Depth into material = T/2. Width down from top = Fw+Fe (= 14 mm default).
   - Implemented via `Brep.CreateBooleanDifference` against a rebate Box.
6. **MATING cleats (one per frame cleat):**
   - Length = frame_cleat_length - 2*Sh. Centered on same X as corresponding frame cleat.
   - Same Z, Y positions as frame cleats (emitted in engaged position for visual alignment).
   - Rebate **BOTTOM-FRONT:** cut region Y ∈ [T/2, T], Z ∈ [Z_bot, Z_bot + Fw], X = full mating cleat length.
   - Depth = T/2. Width up from bottom = Fw (= 10 mm — no +Fe).
7. **Why Fe = 4 mm extra on frame:** gives 4 mm vertical drop-in play when mating cleat engages.
8. **Why Sh = 20 mm shorter per side:** gives 40 mm total horizontal play for install alignment.

### UI auto-generation (canvas)
Component, on first `RunScript`, detects missing upstream parameters and creates them via `GH_Document`:
- Sliders: `GH_NumberSlider` with sensible min/max and the default value set above.
- Value list: `GH_ValueList` with the 6 thickness items, item 18 selected.
- Slider placement: stacked left of component, connected to the corresponding input.
- Only generate if no upstream source is already connected (check `Params.Input[i].SourceCount == 0`).
- Use `this.Component.OnPingDocument().ScheduleSolution()` pattern to avoid solution-reentry issues.

### Script_Instance shape requirements (from grasshopper_csharp_learnings.md)
- Output `A`, `B` are `ref object` — assign directly, never `return`.
- No `out var`, no pattern matching, no switch expressions, no records.
- ASCII only, English comments only.
- Null guard: if `S` is null or non-planar → `AddRuntimeMessage(Warning)`, set A and B to empty `List<Brep>()`, return.
- Null guard with defaults BEFORE guard clauses (prevents null downstream outputs).
- Use `this.Component.AddRuntimeMessage` for any warnings.
- Use `for` loops (no `while`) for geometry iteration.

### Claude's Discretion
- Exact slider min/max ranges (use sensible defaults; honor user-provided values if within range).
- Slider pivot positions on canvas (stack tidily to the left).
- Whether to rebuild the UI on every run or only once (recommend: check `SourceCount == 0` each run; idempotent).
- Internal helper method names and struct layouts.
- Boolean difference tolerance — use `RhinoMath.SqrtEpsilon` or similar default.

</decisions>

<specifics>
## Specific References

- `grasshopper_csharp_learnings.md` — MANDATORY read before writing any C#. Contains Brep operations (§21), intersection patterns (§22), transform operations (§23), runtime error prevention (§27), output parameter rules (§3), input access types (§4), plane construction (§8), and the complete minimal template (§18).
- `templates/` folder — check for any existing UI-auto-generation template; if one exists, base the slider/value-list spawning on that.
- Rhino 8 Script component uses Roslyn-based editor; full class body (including `using` statements and `Script_Instance` class) must be output.

</specifics>

<canonical_refs>
## Canonical References

- `D:\Projekte\SynologyDrive\55_C#_GH_Editor\grasshopper_csharp_learnings.md` — project-wide compiler rules and API patterns (2888 lines, 32+ sections)
- `D:\Projekte\SynologyDrive\55_C#_GH_Editor\CLAUDE.md` — project instructions (output format, critical rules)
- `D:\Projekte\SynologyDrive\55_C#_GH_Editor\templates\` — existing templates to follow as style reference

</canonical_refs>

---

*Quick task: 260422-3wx-falzleiste-hanging-system-component*
*Context captured: 2026-04-22 via direct conversation*
