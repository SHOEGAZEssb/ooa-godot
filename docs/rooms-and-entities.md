# Rooms and entities

## Room geometry and identity

Small room layouts are 10 by 8 metatiles, or 160 by 128 pixels. Large room
layouts use a 16 by 11-metatile storage grid with the original 16-byte row
stride; only 15 by 11 metatiles (240 by 176 pixels) are playable, and column 16
is padding.

A room is identified by group and hexadecimal room ID. Group aliases in save
tables do not by themselves make runtime rooms interchangeable. The original
tileset and object-pointer tables explicitly alias side-scrolling groups `$06`
and `$07` to source groups `$04` and `$05`; retain the active side-scrolling
identity while resolving their room tilesets and placed objects through that
source alias. Dungeon neighbors come from imported dungeon floor layouts, not
room-ID arithmetic. `RoomSession` owns the active identity and must be used for
neighbor and layout resolution.

## Side-scrolling passages

A tileset with `TILESETFLAG_SIDESCROLL` routes Link through the fixed-update
side-view controller. It samples the metatile at Link's center and eight pixels
below through the imported bitwise side-tile table. Ordinary dry movement is
horizontal only. Either sample carrying the ladder bit retains full directional
input, and a ladder-top sample applies the source ninth-pixel upward clamp.

Empty space starts an airborne state at speedZ zero. Each original update
applies signed 8.8 Y displacement, adds gravity `$24`, caps downward speed at
`$0300`, permits horizontal-only air control, and uses adjacent-wall masks
`$c0/$30` for ceilings and floors. Landing preserves the Y subpixel while
snapping the high byte with `(yh & $f8) + 1`, plays `SND_LAND`, and clears the
air state. The side-view Feather branch launches at `-$0230`, plays `SND_JUMP`
on its first airborne update, and uses the shared 9/9/6-update jump animation.

Groups `$06/$07` check imported vertical edge warps only. An uncovered edge
must not resolve through the aliased `$04/$05` dungeon layout or begin an
ordinary room scroll. Aquatic/lava/ice Link states and side-view moving or
disappearing platforms remain separate native systems; the dry controller
rejects those tile modes with a source-aware diagnostic until their handlers
are implemented.

## Transition lifetime

A top-down tile warp samples the metatile at Link's high-byte position
`(yh+4,xh)`, then applies the source's ten-pixel center window on each required
axis. A walkable single-tile warp checks both X and Y. A nonzero-collision warp
checks Y with its two-pixel offset and ignores X; horizontally adjacent warp
tiles likewise ignore X while retaining the collision-selected Y window. Do not
treat the complete 16-by-16 metatile as the activation area.

A scrolling transition keeps an active room/entity set and an outgoing set.
The destination room and its entities may be created before the scroll starts,
but ordinary destination entities and room events do not update until scrolling
finishes. Outgoing ordinary entities are likewise frozen while retained for
drawing. This prevents destination AI, cutscenes, drops, and counters from
fast-forwarding during the 32-update scroll. The boundary clamp and transition
motion preserve Link's 8.8 fractional bytes. Destination state-0 code may
overwrite the orthogonal high coordinate before the scroll begins; retain that
write because the original transition updater changes only the scrolling axis.
Once the final transition update marks scrolling complete, ordinary destination
logic may resume later in that same application update.

Before that freeze begins, `RoomEntityManager` completes the explicit
`IScreenTransitionPreloadRoomEntity` creation phase. This capability is only
for source state-0 presentation work needed to draw the incoming room:
initializing graphics, resolving visibility/deletion predicates, and creating
presentation children. Children marked `UpdateThisFrame` must declare the same
capability and are prepared recursively in source order. Missing declarations
fail with the spawn and entity types instead of leaving an object invisible
until the transition ends. This phase must not advance ordinary movement,
animation, counters, RNG, collision, or scripts.

When a room event releases dynamic actors during the destination-load callback,
it must drop its bookkeeping without deactivating nodes that
`RoomEntityManager` has already transferred to the outgoing set. Those nodes
remain visible and frozen until the scroll completes; the entity manager then
retires them. Fairies' Woods room `$0:$82` uses this path for discovered
`$49:$01` fairies and recreates them only when `$0:$82` becomes active again.

An object may update during a transition only when the original explicitly does
so. The retained Impa follower is one such behavior: it receives a separate
transition update path instead of allowing accumulated ordinary event time to
drain after the scroll. Her room `$0:$59` stone reaction clears both the global
following slot and the interaction's always-update bit, so dialogue in that
sequence must not replay the stale follower path; both are restored only when
the post-stone script returns to following.

The transition controller supplies draw offsets and updates the room camera.
Logical room coordinates stay in their original space. At completion, rebuild
state that the original rebuilds (for example a follower path buffer) rather
than carrying stale source-room history into the destination.

An imported edge warp with source transition `$03`, destination transition
`$03`, and destination position `$ff` uses the Maku Tree courtyard's full-load
tilemap reveal. `initializeVramMaps` first leaves the destination map blank.
That cleared map is not a solid-color fade: every map entry is tile `$00` with
attribute `$80`. Graphics-register state `$02` uses signed BG tile addressing,
so tile `$00` reads VRAM `$9000` bank 0; `loadCommonGraphics` has just loaded
`GFXH_HUD` there, making the clear tile `gfx_hud` tile 0 (solid shade 2)
through BG palette 0, with BG-over-OBJ priority. The screen-space cleared
columns must therefore cover world sprites such as the source Maku Tree face
while remaining below the HUD. `screenTransitionState0/1` then waits through
three initialization updates and copies one 8-pixel tile column per update for
32 updates, starting with columns 9 and 10 and alternating left and right from
there. The visible 20 columns are therefore complete after update 20, while
the transition still loads the 12 offscreen ring-buffer columns. Destination
transition `$03` keeps Link fixed offscreen until the column load completes,
then begins its separate 28-update entry walk. Select this from the imported
transition fields; do not key the effect to rooms `$38/$48`, substitute an
arbitrary flat white, or approximate it with a continuous mask.

Present Fairies' Woods has a source table that overrides ordinary overworld
neighbors while `GLOBALFLAG_FOREST_UNSCRAMBLED` is clear. Resolve that imported
nine-room, four-direction table in `RoomTransitionController`, before the
standard neighbor lookup. A zero table byte means "use the ordinary neighbor";
it is not an absent exit. Edge checks, `HasNeighborFor`, and the forced
southward ledge-scroll path must all call the same resolver so Link collision,
scroll availability, destination preload, and the actual transition cannot
disagree. After the completion flag is set, every direction delegates to the
normal room layout without requiring a room reload.

Every full room load runs the original era-display predicate after destination
entities are loaded. An outdoor, non-large-indoor room creates
`INTERAC_ERA_OR_SEASON_INFO $e0` for the tileset's present/past bit unless
global flag `$16` suppresses and clears that one display or
`wSentBackByStrangeForce` equals `$01`. Its fixed-update controller starts at
`$b0,$0a`, enters four pixels per update to X `$10`, holds for 40 updates, and
exits six pixels per update for six updates. Scrolling transitions do not run
this full-load path, but an existing display retains its native always-update
behavior while outgoing objects scroll. The new-game
`linkSummonedCutscene` is a distinct source load path: its state 0 loads the
arrival room without calling `checkDisplayEraOrSeasonInfo`, so the port also
skips the predicate for that one initial load instead of creating a transient
era display behind the summon fade.

## Ordered room objects and enemy reservations

Enemy placement executes one importer-generated ordered room-object stream. Do
not group records by species before creation. The original order determines
which fixed objects reserve positions before later random enemies.

Every random, fixed, and parameter-enemy record resolves its ID/subid through
the generated enemy handler registry before applying its placement opcode.
That typed resolution owns the implemented, dynamic/special, or deliberately
unsupported classification and is the sole construction-capability test used
by both ordinary room creation and dungeon enemy-shutter completeness. The
registry also retains the raw source collision-mode byte. For an implemented
row, the factory combines that byte with the placement flags, handler, source
location, and transient killable-enemy index in one typed combat source
descriptor. The placement opcode still owns slot and reservation behavior:
unsupported and event-owned dynamic/special rows must consume the same slots,
fixed positions, random-placement RNG, and reservation entries as an
implemented row even though the ordered factory does not create a node for
them. Do not restore a second supported-ID switch or treat a missing handler
key as unsupported.

Use one occupied-position set for the complete stream:

- Every applicable fixed enemy or part reserves its packed tile before later
  random placement.
- Random enemies reserve the accepted tile immediately.
- Unsupported objects that reserve space remain in the stream as explicit
  reservation-only records.
- Do not clear reservations between Keese, Octoroks, ordinary Stalfos, Zols,
  Gels, or another species group.

If a placement still appears unusual, trace the original validity routine and
entry context before changing tile filters. A visually implausible water spawn
may indicate a missed terrain restriction, an incorrect packed coordinate, a
lost reservation, or the wrong transition exclusion region.

