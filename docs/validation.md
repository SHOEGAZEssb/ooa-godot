# Validation

## Assembly boundary

Headless regression orchestration lives in the separate
`validation/oracle-of-ages.validation.csproj` project and
`validation/validation.tscn`. Production compilation excludes validation source.
The validation assembly references the built game assembly and receives a narrow
internal surface through `InternalsVisibleTo`.

Do not add validation-only state machines, audit masks, trace lists, or public
compatibility accessors to production classes. Observe externally meaningful
state or provide a small internal operation that is also a truthful view of the
runtime owner. Cutscene command tracing is attached by validation rather than
stored permanently on each event.

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

## Regression design

Every fixed bug or new gameplay system gets a focused headless regression. A
useful regression asserts the original cause and the visible/runtime result,
rather than only checking a clone implementation detail.

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

Use canonical rooms that exercise the real imported data. Failure messages
should include hexadecimal group/room/object IDs and expected/actual values so
the mismatch can be traced without reproducing it interactively first.

A validation must remain deterministic under a long rendered frame that causes
several fixed updates. Avoid tests that pass only because they call private
steps in an order the game never uses.
