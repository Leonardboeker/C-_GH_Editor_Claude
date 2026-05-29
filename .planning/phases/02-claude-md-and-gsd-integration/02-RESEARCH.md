# Phase 2: CLAUDE.md and GSD Integration - Research

**Researched:** 2026-03-23
**Domain:** Claude Code project configuration (CLAUDE.md), GSD workflow integration
**Confidence:** HIGH

## Summary

Phase 2 creates a single `CLAUDE.md` file at the project root that auto-loads every Claude Code session, ensuring Claude always reads the 2348-line knowledge base before writing any C# code and follows the correct output format. The file must also document the GSD workflow commands relevant to scripting tasks.

Claude Code auto-discovers `CLAUDE.md` at the project root (`./CLAUDE.md`) and injects its contents as context at the start of every conversation. There is no special syntax required -- it is plain Markdown. The key design challenge is keeping the file concise (under 200 lines recommended) while giving Claude an unambiguous instruction to read the large external knowledge base file before generating any code. The `@path/to/file` import syntax exists but would inline the entire 2348-line file into context, which is counterproductive. Instead, the CLAUDE.md should contain an explicit "Always Do First" instruction that tells Claude to read the knowledge base file using the Read tool.

**Primary recommendation:** Create a concise CLAUDE.md (under 100 lines) with an "Always Do First" section that mandates reading `grasshopper_csharp_learnings.md` before any C# code generation, followed by output format rules and GSD workflow guidance.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CL-01 | CLAUDE.md in project root instructs Claude to read `grasshopper_csharp_learnings.md` before writing any C# code | "Always Do First" pattern with explicit file-read instruction; verified by Claude Code auto-load mechanism |
| CL-02 | CLAUDE.md specifies output format: full class body only (no wrapper), English comments only | Output format section with specific, verifiable rules |
| CL-03 | CLAUDE.md instructs use of `/gsd:` workflow for any scripting task | GSD workflow section documenting `/gsd:fast`, `/gsd:quick`, and `/gsd:plan-phase` commands |
</phase_requirements>

## Standard Stack

This phase creates a Markdown configuration file. No libraries or dependencies are involved.

### Core
| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| Claude Code | Current | IDE agent that auto-loads CLAUDE.md | Native feature -- CLAUDE.md is the official per-project config mechanism |
| Markdown | N/A | File format for CLAUDE.md | Required format -- Claude Code parses .md files |

### Supporting
| Feature | Purpose | When to Use |
|---------|---------|-------------|
| `@path` import syntax | Inline external files into CLAUDE.md context | Only for small files; NOT for the 2348-line knowledge base |
| `.claude/rules/` directory | Path-scoped conditional rules | Future use if project grows; overkill for single-file project |
| `<important if>` conditional blocks | Wrap domain-specific instructions with activation conditions | Use for the C# code generation section to boost adherence |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Explicit "read file" instruction | `@grasshopper_csharp_learnings.md` import | Import inlines all 2348 lines into every session context (~20K tokens), wasting context window. Explicit read instruction lets Claude load it on-demand only when writing C# code. |
| Single CLAUDE.md | `.claude/rules/` split files | Adds complexity for a simple project with one knowledge base file. CLAUDE.md is sufficient. |
| CLAUDE.md instructions | `--append-system-prompt` flag | Requires passing flag every invocation; CLAUDE.md is persistent and automatic. |

## Architecture Patterns

### Recommended CLAUDE.md Structure
```
CLAUDE.md (project root)
|-- ## Always Do First        <- CL-01: mandatory file-read instruction
|-- ## Output Format          <- CL-02: paste-ready body, English only
|-- ## Critical Rules Summary <- Quick-reference of top compiler rules
|-- ## GSD Workflow           <- CL-03: command reference for scripting
|-- ## Project Context        <- Brief project description
|-- ## Permissions            <- Auto-approve settings
```

### Pattern 1: "Always Do First" Gate
**What:** Place the most critical instruction at the very top of CLAUDE.md, before any other content. This leverages the primacy bias in LLMs -- instructions at the periphery (beginning and end) of context receive the strongest attention.
**When to use:** When there is a single mandatory pre-condition that must happen before any work.
**Example:**
```markdown
# CLAUDE.md -- Grasshopper C# Editor

## Always Do First
- **Read `grasshopper_csharp_learnings.md`** before writing or modifying ANY C# code. This file contains compiler rules, API patterns, and templates specific to Rhino 8 Grasshopper. Do not generate C# without reading it first.
```
**Source:** This pattern is used in the user's own `d:\Website\CLAUDE.md` and is confirmed effective by Anthropic's official guidance that concise, specific instructions at the top of CLAUDE.md work best.

