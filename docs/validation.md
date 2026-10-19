# Validation

## Assembly boundary

Headless regressions live in the separate
`validation/oracle-of-ages.validation.csproj` assembly and run through
`validation/validation.tscn`. Production compilation excludes
`validation/**/*.cs`; the validation project references production through a
narrow `InternalsVisibleTo` surface.

`ValidationRoot` is a partial class organized by use case under
`validation/Features/`. ROM-backed scenarios and their dedicated execution
fixtures and helpers live together in `validation/Rom/`. Keep the runner and
ordered registration in `Features/Framework/Validation.cs`; put other scenarios
in the matching feature file. Fixtures, test doubles, observers, audit history,
and expected traces stay in the validation assembly.

Production may expose a narrow internal operation or observer when it is a
truthful view of the runtime owner. Do not add validation-only state machines,
public compatibility properties, permanent trace lists, sound request counts,
or cache histories to production classes.

Register the strongest comparison for a fixture. When a complete-frame ROM
scenario also checks state, OAM and the unobscured background, retire wrappers
that only replay those same checks. Retain independent import goldens,
unsupported-input diagnostics, save persistence and port-specific behavior.

Keep matrices focused on source branches and boundaries. Check independent
inputs separately instead of multiplying every data row by every button,
message speed and host schedule. ROM matrices use `RomHostSchedules` to repeat
their first case with multiple updates in one host frame; their remaining
cases use individual host frames. All cases still observe each gameplay update.
Keep additional batching fixtures when the behavior depends on the host-frame
boundary. Check before, during, at completion, after completion, cancellation
and repeated use. Bound waits by an observed completion state, and avoid
repeated pixel checks of a completed message solely to consume a fixed delay.

Use `ApplicationValidationFixture` to supply host input and advance the real
application loop, including frontend scenarios. It drives the host's input
buffer and fixed-update scheduler through typed internal operations. Its
optional observer runs after each complete update, outside the input snapshot
scope, in both split and batched calls. Fresh standard saves, default test
names, and room/transition convenience aliases belong in validation; the
production host owns session teardown and initialization from a supplied save.
Call available owner operations directly. Reserve reflection for source-state
probes that have no meaningful runtime operation or observable result.

Keep every phase of a scripted update sequence on explicit input samples,
including cancellation and restart. Calling `GameRoot._Process` captures live
Godot input again; a released key can remain just-pressed until the next Godot
frame and unexpectedly open a menu. Scenarios that deliberately inject such
host edges must release them and await a real frame before returning.

Scenarios may return `Task` to observe deferred Godot draw notifications. Suspend
automatic gameplay processing while awaiting host frames and advance gameplay
through the explicit fixture. The runner awaits each scenario before resetting
the next fixture; the launcher enforces its wall-clock timeout without a host-frame
quit limit.

Synchronous session resets explicitly free parentless nodes already queued for
deletion after replacing the old gameplay graph. Retired room actors are
detached before `QueueFree`; without this drain they can retain entire discarded
sessions until a real engine frame. Keep this cleanup at reset boundaries in
validation, preserve live orphan fixtures, and use a debug Godot build that
supports `Node.GetOrphanNodeIds`. Production retirement still follows the normal
engine frame. `-ProfileMemory` on the launcher logs managed heap, process memory,
and collection counts every sixteen resets without forcing collection.

## Shared-system regressions

Derive the expected contract from source callers and consumers, including raw
IDs, construction operands, collision effects and memory ownership. Do not use
the generated record being tested as the sole oracle, or update an expected
palette, counter or effect merely to match runtime output. For a bugfix, make
the focused comparison expose the original mismatch before changing production
behavior when practical; retain evidence boundaries in the handoff.

Observe both sides of a shared handoff. Allocation checks include actual slot
occupancy and failed-result continuation; combat checks include item-side flags
and the next status update as well as HP. Compare scratch writes and sound
requests at their publication phase, before later handlers can overwrite or
consume them. Native reference outputs must retain their own evolution; copying
runtime outputs back into the reference can conceal the defect being tested.

Choose cases from the source branches: available/full capacity, ordered partial
admission, earlier/later reused slots, accepted/rejected contact and the relevant
freeze gates. Include completion, cancellation and repeat use through the real
gameplay loop. Declare any staged geometry, masks or native entry boundary;
distinguish an executed mismatch from a source-only suspicion and a coverage
limit. Passing the registered suite does not close untested callers or states.

## Run validations

Build with optimization, then run the complete suite with the standard 8
workers. Agents must use this build command for both focused and full validation:

