# Shared-system integration checklist

Use this with the relevant [subsystem guide](README.md#task-map) when adding or
changing gameplay. Apply the checks supported by that source path; keep the
concrete trace beside its implementation and focused regression. This is a
review of the whole handoff, including producer, allocator, dispatcher, effect
and consumer, rather than only the new object's handler.
The boxes are a reusable review aid, not a feature-completion ledger.

- [ ] **Source contract and imported inputs.** Trace callers and construction
  writes as well as the target handler. Retain raw identity, mode, damage,
  flags and fixed construction operands in generated data when needed. Keep
  values with different source meanings separate even when they share an ID
  or address. See [Data import](data-import.md#what-to-import).
- [ ] **Allocation result and footprint.** Use the authoritative pool and the
  source's physical slot count, including invisible helpers. Check admission
  before creating a node. Trace whether failure skips, retries, stops a child
  sequence or leaves parent work intact; preserve counts, flags, RNG and cues
  on their original sides of that decision. Unchecked native failure can still
  write through a retained pointer; trace the resulting alias and its consumer.
- [ ] **Slot lifetime and dispatch.** Enroll every supported native object,
  including controllers and effects, in its actual category and slot walk.
  Preserve earlier/later-slot birth timing and the parent's occupancy during
  child allocation. Use native same-slot replacement when the source replaces
  an object; do not turn it into a death followed by a fresh allocation.
- [ ] **Initialization, masks and RNG.** Trace state-zero dispatch separately
  from construction and preload, including common initialization RNG draws.
  Check category masks, enabled-bit exceptions, text, palette threads and
  scrolling independently. An always-update object can still have its own
  source guard. Keep input ownership separate from update eligibility.
- [ ] **Movement side effects.** Bind executed movement to the shared runtime
  memory owner. Preserve fixed-point arithmetic, flags and scratch writes even
  when a geometry-only calculation produces identical coordinates. Observe
  writes before a later phase can overwrite them.
- [ ] **Collision dispatch and metadata.** Follow the actual item/target scan
  through live identity, enable bits, mode, mask, XY/Z and status gates. Check
  active children and changing identities separately from placed parents.
  Represent the selected effect, including accepted no-op contacts and
  non-damaging notifications; do not force it into a generic damage response.
- [ ] **One signal owner.** Trace every writer, consumer and clear of pending
  hits and other shared signals. A species reaction may classify a common hit,
  but must not queue a second skipped update for the same publication. Preserve
  item-side effects as well as enemy-side effects, their next-eligible-update
  consumption, and their lifetime through freezes and cancellation. Keep health,
  deletion, room counts, drops and recent-defeat marks independent.
- [ ] **Script and control handoff.** Preserve source command carry/yield
  boundaries, native tails and exact waits even when combining operations
  produces the same final state. Use the shared command runner for source
  scripts; retain the original completion, cancellation and control-release
  owners. See [Command runner](command-runner.md#fixed-update-semantics).
- [ ] **Authoritative bytes.** Route typed and raw accesses, aliases and all
  readers/writers through the same live state owner. Do not maintain a second
  copy for an event, serialization or an audio feature. Commit to disk only at
  explicit source save boundaries. See [Saves and state](saves-and-state.md).
- [ ] **Presentation and sound.** Retain source palette/construction bytes
  separately from animation parameters. Apply native priority arithmetic before
  mapping to Godot Z, and preserve fixed priority when the source keeps it
  fixed. Request each cue through the sound owner at its source phase, with the
  original success/failure condition and no replay at expiry or re-entry. See
  [Graphics and audio](graphics-and-audio.md).
- [ ] **Independent integration evidence.** Exercise the actual gameplay
  handoff, not just its callback. Check relevant failure, freeze, completion,
  cancellation and repeat boundaries against independently traced source or
  native execution. Verify both metadata and downstream effects. Keep staged
  inputs and untested paths explicit; a suite pass does not prove universal
  parity. See [Validation](validation.md#shared-system-regressions).