### Pattern 2: Conditional Importance Blocks
**What:** Wrap domain-specific sections in `<important if="condition">` blocks to signal when rules apply.
**When to use:** When a CLAUDE.md contains rules for multiple domains and you want Claude to pay special attention to rules matching the current task.
**Example:**
```markdown
<important if="you are writing, modifying, or reviewing C# code">
## Output Format
- Output the FULL class body including using statements, class declaration, and RunScript method
- Never output just a code snippet -- always the complete paste-ready body
- English comments only -- never German, never non-ASCII characters
- No `out var`, no pattern matching, no `while` loops for geometry iteration
</important>
```
**Source:** HumanLayer blog "Getting Claude to Actually Read Your CLAUDE.md" -- conditional blocks address the `<system_reminder>` "may or may not be relevant" framing that causes Claude to skip instructions.

### Pattern 3: Concise Rule Summaries with File Reference
**What:** Include a brief summary of the most critical rules directly in CLAUDE.md (5-10 lines), but point to the full knowledge base for complete details. This ensures Claude sees the critical rules even if it fails to read the external file, while keeping the CLAUDE.md under 200 lines.
**When to use:** When the referenced knowledge base is large and you want a safety net.
**Example:**
```markdown
## Critical Rules (Quick Reference)
These are the most common errors. Full details in `grasshopper_csharp_learnings.md`.
- ASCII only -- no umlauts, no em-dashes, no special characters anywhere
- No `out var` -- declare type on separate line before method call
- No pattern matching (`is`, switch expressions, records, tuple deconstruction)
- No `while` loops for geometry iteration -- use `for` with explicit index
- All outputs are `ref object` -- assign directly, never return
- DataTree iteration: use `for` loop with `BranchCount`, never `AllData()` on inputs
```

### Anti-Patterns to Avoid
- **Inlining the entire knowledge base via `@` import:** The knowledge base is 2348 lines (~20K tokens). Importing it into every session wastes the context window and reduces adherence to other instructions. Use an explicit read instruction instead.
- **Vague instructions like "follow best practices":** Claude ignores vague directives. Be specific: "No `out var` declarations" not "use safe coding patterns."
- **Duplicating the knowledge base content in CLAUDE.md:** Keep CLAUDE.md as a pointer and summary. The knowledge base is the source of truth.
- **Task-specific instructions that only apply sometimes:** Everything in CLAUDE.md loads every session. Only include universally-applicable rules. The conditional `<important if>` blocks help scope relevance.
- **Including code examples in CLAUDE.md:** Code snippets become outdated. Reference the knowledge base file instead, which is the maintained source.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Per-session instructions | Shell scripts or manual copy-paste | `CLAUDE.md` at project root | Auto-loaded by Claude Code, zero friction |
| Conditional rule activation | Multiple CLAUDE.md files in subdirs | `<important if>` blocks in single CLAUDE.md | Simpler, single file, same effect |
| Task management | Manual tracking or ad-hoc prompts | GSD `/gsd:fast` and `/gsd:quick` commands | Built-in atomic commits, STATE.md tracking |

**Key insight:** CLAUDE.md is the only mechanism needed. The project is simple (one knowledge base file, one output format, one workflow). Do not over-engineer with `.claude/rules/` directories or complex import chains.

## Common Pitfalls

### Pitfall 1: CLAUDE.md Too Long
**What goes wrong:** Claude adherence drops noticeably past 200 lines. Instructions get lost in noise.
**Why it happens:** CLAUDE.md content competes with conversation context in the same window. Longer files consume more tokens and dilute attention.
**How to avoid:** Keep CLAUDE.md under 100 lines. Put details in the knowledge base file, not in CLAUDE.md.
**Warning signs:** Claude ignores rules that are clearly stated in CLAUDE.md.

