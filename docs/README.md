# Engineering documentation

These guides explain how evidence from the original game moves through the
importer, runtime, and validation suite. They are written for contributors and
coding agents; each guide should be useful without becoming an inventory of
everything already implemented.

## Start here

Use the task map to select the relevant guide sections and the code entry points
to locate their owners. Read [Project principles](project-principles.md) before
gameplay changes and apply the relevant [integration checks](shared-system-integration.md).
Read [Development](development.md) for setup or workflow work, and
[Validation](validation.md#regression-design) when designing a regression.
Do not read every guide before a small change.

## Task map

| If you are changing... | Read |
| --- | --- |
| Project fidelity, evidence, or definition of done | [Project principles](project-principles.md) |
| Integrating a gameplay object with shared owners | [Shared-system integration](shared-system-integration.md) |
| Reusing mechanics across games or assessing shared-engine completion | [Cross-game readiness](shared-system-integration.md#cross-game-readiness) |
| Setup, commands, controls, or contributor workflow | [Development](development.md) |
| Generated data, assembly parsing, schemas, or import stages | [Data import](data-import.md) |
| Composition, scene ownership, fixed updates, or input ordering | [Runtime architecture](runtime-architecture.md) |
| Rooms, transitions, terrain, entities, enemies, placement, or RNG | [Rooms and entities](rooms-and-entities.md) |
| NPCs, native interactions, linked actors, or room events | [Owner selection](npcs-and-events.md#choose-the-original-owner), then the relevant actor/event section |
| Which imported NPC records are implemented, partial, or unsupported | [NPC interaction coverage](ages/npc-interaction-coverage.md) |
| Source-wide interaction, room-event and cutscene inventory and missing work | [Interaction, event and cutscene coverage](ages/interaction-event-cutscene-coverage.md) |
| Seasons source NPC/interaction inventory, supported opening and outstanding scope | [Seasons NPC coverage](seasons/npc-interaction-coverage.md), [Seasons interaction and cutscene coverage](seasons/interaction-event-cutscene-coverage.md) |
| An `interactionRunScript` stream or a new script command | [Command runner](command-runner.md) |
| Menus, dialogue/modal input, fades, or pause ownership | [Menus and input](menus-and-input.md) |
| WRAM fields, flags, inventory, checkpoints, or disk persistence | [Saves and state](saves-and-state.md) |
| Imported graphics, OAM, palettes, caching, sound, or audio RNG | [Graphics and audio](graphics-and-audio.md) |
| A regression, fixture, trace, or validation boundary | [Validation](validation.md) |

## Code entry points

These are boundary entry points, not a complete class inventory. Follow callers
and the selected game's bindings, including spawned children. Shared file
placement does not prove both games construct or run a behavior.

| Task | Runtime / import entry | Validation entry |
| --- | --- | --- |
| Composition and update order | [Application](../src/Application/); [fixed scheduler](../src/Application/ApplicationFixedUpdateScheduler.cs) | [Shared framework](../validation/Shared/Features/Framework/) |
| Rooms, object slots and placement | [Room session](../src/Features/World/Rooms/RoomSession.cs), [entity manager](../src/Features/World/Entities/Core/RoomEntityManager.cs); [import stage contracts](../tools/import_oracles.ps1) | [Ages world](../validation/Ages/Features/World/), [shared world](../validation/Shared/Features/World/) |
| Ordinary NPCs and interaction routing | [NPC database](../src/Features/World/Entities/Npcs/NpcDatabase.cs), [interaction controller](../src/Features/World/Interactions/Core/InteractionController.cs); [Ages NPC import](../tools/import_oracles/Ages/Import-NpcData.ps1), [Seasons talk import](../tools/import_oracles/Seasons/Import-TalkNpcs.ps1) | [Ages NPCs](../validation/Ages/Features/Npcs/), [Seasons world](../validation/Seasons/Features/World/) |
| Native actors and story events | [Shared entities](../src/Features/World/Entities/), selected-game factories under [Ages](../src/Games/Ages/Features/World/Entities/Core/) or [Seasons](../src/Games/Seasons/Features/World/Entities/), and each game's story bindings | [Ages story](../validation/Ages/Features/Story/), [Seasons features](../validation/Seasons/Features/) |
| Original script streams | [Command runner](../src/Features/Scripting/CutsceneCommandRunner.cs); [cutscene import](../tools/import_oracles/Shared/Import-CutsceneData.ps1) | [Ages cutscenes](../validation/Ages/Features/Story/Validation.Cutscenes.cs) |
| Scenario discovery and execution | [Ordered registration](../validation/Ages/Features/Framework/Validation.cs); [launcher](../tools/validate_parallel.ps1) | Search registered names with `rg`, then use `-ValidateOnly` |

Disassembly entry points are `external/oracles-disasm/data/{game}/interactions.s`
and `object_code/{game}/interactions/`, with shared handlers under
`object_code/common/interactions/`. Import outputs live under
`assets/oracle/{game}/`; never hand-edit them.

## Documentation boundary

Keep durable evidence rules, ownership, lifecycle, ordering, format contracts,
and contributor workflow here. Put source-specific constants and branch details
beside code and focused validations. Do not append implementation diaries,
class inventories, per-feature test summaries, or unowned plans.

Fidelity audits and working findings belong in untracked `local-audits/`,
excluded through `.git/info/exclude`. Do not link tracked guides to them.

The Ages [NPC coverage ledger](ages/npc-interaction-coverage.md) and
[interaction, event and cutscene ledger](ages/interaction-event-cutscene-coverage.md),
and the corresponding Seasons [NPC](seasons/npc-interaction-coverage.md) and
[interaction](seasons/interaction-event-cutscene-coverage.md) ledgers,
are the deliberate inventory exceptions. Update affected statuses, counts and
checklist items together. Keep the NPC ledger's updates within its tables;
keep audit narratives out of both ledgers.

When removing or moving documentation, update repository-relative links and
run `git diff --check`. Generated `assets/oracle/` files are never hand-edited.