## Placement RNG and entry exclusions

`OracleRandom` is game-wide because enemy AI, drops, sounds, and placement share
the original RNG stream. At the beginning of every room-object parse, regenerate
the placement buffer and reset its index. Regeneration deliberately consumes
the original 256 global RNG calls; reusing a previous permutation changes later
AI and drops even if the placements happen to look valid.

`EnemyPlacementContext` describes why the room was loaded:

- `Unrestricted` is used when there is no incoming exclusion.
- `Scrolling` excludes the original rows or columns near Link's incoming edge.
- `Warp` excludes the surrounding 5 by 5-metatile area around the packed
  destination.
- `ScreenWarp` retains the same incoming-edge enemy exclusion as upward
  scrolling for destination bytes `$f0`-`$ff`, while remaining distinguishable
  to whiteout-only room interactions such as dungeon-stuff `$12:$00`.

Do not infer the context from Link's eventual position after entities have
already been parsed. Pass it from the transition/load operation that owns the
entry.

## Harp and time portals

`HarpController` owns playable `ITEM_HARP`, not the scripted teaching
performance in room `3:ae`. It reads the imported 260-update song contract and
full `LINK_ANIM_MODE_HARP_2` sequence, requests the selected tune sound once,
and creates a floating note every 32 updates on the side selected by the live
animation parameter. Each note consumes exactly one game-wide RNG value for
its horizontal drift; note sway and the 70-update lifetime remain local effect
state.

During playback the source `wDisabledObjects=$7e` mask freezes ordinary room
objects. `GameRoot` advances only the global frame, the Harp effect, and
`TimePortal` observers; destination preloading, contacts, enemies, and other
interactions do not update. At the song boundary, prohibited tileset bits
`$7e`, a missing Echoes spot, or present-era Currents open imported TX `$5110`.
Currents in the past and Ages in either era enter the normal time-warp
controller.

Ordinary `INTERAC_TIMEPORTAL_SPAWNER $e1:$00` records must be emitted in room
object order even while invisible. Tune of Echoes sets room flag `$02`, waits
for `wLinkPlayingInstrument` to clear, requests `SNDCTRL_STOP_SFX` and
`SND_TELEPORT`, then exposes the portal. Its contact transition is the same
placed-portal path already used by active subtypes.

Direct song warps create `INTERAC_TIMEPORTAL $de:$00` at Link's packed
destination position. The temporary portal uses common sprite tile base
`$4a`, palette 1, an alternating palette cycle, and combined collision radius
9. It first requires Link to leave its contact area, then clears the saved
portal marker and returns through the paired time-warp transition. Ordinary
room parsing recreates it from the saved group, room, and position, so its
lifetime is not an event-local boolean. The imported entry and return
replacement dictionaries clear the exact source breakable metatiles under
direct arrivals, saved portals, and portal returns.

## Maple encounters

`checkAndSpawnMaple` runs before the room enemy/item pointer. In one of the 119
imported group `$00/$01` locations, `wMapleKillCounter` reaching 30 creates
Maple and resets the counter; an equipped Maple's Ring lowers that exact
boundary to 15. Ordinary interactions, ground treasure, and portals already
emitted for the room remain, but Maple suppresses the complete enemy/item
pointer through the original `wcc85` skip. Do not approximate eligible rooms
from geography or suppress the entire room entity set.

`MapleEncounter` owns the native special-object states: the invisible
animation-`$19` shadow route, meeting-count-based broom/vacuum/UFO and movement
variation, collision recoil, 15-update horizontal-only shared-RNG shake,
120-update ground wait, greeting, item race, score dialogue, and departure.
Visible bit 6 gives her the fixed one-cell terrain-effect shadow on alternating
updates while airborne; do not substitute the larger `PART_SHADOW` actor.
Flight positions remain unsigned 8.8 object coordinates: paths starting near
`$f0` intentionally wrap across `$ff->$00` before Maple enters the viewport.
Do not replace that wrap with unbounded world coordinates.
Screen transitions are disabled from collision through departure. Ordinary
objects remain enabled during Link's initial 24-update knockback, then the
encounter freezes Link and ordinary object input for Maple's recovery and
greeting before releasing the item race. The shared screen-transition handler
still clamps Link's high coordinate to `$06` or the far room boundary before
honoring that lock, matching `screenTransitionState2`; disabling the transition
must not let Link walk beyond the current screen.

Scattered `PART_ITEM_FROM_MAPLE $14/$15` records are independent fixed-update
entities sharing one encounter slot list and the game-wide RNG. Each consumes
two RNG values on its creation update, follows the source 8.8 bounce and screen
clamp, becomes collectible by Link only after settling, and can disappear on a
terrain hazard. Maple selects unique IDs `$00-$04` first in ascending order,
then normal IDs `$05-$0d`; equal-distance normal items select the later part
slot. Reward collection preserves tier-ring RNG, Joy Ring quantities, the
Potion sound override, and the held Heart Piece path without setting the
room-item flag.

The Ages Touching Book branch replaces all scattered drops. It retains
`wMapleState` bit 4 while active, runs TX `$070d-$0711` through the separate
book flight/chase, grants and presents the Magic Oar, sets completion bit 5,
then clears the temporary bit and increments the capped meeting count on
departure. While the vacuum is collecting, a touching grounded `ITEM_BOMB`
releases Maple's current target, resets the Bomb fuse, moves one pixel per axis
toward Maple, then raises by `$0040` until `zh=$f8`. The Bomb is removed and
Maple enters the existing source-timed stun path.

## NPC A-button routing

`linkInteractWithAButtonSensitiveObjects` probes ten pixels in Link's facing
direction, walks `wAButtonSensitiveObjectList` in insertion order, and stops at
the first object whose strict high-byte collision radii contain that point.
`RoomEntityManager.FindNpcInteractionTarget` preserves the same active entity
order and resolves the matching `INpcTalkLifecycle` owner at the same time.
The returned `NpcInteractionTarget` is the only begin/end/cancel token for that
talk; do not restore a second lifecycle scan after the room collection may have
changed.

After target selection, `NpcInteractionRouter` applies one source-labelled
first-claim sequence: family naming, event-owned actor handlers, typed
`interactionRunScript` hosts, and ordinary dialogue. Player-only shop handling
runs only when no NPC target exists. Chest/sign/tile probing remains later,
matching Link's source handler. Registrations define priority only; Fairies'
Woods, shops, story actors, and typed scripts retain their own update and
room-change cancellation state machines.

Event-owned native interactions may toggle their textbox-update bit from a
script rather than carrying it for their whole lifetime. Room `$0:$39`'s
`INTERAC_BIRD` sets bit 7 only after Link talks to it, so its
`interactionAnimateAsNpc` frames and repeating hop continue during TX `$3214`;
the other audience interactions remain frozen during their textboxes. In the
later possession sequence, follower Impa retains the bit after
`clearFollowingLinkObject` clears only the global follower slot, and
`INTERAC_GHOST_VERAN $3e:$00` sets it during initialization. Their animation
handlers therefore continue under the intro textboxes. Nayru, Ralph, Human
Veran, and the aftermath actors do not opt in and remain frozen.

## Entity contracts

`RoomEntityManager` composes behavior through small capabilities rather than a
single universal entity base class:

| Contract | Meaning |
| --- | --- |
| `IVariableRoomEntity` | Delta-driven presentation or behavior that is intentionally variable |
| `IFixedRoomEntity` | One original-engine update with deterministic spawn output |
| `ILinkContactEntity` | Post-update Link contact handling |
| `ISwordHittableRoomEntity` | Sword collision and hit response |
| `IObjectCollisionHeightRoomEntity` | Optional object `zh` for item/enemy collision; absent means ground height |
| `ISeedHittableRoomEntity` / `ISeedProjectileRoomEntity` | Active seed hit response and one-shot projectile collision ownership |
| `IPlayerProjectileRoomEntity` | Player-owned projectile bounds, damage, and accepted-hit completion |
| `IRoomBlocker` / `ITalkTarget` | Collision or interaction capability |
| `IOrdinaryNpcEntity` | A placed NPC eligible for live imported save-predicate refresh |
| `IPlayerRestriction` | Native interaction-owned sword and/or movement input suppression |
| `IRoomEntityLifetime` | Completion; `OnFinished` is an optional final spawn/effect hook |
| `IRoomEnemyCounterEntity` | A live combat enemy or native puzzle sentinel contributing to `wNumEnemies` |
| `IRoomEnemyOutcomeSource` | One-shot source outcomes whose room-count, recent-defeat, and global kill-counter effects are independent |
| `IRoomSaveStateEntity` | Refresh from changed live save state |

`RoomEntityManager` evaluates each `IPlayerRestriction` capability through one
ordered predicate query; movement retains its separate sword-disable frame
special case. Truly identical short-lived visuals derive from
`FixedEffectNode2D` and use `FixedEffectRoomEntityAdapter<T>` (or its
dialogue-update variant) for fixed updates, transition offset, and lifetime.
This adapter is not a substitute for NPC, combat, contact, or source-order
capabilities. `IUpdatesDuringDialogueRoomEntity` defaults to updating; only
state-dependent implementations override that decision.

