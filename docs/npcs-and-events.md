# NPCs and events

Visible characters are usually interaction objects, but their placement,
initialization, scripts, native handlers, and linked parts may have different
owners. Choose the runtime representation from those original owners, not from
the character's appearance or the apparent size of the scene.

```text
ROM/disassembly
    -> strict generated records with source identity
    -> typed runtime database
    -> room entity, linked interaction, or room event
    -> focused headless regression
```

## Choose the original owner

| Original behavior | Runtime representation |
| --- | --- |
| Positioned actor with ordinary animation, facing, solidity, and text | Imported NPC record plus ordinary room entity |
| State-0 deletes or retains an actor | Imported visibility predicate |
| Story state selects text or a static initial position | Imported dialogue or position rule |
| Native object code updates movement, counters, collision, or animation | Specialized room entity with narrow capabilities |
| Several object slots exchange signals or share a part | Linked interaction state owner, preserving one update per slot |
| Room entry coordinates Link, actors, dialogue, transitions, flags, or audio | `RoomEventController` event |
| An `interactionRunScript` stream drives behavior | Typed command runner inside the owning entity or event |
| Native cutscene, special-object, palette, or transition state machine | Specialized native controller |

Complexity is not the decision. A moving villager may remain an ordinary room
entity; a short sequence that owns input, room changes, or persistent story
state may require a room event.

## Evidence packet

Before implementing an interaction, record:

1. Placement and surrounding object order.
2. Interaction ID/subid dispatch, state 0, later states, and every caller.
3. Scripts, native helpers, created parts/interactions, shared temporary bytes,
   and cross-object signals.
4. Flag addresses, masks, aliases, branch precedence, and deletion conditions.
5. Per-object update order, counter initialization/decrement boundaries, and
   same-update versus next-update work.
6. Positions, collision radii, speed/angle/fixed-point arithmetic, animations,
   OAM, palettes, text, sounds, and input-disable masks.
7. Completion writes, room exit, cancellation, scrolling, re-entry, and
   save/reload behavior.

Keep original identifiers in generated rows, diagnostics, source comments, and
validation failures. “Room `1:49` missing interaction `$3c:$0e`” is actionable;
“missing NPC” is not.

## Data-driven NPCs

`Import-NpcData.ps1` owns ordinary placement, implementation classification,
graphics/animation inputs, visibility, dialogue, initial positions, and small
interaction-family tables. Runtime databases strictly load those records.

Use an imported predicate when initialization selects among source facts. Do
not snapshot story state at database construction; visibility and dialogue
must observe live state at the same boundary as the original. Preserve branch
order where several flags overlap.

An ordinary NPC entity owns only its actor behavior and presentation. It
registers dialogue or script interaction through the central interaction
router. It does not directly scan input, own a room-wide sequence, or duplicate
save state.

If an interaction classified as specialized reaches the generic path, fail
with group, room, interaction ID/subid, and source context. A placeholder actor
must never silently stand in for an unsupported native behavior.

## Native and linked interactions

Use a specialized `IRoomEntity` when object code owns per-update state that
cannot be expressed as ordinary NPC data. Give it only the capabilities its
shared systems require. Keep byte counters, fixed-point movement, animation
boundaries, collision ownership, and native signals explicit.

For linked interactions, preserve each original object slot and its update
position. A shared state owner may coordinate them, but it must not collapse
several independent counters or reorder their updates. Children created into a
later slot may run later in the same pass when the source does.

The entity lifecycle owner is also the interaction-target lifecycle owner.
Room change, natural deletion, or cancellation removes the target without
rescanning a changed collection halfway through an interaction.

## Room events

`RoomEventController` owns sequences whose original mechanism coordinates
several systems. Events use explicit entry predicates and priority; they do not
poll every room from unrelated controllers.

An event owns:

- the input/pause lease it acquires;
- its actors or registrations and their original update order;
- native state plus any typed script runners;
- transition-safe behavior explicitly allowed by source masks;
- completion writes and cancellation cleanup.

Event boundaries follow the original interaction/script, not a map area. Only
actors coordinated by one source sequence share an event owner; independent
dialogues, trades, room-entry scripts, and minigames remain separate even when
they reuse one imported database or live in adjacent rooms.

Ordinary destination events remain frozen during scrolling. Clear runners,
release input, detach registrations, and remove transient actors on cancellation
or room invalidation. Persistent completion is derived from authoritative save
or room flags, never an event-local boolean.

Use [Command runner](command-runner.md) only for original script streams. Waits,
text, or animation inside a native state machine are not a reason to convert it
to script commands.

## Implementation workflow

1. Build the evidence packet and select the original owner.
2. Extend `Import-NpcData.ps1` for ordinary/state-derived actor data or
   `Import-CutsceneData.ps1` for actual script/event records.
3. Add strict runtime loading; reject unsupported classifications and operands.
4. Implement the smallest ordinary entity, linked owner, native entity, or room
   event that preserves update order.
5. Route player interaction through the central registry.
6. Validate actor state, exact counters, source-ordered updates, dialogue and
   input boundaries, scroll/warp entry, cancellation, re-entry, and persistence.
7. Update the affected row, summary counts, boundaries, and snapshot date in
   [NPC interaction coverage](npc-interaction-coverage.md). This is required for
   implemented, partial, suppressed, or reclassified NPC records.
8. Update [implementation status](implementation-status.md) only if a broad
   player-visible boundary changed.

Useful starting points are `data/ages/interactions.s`,
`object_code/ages/interactions/`, `tools/import_oracles/Import-NpcData.ps1`,
`tools/import_oracles/Import-CutsceneData.ps1`, `src/Features/World/Entities/`,
`src/Features/Story/`, and `validation/Features/Npcs/` or
`validation/Features/Story/`.