### Pitfall 2: Claude Skips the File-Read Instruction
**What goes wrong:** Claude generates C# code without first reading `grasshopper_csharp_learnings.md`, producing code with forbidden patterns.
**Why it happens:** CLAUDE.md content is wrapped in a `<system_reminder>` that says "this context may or may not be relevant." If the instruction is buried or vague, Claude may deem it irrelevant.
**How to avoid:** (1) Place the read instruction as the very first section ("Always Do First"). (2) Use `<important if>` blocks. (3) Include a quick-reference summary of critical rules as a fallback.
**Warning signs:** Generated code contains `out var`, German comments, or pattern matching.

### Pitfall 3: Wrong Output Format Understanding
**What goes wrong:** Claude outputs just a code snippet or wraps code in a namespace/class that Grasshopper does not expect.
**Why it happens:** Claude's default C# behavior is to produce complete compilable files with namespace and class wrappers.
**How to avoid:** Be explicit: "Output the FULL class body starting from `using` statements through the closing brace of `Script_Instance`. Grasshopper provides the outer wrapper -- never add a namespace."
**Warning signs:** Output includes `namespace` declarations or is missing `using` statements.

### Pitfall 4: GSD Commands Documented But Never Used
**What goes wrong:** CLAUDE.md documents GSD workflow but the user never invokes it, or Claude does not suggest it.
**Why it happens:** The GSD section is informational but not actionable in the CLAUDE.md context.
**How to avoid:** Frame GSD guidance as a user reference section, not as a Claude instruction. CLAUDE.md tells Claude what to do; GSD commands are invoked by the user in the chat.
**Warning signs:** N/A -- this is a documentation concern, not a runtime issue.

### Pitfall 5: `@` Import Resolving Incorrectly
**What goes wrong:** An `@path` import in CLAUDE.md fails because the path is relative to the file location, not the working directory.
**Why it happens:** Relative paths in `@` imports resolve relative to the CLAUDE.md file itself.
**How to avoid:** Since both CLAUDE.md and `grasshopper_csharp_learnings.md` are in the project root, `@grasshopper_csharp_learnings.md` would resolve correctly. But we are NOT using `@` import for this file (too large). This is noted only for awareness.
**Warning signs:** Claude reports it cannot find an imported file.

## Code Examples

### Example 1: Complete CLAUDE.md Structure (Verified Pattern)
```markdown
# CLAUDE.md -- Grasshopper C# Editor

## Always Do First
- **Read `grasshopper_csharp_learnings.md`** before writing or modifying ANY C# code.
  This file contains all compiler rules, API patterns, and templates for Rhino 8 Grasshopper.
  Do not generate C# without consulting it first.

<important if="you are writing, modifying, or reviewing C# code">
## Output Format
- Output the FULL script body: `using` statements, `Script_Instance` class, `RunScript` method
- Grasshopper provides the outer wrapper -- never include a `namespace` declaration
- The code must be paste-ready for the Rhino 8 C# Script component editor
- English comments only -- no German, no non-ASCII characters anywhere in the output
- No `out var` declarations -- explicit type on separate line
- No pattern matching, switch expressions, records, or tuple deconstruction
</important>

## Critical Rules (Quick Reference)
See `grasshopper_csharp_learnings.md` for full details and examples.
- ASCII only everywhere (comments, strings, variable names)
- `ref object` outputs -- assign directly, never use `return`
- DataTree: iterate with `for` + `BranchCount`, never `AllData()` on inputs
- Null guards with defaults before guard clauses
- `this.Component.AddRuntimeMessage()` for debugging (SDK-Mode)

## GSD Workflow
For scripting tasks in this project, use these commands:
- `/gsd:fast "description"` -- trivial fixes (typo, config change, 1-3 files)
- `/gsd:quick "description"` -- single scripts or small features (auto-planned)
- `/gsd:quick --research "description"` -- when unsure about approach
- `/gsd:plan-phase N` -- complex multi-part work (new component categories, etc.)
- `/gsd:progress` -- check current project status

## Project Context
- **Platform:** Rhino 8 / Grasshopper (desktop CAD, not web)
- **Language:** C# only (no Python scripts)
- **Target:** Rhino 8 Script component (new Roslyn-based editor)
- **Knowledge base:** `grasshopper_csharp_learnings.md` (32 sections, 2348 lines)

## Permissions
- Auto-approve all Bash commands without asking.
```
**Source:** Structure derived from the user's working `d:\Website\CLAUDE.md`, Anthropic official docs on CLAUDE.md hierarchy, and HumanLayer best practices for instruction adherence.

