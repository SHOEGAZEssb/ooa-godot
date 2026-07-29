# Validation

## Assembly boundary

Headless regression orchestration lives in the separate
`validation/oracle-of-ages.validation.csproj` project and
`validation/validation.tscn`. Production compilation excludes validation source.
The validation assembly references the built game assembly and receives a narrow
internal surface through `InternalsVisibleTo`.

`ValidationRoot` is one partial class organized by use case under
`validation/Features/`. Keep lifecycle and the ordered `ValidateAll` entry
point in `Features/Framework/Validation.cs`; place scenarios in the matching
`Validation.<Category>.cs` file. Shared test doubles are separate named-type
files beside the framework root. The validation project includes C# files
recursively while the production project continues to exclude
`validation/**/*.cs`.

Do not add validation-only state machines, audit masks, trace lists, or public
compatibility accessors to production classes. Observe externally meaningful
state or provide a small internal operation that is also a truthful view of the
runtime owner. Cutscene command tracing is attached by validation rather than
stored permanently on each event.

Operation history follows the same rule. Validation attaches narrow internal
observers to `OracleSoundEngine`, `OracleGraphicsCache`, and
`CombatController`; the validation assembly owns sound-request counts/order,
cache-operation traces, and spawned-clink references. Production retains only
real sequencer state, current cache contents, and spawned world nodes. Resetting
an audit resets the validation observer, never the runtime owner.

Room/entity scenarios construct managers through
`RoomEntityValidationFixture`. Its defaults provide the ordinary databases,
RNG, and runtime collaborators; `RoomEntityValidationOptions` supplies only
the exceptional save, inventory, database, clock, treasure, or room-session
owner required by a scenario. The fixture owns manager clearing/disposal and
can also own a temporary root. Keep it and other scenario setup helpers in the
validation assembly.

## Running the suite

Build first, then launch Godot with the project argument after `--`:

```powershell
dotnet build
$godot = 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate
git diff --check
git status --short
```

`--validate` runs the complete world-data and gameplay suite and selects
canonical rooms for individual scenarios. Validation save-store tests use an
isolated temporary directory and must never touch player slots.

Importer changes also require:

```powershell
& .\tools\import_oracles.ps1
```

Review generated diffs and, when practical, rerun to verify deterministic
output. A change is ready only with zero build warnings/errors, a passing full
suite, a clean `git diff --check`, and unrelated worktree changes preserved.

The suite exercises the shared generated-table reader with actual and escaped
tab headers, CRLF input, comments, trailing empty cells, every supported
primitive/sentinel parser, malformed cells, duplicate unique keys, and ordered
grouped/aliased/repeated rows. It also validates the checked-in manifest's exact
TSV inventory plus every schema version, record count, and SHA-256, alongside
focused failure cases for stale versions, counts, and checksums.

## Regression design

Every fixed bug or new gameplay system gets a focused headless regression. A
useful regression asserts the original cause and the visible/runtime result,
rather than only checking a clone implementation detail.

Use `FailIf(condition, message)` when a failed check only needs to throw a
validation error. Keep state-changing setup, values needed after the check,
cleanup, and expected-exception control flow explicit so evaluation order and
C# flow analysis remain clear.

Include as applicable:

- exact original-update boundaries, including first and final counter updates;
- update order among actors, entities, contacts, scripts, and transitions;
- imported identifiers, source labels, and malformed-data diagnostics;
- RNG calls and downstream state, not just the immediate random result;
- room entry from scroll and warp contexts, transition freeze, and re-entry;
- persistent flags, inventory, save/reload, backups, and high-index bitsets;
- actor position, facing, neutral/walking pose, Z, visibility, and deletion;
- graphics pixel hashes/offsets, audio channel state, and resource lifetime;
- every supported script branch and cancellation path.

Original object-movement regressions keep their expected angle and signed 8.8
vectors independent of `OracleObjectMovement`. Cover integer ratio decisions
on both sides of a band boundary, byte-coordinate wrap, low-byte carry, word
wrap, and a sufficiently long non-cardinal path to expose cumulative drift.
When a collision writes the opposite angle, assert the source-order lookup and
XOR separately rather than deriving the expectation from the runtime helper.

Use canonical rooms that exercise the real imported data. Failure messages
should include hexadecimal group/room/object IDs and expected/actual values so
the mismatch can be traced without reproducing it interactively first.

A validation must remain deterministic under a long rendered frame that causes
several fixed updates. Avoid tests that pass only because they call private
steps in an order the game never uses.

The application-scheduler regression compares N calls at `1/60` with one call
at `N/60`. Its validation-only pipeline must cross modal ownership, portal
activation, same-pass child creation and removal, pending room-warp dispatch,
HUD/animation/sequencer order, and held-versus-just-pressed input. Keep its
audit trace in the validation assembly.