Do not infer an enemy outcome from Godot entity removal. `RoomEnemyOutcome`
represents the source paths separately: counted `enemyDie`,
`enemyDie_uncounted`, room-count decrement, hazard or replacement deletion,
silent deletion, Wallmaster spawner completion, boss teardown, and placement
consumption. Each outcome states independently whether it removes a
`wNumEnemies` contribution, marks the transient recent-defeat index, or
advances the shared Slayer/Maple/Gasha kill counters. The manager drains
pending outcomes every update before lifetime cleanup because an owner such as
Wallmaster may emit hand-death outcomes while its spawner remains alive.

`EnemyCombatDescriptor` owns the ordinary adapter contract for contact damage,
sword and burn routing, death-puff/drop identity, room-count participation,
killable-enemy index, and completion outcome. Its
`EnemyCombatSourceDescriptor` preserves the source ID/subid, raw collision
mode, placement flags, handler, and diagnostic location; room-count exemption
comes from object flag `$02`, and each species' sword response must agree with
the imported collision mode. Species with unusual lifecycles compose a custom
combat component or completion outcome from the same source descriptor.
`Special` descriptors are reserved for genuinely non-ordinary entities such as
bosses and their child parts, where source placement policy does not apply.

Shared combat, terrain movement, vertical motion, and animation components may
remove mechanical duplication. Single-body enemies inherit the record-neutral
`EnemyCharacter`, which owns health/lifetime state, collision radii, the
`EnemyAnimationPlayer`, and optional invulnerability rendering. Each species
still owns its typed imported record, decisions, counters, movement, hit/death
policy, and RNG consumption. Multi-part enemies whose independently animated
parts have different health or collision state, such as Pumpkin Head, compose
those mechanics directly instead of forcing them into one character tuple.
Spawn records state whether a child updates in the creation frame; preserve
that distinction. Drawable room nodes ultimately inherit
`TransitionOffsetNode2D`; it owns only the presentation offset applied during
scrolling and never changes logical room/world coordinates.

`OracleObjectMovement` is the game-wide owner of original object movement. It
combines the complete generated `bank3.objectSpeedTable`—all 24 source speeds
and 32 angles as signed 8.8 Y/X components—with the 64 generated
`bank0.pushDirectionData` results used by `objectGetRelativeAngle`. The angle
path uses byte high positions, the source's integer ratio bands, and its
add-eight unsigned subtraction; the latter deliberately places the coordinate
wrap boundary at `$f8`. Collision knockback first aims from the struck object
toward Link or the item and then XORs `$10`, matching the source write rather
than reversing a host-computed vector.

Objects that call `objectApplySpeed` retain unsigned wrapping 8.8 Y/X words
between updates. Signed table components add with low-byte carry and whole-word
wrap; logical state and source collision probes read the high bytes. Drawable
object presentation additionally converts camera-relative high bytes
`$f8-$ff` to `-8..-1`, matching the partial Game Boy OAM rows and columns at
the top and left viewport edges without changing the stored position.
Maple and her drops, scripted Impa/Nayru actors, Fairies' Woods flights,
Graveyard ghost children, Black Tower workers, Gasha nuts, tree seeds, essence
beads, Running Bipin, moving platforms, push blocks, item/shovel debris,
cutscene command motion, enemy walking/flying/recoil/hole pull, bosses,
boomerangs, and hostile projectiles all use this owner. Their state machines
still select source speeds/angles and preserve counter and RNG order; no
gameplay path reconstructs 32-step vectors or relative angles with host
trigonometry.

`EnemyAdjacentWallResolver` owns the generated
`ecom_sideviewAdjacentWallOffsetTable` and
`ecom_topDownAdjacentWallOffsetTable` plus
`ecom_bounceOffScreenBoundary@angleTable`. It rounds a 32-step angle to the
source octant, floors the object's room position once, then applies all four
signed Y/X pairs cumulatively. Collision results from the first two probes
block Y movement (`$0c`); the last two block X movement (`$03`). Keese screen
reflection, ordinary Stalfos wall/hole reflection, and common enemy knockback
share the side-view stream and supply only their collision predicate. Spiny
Beetle's covered charge uses the separate top-down stream. Common walking
movement preserves the source `$0060` wall-slide displacement after a blocked
axis rather than substituting host collision response.

Sword recoil is selected by the original item-collision and enemy-collision
tables, not by damage. Level-1 swings, the held sword, and Fist Ring punches
use the low profile; level-2/3 swings and sword beams use normal; Spin Attacks
and Expert's Ring punches use high. For enemy modes that map those collisions
to `COLLISIONEFFECT_SWORD_*_KNOCKBACK`, the accepted hit writes
invincibility/knockback counters `$10/$08`, `$15/$0b`, or `$1a/$0f`, aims from
the attack source away through the enemy, and runs `SPEED_200` movement after
the counter's pre-handler decrement. Movement uses the signed 8.8
`bank3.objectSpeedTable` components and the shared cumulative adjacent-wall
probes even for top-down enemies. A blocked movement clears the remaining
counter. Species whose handler calls the
solid form use terrain collision and perform their usual post-movement hazard
check; Ghini, Keese, Crow, and Giant Ghini children use the
screen-boundary-only form. A health-zero hit disables collision immediately
but remains visible through every recoil update; the death handler and puff
run on the update after the counter reaches zero, at the final position. Zols
and Gels map sword rows to
`COLLISIONEFFECT_SWORD_NO_KNOCKBACK`: they receive `$20` invincibility updates
without a recoil counter. Boss-specific collision modes retain their own
no-recoil response. While a positive `invincibilityCounter` remains, ordinary
enemies select OBJ palette `$05` whenever global `wFrameCounter` bit 2 is
clear, restoring their source palette when it is set or the counter reaches
zero. An enemy whose source palette is already `$05` flashes with palette
`$02`, matching the common post-update palette path.
Hardhat Beetle collision mode `$b8` instead maps non-high sword rows to
21 invincibility / 11 recoil updates and high rows to 26 / 15 without health
damage or accepted-hit audio. Its negative internal invincibility countdown
preserves the source no-damage, no-blink interval while still rejecting a
second hit.

Enemy hazard handling follows `ecom_checkHazards` rather than sampling only
the object's center. Grounded supported species test `yh+$05,xh-$01` first and
`yh+$05,xh+$01` second, nudge `xh` one pixel toward the accepted side, clear
invincibility and recoil, and disable collision. Water and lava delete the
enemy immediately into `INTERAC_SPLASH` or `INTERAC_LAVASPLASH` with
`SND_SPLASH`. Holes keep the enemy visible for a 60-update `counter1`: every
eighth update it takes one signed-8.8 `SPEED_80` step toward the metatile
center and may finish early when both high-byte coordinates are centered.
Only then is the enemy replaced by `INTERAC_FALLDOWNHOLE $0f:$00`, which owns
the imported terminal animation and `SND_FALLINHOLE`. The ordinary helper
subtracts three from `animCounter` on every pull update; Zols and Gels use the
source no-animation variant and remain on their last frame. A negative `zh`
suppresses the initial hazard test until the enemy lands. The same path runs
after accepted sword recoil, so hazard disposal never creates a normal enemy
death puff or item drop.

`EnemyDatabase` keeps one ordered room-object collection as the placement
authority. Keese, Octorok, Stalfos, Zol, Gel, Crow, Hardhat Beetle, and Spiny
Beetle graphics, attributes, and animations are unique ID/subid definitions;
the factory joins a definition to the ordered record that supplies group,
room, source order, opcode, flags, count, condition, and coordinates.
Validation performs the same join and derives species totals from the ordered
records rather than maintaining parallel room indexes.

State-machine lookup and state-entry operands are generated data.
`EnemyBehaviorTables` provides one strict runtime owner for 188 rows: 77
Keese/Octorok/Boomerang Moblin lookup counters, enemy-arrow directional
geometry, Giant Ghini child offsets, and Pumpkin Head
timing/follower/projectile records, plus 111 typed sword, recoil, hazard,
bounce, speed, counter, gravity, bounds, and projectile profiles. Consumers
index lookup records at the same RNG/state/direction boundary as the source;
they do not reorder the Giant Ghini `3,2,1` child allocation or Pumpkin Head's
`0,2,1` projectile creation. Native state machines still own transitions and
branching, but state-entry fields come from the typed generated profiles.

Ordinary enemy species are not owned by the first room or dungeon that makes
them playable. Boomerang Moblin, Arrow Moblin, Rope, Ghini, Wallmaster,
Hardhat Beetle, and Spiny Beetle live with the other species and resolve their
implemented definitions through `EnemyDatabase` for every matching ordered
room record. Unsupported Arrow Moblin, Rope, and Ghini subids remain explicit
reservations rather than silently receiving the wrong state machine.
Wallmaster capture resolves the active dungeon's imported
`wDungeonWallmasterDestRoom`; it does not encode Spirit's Grave room `4:24` in
the entity adapter. Its source placement also owns the spawner count: room
`4:12` supplies five hands and dungeon `$0b` room `4:c5` supplies two.

