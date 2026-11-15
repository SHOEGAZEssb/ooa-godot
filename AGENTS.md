# Oracles Godot port: agent guide

Reconstruct *Oracle of Ages* and *Oracle of Seasons* in Godot 4.7.1/.NET with
1:1 fidelity to each supported clean US source. Scope and content boundaries
live in the README and coverage ledgers. The pinned disassembly is
`external/oracles-disasm`.

## Start every task this way

1. Run `git status --short`; preserve unrelated user changes.
2. Use the [task map](docs/README.md#task-map) and
   [code entry points](docs/README.md#code-entry-points) to locate the runtime
   owner, importer stage, generated data and validation. Read only the relevant
   guide sections; setup instructions are needed only for setup work.
3. Before changing gameplay, read [Project principles](docs/project-principles.md)
   and apply the relevant [integration checks](docs/shared-system-integration.md).
   Evidence order: executed clean US ROM, disassembly, generated typed data,
   runtime/validations, then assumptions or visual approximation.
4. Trace source inputs, callers, tables, branches, order, counters, arithmetic,
   side effects and persistent state before designing the change. Verify room
   routes before expanding content bindings; keep optional work separate.
5. Extend the importer first if source information is missing. Implement the
   smallest source-supported rule; fix existing construction, ownership or data
   bindings before adding another handler. Never hand-edit `assets/oracle/`.
6. Add or extend a focused headless regression. Follow
   [regression design](docs/validation.md#regression-design) and
   [shared-system regressions](docs/validation.md#shared-system-regressions).
7. Run the required checks below and update only durable contracts or changed
   coverage. Report what was verified and unresolved paths; a suite pass alone
   does not prove ROM parity.

Use `rg` or `rg --files` for searches and `apply_patch` for edits. Perform
one-off cleanup directly; do not add cleanup scripts or legacy-deletion blocks.

## Implementation rules

- Preserve original object/table order, global RNG consumption, integer and
  fixed-point arithmetic, and exact 60-update counter boundaries.
- Trace behavior gates through writers, callers and consumers, including object
  eligibility, collision/input masks and signal lifetime across update phases.
  A helper name or implemented interface does not establish correct use.
- Keep one authoritative owner for room identity, save bytes, inventory, RNG,
  transitions, modal state and audio. Production reads generated assets, never
  disassembly. Unsupported behavior must fail with source context or be
  represented explicitly and safely; never silently skip a row, opcode or state.
- Gameplay coordinates are room/world space; camera offsets are presentation.
  HUD, dialogue, fades, menus and debug overlays use screen space.
- Stable nodes belong in scenes; runtime owners create content-dependent actors
  and effects. Validation-only traces and orchestration stay in validation.
- Keep hexadecimal source IDs in diagnostics, failures and source comments.
- Place types with their owning feature and original dispatch boundary. Keep
  one class/interface per matching C# file; narrow records/enums may stay beside
  their owner. Shared behavior does not belong to its first consuming dungeon.

`src/Application/` composes the game; `src/Features/` owns shared behavior;
`src/Games/{game}/` owns content and source-specific bindings;
`src/Infrastructure/` owns external/generated-data boundaries; `src/Shared/`
holds small behavior-neutral primitives.

Before cross-game reuse or shared-engine completion claims, apply
[cross-game readiness](docs/shared-system-integration.md#cross-game-readiness).
Trace selected-game construction and data through spawned children; common file
placement and passing Ages tests do not prove Seasons readiness.

World invariants: small rooms are 10 by 8 metatiles (160 by 128 pixels); large-room
storage is 16 by 11 with a 16-byte stride and a padded last column (15 by 11 playable).
The viewport is 160 by 144 with a 16-pixel HUD and 160 by 128 gameplay field. Dungeon
neighbors come from imported floor layouts. Destination entities/events freeze
during scrolling. Enemy placement consumes one ordered object stream and the
shared RNG placement buffer. Only explicit save flows write live state to disk.

## Checks and commands

| Change | Required checks |
| --- | --- |
| Documentation only, including `AGENTS.md` | Diff and relevant links; no build or gameplay validation, even with unrelated pre-existing source changes |
| Actual game source files in this handoff | Optimized rebuild, focused regression and full parallel suite |
| Import code or generated inputs | Run importer; parser, stage-boundary, schema or determinism changes also require import verification |

```powershell
& .\tools\import_oracles.ps1                  # only when import inputs/code changed
& .\tools\verify_oracle_import.ps1            # required for the import changes above
dotnet build -t:Rebuild -p:Optimize=true
& .\tools\validate_parallel.ps1 -ValidateOnly ValidateMethodName # development
& .\tools\validate_parallel.ps1              # full suite; 8 workers by default
git diff --check
git status --short
```

Always rebuild production and validation assemblies with optimization and zero
warnings/errors. This preserves Godot's Debug output paths; incremental builds
can retain old optimization settings. Build once and reuse assemblies until
source changes require rebuilding. Unoptimized builds require an explicit user
request for debugging. Before a game-source handoff, require success from all
workers and the complete registered scenario count. Focused or serial runs do
not replace the full parallel run; report failures or incomplete runs. Use 8
workers unless the user explicitly requests otherwise.

Put the Godot 4.7.1 .NET executable on `PATH`, or pass `-Godot PATH` to the
launcher. Its default filename is `Godot_v4.7.1-stable_mono_win64_console.exe`;
substitute the installed filename on other platforms. See
[validation commands](docs/validation.md#run-validations) for options and
[performance measurements](docs/validation.md#performance-measurements) for
required before/after evidence.

## Documentation rules

Guides explain durable evidence, ownership, invariants, formats and workflow.
Put source-specific detail beside implementation, importer diagnostics and
focused validations. Do not duplicate class inventories, generated tables,
per-room implementation notes or validation method contents.

Keep audits and working findings untracked under `local-audits/`, excluded
through `.git/info/exclude`. Never put audit narratives in tracked docs or link
tracked docs to local audits.

The [coverage ledgers](docs/README.md#documentation-boundary) are the inventory
exceptions. Any implemented, extended, partial, suppressed or reclassified NPC
record requires affected table entries and summary counts to change together.
Only edit NPC ledger tables, recording resulting status without bugfix history,
regression details or progress notes. A bugfix without a coverage change needs
no ledger update. Source-wide interaction/event/cutscene coverage changes must
update the affected ledger statuses, counts and proof checklist items together.

Change the root README only for scope, setup, quick start or navigation; change
a subsystem guide only when its durable contract changes.
