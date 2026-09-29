# Continuous TAS fidelity replay

The TAS runner feeds a recorded playthrough through the original emulator core
and the actual Godot application loop. Shared system adapters capture state at
every completed original update. A separate presentation stream compares visible
file-menu properties at every movie-frame endpoint, including during loading.
It stops at the first disagreement in either stream. Adding another encounter
on the same recorded route needs no encounter fixture.

## Setup and command

This Windows adapter supports the clean-US ROM and cold-boot Gambatte movies
made with **BizHawk 1.11.5**, using the published movie's exact sync settings.
The native Gambatte core and movie are included in the repository under
[`tools/fidelity/dependencies/`](../tools/fidelity/dependencies/), which is not
ignored by Git. Their upstream sources are the official
[BizHawk 1.11.5 portable release](https://github.com/TASEmulators/BizHawk/releases/tag/1.11.5)
and [scorpianman42's TAS publication](https://tasvideos.org/3127M).

From the repository root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/compare_tas.ps1
```

The launcher defaults to the bundled `scorpianman42-lozoracleofages.bk2` and
`bizhawk-1.11.5/` directory. Override them with `-Movie` and `-BizHawk`.
The BizHawk directory contains only `dll/libgambatte.dll`; the reference
transport uses that core directly. The GUI, other emulator cores, databases,
palettes, shaders, download archives, and source copies are not dependencies.
The movie is the inner BK2 from the TASVideos download ZIP; leave the BK2
container intact. The runner downloads nothing.

Dependencies are Python (standard library only), Windows .NET Framework v4's
C# compiler, the x86 Visual C++ 2010 runtime (`MSVCP100.dll` and `MSVCR100.dll`),
.NET for this project, Godot 4.7.1 .NET, generated assets, the
disassembly checkout, and the user's supported clean-US ROM. The launcher
builds the port once and compiles a separate x86 reference transport. It pins
the native core by SHA-256 and verifies the movie's ROM hash, version, settings,
controller columns, and cold-boot origin before executing. Different cores,
embedded SRAM/states, or unsupported settings fail explicitly.

`-Rom`, `-Disassembly`, `-Godot`, and `-Output` override paths. `-SkipBuild` uses
existing assemblies and records their hashes. `-MaxUpdates` and `-MaxFrames`
bound a development run. `-TimeoutSeconds` defaults to 600. `-BatchSize 8`
advances up to eight original updates within one Godot host frame, retaining
the distinct input sample and comparison boundary for every update.

The default follows the entire movie until the first difference or deadline.
Exit 0 means the compared prefix matches; 1 indicates drift and 2 an execution
or input error. Inspect `completeMovie` in the report: reaching a development
limit does not establish a full-movie match. Complete movie consumption means
all recorded input frames were executed and all reached comparison boundaries
matched; it does not independently certify the ending or 100% completion.

## Timing and ownership

[`TasReference.cs`](../tools/fidelity/TasReference.cs) hosts the unmodified
`libgambatte.dll` from the pinned release. It reproduces the release's
[`Gambatte.cs:FrameAdvance`](https://github.com/TASEmulators/BizHawk/blob/1.11.5/BizHawk.Emulation.Cores/Consoles/Nintendo/Gameboy/Gambatte.cs)
equal-length frame transport: 35,112 sound-sample periods, carrying instruction
overflow between frames. GBA CGB initial registers are enabled as the movie
requires. Audio hardware remains emulated. No ROM, gameplay RAM, RNG, or
sampled-input bytes are patched. Native memory access is read-only.

Movie frame numbers are zero-based emulator transport frames. They are not
original-update numbers: loading operations can span several video frames.
The reference captures snapshot 0 at the first `_mainLoop` entry, `$00:$0933`,
after ROM initialization and before the first `pollInput`. Each subsequent
snapshot follows the previous loop's threads, palette work, and VBlank. A hook
at `$00:$0936` reads the held and newly pressed buttons that `pollInput` actually
sampled. Both signatures are verified against the pinned clean ROM.

Godot starts its ordinary frontend before its first application update; asset
preparation consumes no simulated updates. Each reference input sample passes
through `ApplicationInputBuffer` and `ApplicationFixedUpdateScheduler`. Capture
occurs after `GameRoot.AdvanceApplicationUpdate`, including completion of its
pending loading work and all intervening sound timer interrupts.
The reference waits for comparison before executing another update. Thus the
first disagreement stops both streams without accumulating later noise.

The validation host substitutes the persistent file-store boundary with three
isolated in-memory slots. Actual file-menu, explicit-save, load, erase, and
restart decisions still execute. Loaded images are copies of committed images;
ordinary live mutations do not commit. Player save files are never opened or
overwritten. The reference uses its native cold-boot SRAM and never writes a
battery-save file to disk.

The production application clock advances audio during imported blocking loads.
The adapter supplies no reference elapsed time or audio tick counts to that
clock. CPU clocks and timer counts are diagnostic fields; shared-state values
still compare exactly. Loading coverage currently includes cold startup, title
initialization, file select, new-file options, name entry and commit, and the
message-speed confirmation and unlinked pregame initialization before the first
graphics-thread yield. Later game initialization and other menu/gameplay
loaders can still expose missing foreground work in the comparison.

## Trace contract and coverage

The default command runs both comparisons. Presentation uses a second isolated
Godot application driven by the movie's physical held buttons and elapsed CPU
clock. The original update adapter continues to use sampled inputs and its own
timing; its state checks receive no reference elapsed time. The presentation
clock selects observation times, without completing pending loads or copying
reference menu state into the port. Its actual clock is retained in diagnostics.

The native presentation adapter freezes BG tilemap/attributes, palettes and OAM
at scanline zero, then publishes that metadata only when Gambatte emits a video
frame. LCD-off and discarded LCD-on frames publish blank content; transport
frames without video output retain the last publication. This prevents pending
VRAM uploads or logical menu changes from being mistaken for visible changes.
No framebuffer is read and no pixel equality is required.

Profile `ages-menu-presentation-v1` compares file-menu visibility, the displayed
screen and message-speed overlay, the effective RGB5 fade level after GBA
brightening, and ordered acorn/text-speed cursor visibility and coordinates.
Page recognition uses static source tilemap/attribute regions, excluding dynamic
names and save summaries. The Godot adapter reads the displayed screen, not its
controller's pending page. A fully white menu exposes only visibility zero;
hidden page/cursor contents are not compared. The same zero value covers screens
outside this profile, whose status is retained in reference diagnostics.

Title/cinematic/gameplay presentation, name/secret-entry glyphs and cursors,
copy/erase confirmation overlays, and mid-scanout video mutations are not yet
mapped. Raw video metadata remains available in diagnostics for investigation.
Unsupported visible file-menu tilemaps fail explicitly instead of being guessed.
`coveredPresentationFrames` counts visible file-menu observations separately
from `matchedPresentationFrames`, which also includes blank/out-of-profile
observations. These counts do not claim full visual coverage.

Rendered startup audits use a separate validation mode, `--validate
--capture-startup-video`, with a real renderer (not `--headless`) and a 160 by
144 viewport. `--video-inputs=PATH` supplies the parsed movie's two-byte
held-buttons/power records. `--video-timeline=PATH` supplies a JSON array of
consecutive zero-based `frame` records with native CPU endpoints in
`fields["p_->cpu/cycleCounter_"]`. `--video-first=N`, `--video-last=N`, and
`--video-output=PATH` select the inclusive capture range and output directory.
The mode advances the production clock to each recorded observation time,
then saves the actual rendered PNG and a `frames.jsonl` metadata row. It does
not finish a pending load for a picture or resynchronize presentation states.
It uses the TAS host's isolated file store. Captures and audit reports belong
under `local-audits/`; they are not part of the shared-state pass/fail profile.

[`tas.schema.json`](../tools/fidelity/tas.schema.json) defines streaming format
version 3. A run has `manifest.json`, `rom.jsonl`, `godot.jsonl`,
`rom-presentation.jsonl`, `godot-presentation.jsonl`, engine/compiler logs, and
`comparison.json`. The manifest identifies both profiles, their boundaries,
presentation enum values, and movie/input/core/ROM hashes,
sync settings, verified hooks and source addresses, disassembly and port
revisions, working-file and assembly hashes, generated-data identity, limits,
and the supported/unavailable/out-of-scope groups.

Each update JSONL row contains `update`, `input`, `pressed`, exact integer `state`
fields, and producer-specific `diagnostics`. Presentation rows instead identify
`movieFrame`, physical `input`, and the observation's `cpuClocks`, with their
own integer `state` and `diagnostics`. The first divergence records its `layer`,
movie frame, fields and owners. No timing offsets or numerical
tolerances are accepted. Phase-dependent fields are present only when meaningful;
different phase/field sets are reported as disagreements. Raw diagnostics are
retained as evidence but are not claimed as equivalent across engines.

The shared profile compares:

- Global RNG bytes.
- Frontend stage and non-cinematic state.
- Sound fade, disable, and volume fields, plus eight channel enable/wait fields.
- During gameplay: room identity, Link's 8.8 position, direction, cutscene
  control and health; all sixteen enemy-slot occupancy bits in native page
  order; and the original live save payload `$c5ba-$caff`.

The live-save comparison excludes the committed checksum/signature bytes.
Inactive audio counters are excluded because their contents are not initialized
or interpreted while the channel is disabled. Cinematic frontend substates use
different representations and are explicitly unavailable. Enemy slots preserve
their original `$d080` through `$df80` identities; matching never sorts actors
by their current positions.

Full Link state/counter mapping, enemy IDs/species states, item/part pools,
placement buffers, file-select/new-game internal states, full graphics, and audio
samples are not covered yet. Hardware Power input fails explicitly when reached;
its Godot adaptation is unavailable. Add mappings by shared owning system and
test them independently. Change the profile/version when existing field meanings
or timing boundaries change.

This TAS uses glitches and skips content. A matching prefix establishes only
the declared fields along the visited route. Later mismatches can remain hidden
behind the first failure. The tool reports that frontier without automatically
resynchronizing either game or changing inputs.

Run `python tools/fidelity/test_tas.py` for movie/parser, shared-mapping and
presentation-mapping checks. Presentation checks use generated menu assets and
small executed-ROM metadata fixtures, not per-encounter input scripts. Gameplay
fixes still require the complete eight-worker validation suite.