`ENEMY_HARDHAT_BEETLE $4d:$00` has 12 ordered records / 15 instances. Its
single state tracks Link's exact relative angle every update, moves at
`SPEED_60` through side-view no-hole probes, and uses collision mode `$b8`'s
sword-bump response. `ENEMY_SPINY_BEETLE $1b:$01` has 11 records. Its
uncounted `ENEMY_BUSH_OR_ROCK` child mimics dungeon metatile `$20` in front of
the hidden radius-3 parent. Axis alignment within 12 pixels selects a cardinal
56-update `SPEED_e0` charge except upward proximity charges; contact may select
any cardinal direction. The cover rises to Z `-4`, returns for a 30-update
rest, and can independently be cut, Ember-burned, or carried/thrown with the
Power Bracelet. Losing it reveals the radius-6 parent for 60 updates before
40-update shared-RNG cardinal wanders.

`ENEMY_ARROW_MOBLIN $0c:$00` selects a cardinal direction and then a
`$30+(RNG&$3f)` movement duration on its first update. It moves at `SPEED_80`
without entering holes, stands for eight updates when the counter expires or
movement is blocked, and repeats the direction-then-duration RNG order.
`var30` makes only odd-numbered route changes eligible to fire, and even those
create `PART_ENEMY_ARROW $1a` only when the selected direction faces Link.
The child receives its directional offset, collision radii, animation, and
visibility on the creation update without moving; its `SPEED_200` flight begins
on the following update. Room `0:84` is the canonical single-red-Moblin
placement.

`HostileProjectileLifecycle` owns the common Octorok-rock and enemy-arrow
visible-boundary check, Link/shield contact, room lifetime, and
`partCommon_bounceWhenCollisionsEnabled` path. Its typed profile keeps behavior
that is not common in explicit source order. `PART_OCTOROK_PROJECTILE $18`
probes the destination, applies its last `SPEED_200` step into a solid tile,
and initializes the reversed bounce from state 2 on the next update.
`PART_ENEMY_ARROW $1a` probes its current tile and initializes the bounce
immediately without that movement. Both then use `SPEED_40`, speedZ `-$00e0`,
gravity `$0e`, and delete when the `$20`-update counter reaches zero. Their
imported normal/directional and bounce animations stay with the concrete
projectile nodes. The generic room adapter applies the same fixed-update,
sword, scrolling-offset, and completion ownership to both; owner-returning
Moblin boomerangs keep their specialized outbound/return state machine.

Side-view terrain movement must preserve the source velocity table's exact zero
components for cardinal angles. A blocked cardinal move returns zero; it must
not test the unchanged perpendicular coordinate and report success. Rope
`$10:$00` depends on that return value: `objectCheckCenteredWithLink` accepts an
inclusive ten-pixel match on either axis. Its initialization and ordinary
wander both use `SPEED_60`; the Rope takes one fixed cardinal `SPEED_140` lock,
and only a wall/hole collision ends the charge. That collision restores
`SPEED_60`, sets `counter2` to `$40`, and calls `rope_changeDirection`; the
charge does not continuously retarget Link.

## Ledge-jump ownership

`LedgeJumpDatabase` is the typed runtime authority for
`checkLinkJumpingOffCliff`, the collision-set-specific cliff and solid-landing
exceptions, and `LINK_STATE_JUMPING_DOWN_LEDGE`. `TerrainController` accepts
only cardinal motion whose movement angle matches Link's facing, requires both
imported adjacent-wall bits, and tests both imported signed cliff probes.
Landing search starts at Link `yh+$05`, advances eight pixels in the movement
direction, allows holes through the collision query, and treats a solid tile
as landable only when source `$05` can break it or the active collision set
lists it as a raisable-floor exception. A zero tile or room boundary selects
the original transition branch; do not replace the scan with a fixed
two-metatile destination.

`Player` owns the resulting fixed-update state. An in-room jump begins at
speedZ `-$1c0`, applies gravity `$20`, and selects one of the 11 imported
planar speeds from the capped cliff length. It uses
`LINK_ANIM_MODE_JUMP`'s 9/9/6-update phases followed by the terminal frame,
requests `SND_JUMP` on initialization, and draws the universal alternating
terrain shadow while Z is negative. Room-entity contact and floor-button
weight remain disabled for the entire bit-7 airborne state.

When the landing scan reaches a boundary, preserve the original special case:
snap Link's ground Y to `wScreenTransitionBoundaryY`, retain the signed
difference in Z, set planar speed to zero and speedZ to `-$100`, disable the
shadow, and force transition `$82` only when that fall reaches Z zero. Link's
ledge physics and animation remain frozen throughout the ordinary scrolling
transition. After destination scrolling completes, scan downward again, move
the ground coordinate to that landing, restore the equivalent signed Z and
shadow, and continue with the retained positive speedZ. Only this cross-screen
landing updates the local respawn position.

Every landing probes `yh+$05` through
`BREAKABLETILESOURCE_LANDED $05` before clearing the airborne state and
requesting `SND_LAND $a3`. Replacement, drops, debris, solve sound, Gasha
maturity, and linked room flags remain owned by the shared breakable-tile
record. The source's original-layout restoration for diamond switches and
collision-set-1/2 moving pots applies to this path as it does to Bracelet
breakage.

## Bracelet tile and entity ownership

`BraceletController` owns `ITEM_BRACELET $16`'s parent/child lifetime rather
than treating a lift as an immediate tile deletion. Its wall test consumes the
same paired `w1Link.adjacentWallsBitset` edge used by movement collision; a
single solid point is insufficient. The controller then owns the opposite-
direction pull gate, Link animation modes, movement/turning lock, carried
offsets, either-button release, weight-0 8.8 flight, and interruption cleanup.
At release, a held direction selects Link's current input-resolved facing. With
no direction, the parent preserves item angle `$ff`; `itemBeginThrow` still
applies the one-pixel retained-facing offset but clears planar speed and
`speedZ`, so the object falls straight down from the carried height. Lifted
metatiles and native grabbable entities use the same distinction.
`BreakableTileDatabase` remains the authority for whether the active collision
set accepts `BREAKABLETILESOURCE_BRACELET`, the replacement tile, drop,
persistent flags, and stored impact interaction.

Damage interruption mirrors `dropLinkHeldItem`: the held child is released at
its current height with motionless angle `$ff`, zero lateral speed, and zero
initial Z speed. It then retains its independent item update while Link's
knockback or death state owns the player update, so weight gravity begins on
the next original update instead of waiting for Link to accept item input
again.

Build the lifted graphic before replacing the room metatile. It is a live
`itemMimicBgTile` snapshot, so it must retain position mapping overrides,
animated/dynamic BG tile sources, X/Y flips, and the active room palette while
making BG color 0 transparent in OBJ palette 7. The room layout changes at the
successful lift boundary; the stored interaction is not created until the
thrown tile lands or collides with a wall. Water, lava, and holes run the item
hazard replacement first and suppress that debris.

Native grabbable entities implement `IBraceletInteractableRoomEntity`, but do
not own a second Link item state machine. The shared controller wraps their
accepted lift and release in the same 13-update lift, eight-update throw,
offset, and sound boundaries. The entity continues to own its body-specific
motion and outcome; Pumpkin Head's head/ghost collision is the current example.
Thrown metatiles use enemy/part collision capabilities for damage and continue
flying when the original object-collision table applies damage only to the
target. Their planar collision remains centered on the item's ground-space
`yh/xh`; rendered Z never shifts that rectangle. After lateral and vertical
item motion, the enemy pass separately accepts only target/item `zh` values
within the source's strict seven-pixel range. A landing tile is replaced before
that pass and cannot apply one final airborne hit.

Link's standing-state A-button arbitration checks button-sensitive entities and
`interactWithTileBeforeLink` before allocating an equipped parent item. Chests,
signs, and keyholes therefore retain priority when the Bracelet is equipped to
A. A failed Bracelet pull against an unbreakable wall holds
`LINK_ANIM_MODE_LIFT_3`'s terminal strain frame while retrying the tile probe;
it does not restart the 11-update pull animation.

## Bomb parent, child, and explosion ownership

`BombController` owns `ITEM_BOMB $03`'s Link-side parent. A use first searches
the active entity list for a touching live unexploded Bomb and lifts that actor
without consuming ammo. Only then does it test packed-BCD Bomb inventory and
the active-object cap: one normally or two with Bomber's Ring. A successful
allocation creates exactly one child before decrementing ammo. The parent
shares the imported 7/4/2 lift offsets and eight-update throw pose with the
Bracelet; either item button throws a held Bomb, and Toss Ring selects
`SPEED_280` instead of `SPEED_180`. A held direction supplies the throw angle;
without one, angle `$ff` clears both speeds after the one-pixel retained-facing
offset and the Bomb drops in place.