```powershell
dotnet build -t:Rebuild -p:Optimize=true
& .\tools\validate_parallel.ps1
```

This preserves all scenarios and assertions and uses the same Debug output
locations expected by Godot and the validation host. Build once, then reuse
those assemblies until source changes require another build. Rebuild explicitly;
an incremental build can retain the previous optimization setting. Agents may
use an unoptimized build only when the user explicitly requests it for debugging.
The launcher's elapsed time includes worker startup and shutdown, but excludes
the preceding build.

Run one exact registered method while developing:

```powershell
& .\tools\validate_parallel.ps1 -ValidateOnly ValidateMethodName
```

An unknown name fails. A focused run is a development aid; run the complete
suite before handoff.

ROM-backed comparisons are registered in the normal suite. The launcher
automatically resolves the same verified ROM cache used by the importer,
building it on first use. Use `-Rom PATH` to select an existing image;
the launcher passes the resolved path as `--validation-rom=PATH`.
Missing or unsupported ROMs fail explicitly when a ROM-backed scenario runs.
Use `-SkipRomValidation` for focused scenarios that do not need a ROM when
the build tools and cache are unavailable.

CI rebuilds the clean US ROM from a pinned disassembly revision, verifies its
MD5, and passes it with `-Rom` to every gameplay worker. Each worker also
checks the ROM's size and SHA-256 before reference execution. CI uses
`-FailOnEngineDiagnostics` to reject Godot errors or warnings in worker logs.
The validation workflow runs four gameplay partitions concurrently, each with
four workers and a 14-minute job limit including setup and the optimized build.
Gameplay has a 450-second launcher deadline inside an eight-minute step limit.
Importer verification runs alongside gameplay on a separate runner. The final
coverage check has a one-minute job limit.
`Build and headless validation` requires every job to pass, all 16 global
worker IDs, and exactly one successful timing record for every registered
scenario, with no skips. Missing partitions or duplicate scenarios fail it.
Only this complete merge refreshes the cached timing profile. A cache miss keeps
the same coverage using round-robin assignments. Worker stdout, stderr, and engine
logs are retained for seven days in `validation-worker-logs-*` artifacts, including
failed and timed-out runs. The release workflow retains its single-runner suite
with a 14-minute launcher deadline inside a 15-minute step limit.

Use `tools/validate_parallel.ps1 -SkipRomValidation` for an explicit run without
ROM-dependent scenarios. Mark these scenarios with `requiresRom: true` in the
ordered registration.
Skipping preserves shard assignments and reports each excluded scenario plus
separate passed/skipped totals; it never counts a skipped test as passed.
Normal local runs continue to execute every registered scenario.

Reference execution verifies the ROM's size and SHA-256 before running bounded
original routines with declared memory access. Tests compare resulting state
with production implementations without synchronizing elapsed CPU time.
ROM loading and comparison setup belong entirely to the validation assembly;
production continues to read generated assets only.
Each worker loads and verifies the ROM once, then shares a read-only image.
Registers and writable memory remain private to each execution fixture. Start
a new run to validate a changed ROM file.

When changing source-backed runtime symbol definitions, also run
`& .\tools\verify_runtime_symbols.ps1`. It checks annotated constants against
the active vanilla Ages disassembly through the importer source repository,
including enum order and game-specific definitions. It accepts `-Disassembly`
for a checkout outside the pinned `external/oracles-disasm` submodule. Runtime
code never reads the disassembly. This check complements the gameplay regressions
and does not replace them.

For a cold-start timing profile, run Godot with
`--max-fps 60 -- --validate --profile-startup`.
This bypasses the suite's already-warmed gameplay fixture, measures the actual
boot frames and a fresh new-file handoff, and exits without writing a save.
It reports setup, per-resource-stage work, total loading time, host frame
intervals and file-selection preparation. Use a rendered run and a realistic
frame cap when assessing animation and elapsed loading time: uncapped runs can
hide waits imposed by frame scheduling. Desktop timings do not establish
Android frame times.

8 workers is the launcher default. Override it with `-Workers` (1–64).
For a serial run when debugging:

```powershell
& .\tools\validate_parallel.ps1 -Workers 1
```

