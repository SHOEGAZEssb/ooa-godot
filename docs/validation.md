# Validation

## Assembly boundary

Headless regressions live in the separate
`validation/oracle-of-ages.validation.csproj` assembly and run through
`validation/validation.tscn`. Production compilation excludes
`validation/**/*.cs`; the validation project references production through a
narrow `InternalsVisibleTo` surface.

`ValidationRoot` is a partial class organized by use case under
`validation/Features/`. Keep the runner and ordered registration in
`Features/Framework/Validation.cs`; put a scenario in the matching feature
file. Fixtures, test doubles, observers, audit history, and expected traces stay
in the validation assembly.

Production may expose a narrow internal operation or observer when it is a
truthful view of the runtime owner. Do not add validation-only state machines,
public compatibility properties, permanent trace lists, sound request counts,
or cache histories to production classes.

## Run validations

Build first, then pass project arguments after `--`:

```powershell
dotnet build
$godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate
```

Run one exact registered method while developing:

```powershell
& $godot --headless --path . --quit-after 10 -- --validate --validate-only=ValidateMethodName
```

An unknown name fails. A focused run is a development aid; run the complete
suite before handoff.

Importer/parser/schema changes also require:

```powershell
& .\tools\verify_oracle_import.ps1
```

Handoff checks:

```powershell
dotnet build
& $godot --headless --path . --quit-after 10 -- --validate
git diff --check
git status --short
```

The build must have zero warnings and errors. Preserve unrelated worktree
changes and review every generated diff.

## Scenario isolation

Each top-level scenario starts from a newly constructed standard gameplay graph.
The runner disposes the prior graph and recreates live save/runtime state, RNG,
rooms, entities, story controllers, menus, input buffers, application counters,
and validation observers.

A scenario arranges every flag, item, room, actor, and RNG prerequisite it
asserts. It may not depend on registration order or state left by another
scenario. Immutable resource caches may remain keyed across scenarios, but
observers and audit state reset. Save-store checks use an isolated temporary
directory and never touch player slots.

Use shared fixtures to construct production owners with normal dependencies.
Options should describe only exceptional inputs needed by the scenario. The
fixture owns cleanup and disposal.

## Regression design

Every fixed bug or newly supported gameplay system gets a focused regression
that asserts the original cause and observable result, not only a clone-side
implementation detail.

Cover as applicable:

- imported source rows, labels, aliases, IDs, and malformed-data failures;
- exact first, zero, final, and following update boundaries;
- ordering among object slots, contacts, children, scripts, transitions, HUD,
  and audio;
- global RNG calls and downstream state;
- byte/fixed-point arithmetic, wrap, carry, collision boundaries, and long-path
  drift;
- scroll preload, warp entry, transition freeze, cancellation, and re-entry;
- flags, inventory transactions, explicit saves, reload, and backups;
- logical position, presentation position, OAM pixels/offsets, palettes, and
  resource lifetime;
- sounds, audio channel state, and input/pause ownership;
- every supported branch of a script or native state machine.

Use canonical rooms that consume real imported data. Failure messages include
hexadecimal group, room, object/interaction, flag, treasure, or sound IDs plus
expected and actual values.

Keep expected arithmetic independent of the production helper being tested.
For movement, calculate source vectors and boundary cases separately instead of
calling the same runtime helper to produce expectations.

A regression must remain deterministic when one rendered frame causes several
fixed updates. Compare repeated `1/60` updates with a batched host frame for
systems that cross input edges, modal ownership, same-pass child creation,
transition dispatch, or sequencer order.

## Validation scope

Prefer a small focused scenario for one mechanic and a route-level scenario
when several real rooms must prove that imported coverage composes. A large
route does not replace focused failure localization, and a focused unit does not
prove room/object ordering or integration.

Do not document the contents of every validation method here. The registered
methods and feature files are the current inventory; use `rg` to find them.