`BombEffect` remains in the fixed room-entity order from allocation through
deletion. It owns the imported OAM animation, 116-update fuse, held Peace Ring
reset, signed 8.8 weight-0 gravity, direction-specific wall probes, reduced
bounce speeds, conveyors, landing sound, and terrain-hazard removal. Damage
interruption releases a held Bomb motionless at its current height; room
changes and cutscene/death cleanup discard it with the other carried-object
state.

Explosion animation parameters are the collision radii and terminal flags.
The room manager applies the current Blast Ring-adjusted damage to overlapping
enemy collision owners, while the Bomb applies the same own-Bomb source to
Link so Bombproof Ring can reject it. One
`BREAKABLETILESOURCE_BOMB $04` probe runs per explosion update in the original
reverse table order: center, cardinals, then diagonals. Replacement, drops,
debris, solve sound, persistent flags, and linked-room effects stay owned by
`BreakableTileDatabase`.

Room `0:50`'s Bomb Upgrade Fairy trigger and capacity-upgrade cutscene are a
separate deferred interaction; ordinary Bomb allocation must not special-case
that room.

Common sign/chest outcomes that do not come from a room lookup are owned by
`TileInteractionFallbackDatabase`. Wrong-side reads use imported TX `$510e`
for signs and TX `$510d` for chests. A sign metatile absent from `signText.s`
uses Ages TX `$0901` (the Eternal Spirit description), while a closed chest
absent from `chestData.s` uses `getChestData`'s `$2800`
`TREASURE_OBJECT_RUPEES_00` result, including graphic `$28`, TX `$0001`, and
one Rupee. These branches must remain independent of ordinary sign/chest
records and debug chest overrides.

## Dungeon-specific native objects

Keep a dungeon's native handlers in a typed generated stream when its ordinary
object list contains script or interaction subids whose state machines are not
shared globally. Spirit's Grave uses `spirits_grave_objects.tsv`,
`spirits_grave_enemies.tsv` for its three native boss records,
`spirits_grave_visuals.tsv`, and
`spirits_grave_constants.tsv`. The importer resolves source object order,
predicates, enemy attributes, graphics, OAM, animation loops, text, and timing
constants; runtime code must not reconstruct those records from room IDs or
parse disassembly text.

Merge these records with shared dungeon mechanics and entrance interactions by
their imported `order`. Before-event bosses are gated by their source room flag
before they contribute to `wNumEnemies`; their reward script remains a separate
ordered controller that observes the same live enemy count. Child enemies and
projectiles use explicit spawn records so update-this-frame behavior is not
lost. Boss completion owns the persistent room flag, while the ordered reward
controller owns the Heart Container or miniboss portal.

Common boss initialization arms Link's `LINK_STATE_FORCE_MOVEMENT` only after
the boss's first enemy update. On the next update Link initializes the forced
state; its Ages `$16` countdown then performs 21 standard-speed one-pixel
updates before returning to the standing state. Run that Link-owned movement
before doors and enemies and bypass adjacent-wall collision, so the incoming
shutter observes Link fully inside before closing. Both Giant Ghini and Pumpkin
Head consume this shared entry contract; direct-room validation loads have no
scroll direction and therefore do not synthesize an entry walk.

Completed boss rooms retain a complete enemy-count source even though room flag
`$80` suppresses their before-event boss record. On re-entry, the two enemy
shutter controllers therefore observe zero enemies and run their ordinary
six-update interleaved opening animations; suppressing the boss must not leave
those doors in the fallback state used for genuinely unsupported enemy streams.

Common boss teardown is a chain, not an ordinary enemy deletion. The boss
sets the room-wide Link-collision/menu lock for its 120-update flicker, restores
the saved room music when it creates `PART_BOSS_DEATH_EXPLOSION`, and leaves
that imported 78-update part in `wNumEnemies`. The reward controller clears the
lock only after the explosion releases the enemy count and the source reward
script reaches its enable step. Do not route bosses through the ordinary death
puff/drop producer. Instead, the final boss explosion separately calls the
common drop selector with its defeated enemy ID after decrementing the live
enemy count to zero. This preserves record `$70`'s guaranteed
`ITEM_DROP_FAIRY` for Giant Ghini and record `$78`'s `$ff` no-drop result for
Pumpkin Head. Airborne bosses attach the reusable imported `PART_SHADOW`; its
size comes from the parent's raw Z high byte and its visibility alternates every
update.

Pumpkin Head's body and exposed ghost do not share one combat record. The body
resets to eight health each time its head is exposed, while the ghost retains
the enemy record's eight health across every regeneration. Collision mode
`$5e` applies ordinary item damage to both sword collision rows and the
Bracelet proxy; the weight-0 thrown head therefore deals the Bracelet record's
three damage and uses its separate planar radii plus strict Z test. The common
32-update enemy invincibility and `SND_BOSS_DAMAGE` response apply to either
accepted ghost hit. During state `$15`, `objectCopyPosition` copies the active
head's final X/Y into its related body before resetting body Z, so the rebuilt
boss belongs at the landed head rather than the previous body or fleeing ghost.

Spirit's Grave room `4:20` shares one transient puzzle state across its cube,
four flames, light sensor, and trigger sensor. The cube selects from all 30
imported roll/orientation animations and updates the shared color/position only
after a complete 16-pixel roll. After its one-time initialization separation,
the source handles solidity through the cube cell's `wRoomCollisions` `$0f`,
clears that byte during the roll, and restores it at the centered destination;
the runtime must not also apply a continuous entity-radius blocker, which would
stop Link before his adjacent-wall probes select the push pose. The cube's own
20-update push test reads Link's cardinal movement intent, facing, grounded and
item/button state directly. Flame actors apply the cube's current color and bit
7 visibility during construction, after the earlier ordered cube initializes
the shared state, so a room-entry render cannot expose the previous solved
appearance before the first fixed update. Room `4:16` similarly keeps its
button trigger separate from the native 30-update moving-platform spawn script.
This avoids encoding either puzzle as a room-load shortcut and preserves source
ordering.

Top-down `INTERAC_MOVING_PLATFORM` `$79` owns the shared
`wLinkRidingObject`-style support state. It tests Link's point at Y+5 against
the imported strict collision radii before advancing the platform, suppresses
Link's underlying hole/water/lava/conveyor terrain while claimed, and applies
the same `SPEED_80` 8.8 displacement to Link while his ordinary ground state
allows it. Keep the platform and Link fractional coordinates independent from
their floored draw coordinates; reconstructing either movement from a rendered
position loses or doubles half-pixel travel. Room `4:15` uses size subid `$05`
(`$10` by `$10` radii), while room `4:16` spawns subid `$09` (size `$01`,
`$08` by `$10` radii).

The Eternal Spirit remains a room entity until its exact approach predicate is
met, then hands control to `RoomEventController`. The event owns input lock,
dialogue, room/essence flags, `MUS_GET_ESSENCE` during the get text, the later
`MUS_ESSENCE`/energy-swirl cadence, full-screen fades, and the final delayed
warp. The entity owns the separately imported pedestal, animation-3 flickering
glow, collectible, and bead presentation. Clear any room-local background fade
when the transition loads the destination so a source-room effect cannot leak
into ordinary gameplay.

## Shared dungeon entrance interactions

The importer keeps `$12:$00`, `$e2:$01`, and `$7e:$00` placements in one
`dungeon_shared_placements.tsv` stream with their original room-object indices.
`RoomEntityFactory` merges that stream with `dungeon_mechanics.tsv` by the
imported `order` field; do not append either family by type, because doors,
chests, entry handlers, eyes, and portals can share a room and observe one
another's update order. Room `4:e7` retains its source NPC/handler interleave by
inserting the handler after the first construction soldier.

`INTERAC_DUNGEON_STUFF $12:$00` exists for one enabled update only when the room
was entered through the `$ff` whiteout screen warp and Link's Y is at least
`$78`. Initialization clears the Ages `wToggleBlocksState`, `wSwitchState`, and
`wSpinnerState` session bytes, then applies the imported per-dungeon spinner
value. Strict-radius contact shows the imported TX `$0200-$020f` label, records
the death checkpoint through the shared event, and deletes the interaction.

`INTERAC_STATUE_EYEBALL $e2:$01` scans large-room layout bytes from `$ae` down
through `$01` for tile `$ee`. Each child receives a same-update setup at the
tile center minus two Y pixels. Starting on its following update, it recenters,
quantizes `objectGetAngleTowardLink` to the source eight directions, applies the
imported low-nibble Y/X offset, and retains the default animation `$04` OAM.
The direction is represented by moving that one fixed eye sprite around the
statue; the other animation indices belong to `$e2:$00` and must not be selected
for the scanner's `$e2:$02` children.

