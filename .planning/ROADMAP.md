# Roadmap: GH C# Editor Toolkit

## Overview

Build a comprehensive knowledge base, auto-load rules, and component templates so that every C# script Claude writes for Rhino 8 Grasshopper works correctly on the first run — no debugging, no crashes, no re-runs.

## Phases

- [ ] **Phase 1: Knowledge Base** - Expand grasshopper_csharp_learnings.md with all patterns needed for working Rhino 8 C# scripts
- [ ] **Phase 2: CLAUDE.md and GSD Integration** - Auto-load rules each session and integrate GSD workflow for scripting tasks
- [ ] **Phase 3: Component Templates** - Create ready-to-paste templates for geometry, KUKAprc, DataTree, and Galapagos components

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
- [ ] 01-03: Add KUKAprc and DataTree patterns (axis input, speed lists, path iteration)
- [ ] 01-04: Add debugging and error handling patterns (AddRuntimeMessage, runtime guards)
- [ ] 01-05: Add performance patterns and embed all component templates

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
- [ ] 02-01: Write CLAUDE.md with per-session rules and output format requirements
- [ ] 02-02: Add GSD workflow guidance to CLAUDE.md
- [ ] 02-03: Copy expanded knowledge base to project root and verify file placement

### Phase 3: Component Templates
**Goal**: Create standalone, ready-to-paste component templates for the three main use cases.
**Depends on**: Phase 2
**Requirements**: TPL-01, TPL-02, TPL-03, TPL-04
**Success Criteria** (what must be TRUE):
  1. Each template compiles in Rhino 8 C# Script component without modification
  2. Templates include English-only comments and correct parameter structure
  3. Templates are referenced from grasshopper_csharp_learnings.md
**Plans**: 4 plans

Plans:
- [ ] 03-01: Create geometry processing template
- [ ] 03-02: Create KUKAprc toolpath template
- [ ] 03-03: Create DataTree processing template
- [ ] 03-04: Create Galapagos fitness template

---
*Roadmap created: 2026-03-23*