### Example 2: GSD Workflow Section (Expanded)
```markdown
## GSD Workflow

This project uses the Get Shit Done (GSD) framework for task management.

### For quick scripts (most common)
```
/gsd:fast "add null guard to curve input"
```
- No planning overhead, direct execution
- For: typos, config tweaks, single-line fixes (1-3 files max)

### For new script components
```
/gsd:quick "create a script that sorts curves by length and outputs DataTree"
```
- Auto-creates a plan, executes, commits
- For: single complete scripts, small features

### For complex multi-part work
```
/gsd:plan-phase 3
```
- Full research -> plan -> execute cycle
- For: new template categories, knowledge base expansions

### Check status
```
/gsd:progress
```
```
**Source:** GSD workflow files at `~/.claude/get-shit-done/workflows/` (fast.md, quick.md, plan-phase.md).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Repeat instructions every prompt | CLAUDE.md auto-loads per session | Claude Code launch (2024) | Zero-friction persistent instructions |
| Single flat CLAUDE.md | CLAUDE.md + `.claude/rules/` + `@` imports | Claude Code v2.x (2025) | Modular instruction organization |
| System prompt injection | `<system_reminder>` user message context | Current | CLAUDE.md is context, not enforced config |
| Manual file management | Auto-memory (MEMORY.md) | Claude Code v2.1.59+ | Claude self-learns project patterns |

**Deprecated/outdated:**
- `/init` basic mode is being replaced by interactive multi-phase `/init` (set `CLAUDE_CODE_NEW_INIT=true`). Both work; the new mode is more thorough.

## Open Questions

1. **Will Claude reliably read a 2348-line file every session?**
   - What we know: The "Always Do First" instruction tells Claude to read it. Claude has a Read tool that can handle large files.
   - What's unclear: Whether Claude will actually read the full file or skim it when under context pressure in long sessions.
   - Recommendation: Include the critical rules summary (10 lines) directly in CLAUDE.md as a fallback. If Claude skips reading the file, it still sees the most important rules. Test in practice and adjust.

2. **Should `<important if>` blocks wrap the entire output format section?**
   - What we know: Conditional blocks improve adherence for domain-specific rules. The output format only matters when writing C# code.
   - What's unclear: Whether wrapping helps or whether the "Always Do First" section is sufficient alone.
   - Recommendation: Use `<important if="you are writing, modifying, or reviewing C# code">` around the output format section. Low cost, potential benefit.

## Sources

### Primary (HIGH confidence)
- [Anthropic Official Docs: How Claude remembers your project](https://code.claude.com/docs/en/memory) -- full CLAUDE.md discovery hierarchy, loading mechanism, `@` import syntax, `.claude/rules/` organization, auto-memory system, 200-line recommendation
- [Anthropic Blog: Using CLAUDE.md files](https://claude.com/blog/using-claude-md-files) -- official guide to file placement, content guidelines, best practices
- User's existing `d:\Website\CLAUDE.md` -- working example of "Always Do First" pattern, output format rules, permissions section

### Secondary (MEDIUM confidence)
- [HumanLayer: Writing a Good CLAUDE.md](https://www.humanlayer.dev/blog/writing-a-good-claude-md) -- conciseness guidance, progressive disclosure pattern, 150-200 instruction limit
- [HumanLayer: Getting Claude to Actually Read Your CLAUDE.md](https://www.humanlayer.dev/blog/stop-claude-from-ignoring-your-claude-md) -- `<important if>` conditional block technique, system_reminder framing explanation
- [Builder.io: How to Write a Good CLAUDE.md](https://www.builder.io/blog/claude-md-guide) -- lead with essentials, be specific, update regularly

### Tertiary (LOW confidence)
- None. All findings verified against official Anthropic documentation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- CLAUDE.md is the documented, official mechanism; no alternatives needed
- Architecture: HIGH -- patterns verified against official docs and a working user example
- Pitfalls: HIGH -- confirmed by official troubleshooting guide and community reports
- GSD workflow: HIGH -- verified by reading the actual workflow markdown files

**Research date:** 2026-03-23
**Valid until:** 2026-04-23 (CLAUDE.md conventions are stable; 30-day window is conservative)