The launcher uses separate headless Godot processes; each keeps scene-tree,
input, RNG, and static cache access on its own main thread. The runner partitions
the ordered scenario registrations with `--validate-shard=INDEX/COUNT`
(one-based). Sharding cannot be combined with `--validate-only`. No separate
scenario list needs maintenance. Save regressions use unique temporary paths,
and each worker has separate engine and console logs in the printed temporary
directory. Build once before launching; do not rebuild or import during a run.
Complete full-suite runs record scenario costs in `.godot/validation-timings.tsv`.
The next full run snapshots that profile and assigns long scenarios first to
the least-loaded worker; each worker still executes in registration order.
Without timing history, assignment remains round-robin. New scenarios use the
median cost of known current registrations; removed scenarios are ignored.
This changes distribution only, preserving every scenario, assertion and update.
Use `-TimingProfile PATH` to freeze a profile for repeated measurements; an
explicit profile is read without refreshing the default. Focused runs and ROM
skip runs do not overwrite complete-suite timing history.
For distributed runs, `-PartitionIndex INDEX -PartitionCount COUNT` assigns a
contiguous group of global shards to this runner. `-Workers` is the number of
processes on each runner; every partition must use the same worker count and
timing-profile snapshot. Distributed runs require `-ResultPath PATH` and reject
focused filters and ROM skips. A partition success is not a full-suite pass.
`tools/Merge-ValidationResults.ps1` verifies every partition report and writes
the complete timing profile only after exact scenario coverage is established.
Ordinary local runs still default to one complete eight-worker suite.
Use this launcher for focused runs too: `-ValidateOnly` selects one scenario
in one worker. Each run uses a writable, unique engine log instead of Godot's
shared user-log rotation. On Windows, workers inherit an error mode that
suppresses native crash dialogs; crashes still fail the run and retain logs.
The calling PowerShell process's original error mode is restored on exit.
On Windows, the launcher creates every worker inside one job before its first
instruction. The job includes console-wrapper children and terminates remaining
descendants on close, including on timeout or interruption. It imposes no CPU
or RAM limits and preserves inherited process priority. The launcher still reports
the job's peak committed memory; `-ProfileMemory` retains per-process and managed
memory diagnostics for investigating leaks. Keep worker counts, scenario coverage,
timing profiles, and build settings fixed when comparing performance.
Run `tools/Test-ValidationLauncher.ps1` after changing the launcher; its fixture
checks the absence of resource caps and forced priority, descendant cleanup,
process flags, crash failure, log isolation, and completion handling without
requiring a game build.

`-Godot` overrides the executable and `-TimeoutSeconds` sets the overall deadline
(default 600 seconds). The launcher fails on a worker error, timeout, missing
completion marker, or incomplete scenario count, and stops remaining processes
on exit. `-FailOnEngineDiagnostics` also rejects engine errors and warnings in
each worker's stdout, stderr, and engine log, even with a successful exit and
completion marker. A successful parallel run covers the complete suite and
satisfies the full-suite handoff check. The serial command remains available
for debugging.

Use `-ContinueOnFailure` when measuring complete-suite performance in a worktree
with known scenario failures. Each failed scenario retains its diagnostic and
timing, then the worker resets the fixture and runs its remaining scenarios.
Passed, skipped, and failed counts remain separate; any failure still makes the
run fail. Setup failures and process crashes remain fatal. Use the same mode,
worker count, and scenario coverage for before/after measurements.

When deliberately simplifying coverage, also report removed registrations and
sampled dimensions. Such measurements describe a reduced suite and must not
be presented as an improvement with unchanged coverage. Preserve failure
diagnostics; a shorter run with a remaining failure is still a failing run.

Worker stdout logs include `VALIDATION_TIMING` records with invariant-culture
milliseconds for each scenario. `setup_ms` measures the initial gameplay graph
reset; `total_ms` includes that reset and the scenario body (including any
additional resets inside it). These are diagnostic measurements, not timing
assertions. Expensive independent ranges may be registered separately so
workers can share the work while preserving every case and update.

Importer/parser/schema changes also require:

```powershell
& .\tools\verify_oracle_import.ps1
```

Handoff checks:

```powershell
dotnet build -t:Rebuild -p:Optimize=true
& .\tools\validate_parallel.ps1
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

Synchronous scenarios share a Godot host frame. Releasing a native action does
not expire its just-pressed edge, so direct controller updates must use an
explicit `ApplicationInputSnapshot`, including neutral updates. Pair
`Input.BeginOriginalUpdate` with `Input.EndOriginalUpdate` in `finally`; tests
using the application scheduler supply samples through its input buffer.

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

Keep scenarios focused on individual gameplay systems and their immediate
interactions. Do not automate whole-dungeon progression or long routes that
chain unrelated combat, traversal, puzzles, and rewards.

Use the actual gameplay update loop and real room geometry when a system needs
integration coverage. Arrange the prerequisites directly, then exercise only
the mechanic, transition, or handoff under test.

Do not document the contents of every validation method here. The registered
methods and feature files are the current inventory; use `rg` to find them.