`INTERAC_MINIBOSS_PORTAL $7e:$00` reads flag `$80` from the imported miniboss
room pair, not from whichever portal room is active. Its initial-overlap state
requires Link to leave before contact can trigger. Fresh contact plays
`SND_TELEPORT`, pins Link at packed position `$57`, rotates his direction every
fourth global update for exactly `$30` updates, and requests the imported basic
destination/fadeout warp to the other room in the pair. The transition
controller remains the owner of the actual room swap and fade.

Active Shovel use keeps parent-item timing in `Player` and delegates the
update-4 child probe to `ShovelController`. The controller reads
`BreakableTileDatabase` source `$06`, normalizes the hit to the metatile center,
applies replacement/drop/effect ordering, and spawns fixed-update
`ShovelDebrisEffect` through `RoomEntityManager`. Do not duplicate shovel dirt
lists or encode room-specific dig coordinates.

Sword-cut grass and bushes follow the same imported breakable-tile metadata.
`CombatController` decodes the effect byte's low nibble as the debris
interaction and bit 4 as its flicker subid, normalizes the spawn to the tile
center, and creates a fixed-update room entity. This keeps its OAM animation,
normal/underwater palette choice, sound, transition offset, scrolling freeze,
and deletion order in the same managed lifecycle as other room entities.

Active Seed Satchel use follows the same parent/child ownership boundary.
`Player` owns `LINK_ANIM_MODE_21`'s eight-update input/movement lock;
`SeedSatchelController` rejects use while a prior seed child remains active,
checks the selected BCD counter, allocates the child through
`RoomEntityManager`, and only then performs `decNumActiveSeeds`.
`EmberSeedEffect` owns `ITEM_EMBER_SEED $20` subid `$00`: the setup-only first
update, signed Link-relative offset, `SPEED_c0` motion, speedZ `-$20`, gravity
`$1c`, ground/hazard landing, item animation, and the `$3a`-update flame. On
expiry it probes `BreakableTileDatabase` with source `$0c` and applies the
imported replacement, drop, room-flag, Gasha-maturity, and solve-sound effects.
Breakable-room actions with bit 7 set use the low-nibble direction to set both
the active room's directional flag and the opposite flag in the neighbor from
the imported dungeon layout. For example, Spirit's Grave tile `$69` uses
action `$8c`: burning room `4:1d`'s left wall sets flag `$08` there and flag
`$02` in room `4:1c`, then plays `SND_SOLVEPUZZLE` and terminates the Ember
child. Do not reduce linked breakable actions to a current-room-only flag.
Cached `OracleRoomData` instances restore their source layout on every entry,
then run the original substitution order: `SingleTileChangeDatabase`,
`StandardTileSubstitutionDatabase`, chest/key-door state, and room-specific
changes. Room flag `$80` therefore restores a directly persistent burnt tree
`$cf` as `$dc`. Visually similar tree `$ce` has no direct breakable-table flag,
but may still be permanent when the room places `INTERAC_MISCELLANEOUS_2
$dc:$08`: that invisible entity snapshots its imported packed position and ORs
its imported mask into the room flags when the tile changes. Room `0:48` watches
position `$68` with mask `$02`; its matching `singleTileChanges.s` row restores
`$3a` on re-entry and after save reload. Unwatched `$ce` tiles remain transient.
Room `3:9e` also demonstrates a native layout-only write: Impa replaces logical
position `$22` with staircase `$45` while preserving the rendered `$e5`
metatile. Use a position visual override for such direct `wRoomLayout` writes;
collision, warp lookup, and gameplay read the replacement, while rendering and
background-mapping inspection read the preserved visual.
Enemy adapters share their accepted hit/death path with the seed
capability; the projectile disables its collision after the first accepted hit
and changes to the flame state. Enemy contact mirrors `COLLISIONEFFECT_BURN`
and `PART_BURNING_ENEMY $12`: contact during either flight or the landed flame
adopts the related enemy, follows it, suppresses its updates and contact, and
resolves the two-damage hit after the part's 59-update counter. A lightable torch
instead consumes the Ember Seed immediately without creating that flame
animation, then lights on its following object update.

`ITEM_MYSTERY_SEED $24` shares that Satchel child allocation, setup-only
update, signed offset, flight, and landing path. Its state-0 setup consumes one
global RNG byte and retains the source `& $03` random-effect choice. Activation
switches to the imported tile-base `$18`, palette `$00`, `SND_MYSTERY_SEED`,
and terminal animation while disabling further collision. The wider
random-effect transformations against ordinary enemies remain separate from
the Owl Statue activation slice; do not reinterpret Mystery as Ember damage.
Keep Scent, Pegasus, and Gale state machines distinct when they are
implemented.

Mystical Seed Trees use a separate common room-object path.
`SeedTreeDatabase` consumes all ten imported `ENEMY_SEEDS_ON_TREE $5a`
placements. The subid's high nibble selects Ember/Scent/Pegasus/Gale/Mystery
and its low nibble selects one of the sixteen refill bits. Destination parsing
finds `TILEINDEX_MYSTICAL_TREE_TL $6e`, checks the refill bit, and prebuilds
three visible `PART_SEED_ON_TREE $10` children in source order at offsets
`(0,-8)`, `(-8,0)`, and `(8,0)` from the 2-by-2 tree center. This graphics
setup happens before scrolling begins, so the children receive the incoming
room's transition draw offset while their fixed updates remain frozen. Room
`0:78` is the canonical `$5a:$06` Ember placement; emit its controller before
the old-lady interaction.

A slash without the Seed Satchel clears that child's collision and shows
TX `$0035` without consuming the tree. With the Satchel, the child launches
toward Link at `SPEED_100`, speedZ `-$140`, gravity `$20`, and the original
two-update collision delay. Link contact or completion of the halving bounce
gives six packed-BCD seeds through the normal treasure interpreter, plays
`SND_GETSEED`, and shows the imported first-type text when needed. The child
then notifies its controller; on the following controller update the refill bit
is cleared and the controller deletes, while uncollected siblings remain live.

Seed-tree refill history is session-local, not save data. Ages initializes the
two refill bytes to `$f0,$ff` and keeps sixteen eight-byte histories in banked
WRAM. Only an outdoor scrolling transition runs the update; direct loads and
warps do not. For every clear refill bit, it records the incoming room byte
unless already present, without storing its group. Entering that index's
group/room sets the bit only when all eight bytes are nonzero, then clears the
history even when it was incomplete. Keep the dummy and non-tree refill
locations active because the source shares this mechanism with child/event
progression.

`PART_OWL_STATUE $13` remains in the ordered enemy-pointer stream as a
reserving part. State 0 replaces its packed layout cell with tile `$00`, writes
collision `$0f`, and draws the imported two-cell idle OAM at fixed visible
priority `$83` from `spr_roller_owl_barrier_orb`. Its `var3f` bit-5 collision
guard makes `func_07_47b7` replace the random effect collision with the
canonical Mystery Seed collision `$9a`; only that collision starts it.
The part pre-decrements counter `$32`; values `$30,$28,$20,$18,$10,$08`
allocate `INTERAC_SPARKLE $84:$00` at the six source offsets in reverse table
order. At zero it loads counter `$1e` and the three-cell speaking pose, then
shows `TX_39xx` selected by the part subid when the counter reaches `$16`.
The text freezes the part at `$16` until it closes; counter zero afterward
restores the idle pose. The sparkle children retain their
setup-only first update, terminal `$ff` animation parameter, transition draw
offset, visible priority `$82`, and always-update behavior during dialogue.
Room `1:80` is the
canonical outdoor case: `$13:$06` at packed position `$33` shows `TX_3906`
beside its `$5a:$4d` Mystery Seed Tree.

`INTERAC_GASHA_SPOT $b6` is split between room initialization and one native
interaction entity. `GashaSpotDatabase` applies the planted `$f5` sprout below
20 kills or the solid `$4e/$4f/$5e/$5f` 2-by-2 tree from 20 kills onward.
Only an unplanted, exposed `$d2` tile becomes A-button-sensitive; the Discovery
Ring cue occurs on the interaction's first enabled update even while the spot
is still buried. At 40 kills the interaction creates the nut at the source
offset. A sword hit changes its radius, applies speed `$28`, speedZ `-$140`,
gravity `$20`, and aims at Link. From that hit until the tree is gone, Link's
movement/items/sword and ordinary menus remain disabled as by
`DISABLE_ALL_BUT_INTERACTIONS` plus `wMenuDisabled`.

Reward resolution consumes the shared RNG at exactly the source distribution
and ring-tier calls. The first nut forces a tier-3 ring without maturity debit;
later nuts select by the five maturity ranges and spot rank, debit 200, replace
a repeated Heart Piece with a tier-0 ring, and fully heal for an already-owned
Potion while retaining the Potion reward. The held two-hand reward and text
remain interaction-owned until displayed Hearts/Rupees catch up. Then the
tree makes its four metatiles walkable, runs the nine eight-update 4-by-4 BG
shrink frames over the spot-specific grass/dirt/sand source, clears the planted
bit, and writes the imported 2-by-2 ground replacement. Emit the Gasha entity
after placed actors and before the enemy stream; this preserves room `0:7b`'s
three-child-before-Gasha source order. The replacement is transient: the next
ordinary entry resets the cached room to its source layout, restoring the soft
soil and allowing the cleared spot to be planted again.

Active Shield use is a held-input parent, not a one-shot item action. `Player`
retains which equipped button allocated the parent, plays `SND_SHIELD` only on
its state-0 initialization, and writes the effective `wUsingShield` state only
while no other parent item owns Link. Scrolling temporarily lowers the shield
without deleting that parent, so a continuously held button raises it again
after the scroll without replaying the sound. Dialogue, warps, damage, and
cutscene control delete the parent. Collision uses the source per-direction
`wShieldY/X` center and radii before Link's ordinary body rectangle. Supported
enemy projectiles own their resulting bounce state; `Player` owns only the
shield predicate, overlap test, and `LINKDMG_$20` clink.

Sword beams use the same parent/child split. `Player` creates the single
object-capped `ITEM_SWORD_BEAM $27` on the sword animation's bit-5 update or
when the Energy Ring charge counter underflows. `SwordBeamEffect` owns its
setup-only first update, signed direction offset, `SPEED_300` motion, 2-by-2
collision radius, global four-update palette toggle, tile/screen termination,
and flickering `INTERAC_CLINK $81` collision result. `RoomEntityManager`
applies projectile damage before movement on each fixed update and freezes the
beam with the rest of the destination entity set during scrolling.

`PART_ITEM_DROP` spawn records distinguish ordinary stationary enemy drops from
Shovel-created drops. A dug-up drop copies Link's cardinal angle and applies
`SPEED_a0` (0.625 pixels per original update) during its airborne bounce, using
the allow-holes front/current tile probes before movement. Horizontal movement
ends with the bounce; it must not leak into ordinary drops or grounded lifetime
updates.

The common selector consumes its probability RNG value and, when that succeeds,
its set-selection RNG value before testing availability. Hearts, Rupees, and
Fairies are unconditional; Bombs and each of the five seed subids require their
matching obtained-treasure bit. Use the live `InventoryState` as the primary
view and the save image as the fallback for source-created producers, rather
than maintaining a second enemy-only predicate. Current quantity and capacity
do not affect spawning. Collection applies the Bomb/seed cap and Red, Blue, or
Gold Joy Ring multiplier after the drop already exists.

Subid `$00`, `ITEM_DROP_FAIRY`, is the exception to stationary grounded
behavior. Its initialization consumes three values from the shared global RNG
for an even `$00-$1e` angle, one of `SPEED_40/SPEED_80/SPEED_c0/SPEED_100`, and
an 8-70-update route counter. Movement uses the imported signed 8.8 components
from `bank3.objectSpeedTable`, retains the source front-probe boundary behavior,
and rerolls through the same three-call sequence when the route expires. Its
left/right facing XORs OAM flag `$20` within the fixed source cell; do not mirror
the larger composed texture around the object origin. The fairy begins with the
common `-$0160` speedZ, but its state-1 path is not an ordinary item bounce:
when its rising Z high byte crosses `$fa`, `itemDrop_checkHitGround` clamps it
to `$fa`, preserves the comparison carry, and immediately enters state 2.
It therefore never falls back to the ground. State 2 waits five alternating
countdown ticks before enabling collision while continuing horizontal movement
every update. Collection grants `TREASURE_HEART_REFILL` amount `$18`, doubled
to `$30` by the Gold or Blue Joy Ring.

Grounded/low airborne drops retain part collision mode `$01`. Link's sword
collision types `$04-$0b` select `COLLISIONEFFECT_23`, zeroing the part's health
so its next update grants the item directly to Link. This applies to ordinary
`PART_ITEM_DROP` and Maple's scattered parts. It does not write sword
`Item.var2a`, so collection is not enemy contact and must not trigger
Double-Edged Ring recoil.

Object-data opcode `$fa` does not place `PART_ITEM_DROP` directly. It allocates
an invisible `ENEMY_ITEM_DROP_PRODUCER $59`, reserves the packed position and a
killable-enemy index, and snapshots the underlying metatile on its first update.
Only a later tile change deletes the producer and, when Link owns the matching
Bomb or seed treasure, creates the drop for an immediate same-update advance.
The producer never contributes to `wNumEnemies`, but a successful production
marks its transient recent-defeat index so short re-entry cannot repeat it.
Rooms `0:5d` and `0:6d` are the canonical Bomb/Ember cases.

Item drops use `objectCheckIsOnHazard`, so water, lava, and holes do not consume
them while their Z high byte is negative. On the first ground-height hazard
update, water and lava replace the drop with their corresponding splash
interaction at the drop position. Hole disposal remains distinct and must not be
routed through the splash effect.

Common push blocks request `SND_MOVEBLOCK` only after the push delay succeeds,
the source tile has been replaced, and the moving object becomes visible.
Their movement uses the imported Bracelet contract: level 0/1 uses
`SPEED_80` for `$20` updates, while the level-2 Power Glove uses `SPEED_c0`
for `$15` updates unless property bit 5 marks the block heavy.
Outdoor grave `$d9` is the source-defined hidden-door special case: revealing
its `$dc` staircase at movement start disables Link's movement until the grave
finishes. The shared `(yh+4,xh)` centered warp probe keeps Link's adjacent push
position outside that staircase while it moves. Property bit 7 then releases
Link and requests `SND_SOLVEPUZZLE` on that completion update; there is no
separate post-movement wait.
Their completion event retains the destination hazard type: water/lava create
the splash interaction, while a hole creates `INTERAC_FALLDOWNHOLE $0f:$00`
without changing the hole tile. That interaction requests `SND_FALLINHOLE`,
moves the inherited block position toward the metatile center at `SPEED_60`,
and plays the imported 8/12/12-update terminal animation. Rejected directions,
solid destinations, and interrupted push delays request neither sound nor
effect.

Dynamic blocker collision compares the high-byte pixel coordinates of both
objects, matching `checkObjectsCollided`; fractional 8.8 position bytes must not
stop Link one rendered pixel before contact. Object-side separation helpers may
then replace only the collided coordinate's high byte while retaining its
fractional byte.

Room interaction spawners can produce reusable entities without becoming room
exceptions. `$dc:$07` ground treasures are emitted after placed NPCs and before
portals/enemies in original object order, expose collision through
`ILinkContactEntity`, and use `IRoomEntityLifetime` to disappear only after the
pickup textbox closes. Their room-item bit is checked on every room parse.
Static spawn mode `$00` builds and exposes its visual during destination
parsing, so Heart Pieces scroll in at the destination draw offset even though
their state-0/state-1 updates and collection remain frozen until transition
completion.
The same treasure entity supports source spawn mode `$02`: after its imported
delay, `objectGetZAboveScreen` derives Z from the current gameplay-screen Y
rather than a fixed room coordinate, then shared 8.8 gravity and bounce
metadata drive it to the floor. This is used by both event-created rewards and
room `5:ed`'s Graveyard Key.

Script grants and deferred event rewards enter `RoomEntityManager` through a
source-addressed `GroundTreasureGrantRequest`. The request resolves its imported
treasure object and visual in one place, with an explicit override only for a
source-owned visual such as the Black Tower Shovel. It also records spawn/grab
mode, ROOMFLAG_ITEM timing, ordinary versus concrete unappraised-ring inventory
writes, behavior/grab sound order, dialogue timing and textbox metadata, and
whether the shared interaction controller or the calling event owns completion.
Deferred Maku Tree, dark-room, and Spirit's Grave rewards use the manager's
ordered spawn queue. Immediate `giveitem` paths use the same activation policy;
past Bipin and the Hardhat worker therefore no longer attach or free pickup
nodes directly. A grant may carry active textbox flags, as required by room
`3:ae`'s alternate-palette Tune of Echoes award, while Vasu's caller-owned
grants retain their fixed textbox position and do not set a room item flag.

`RoomEntityManager` owns the room-local `wActiveTriggers` equivalent and clears
all eight bits before every ordinary room parse or destination preload.
`PART_BUTTON $09` writes the bit selected by subid bits 0-2; subid bit 7 only
chooses reusable versus one-shot pressure. Trigger-door `$1e:$04-$07` records
read the bit selected by their source parameter. Trigger-chest `$20:$00` and
`$21:$17` records retain an imported exact-byte or bit-set predicate rather
than deriving it from the room at runtime. Keep buttons and their consumers as
separate ordered fixed-update entities: interactions observe the prior trigger
value before parts update it, so a pressure change affects a door or chest on
the next update. These mechanics do not depend on save/story predicates.

Small-key doors are tile interactions, not placed `$1e` room objects.
`DungeonKeyDoorController` probes imported tiles `$70-$73` through the same
front-tile push geometry as blocks, centralized in
`InteractableTilePushGeometry`. `nextToKeyDoor` initializes its shared counter
to 20 but decrements it twice per qualifying update, so the key check occurs on
the tenth continuous push. Success consumes exactly one key from the current
dungeon, spawns `INTERAC_DUNGEON_KEY_SPRITE $17`, and sets the directional room
flag on both sides of the neighbor resolved through the imported dungeon floor
layout. Opening uses the same six-update mapping-interleaved, still-solid door
frame as shutters before final tile `$a0`; room initialization substitutes
opened `$70-$73` tiles from those saved flags. A missing key shows TX `$5100`
without changing either room.

Permanent trigger chests request the solve cue and puff on their qualifying
edge, wait 15 updates, then install closed chest tile `$f1`. Retractable chests
install `$f1` immediately, and restore the source room-layout tile when their
exact trigger byte stops matching; both transitions create a puff, but only
appearance plays the solve cue. `ROOMFLAG_ITEM` prevents either controller from
running on re-entry and installs opened chest tile `$f0` at the imported
position, including rooms whose source layout never contained a chest tile.

Button pressure is tile-aware as well as Link-aware. Strict high-byte contact
uses the part's `$02/$02` radii plus Link's `$06` radius and rejects airborne
Link. Any tile other than `$0c/$0d` represents an object holding the button;
the object remains rendered while the underlying reusable button is pressed,
then tile `$0d` is revealed and its `$1c` release counter runs after the object
moves. Press and release both request `SND_SPLASH`.

Enemy-shutter door controllers `$1e:$08-$0b` query
`IRoomEnemyCounterEntity` rather than a
room-specific completion boolean. Combat adapters contribute while alive;
native sentinels such as push-block trigger `$13:$01` contribute from their
state-0 increment through their delayed reset. Enemy object flag bit `$02`
retains the original count-exempt behavior. Import every shared placement, but
allow a shutter to solve only when every active, non-exempt enemy record in that
room has a runtime entity capable of contributing to the count and its object
list has no unresolved before/after-event enemy stream. Keep the shutter
controller itself active so `replaceShutterForLinkEntering` can safely admit
Link even when that completion source is not yet implemented. Retain only the
crossed shutter as open floor so Link can backtrack instead of becoming trapped;
all other shutters remain closed, and no solve state or cue is synthesized from
the incomplete count. Standalone `$13:$01` records paired with incomplete enemy
streams remain inactive for the same reason.

Every accepted ordinary sword collision requests `SND_DAMAGE_ENEMY` from the
shared combat descriptor, after the species accepts damage and before any
deferred knockback death completes. Low, normal, high, and no-knockback sword
effects share that sound; a rejected hit during invincibility requests none.
Special combat adapters that bypass the ordinary descriptor must explicitly
retain the same accepted-hit sound policy or their source-specific boss sound.

Common combat death creates `PART_ENEMY_DESTROYED`; the factory requests
`SND_KILLENEMY` when that puff is allocated so every supported species shares
one ownership point. Red Zols instead request it with their special
`INTERAC_KILLENEMYPUFF` split. Hazard deaths suppress both death/drop puffs and
the kill cue; a hole is retained separately on the enemy until lifetime removal
requests `SND_FALLINHOLE`, while water/lava remain silent on that path.
An ordinary counted enemy transfers its `wNumEnemies` contribution to the
death puff, so shutters and count-driven events cannot solve until the puff's
terminal update. Hazard deletion instead releases that contribution with no
recent-defeat mark or global kill-counter advance. When the ordinary puff
completes, its enemy ID enters the same source-table drop selector used by boss
explosions and breakable tiles; do not filter its Bomb or seed result before
the shared obtained-treasure check.

`ENEMY_CROW $41:$00` is a fixed-position combat enemy with a species-specific
native state machine. While perched it has no collision and faces Link, using
the source's unsigned inclusive Y=`$30`/X=`$18` approach test. It then rises for
25 updates to Z=`-$06`, enables collision, consumes one shared RNG value for a
`+/-$04` angle offset, and charges at `SPEED_140`; for the first 90 charge
updates it steers one angle step every eight updates. A charge that crosses the
original Y=`$88` or X=`$a8` screen bounds uses `enemyDelete`, so it creates no
death puff, item drop, kill sound, or recent-defeat mark. Rooms `0:5d` and
`0:6d` provide the three currently imported subid-0 records; flock subid `$01`
remains outside this slice.

Script-created combat replacements use the same contracts as placed enemies.
Room `1:38`'s `$96` Moblin interactions are solid animated cutscene actors only
until `moblin_spawnEnemyHere`; each then deactivates and creates an
`ENEMY_MASKED_MOBLIN $20:$00` through `RoomEntitySpawn`. The replacement owns
normal contact/sword/hazard/death/drop behavior and contributes through
`IRoomEnemyCounterEntity`, so both the controller script and the sprout script
observe the shared live `wNumEnemies` equivalent. Projectile children such as
`PART_ENEMY_ARROW $1a` are separate update-this-frame spawn records and never
contribute to that count.

Scrolling placement context also carries Link's final packed destination.
Directional shutter controllers use it with the scroll direction to mirror
`replaceShutterForLinkEntering`: only the crossed shutter is preloaded as open
floor. It remains non-solid while destination entities are frozen and while
Link overlaps the door's combined radii; its shared six-update interleaved
close starts afterward and applies the closed collision only on completion.
This entry path is independent of enemy-completion support. When the count is
complete, as in room `4:06`, the crossed shutter closes only after Link clears
it. That room then counts two ordinary Stalfos plus its `$13:$01` push-block
sentinel; killing both Stalfos restores source block `$1c`, and moving that
block starts the normal delayed two-shutter solve. In an incomplete room, the
crossed route instead remains open for safe backtracking without synthesizing a
solve.

`replaceShutterForLinkEntering` is a layout substitution, not a property of
placed door-controller records. Before destination object parsing, ordinary
layout tiles `$78-$7b` must also compare their encoded direction and packed
position with the scrolling entry context. The matching tile becomes `$a0`;
the corresponding source table row has bit 7 set, so no auto-close controller
is created. Room `4:1d`'s right tile `$79` is the canonical layout-only case:
scrolling left from `4:1e` opens packed position `$5e`, while a direct room load
retains the closed source tile. Minecart shutters `$7c-$7f` use different
replacement tiles and auto-close interactions and remain a separate mechanic.

Do not infer the entry shutter from a room edge alone, because the original
substitution also requires Link's packed row or column to match that door.
After the entry overlap clears, shift a local respawn stored on the shutter
tile in the direction-specific inward offset. A later close on Link's tile uses
the original instant-respawn path (two invisible updates, one-heart damage,
then 16 recovery updates) before solid collision can strand him.

Ordinary random and fixed enemy records without object flag `$01` advance the
source `numKillableEnemies` counter before allocation. Only indices `$01-$07`
are retained. `RecentEnemyDefeats` mirrors the original eight-entry
`wEnemiesKilledList` ring by room ID; an explicit source outcome marks the
entity's bit, subsequent short re-entry skips that placement before slot
allocation and random positioning, and visiting enough distinct rooms
eventually evicts it. Red Zol split children retain the parent's index while
the replaced parent does not mark it; an escaped Crow deletes silently.
Wallmaster's five hands each emit an `enemyDie_uncounted` counter event, while
only the final spawner completion decrements the room count and marks recent
defeat. Boss teardown marks recent defeat without synthesizing an ordinary kill
counter event; its explosion retains the room-count contribution until it
finishes. This state is runtime-only: scrolling and dungeon warps retain it,
whereas standard warp loading to a non-dungeon destination clears it.

See [NPCs and room events](npcs-and-events.md) for deciding whether an imported
interaction remains an ordinary NPC, receives a specialized room-entity
adapter, or is coordinated by `RoomEventController`.

Room-specific background-map rewrites retain a separate layer from layout
mutation. `OracleRoomData.SetBackgroundSubtileRectangle` copies a
metatile-aligned, even-sized rectangle of BG tile IDs while preserving each
covered mapping's attribute bytes. A native event may therefore show
intermediate tilemap-only phases without changing terrain. Room `0:83` is the
canonical case: three 6x6 collapse maps replace only BG tile IDs, while the
fourth applies its final 6x6 map before committing the imported 3x3
metatile/collision rectangle. `UNCMP_GFXH_AGES_3c` uploads only `w3VramTiles`,
so the mapping override preserves the original façade's attribute bytes even
after its layout IDs change. Re-entry uses the same final operation order when
room flag `$80` is already set. Do not approximate such a sequence by swapping
only the triggering rock or by deriving collision or palette attributes from
the phase graphics.

## Required regressions

Room/entity changes should cover the reported room plus a general invariant:
ordered reservations, fixed and random coexistence, terrain rejection, incoming
edge/warp exclusion, RNG consumption, transition freeze, child-update timing,
death/drop results, and re-entry after persistent flags. Use original IDs in
failure messages so a mismatch can be traced directly.
