# Interaction, event and cutscene coverage

This ledger inventories every clean-US Seasons interaction dispatch entry and
all literal interaction rows in the Seasons object-data files. A runtime route
is not an exhaustive ROM-parity claim. Positioned characters are summarized
separately in the [NPC ledger](npc-interaction-coverage.md).

The interaction inventory starts from the Seasons branch of the
[interaction dispatch table](../../external/oracles-disasm/data/interactionCodeTable.s),
all four distinct [room-object tables](../../external/oracles-disasm/objects/seasons/pointers.s),
their aliases and pointer/event expansions, every Seasons extra object-data
list, the shared family-spawn tables and literal `spawninteraction` commands in
Seasons/shared scripts. Symbolic references in Seasons/shared native code and
scripts are included to locate additional consumers and creation paths.

Groups `$02/$03` alias table `$01`; `$06/$07` alias `$04/$05`. Room counts use
canonical groups `$00,$01,$04,$05` without double-counting those aliases.
Room-flag and season alternatives are retained in source order, not asserted
to be simultaneous or all reachable in a clean playthrough. Literal extra rows
include dormant data. Coordinates and var03 retain source inputs before native
rewriting; no-value placements initialize their other fields in native code.

Room object declarations are imported as shared native control programs.
Native story bindings cover the standard opening and first-sword Hero's Cave route; shared effect
and treasure services also operate through their explicitly bound producers.
Shared dungeon spinners use their native placed room streams.
Handler scripts and native cutscenes are imported only for implemented routes.
The native cutscene, Link, title and file-menu table below records the existing supported boundary;
it is not a source-wide inventory of those separate dispatch systems.
Enemies, items and parts are dependencies rather than additional interaction IDs.

## Status definitions

| Status | Meaning |
| --- | --- |
| **R** | Runtime route located for the source family or explicitly listed placement context; not an exhaustive branch, timing, lifecycle or ROM-verification claim. |
| **P** | A runtime route exists, with known missing producers, variants, placed contexts or external-system branches. |
| **M** | No Seasons production route for the source entry or its placed context. A retained source entry alone does not establish playable reachability. |
| **D** | The original clean-US dispatch deletes the object; no feature implementation is required. |

## Coverage totals

| Inventory | R | P | M | D | Total |
| --- | ---: | ---: | ---: | ---: | ---: |
| Interaction IDs `$00-$e7` | 6 | 28 | 191 | 7 | 232 |
| Expanded placed interaction contexts | 17 | 3 | 1015 | 0 | 1035 |

| Source placement inventory | Count |
| --- | ---: |
| Rooms with placed interactions | 532 |
| Distinct placed interaction ID/subid pairs | 578 |
| Literal interaction rows across all Seasons object-data files | 1114 |
| Distinct literal object-data ID/subid pairs | 681 |
| Distinct literal rows reached by canonical room roots | 985 |
| Additional literal rows outside room-root expansion | 129 |
| Conditional Bipin/Blossom family variants | 72 |
| Literal script `spawninteraction` commands | 36 |

These inventories overlap. Shared labels expand one literal row into several
rooms; room controllers can create actors also listed in extra data or scripts.
Family alternatives are counted by stage/personality selection, not concurrent
actors. Do not sum the inventories as a count of unique live entities.

## Missing implementation checklist

A checked item requires the source-derived import/producer, actual gameplay
route, completion/cancellation/re-entry behavior and focused regression.
Shared Ages helpers and visual-only data do not close a Seasons item.

| Scope | Status |
| --- | --- |
| Standard-opening character rows listed as **I** in the NPC ledger | Implemented within the stated standard-opening boundary |
| Hero's Cave first sword | Runtime route through shared push blocks, floor buttons, key chest, door contacts, shutters and scripted sword return; optional side-view Gasha producer remains unbound |
| Missing character rows and family variants in the NPC ledger | Missing |
| Other room placements listed as **M** below | Missing |
| Additional native/script variants of **P** dispatch families | Missing beyond the explicit runtime boundary |
| Shared tile-break effect producers | Partial; native Shovel and Bracelet routes are bound; additional producer contexts remain open |
| Shared ground-treasure graphics, text and held-item completion | Partial; dynamically constructed native treasure records use the shared session; placed and additional native producers remain unbound |
| Shared dungeon spinner parent, arrow and Link handoff | Runtime route through the three native placed room streams and shared Link state dispatch |
| Shared push, dungeon-key and Subrosia key-door tile contacts | Partial; reserved normal/sliding blocks, key opener/sprite, live-dungeon grants and ordered room-flag reload are bound; Subrosia doors use the native date flag; placed mechanisms and Rosa's NPC/date scripts remain unbound |
| Named overworld keyhole contacts | Partial; contact timing, named-key checks and hints are bound; Gnarled, Floodgate and Dragon entrance scripts and their key-sprite production remain unbound |
| Grave-triggered Ghini | Partial; native grave contact and counted spawning use shared normal, slow and targeted Ghini handlers; graveyard story and progression scripts remain unbound |
| Remaining **M** interaction dispatch entries | No production route; retained/unreachable variants still require reachability tracing |
| Linked-game introduction and linked troupe/Impa dialogue | Missing |
| Seasons attract cinematic and title-timeout replay | Missing |
| Seasons gameplay saving and resuming after the opening | Runtime route through the shared save/checkpoint flow for supported standard overworld and Hero's Cave files |
| Overworld layout navigation after the opening | Runtime route; room `$00:$98` retains its after-event Octorok stream; other later NPC, enemy, event and warp contexts remain unsupported |
| Progression beyond control returning in room `0:b6` | Missing |
| Exhaustive executed clean-US-ROM comparison | Unproven |

## Interaction dispatch inventory

`Placed subids` includes both event/season alternatives. `Other object-list
subids` includes literal lists outside the room roots. `Script-created subids`
lists literal script creation commands, not computed native variants.
`Symbol sites` counts non-comment references to the exact `INTERAC_` symbol in
Seasons/shared native code and scripts, including comparisons and data; it is
not a count of verified creation paths. Follow the named source handler and
its callers for computed subids, ID rewrites and branch eligibility. Every
dispatch ID is retained even when no literal placement or symbol site exists.

| ID | Source handler | Placed subids | Other object-list subids | Script-created subids | Symbol sites | Status and runtime boundary |
| --- | --- | --- | --- | --- | ---: | --- |
| `$00` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 3 | **P** ? Common grass debris; native break producers remain incomplete. |
| `$01` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 0 | **P** ? Common red-grass debris; native break producers remain incomplete. |
| `$02` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 2 | **M** — No Seasons production route. |
| `$03` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 13 | **P** ? Common water splash for player and drop hazards; other native producers remain incomplete. |
| `$04` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 6 | **P** ? Common lava splash for player and drop hazards; other native producers remain incomplete. |
| `$05` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | `$00` | 31 | **P** — Standard opening and Bracelet `$d7` puff `$00`; additional producer contexts remain unbound. |
| `$06` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 3 | **P** ? Common rock debris; native break producers remain incomplete. |
| `$07` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 5 | **P** ? Common projectile, sword and enemy clinks; native combat producers remain incomplete. |
| `$08` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 2 | **P** — Shared `$00` destruction puff for the standard opening; additional producers remain unbound. |
| `$09` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 2 | **P** — Hero Shovel snow-pile `$01` debris; additional producer contexts remain unbound. |
| `$0a` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 2 | **P** — Hero Shovel directional `$00` debris; additional producer contexts remain unbound. |
| `$0b` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$0c` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | — | — | 0 | **P** ? Common second rock-debris variant; native break producers remain incomplete. |
| `$0d` | `interactionDelete` | — | — | — | 0 | **D** — Original dispatch deletes the object. |
| `$0e` | `interactionDelete` | — | — | — | 0 | **D** — Original dispatch deletes the object. |
| `$0f` | [fallDownHole](../../external/oracles-disasm/object_code/common/interactions/fallDownHole.s) | — | — | — | 6 | **P** ? Common falling-hole effect `$00`; dust and other native producers remain incomplete. |
| `$10` | [farore](../../external/oracles-disasm/object_code/common/interactions/farore.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$11` | [farore](../../external/oracles-disasm/object_code/common/interactions/farore.s) | — | `$01, $11, $21, $31, $41, $51, $61, $71` | — | 1 | **M** — No Seasons production route. |
| `$12` | [dungeonStuff](../../external/oracles-disasm/object_code/common/interactions/dungeonStuff.s) | `$00-$02, $04` | — | — | 0 | **P** — Hero's Cave dungeon$00 entry and enemy-clear stairs; other placements and variants remain unbound. |
| `$13` | [pushblockTrigger](../../external/oracles-disasm/object_code/common/interactions/pushblockTrigger.s) | `$01` | — | — | 0 | **P** — Hero's Cave $4:05/$4:07 push-block triggers; other placements remain unbound. |
| `$14` | [pushblock](../../external/oracles-disasm/object_code/common/interactions/pushblock.s) | — | — | — | 2 | **P** — Shared tile contact constructs reserved normal/sliding blocks; additional scripted producers remain unbound. |
| `$15` | [usedRodOfSeasons](../../external/oracles-disasm/object_code/seasons/interactions/usedRodOfSeasons.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$16` | [minecart](../../external/oracles-disasm/object_code/common/interactions/minecart.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$17` | [dungeonKeySprite](../../external/oracles-disasm/object_code/common/interactions/dungeonKeySprite.s) | — | — | — | 1 | **P** — Dungeon-key and Subrosia-door tile contacts construct checked shared key sprites; placed contexts remain unbound. |
| `$18` | [overworldKeySprite](../../external/oracles-disasm/object_code/common/interactions/overworldKeySprite.s) | — | — | — | 0 | **M** — No Seasons production route. |
| `$19` | `interactionDelete` | — | — | — | 0 | **D** — Original dispatch deletes the object. |
| `$1a` | `interactionDelete` | — | — | — | 0 | **D** — Original dispatch deletes the object. |
| `$1b` | `interactionDelete` | — | — | — | 0 | **D** — Original dispatch deletes the object. |
| `$1c` | [faroresMemory](../../external/oracles-disasm/object_code/common/interactions/faroresMemory.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$1d` | [rupeeRoomRupees](../../external/oracles-disasm/object_code/seasons/interactions/rupeeRoomRupees.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$1e` | [doorController](../../external/oracles-disasm/object_code/common/interactions/doorController.s) | `$04-$0b, $10, $12, $14-$15` | `$14` | `$13` | 5 | **P** — Shared dungeon-key contacts and Hero's Cave placed $04/$08/$0a/$0b shutters; other placements and variants remain unbound. |
| `$1f` | [specialWarp](../../external/oracles-disasm/object_code/seasons/interactions/specialWarp.s) | `$00, $02-$0d` | — | — | 0 | **M** — No Seasons production route. |
| `$20` | [dungeonScript](../../external/oracles-disasm/object_code/seasons/interactions/dungeonScript.s) | `$00-$0e` | — | — | 0 | **P** — Hero's Cave $4:03 exact-trigger key chest; other dungeon scripts remain unbound. |
| `$21` | [gnarledKeyhole](../../external/oracles-disasm/object_code/seasons/interactions/gnarledKeyhole.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$22` | [makuCutscenes](../../external/oracles-disasm/object_code/seasons/interactions/makuCutscenes.s) | `$00-$08, $0a` | — | — | 1 | **M** — No Seasons production route. |
| `$23` | [seasonSpiritsScripts](../../external/oracles-disasm/object_code/seasons/interactions/seasonSpiritsScripts.s) | `$01, $11, $21, $30-$31, $40` | — | — | 0 | **M** — No Seasons production route. |
| `$24` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$00, $03` | — | — | 1 | **M** — No Seasons production route. |
| `$25` | [mittensAndOwner](../../external/oracles-disasm/object_code/seasons/interactions/mittensAndOwner.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$26` | [mittensAndOwner](../../external/oracles-disasm/object_code/seasons/interactions/mittensAndOwner.s) | `$00-$01` | — | — | 1 | **M** — No Seasons production route. |
| `$27` | [sokra](../../external/oracles-disasm/object_code/seasons/interactions/sokra.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$28` | [bipin](../../external/oracles-disasm/object_code/common/interactions/bipin.s) | — | — | — | 23 | **M** — No Seasons production route. |
| `$29` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$2a` | [bird](../../external/oracles-disasm/object_code/seasons/interactions/bird.s) | `$00-$09, $0b` | — | — | 1 | **M** — No Seasons production route. |
| `$2b` | [blossom](../../external/oracles-disasm/object_code/common/interactions/blossom.s) | — | — | — | 25 | **M** — No Seasons production route. |
| `$2c` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$00` | — | — | 1 | **M** — No Seasons production route. |
| `$2d` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$80-$86` | — | — | 1 | **M** — No Seasons production route. |
| `$2e` | [fickleGirl](../../external/oracles-disasm/object_code/seasons/interactions/fickleGirl.s) | `$00-$04` | — | — | 0 | **M** — No Seasons production route. |
| `$2f` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$80` | — | — | 0 | **M** — No Seasons production route. |
| `$30` | [subrosian](../../external/oracles-disasm/object_code/seasons/interactions/subrosian.s) | `$00, $02-$0f, $12-$13, $15-$26` | — | — | 1 | **M** — No Seasons production route. |
| `$31` | [datingRosaEvent](../../external/oracles-disasm/object_code/seasons/interactions/datingRosaEvent.s) | `$00, $02` | — | — | 2 | **M** — No Seasons production route. |
| `$32` | [subrosianWithBuckets](../../external/oracles-disasm/object_code/seasons/interactions/subrosianWithBuckets.s) | `$00-$05` | — | — | 0 | **M** — No Seasons production route. |
| `$33` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$80-$85` | — | — | 1 | **M** — No Seasons production route. |
| `$34` | [subrosianSmiths](../../external/oracles-disasm/object_code/seasons/interactions/subrosianSmiths.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$35` | [child](../../external/oracles-disasm/object_code/common/interactions/child.s) | — | — | — | 23 | **M** — No Seasons production route. |
| `$36` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$80-$84` | — | — | 2 | **M** — No Seasons production route. |
| `$37` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$80-$86` | — | — | 1 | **M** — No Seasons production route. |
| `$38` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$00` | — | — | 1 | **M** — No Seasons production route. |
| `$39` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$00-$04` | — | — | 1 | **M** — No Seasons production route. |
| `$3a` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | — | — | — | 0 | **M** — No Seasons production route. |
| `$3b` | [goron](../../external/oracles-disasm/object_code/seasons/interactions/goron.s) | `$02, $07, $11, $13-$16, $20` | — | — | 0 | **M** — No Seasons production route. |
| `$3c` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$80` | — | — | 0 | **M** — No Seasons production route. |
| `$3d` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$80` | — | — | 0 | **M** — No Seasons production route. |
| `$3e` | [miscBoyNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscBoyNpcs.s) | `$00-$04, $10-$13, $20, $30-$34` | — | — | 0 | **M** — No Seasons production route. |
| `$3f` | [miscNpcs](../../external/oracles-disasm/object_code/seasons/interactions/miscNpcs.s) | `$80` | — | — | 0 | **M** — No Seasons production route. |
| `$40` | [piratian](../../external/oracles-disasm/object_code/seasons/interactions/piratian.s) | `$01-$0a, $0d-$0e` | — | — | 1 | **M** — No Seasons production route. |
| `$41` | [piratian](../../external/oracles-disasm/object_code/seasons/interactions/piratian.s) | `$00, $0b` | — | — | 0 | **M** — No Seasons production route. |
| `$42` | [pirateHouseSubrosian](../../external/oracles-disasm/object_code/seasons/interactions/pirateHouseSubrosian.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$43` | [syrup](../../external/oracles-disasm/object_code/common/interactions/syrup.s) | `$80` | `$80` | — | 0 | **M** — No Seasons production route. |
| `$44` | [zelda](../../external/oracles-disasm/object_code/seasons/interactions/zelda.s) | `$00, $08-$09` | `$00-$01, $05` | — | 6 | **M** — No Seasons production route. |
| `$45` | [talon](../../external/oracles-disasm/object_code/seasons/interactions/talon.s) | `$00` | — | `$01` | 1 | **M** — No Seasons production route. |
| `$46` | [shopkeeper](../../external/oracles-disasm/object_code/common/interactions/shopkeeper.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$47` | [shopItem](../../external/oracles-disasm/object_code/common/interactions/shopItem.s) | `$00-$05, $0e-$10` | — | `$07-$08, $0b` | 4 | **M** — No Seasons production route. |
| `$48` | [makuLeaf](../../external/oracles-disasm/object_code/seasons/interactions/makuLeaf.s) | — | — | `$00` | 3 | **M** — No Seasons production route. |
| `$49` | [syrupCucco](../../external/oracles-disasm/object_code/common/interactions/syrupCucco.s) | `$80` | — | — | 0 | **M** — No Seasons production route. |
| `$4a` | [introSprites1](../../external/oracles-disasm/object_code/common/interactions/introSprites1.s) | — | — | — | 5 | **M** — No Seasons production route. |
| `$4b` | [d1RisingStones](../../external/oracles-disasm/object_code/seasons/interactions/d1RisingStones.s) | — | — | — | 2 | **M** — No Seasons production route. |
| `$4c` | [miscStatusObjects](../../external/oracles-disasm/object_code/seasons/interactions/miscStatusObjects.s) | `$00-$03, $07-$0d` | `$07-$08` | `$09` | 2 | **M** — No Seasons production route. |
| `$4d` | [pirateSkull](../../external/oracles-disasm/object_code/seasons/interactions/pirateSkull.s) | `$00-$01` | — | — | 2 | **M** — No Seasons production route. |
| `$4e` | [dinDancingEvent](../../external/oracles-disasm/object_code/seasons/interactions/dinDancingEvent.s) | `$0a-$0b` | `$00-$06, $08-$09` | — | 1 | **P** — Standard opening `$00-$07,$0b`; village/ending actors and linked branches are missing. |
| `$4f` | [dinImprisonedEvent](../../external/oracles-disasm/object_code/seasons/interactions/dinImprisonedEvent.s) | — | — | — | 6 | **P** — Standard imprisonment `$00-$04`; later variants are missing. |
| `$50` | [seasonsFairy](../../external/oracles-disasm/object_code/common/interactions/seasonsFairy.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$51` | [smallVolcano](../../external/oracles-disasm/object_code/seasons/interactions/smallVolcano.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$52` | [biggoron](../../external/oracles-disasm/object_code/seasons/interactions/biggoron.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$53` | [headSmelter](../../external/oracles-disasm/object_code/seasons/interactions/headSmelter.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$54` | [subrosianAtD8Items](../../external/oracles-disasm/object_code/seasons/interactions/subrosianAtD8Items.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$55` | [subrosianAtD8](../../external/oracles-disasm/object_code/seasons/interactions/subrosianAtD8.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$56` | [explosion](../../external/oracles-disasm/object_code/common/interactions/explosion.s) | — | — | — | 4 | **M** — No Seasons production route. |
| `$57` | [ingo](../../external/oracles-disasm/object_code/seasons/interactions/ingo.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$58` | [guruguru](../../external/oracles-disasm/object_code/seasons/interactions/guruguru.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$59` | [lostWoodsSword](../../external/oracles-disasm/object_code/seasons/interactions/lostWoodsSword.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$5a` | [blainoScript](../../external/oracles-disasm/object_code/seasons/interactions/blainoScript.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$5b` | [lostWoodsDekuScrub](../../external/oracles-disasm/object_code/seasons/interactions/lostWoodsDekuScrub.s) | `$80` | — | — | 0 | **M** — No Seasons production route. |
| `$5c` | [lavaSoupSubrosian](../../external/oracles-disasm/object_code/seasons/interactions/lavaSoupSubrosian.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$5d` | [tradeItem](../../external/oracles-disasm/object_code/seasons/interactions/tradeItem.s) | — | — | `$06-$08` | 3 | **M** — No Seasons production route. |
| `$5e` | [quicksand](../../external/oracles-disasm/object_code/seasons/interactions/quicksand.s) | `$00-$05` | — | — | 1 | **M** — No Seasons production route. |
| `$5f` | [companionSpawner](../../external/oracles-disasm/object_code/common/interactions/companionSpawner.s) | `$01-$06` | — | — | 1 | **P** — Flute `$80` native caller with Ricky/Dimitri/Moosh construction and arrival bindings; placed subids remain unbound. |
| `$60` | [treasure](../../external/oracles-disasm/object_code/common/interactions/treasure.s) | — | — | — | 17 | **P** — Shared dynamically constructed treasure graphics, text and held-item completion; placed and additional native producers remain unbound. |
| `$61` | `interactionDelete` | — | — | — | 0 | **D** — Original dispatch deletes the object. |
| `$62` | [unicornsCave4ChestPuzzle](../../external/oracles-disasm/object_code/seasons/interactions/unicornsCave4ChestPuzzle.s) | `$00` | — | — | 1 | **M** — No Seasons production route. |
| `$63` | [unicornsCaveReverseMovingArmos](../../external/oracles-disasm/object_code/seasons/interactions/unicornsCaveReverseMovingArmos.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$64` | [unicornsCaveFallingMagnetBall](../../external/oracles-disasm/object_code/seasons/interactions/unicornsCaveFallingMagnetBall.s) | — | — | — | 0 | **M** — No Seasons production route. |
| `$65` | [65](../../external/oracles-disasm/object_code/seasons/interactions/65.s) | — | — | `$00` | 3 | **M** — No Seasons production route. |
| `$66` | [explorersCrypt4ArmosButtonPuzzle](../../external/oracles-disasm/object_code/seasons/interactions/explorersCrypt4ArmosButtonPuzzle.s) | `$00` | — | — | 1 | **M** — No Seasons production route. |
| `$67` | [swordShieldMazeArmosPatternPuzzle](../../external/oracles-disasm/object_code/seasons/interactions/swordShieldMazeArmosPatternPuzzle.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$68` | [swordShieldMazeGrabbableIce](../../external/oracles-disasm/object_code/seasons/interactions/swordShieldMazeGrabbableIce.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$69` | [swordShieldMazeFreezingLavaEvent](../../external/oracles-disasm/object_code/seasons/interactions/swordShieldMazeFreezingLavaEvent.s) | `$00` | — | — | 4 | **M** — No Seasons production route. |
| `$6a` | [danceHallMinigame](../../external/oracles-disasm/object_code/seasons/interactions/danceHallMinigame.s) | `$00` | `$01-$02` | `$03` | 1 | **M** — No Seasons production route. |
| `$6b` | [miscellaneous1](../../external/oracles-disasm/object_code/seasons/interactions/miscellaneous1.s) | `$00-$13, $15, $17-$24, $26` | — | `$14, $16` | 3 | **M** — No Seasons production route. |
| `$6c` | [subrosiansHiding](../../external/oracles-disasm/object_code/seasons/interactions/subrosiansHiding.s) | `$00` | — | — | 1 | **M** — No Seasons production route. |
| `$6d` | [subrosiansHiding](../../external/oracles-disasm/object_code/seasons/interactions/subrosiansHiding.s) | `$00` | — | — | 2 | **M** — No Seasons production route. |
| `$6e` | [stealingFeather](../../external/oracles-disasm/object_code/seasons/interactions/stealingFeather.s) | `$01-$02` | — | — | 1 | **M** — No Seasons production route. |
| `$6f` | [bombFlower](../../external/oracles-disasm/object_code/common/interactions/bombFlower.s) | `$00` | — | `$01` | 1 | **M** — No Seasons production route. |
| `$70` | [holly](../../external/oracles-disasm/object_code/seasons/interactions/holly.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$71` | [companionScripts](../../external/oracles-disasm/object_code/seasons/interactions/companionScripts.s) | `$00-$09` | — | — | 0 | **M** — No Seasons production route. |
| `$72` | [blaino](../../external/oracles-disasm/object_code/seasons/interactions/blaino.s) | — | `$01` | — | 1 | **M** — No Seasons production route. |
| `$73` | [animalMoblinBullies](../../external/oracles-disasm/object_code/seasons/interactions/animalMoblinBullies.s) | `$00-$02` | — | `$03-$05` | 3 | **M** — No Seasons production route. |
| `$74` | [74](../../external/oracles-disasm/object_code/seasons/interactions/74.s) | `$02, $04, $07, $0c` | `$00-$01, $03, $05-$06, $08-$09` | — | 1 | **M** — No Seasons production route. |
| `$75` | [75](../../external/oracles-disasm/object_code/seasons/interactions/75.s) | — | — | — | 2 | **M** — No Seasons production route. |
| `$76` | [sunkenCityBullies](../../external/oracles-disasm/object_code/seasons/interactions/sunkenCityBullies.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$77` | [77](../../external/oracles-disasm/object_code/seasons/interactions/77.s) | — | — | — | 1 | **R** — Opening temple-sinking scenery `$00-$01`. |
| `$78` | [switchTileToggler](../../external/oracles-disasm/object_code/common/interactions/switchTileToggler.s) | `$01-$02, $04, $08, $10` | — | — | 0 | **M** — No Seasons production route. |
| `$79` | [movingPlatform](../../external/oracles-disasm/object_code/common/interactions/movingPlatform.s) | `$00-$01, $05, $08-$09, $10-$11, $13, $15, $18, $1b, $1d, $20, $24-$25, $28, $2b-$2c, $30, $34, $38, $40, $48` | — | — | 0 | **M** — No Seasons production route. |
| `$7a` | [roller](../../external/oracles-disasm/object_code/common/interactions/roller.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$7b` | [magnetSpinner](../../external/oracles-disasm/object_code/seasons/interactions/magnetSpinner.s) | `$00, $84` | — | — | 0 | **M** — No Seasons production route. |
| `$7c` | [trampoline](../../external/oracles-disasm/object_code/seasons/interactions/trampoline.s) | `$00-$01` | — | — | 1 | **M** — No Seasons production route. |
| `$7d` | [spinner](../../external/oracles-disasm/object_code/common/interactions/spinner.s) | `$00` | — | — | 1 | **R** — Native placed parent/arrow handler in `$04:$26`, `$04:$b1`, `$05:$6f`; shared Link state `$08/$0b` handoff. |
| `$7e` | [minibossPortal](../../external/oracles-disasm/object_code/common/interactions/minibossPortal.s) | `$00-$02` | — | `$00` | 3 | **M** — No Seasons production route. |
| `$7f` | [essence](../../external/oracles-disasm/object_code/common/interactions/essence.s) | `$00` | — | — | 2 | **M** — No Seasons production route. |
| `$80` | [fickleOldMan](../../external/oracles-disasm/object_code/seasons/interactions/fickleOldMan.s) | `$00-$03` | — | — | 0 | **M** — No Seasons production route. |
| `$81` | [subrosianShop](../../external/oracles-disasm/object_code/seasons/interactions/subrosianShop.s) | `$00, $04, $0a, $0c-$0d` | — | — | 0 | **M** — No Seasons production route. |
| `$82` | [horonDog](../../external/oracles-disasm/object_code/seasons/interactions/horonDog.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$83` | [ballThrownToDog](../../external/oracles-disasm/object_code/seasons/interactions/ballThrownToDog.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$84` | [sparkle](../../external/oracles-disasm/object_code/seasons/interactions/sparkle.s) | — | — | — | 19 | **M** — No Seasons production route. |
| `$85` | [introSceneMusic](../../external/oracles-disasm/object_code/seasons/interactions/introSceneMusic.s) | `$00` | — | — | 0 | **R** — Opening carnival-music route `$00`. |
| `$86` | [templeSinkingExplosion](../../external/oracles-disasm/object_code/seasons/interactions/templeSinkingExplosion.s) | — | — | — | 1 | **R** — Opening temple explosions `$00-$04`. |
| `$87` | [makuTree](../../external/oracles-disasm/object_code/seasons/interactions/makuTree.s) | `$00` | `$01-$02` | — | 0 | **M** — No Seasons production route. |
| `$88` | [88](../../external/oracles-disasm/object_code/seasons/interactions/88.s) | `$01` | — | — | 1 | **P** — Opening castle lightning `$00-$01`; the later room/story branches are missing. |
| `$89` | [vasu](../../external/oracles-disasm/object_code/common/interactions/vasu.s) | `$00-$01, $06` | — | — | 0 | **M** — No Seasons production route. |
| `$8a` | [sunkenCityNpcs](../../external/oracles-disasm/object_code/seasons/interactions/sunkenCityNpcs.s) | `$00-$04` | — | — | 1 | **M** — No Seasons production route. |
| `$8b` | [sunkenCityNpcs](../../external/oracles-disasm/object_code/seasons/interactions/sunkenCityNpcs.s) | `$00-$04` | — | — | 2 | **M** — No Seasons production route. |
| `$8c` | [flyingRooster](../../external/oracles-disasm/object_code/seasons/interactions/flyingRooster.s) | `$00-$01` | — | — | 1 | **M** — No Seasons production route. |
| `$8d` | [sunkenCityNpcs](../../external/oracles-disasm/object_code/seasons/interactions/sunkenCityNpcs.s) | `$00-$03` | — | — | 1 | **M** — No Seasons production route. |
| `$8e` | [8e](../../external/oracles-disasm/object_code/seasons/interactions/8e.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$8f` | [oldManWithJewel](../../external/oracles-disasm/object_code/seasons/interactions/oldManWithJewel.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$90` | [jewelHelper](../../external/oracles-disasm/object_code/seasons/interactions/jewelHelper.s) | `$00-$07` | — | — | 1 | **M** — No Seasons production route. |
| `$91` | [bubble](../../external/oracles-disasm/object_code/common/interactions/bubble.s) | — | — | — | 2 | **M** — No Seasons production route. |
| `$92` | [jewel](../../external/oracles-disasm/object_code/seasons/interactions/jewel.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$93` | [makuSeed](../../external/oracles-disasm/object_code/seasons/interactions/makuSeed.s) | `$00` | — | — | 1 | **M** — No Seasons production route. |
| `$94` | [ghastlyDoll](../../external/oracles-disasm/object_code/seasons/interactions/ghastlyDoll.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$95` | [kingMoblin](../../external/oracles-disasm/object_code/seasons/interactions/kingMoblin.s) | `$00` | — | — | 4 | **M** — No Seasons production route. |
| `$96` | [moblin](../../external/oracles-disasm/object_code/seasons/interactions/moblin.s) | — | `$00` | — | 10 | **M** — No Seasons production route. |
| `$97` | [97](../../external/oracles-disasm/object_code/seasons/interactions/97.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$98` | [woodenTunnel](../../external/oracles-disasm/object_code/common/interactions/woodenTunnel.s) | `$00-$03` | — | — | 0 | **M** — No Seasons production route. |
| `$99` | [oldManWithRupees](../../external/oracles-disasm/object_code/seasons/interactions/oldManWithRupees.s) | `$00-$07` | — | — | 0 | **M** — No Seasons production route. |
| `$9a` | [9a](../../external/oracles-disasm/object_code/seasons/interactions/9a.s) | `$02` | — | — | 2 | **M** — No Seasons production route. |
| `$9b` | [9b](../../external/oracles-disasm/object_code/seasons/interactions/9b.s) | — | `$00` | — | 0 | **M** — No Seasons production route. |
| `$9c` | [springBloomFlower](../../external/oracles-disasm/object_code/seasons/interactions/springBloomFlower.s) | `$00` | `$00` | — | 0 | **M** — No Seasons production route. |
| `$9d` | [impa](../../external/oracles-disasm/object_code/seasons/interactions/impa.s) | `$00-$01, $03-$05` | — | — | 1 | **P** — Standard opening `$00`, its `$01` handoff and the standard-file `$04` deletion guard; other placed contexts and linked activation are missing. |
| `$9e` | [samasaDesertGate](../../external/oracles-disasm/object_code/seasons/interactions/samasaDesertGate.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$9f` | [exclamationMark](../../external/oracles-disasm/object_code/common/interactions/exclamationMark.s) | — | — | — | 2 | **R** — Shared `$00` body with native pack graphics for opening, helper and silent companion-hover allocations. |
| `$a0` | [floatingImage](../../external/oracles-disasm/object_code/common/interactions/floatingImage.s) | — | — | — | 1 | **P** — Native music-note `$01` allocation, motion and lifetime; snore `$00` and other producers remain unbound. |
| `$a1` | [movingSidescrollPlatform](../../external/oracles-disasm/object_code/common/interactions/movingSidescrollPlatform.s) | `$00-$09, $0b-$0f, $13-$14` | — | — | 0 | **M** — No Seasons production route. |
| `$a2` | [movingSidescrollConveyor](../../external/oracles-disasm/object_code/common/interactions/movingSidescrollConveyor.s) | `$00-$04` | — | — | 0 | **M** — No Seasons production route. |
| `$a3` | [disappearingSidescrollPlatform](../../external/oracles-disasm/object_code/seasons/interactions/disappearingSidescrollPlatform.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$a4` | [subrosianSmithy](../../external/oracles-disasm/object_code/seasons/interactions/subrosianSmithy.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$a5` | [din](../../external/oracles-disasm/object_code/seasons/interactions/din.s) | `$08-$09` | `$02-$05, $07` | — | 3 | **P** — Standard collapsed-Link discovery `$09`; ending and linked contexts are missing. |
| `$a6` | [dinsCrystalFading](../../external/oracles-disasm/object_code/seasons/interactions/dinsCrystalFading.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$a7` | [endgameCutsceneBipsomFamily](../../external/oracles-disasm/object_code/common/interactions/endgameCutsceneBipsomFamily.s) | — | `$00-$03` | — | 0 | **M** — No Seasons production route. |
| `$a8` | [a8](../../external/oracles-disasm/object_code/seasons/interactions/a8.s) | — | `$00, $02-$04, $13-$14, $24, $34, $44, $54` | — | 0 | **M** — No Seasons production route. |
| `$a9` | [a9](../../external/oracles-disasm/object_code/seasons/interactions/a9.s) | — | `$00-$05` | — | 0 | **M** — No Seasons production route. |
| `$aa` | [aa](../../external/oracles-disasm/object_code/seasons/interactions/aa.s) | — | `$00, $02-$03` | — | 0 | **M** — No Seasons production route. |
| `$ab` | [moblinKeepScenes](../../external/oracles-disasm/object_code/seasons/interactions/moblinKeepScenes.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$ac` | [bipinBlossomFamilySpawner](../../external/oracles-disasm/object_code/common/interactions/bipinBlossomFamilySpawner.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$ad` | [ad](../../external/oracles-disasm/object_code/seasons/interactions/ad.s) | — | `$00-$04` | — | 0 | **M** — No Seasons production route. |
| `$ae` | [creditsTextHorizontal](../../external/oracles-disasm/object_code/common/interactions/creditsTextHorizontal.s) | — | — | — | 2 | **M** — No Seasons production route. |
| `$af` | [creditsTextVertical](../../external/oracles-disasm/object_code/common/interactions/creditsTextVertical.s) | — | — | — | 2 | **M** — No Seasons production route. |
| `$b0` | [twinrovaFlame](../../external/oracles-disasm/object_code/common/interactions/twinrovaFlame.s) | `$08-$0a` | `$08-$0d` | — | 9 | **M** — No Seasons production route. |
| `$b1` | [shipPiratian](../../external/oracles-disasm/object_code/seasons/interactions/shipPiratian.s) | `$10-$16, $18-$1a` | `$01, $03, $07-$09, $0d-$0e` | `$02, $0a-$0c` | 4 | **M** — No Seasons production route. |
| `$b2` | [shipPiratian](../../external/oracles-disasm/object_code/seasons/interactions/shipPiratian.s) | `$02` | `$00` | `$01, $03` | 2 | **M** — No Seasons production route. |
| `$b3` | [linkedCutscene](../../external/oracles-disasm/object_code/seasons/interactions/linkedCutscene.s) | `$00, $02` | `$01, $03` | — | 1 | **M** — No Seasons production route. |
| `$b4` | [b4](../../external/oracles-disasm/object_code/seasons/interactions/b4.s) | — | — | — | 3 | **M** — No Seasons production route. |
| `$b5` | [finalDungeonEnergy](../../external/oracles-disasm/object_code/common/interactions/finalDungeonEnergy.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$b6` | [gashaSpot](../../external/oracles-disasm/object_code/common/interactions/gashaSpot.s) | `$00-$0f` | — | — | 0 | **M** — No Seasons production route. |
| `$b7` | [kissHeart](../../external/oracles-disasm/object_code/common/interactions/kissHeart.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$b8` | [ambi](../../external/oracles-disasm/object_code/seasons/interactions/ambi.s) | `$00-$02, $04` | — | — | 1 | **M** — No Seasons production route. |
| `$b9` | [horonDogCredits](../../external/oracles-disasm/object_code/common/interactions/horonDogCredits.s) | — | `$00-$07` | — | 0 | **M** — No Seasons production route. |
| `$ba` | [ba](../../external/oracles-disasm/object_code/seasons/interactions/ba.s) | — | — | `$01` | 4 | **M** — No Seasons production route. |
| `$bb` | [bb](../../external/oracles-disasm/object_code/seasons/interactions/bb.s) | — | `$01-$02` | — | 2 | **M** — No Seasons production route. |
| `$bc` | [bc_bd_be](../../external/oracles-disasm/object_code/seasons/interactions/bc_bd_be.s) | `$04` | `$03` | — | 4 | **M** — No Seasons production route. |
| `$bd` | [bc_bd_be](../../external/oracles-disasm/object_code/seasons/interactions/bc_bd_be.s) | — | — | — | 4 | **M** — No Seasons production route. |
| `$be` | [bc_bd_be](../../external/oracles-disasm/object_code/seasons/interactions/bc_bd_be.s) | — | — | — | 4 | **M** — No Seasons production route. |
| `$bf` | [bf](../../external/oracles-disasm/object_code/seasons/interactions/bf.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$c0` | [banana](../../external/oracles-disasm/object_code/common/interactions/banana.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$c1` | [c1](../../external/oracles-disasm/object_code/common/interactions/c1.s) | — | `$00` | — | 0 | **M** — No Seasons production route. |
| `$c2` | [mayorsHouseUnlinkedGirl](../../external/oracles-disasm/object_code/seasons/interactions/mayorsHouseUnlinkedGirl.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$c3` | [zeldaKidnappedRoom](../../external/oracles-disasm/object_code/seasons/interactions/zeldaKidnappedRoom.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$c4` | [zeldaVillagersRoom](../../external/oracles-disasm/object_code/seasons/interactions/zeldaVillagersRoom.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$c5` | [d4HolesFloortrapRoom](../../external/oracles-disasm/object_code/seasons/interactions/d4HolesFloortrapRoom.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$c6` | [herosCaveSwordChest](../../external/oracles-disasm/object_code/seasons/interactions/herosCaveSwordChest.s) | `$00` | — | — | 0 | **R** — Unlinked Hero's Cave sword chest, native spin handoff and direct return warp. |
| `$c7` | [createObjectAtEachTileindex](../../external/oracles-disasm/object_code/common/interactions/createObjectAtEachTileindex.s) | `$04, $08, $bc` | `$a0` | — | 0 | **M** — No Seasons production route. |
| `$c8` | [boomerangSubrosian](../../external/oracles-disasm/object_code/seasons/interactions/boomerangSubrosian.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$c9` | [boomerang](../../external/oracles-disasm/object_code/seasons/interactions/boomerang.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$ca` | [troy](../../external/oracles-disasm/object_code/seasons/interactions/troy.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$cb` | [linkedGameGhini](../../external/oracles-disasm/object_code/seasons/interactions/linkedGameGhini.s) | `$00, $03` | — | — | 1 | **M** — No Seasons production route. |
| `$cc` | [goldenCaveSubrosian](../../external/oracles-disasm/object_code/seasons/interactions/goldenCaveSubrosian.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$cd` | [linkedMasterDiver](../../external/oracles-disasm/object_code/seasons/interactions/linkedMasterDiver.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$ce` | [businessScrub](../../external/oracles-disasm/object_code/common/interactions/businessScrub.s) | `$00, $03, $06, $09-$0a` | — | — | 1 | **M** — No Seasons production route. |
| `$cf` | [cf](../../external/oracles-disasm/object_code/common/interactions/cf.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$d0` | [companionTutorial](../../external/oracles-disasm/object_code/common/interactions/companionTutorial.s) | `$00-$06` | — | — | 0 | **M** — No Seasons production route. |
| `$d1` | [gameCompleteDialog](../../external/oracles-disasm/object_code/common/interactions/gameCompleteDialog.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$d2` | [titlescreenClouds](../../external/oracles-disasm/object_code/common/interactions/titlescreenClouds.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$d3` | [introBird](../../external/oracles-disasm/object_code/common/interactions/introBird.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$d4` | [linkShip](../../external/oracles-disasm/object_code/common/interactions/linkShip.s) | — | `$00, $02, $11, $21, $31, $41, $51, $61, $71` | — | 0 | **M** — No Seasons production route. |
| `$d5` | [greatFairy](../../external/oracles-disasm/object_code/seasons/interactions/greatFairy.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$d6` | [dekuScrub](../../external/oracles-disasm/object_code/seasons/interactions/dekuScrub.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$d7` | [d7](../../external/oracles-disasm/object_code/seasons/interactions/d7.s) | — | — | — | 0 | **D** — Original dispatch deletes the object. |
| `$d8` | [linkedFountainLady](../../external/oracles-disasm/object_code/seasons/interactions/linkedFountainLady.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$d9` | [faroreGiveItem](../../external/oracles-disasm/object_code/common/interactions/faroreGiveItem.s) | — | — | — | 1 | **M** — No Seasons production route. |
| `$da` | [zeldaApproachTrigger](../../external/oracles-disasm/object_code/common/interactions/zeldaApproachTrigger.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$db` | [linkedSecredGivers](../../external/oracles-disasm/object_code/seasons/interactions/linkedSecredGivers.s) | `$00-$02` | — | — | 0 | **M** — No Seasons production route. |
| `$dc` | [miscPuzzles](../../external/oracles-disasm/object_code/seasons/interactions/miscPuzzles.s) | `$00-$0f` | — | — | 1 | **P** — Unlinked $0:d4 $01/$02 deletion paths; linked entrance and other puzzle variants remain unbound. |
| `$dd` | [goldenBeastOldMan](../../external/oracles-disasm/object_code/seasons/interactions/goldenBeastOldMan.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$de` | [makuSeedAndEssences](../../external/oracles-disasm/object_code/seasons/interactions/makuSeedAndEssences.s) | — | — | — | 2 | **M** — No Seasons production route. |
| `$df` | [nayruRalphCredits](../../external/oracles-disasm/object_code/common/interactions/nayruRalphCredits.s) | — | `$00-$01` | — | 0 | **M** — No Seasons production route. |
| `$e0` | [eraOrSeasonInfo](../../external/oracles-disasm/object_code/common/interactions/eraOrSeasonInfo.s) | — | — | — | 1 | **P** — Shared banner after bound ordinary full-room loads; placed contexts and unsupported room/event producers missing. |
| `$e1` | [portalSpawner](../../external/oracles-disasm/object_code/seasons/interactions/portalSpawner.s) | `$00-$01` | — | — | 1 | **M** — No Seasons production route. |
| `$e2` | [statueEyeball](../../external/oracles-disasm/object_code/common/interactions/statueEyeball.s) | `$00-$01, $03` | — | — | 1 | **M** — No Seasons production route. |
| `$e3` | [vire](../../external/oracles-disasm/object_code/seasons/interactions/vire.s) | — | — | — | 0 | **M** — No Seasons production route. |
| `$e4` | [linkedHerosCaveOldMan](../../external/oracles-disasm/object_code/seasons/interactions/linkedHerosCaveOldMan.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |
| `$e5` | [ringHelpBook](../../external/oracles-disasm/object_code/common/interactions/ringHelpBook.s) | `$00-$01` | — | — | 0 | **M** — No Seasons production route. |
| `$e6` | [getRodOfSeasons](../../external/oracles-disasm/object_code/seasons/interactions/getRodOfSeasons.s) | `$00` | — | `$02` | 3 | **M** — No Seasons production route. |
| `$e7` | [loneZora](../../external/oracles-disasm/object_code/seasons/interactions/loneZora.s) | `$00` | — | — | 0 | **M** — No Seasons production route. |

## Room interaction inventory

All object-list interaction rows are included here, including character rows
also summarized in the NPC ledger. No-value ensemble spawners remain listed as
controllers here; their explicitly selected character lists are expanded in
the NPC ledger. **R** applies to the supported standard-opening placement only.
An opening implementation of the same ID does not cover another room's placement.

| Room | Source records in source order |
| --- | --- |
| `0:00` | [M] `$b3:$00/v$00` linkedCutscene<br>[M] `$22:$05/v$00` makuCutscenes |
| `0:01` | [M] `$6b:$10/v$00` miscellaneous1 |
| `0:03` | [M] `$88:$01/v$00` 88 |
| `0:08` | [M] `$52:$00/v$00` biggoron |
| `0:0c` | [M] `$8c:$00/v$00` flyingRooster |
| `0:0d` | [M] `$6b:$03/v$00` miscellaneous1 |
| `0:0f` | [M] `$6b:$21/v$00` miscellaneous1 |
| `0:11` | [M] `$9c:$00/v$00` springBloomFlower |
| `0:14` | [M] `$dc:$0e/v$00` miscPuzzles |
| `0:15` | [M] `$dc:$0e/v$00` miscPuzzles |
| `0:17` | [M] `$dc:$0e/v$00` miscPuzzles |
| `0:1a` | [M] `$6b:$05/v$00` miscellaneous1<br>[M] `$d0:$06/v$00` companionTutorial |
| `0:1d` | [M] `$22:$03/v$00` makuCutscenes<br>[M] `$1f:$0a/v$00` specialWarp |
| `0:1e` | [M] `$e1:$00/v$00` portalSpawner |
| `0:1f` | [M] `$b6:$00/v$00` gashaSpot<br>[M] `$8c:$01/v$00` flyingRooster |
| `0:22` | [M] `$b6:$01/v$00` gashaSpot |
| `0:23` | [M] `$6b:$12/v$00` miscellaneous1 |
| `0:24` | [M] `$dc:$0e/v$00` miscPuzzles |
| `0:25` | [M] `$e1:$00/v$00` portalSpawner |
| `0:26` | [M] `$dc:$0e/v$00` miscPuzzles |
| `0:27` | [M] `$dc:$0e/v$00` miscPuzzles |
| `0:2a` | [M] `$d0:$05/v$00` companionTutorial |
| `0:2b` | [M] `$9c:$00/v$00` springBloomFlower |
| `0:2d` | [M] `$6b:$0a/v$00` miscellaneous1 |
| `0:2e` | [M] `$1f:$02/v$00` specialWarp<br>[M] `$1f:$03/v$00` specialWarp |
| `0:2f` | [M] `$ce:$06/v$00` businessScrub<br>[M] `$5f:$05/v$00` companionSpawner<br>[M] `$71:$06/v$00` companionScripts |
| `0:30` | [M] `$6b:$08/v$00` miscellaneous1 |
| `0:33` | [M] `$6b:$1e/v$00` miscellaneous1 |
| `0:38` | [M] `$b6:$02/v$00` gashaSpot |
| `0:3b` | [M] `$b6:$03/v$00` gashaSpot |
| `0:3d` | [M] `$1f:$08/v$00` specialWarp |
| `0:3f` | [M] `$b6:$05/v$00` gashaSpot |
| `0:42` | [M] `$6b:$07/v$00` miscellaneous1<br>[M] `$6b:$07/v$00` miscellaneous1 |
| `0:44` | [M] `$98:$03/v$00` woodenTunnel<br>[M] `$b6:$04/v$00` gashaSpot |
| `0:45` | [M] `$98:$02/v$00` woodenTunnel |
| `0:4b` | [M] `$74:$0c/v$00` 74 |
| `0:4d` | [M] `$36:$81/v$00` miscNpcs<br>[M] `$36:$82/v$00` miscNpcs<br>[M] `$36:$84/v$00` miscNpcs<br>[M] `$1f:$09/v$00` specialWarp |
| `0:4e` | [M] `$d0:$03/v$00` companionTutorial |
| `0:4f` | [M] `$d0:$03/v$00` companionTutorial |
| `0:52` | [M] `$6b:$06/v$00` miscellaneous1 |
| `0:53` | [M] `$9c:$00/v$00` springBloomFlower |
| `0:54` | [M] `$5f:$01/v$00` companionSpawner<br>[M] `$71:$03/v$00` companionScripts |
| `0:56` | [M] `$6b:$11/v$00` miscellaneous1<br>[M] `$98:$00/v$00` woodenTunnel |
| `0:5a` | [M] `$1f:$04/v$00` specialWarp (season mask $04) |
| `0:5b` | [M] `$ab:$02/v$00` moblinKeepScenes<br>[M] `$db:$00/v$00` linkedSecredGivers |
| `0:5c` | [M] `$ab:$00/v$00` moblinKeepScenes |
| `0:5d` | [M] `$71:$02/v$00` companionScripts<br>[M] `$71:$08/v$00` companionScripts<br>[M] `$36:$80/v$00` miscNpcs<br>[M] `$36:$83/v$00` miscNpcs<br>[M] `$8b:$01/v$00` sunkenCityNpcs<br>[M] `$8b:$02/v$00` sunkenCityNpcs<br>[M] `$8b:$03/v$00` sunkenCityNpcs<br>[M] `$8b:$04/v$00` sunkenCityNpcs<br>[M] `$2e:$00/v$00` fickleGirl<br>[M] `$2e:$01/v$00` fickleGirl<br>[M] `$2e:$04/v$00` fickleGirl |
| `0:5e` | [M] `$3e:$30/v$00` miscBoyNpcs<br>[M] `$3e:$31/v$00` miscBoyNpcs<br>[M] `$3e:$32/v$00` miscBoyNpcs<br>[M] `$3e:$33/v$00` miscBoyNpcs<br>[M] `$3e:$34/v$00` miscBoyNpcs<br>[M] `$8a:$01/v$00` sunkenCityNpcs<br>[M] `$b8:$01/v$00` ambi |
| `0:60` | [M] `$22:$02/v$00` makuCutscenes |
| `0:63` | [M] `$98:$00/v$00` woodenTunnel<br>[M] `$90:$00/v$00` jewelHelper |
| `0:64` | [M] `$5f:$06/v$00` companionSpawner |
| `0:66` | [M] `$98:$01/v$00` woodenTunnel<br>[M] `$6b:$1a/v$00` miscellaneous1 |
| `0:6d` | [M] `$2e:$02/v$00` fickleGirl<br>[M] `$2e:$03/v$00` fickleGirl<br>[M] `$5f:$04/v$00` companionSpawner<br>[M] `$71:$05/v$00` companionScripts<br>[M] `$76:$00/v$00` sunkenCityBullies<br>[M] `$76:$01/v$00` sunkenCityBullies<br>[M] `$76:$02/v$00` sunkenCityBullies |
| `0:6e` | [M] `$1f:$0d/v$00` specialWarp<br>[M] `$3e:$31/v$00` miscBoyNpcs |
| `0:6f` | [M] `$9a:$02/v$00` 9a<br>[M] `$71:$07/v$00` companionScripts |
| `0:71` | [M] `$27:$02/v$00` sokra |
| `0:73` | [M] `$98:$01/v$00` woodenTunnel |
| `0:74` | [M] `$d0:$00/v$00` companionTutorial |
| `0:75` | [M] `$b6:$06/v$00` gashaSpot |
| `0:77` | [M] `$c3:$00/v$00` zeldaKidnappedRoom |
| `0:7c` | [M] `$9c:$00/v$00` springBloomFlower |
| `0:7f` | [M] `$1f:$00/v$00` specialWarp<br>[M] `$70:$01/v$00` holly |
| `0:80` | [M] `$b6:$07/v$00` gashaSpot<br>[M] `$ce:$00/v$00` businessScrub |
| `0:81` | [M] `$6b:$02/v$00` miscellaneous1 |
| `0:82` | [M] `$6b:$18/v$00` miscellaneous1 |
| `0:83` | [M] `$71:$00/v$00` companionScripts |
| `0:87` | [M] `$71:$09/v$00` companionScripts<br>[M] `$6b:$13/v$00` miscellaneous1 |
| `0:89` | [M] `$b6:$08/v$00` gashaSpot |
| `0:8a` | [M] `$22:$04/v$00` makuCutscenes |
| `0:8d` | [M] `$22:$01/v$00` makuCutscenes |
| `0:90` | [M] `$5f:$03/v$00` companionSpawner<br>[M] `$71:$01/v$00` companionScripts<br>[M] `$73:$00/v$00` animalMoblinBullies<br>[M] `$73:$01/v$00` animalMoblinBullies<br>[M] `$73:$02/v$00` animalMoblinBullies |
| `0:92` | [M] `$5f:$02/v$00` companionSpawner<br>[M] `$71:$04/v$00` companionScripts<br>[M] `$73:$00/v$00` animalMoblinBullies<br>[M] `$73:$01/v$00` animalMoblinBullies<br>[M] `$73:$02/v$00` animalMoblinBullies |
| `0:93` | [M] `$d0:$01/v$00` companionTutorial |
| `0:95` | [M] `$b6:$09/v$00` gashaSpot |
| `0:96` | [M] `$22:$00/v$00` makuCutscenes<br>[M] `$21:$00/v$00` gnarledKeyhole |
| `0:97` | [R] `$85:$00/v$00` introSceneMusic |
| `0:98` | [R] `$4e:$0b/v$00` dinDancingEvent |
| `0:9a` | [M] `$e1:$00/v$00` portalSpawner |
| `0:9b` | [M] `$27:$01/v$00` sokra |
| `0:a1` | [M] `$d0:$04/v$00` companionTutorial |
| `0:a6` | [M] `$b6:$0a/v$00` gashaSpot |
| `0:a7` | [R] `$a5:$09/v$00` din |
| `0:ac` | [M] `$b6:$0b/v$00` gashaSpot |
| `0:af` | [M] `$6b:$0a/v$00` miscellaneous1 |
| `0:b0` | [M] `$e1:$00/v$00` portalSpawner |
| `0:b1` | [M] `$6b:$0a/v$00` miscellaneous1 |
| `0:b6` | [R] `$9d:$00/v$00` impa<br>[P] `$9d:$04/v$00` impa |
| `0:b7` | [M] `$26:$00/v$00` mittensAndOwner<br>[M] `$26:$01/v$00` mittensAndOwner<br>[M] `$25:$00/v$00` mittensAndOwner<br>[M] `$25:$01/v$00` mittensAndOwner |
| `0:b9` | [M] `$e1:$00/v$00` portalSpawner |
| `0:bd` | [M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$01/v$00` quicksand<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:be` | [M] `$5e:$00/v$00` quicksand<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:bf` | [M] `$4d:$00/v$00` pirateSkull |
| `0:c0` | [M] `$b6:$0c/v$00` gashaSpot |
| `0:c2` | [M] `$d0:$02/v$00` companionTutorial<br>[M] `$d0:$05/v$00` companionTutorial |
| `0:c5` | [M] `$3c:$80/v$00` miscNpcs<br>[M] `$3e:$10/v$00` miscBoyNpcs |
| `0:c6` | [M] `$9c:$00/v$00` springBloomFlower<br>[M] `$3e:$20/v$00` miscBoyNpcs<br>[M] `$2d:$80/v$00` miscNpcs<br>[M] `$2d:$86/v$00` miscNpcs<br>[M] `$37:$81/v$00` miscNpcs<br>[M] `$80:$02/v$00` fickleOldMan<br>[M] `$9d:$05/v$00` impa<br>[M] `$2a:$0b/v$00` bird |
| `0:c8` | [M] `$b6:$0f/v$00` gashaSpot |
| `0:c9` | [M] `$59:$00/v$00` lostWoodsSword |
| `0:cb` | [M] `$6c:$00/v$00` subrosiansHiding |
| `0:cc` | [M] `$9c:$00/v$00` springBloomFlower |
| `0:cd` | [M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$00/v$00` quicksand<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:ce` | [M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$00/v$00` quicksand |
| `0:cf` | [M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$00/v$00` quicksand |
| `0:d0` | [M] `$22:$06/v$00` makuCutscenes |
| `0:d1` | [M] `$6b:$0a/v$00` miscellaneous1 |
| `0:d4` | [P] `$dc:$01/v$00` miscPuzzles<br>[P] `$dc:$02/v$00` miscPuzzles |
| `0:d6` | [M] `$37:$80/v$00` miscNpcs<br>[M] `$2d:$81/v$00` miscNpcs<br>[M] `$2d:$83/v$00` miscNpcs<br>[M] `$2d:$85/v$00` miscNpcs<br>[M] `$3e:$04/v$00` miscBoyNpcs<br>[M] `$3e:$11/v$00` miscBoyNpcs<br>[M] `$a5:$08/v$00` din<br>[M] `$4e:$0a/v$00` dinDancingEvent |
| `0:d7` | [M] `$6b:$1b/v$00` miscellaneous1 |
| `0:d8` | [M] `$6b:$0a/v$00` miscellaneous1<br>[M] `$8a:$04/v$00` sunkenCityNpcs |
| `0:d9` | [M] `$22:$0a/v$00` makuCutscenes<br>[M] `$2d:$84/v$00` miscNpcs<br>[M] `$37:$84/v$00` miscNpcs |
| `0:da` | [M] `$4c:$00/v$00` miscStatusObjects<br>[M] `$58:$00/v$00` guruguru<br>[M] `$bc:$04/v$00` bc_bd_be |
| `0:dc` | [M] `$5e:$02/v$00` quicksand<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:dd` | [M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$03/v$00` quicksand<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:de` | [M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$00/v$00` quicksand<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:df` | [M] `$5e:$00/v$00` quicksand<br>[M] `$5e:$04/v$00` quicksand<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:e0` | [M] `$6b:$1c/v$00` miscellaneous1 |
| `0:e2` | [M] `$74:$04/v$00` 74<br>[M] `$b2:$02/v$00` shipPiratian<br>[M] `$b1:$10/v$00` shipPiratian |
| `0:e4` | [M] `$90:$02/v$00` jewelHelper |
| `0:e6` | [M] `$2d:$82/v$00` miscNpcs |
| `0:e7` | [M] `$3d:$80/v$00` miscNpcs<br>[M] `$d8:$00/v$00` linkedFountainLady<br>[M] `$4c:$07/v$00` miscStatusObjects<br>[M] `$4c:$08/v$00` miscStatusObjects |
| `0:e9` | [M] `$27:$00/v$00` sokra<br>[M] `$37:$83/v$00` miscNpcs<br>[M] `$80:$01/v$00` fickleOldMan<br>[M] `$6b:$0b/v$00` miscellaneous1<br>[M] `$6b:$0b/v$00` miscellaneous1<br>[M] `$3e:$00/v$00` miscBoyNpcs<br>[M] `$3e:$01/v$00` miscBoyNpcs<br>[M] `$3e:$03/v$00` miscBoyNpcs<br>[M] `$3e:$12/v$00` miscBoyNpcs<br>[M] `$3e:$13/v$00` miscBoyNpcs<br>[M] `$c4:$00/v$00` zeldaVillagersRoom |
| `0:ea` | [M] `$98:$00/v$00` woodenTunnel |
| `0:ec` | [M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:ed` | [M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:ee` | [M] `$74:$02/v$00` 74<br>[M] `$6b:$15/v$00` miscellaneous1 |
| `0:ef` | [M] `$b6:$0d/v$00` gashaSpot<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `0:f0` | [M] `$b6:$0e/v$00` gashaSpot |
| `0:f2` | [M] `$74:$07/v$00` 74<br>[M] `$b1:$11/v$00` shipPiratian |
| `0:f4` | [M] `$90:$03/v$00` jewelHelper |
| `0:f6` | [M] `$3e:$02/v$00` miscBoyNpcs |
| `0:f7` | [M] `$80:$00/v$00` fickleOldMan<br>[M] `$80:$03/v$00` fickleOldMan<br>[M] `$37:$85/v$00` miscNpcs<br>[M] `$dc:$0b/v$00` miscPuzzles |
| `0:f8` | [M] `$37:$82/v$00` miscNpcs |
| `0:fa` | [M] `$98:$01/v$00` woodenTunnel |
| `0:fb` | [M] `$b8:$02/v$00` ambi |
| `0:fc` | [M] `$9e:$00/v$00` samasaDesertGate<br>[M] `$40:$0a/v$00` piratian |
| `0:fd` | [M] `$5e:$05/v$00` quicksand<br>[M] `$c7:$bc/v$00` createObjectAtEachTileindex |
| `1:00` | [M] `$b3:$02/v$00` linkedCutscene<br>[M] `$22:$07/v$00` makuCutscenes<br>[M] `$e2:$00/v$00` statueEyeball<br>[M] `$e2:$00/v$00` statueEyeball |
| `1:03` | [M] `$30:$15/v$00` subrosian<br>[M] `$30:$16/v$00` subrosian |
| `1:05` | [M] `$e1:$00/v$00` portalSpawner<br>[M] `$30:$17/v$00` subrosian<br>[M] `$30:$18/v$00` subrosian<br>[M] `$6b:$26/v$00` miscellaneous1 |
| `1:06` | [M] `$6b:$09/v$00` miscellaneous1<br>[M] `$33:$80/v$00` miscNpcs<br>[M] `$33:$81/v$00` miscNpcs<br>[M] `$33:$82/v$00` miscNpcs |
| `1:07` | [M] `$6b:$09/v$00` miscellaneous1<br>[M] `$51:$00/v$00` smallVolcano |
| `1:0a` | [M] `$53:$00/v$00` headSmelter<br>[M] `$30:$00/v$00` subrosian |
| `1:0b` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:0c` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:10` | [M] `$51:$00/v$00` smallVolcano |
| `1:11` | [M] `$32:$01/v$00` subrosianWithBuckets |
| `1:13` | [M] `$e1:$00/v$00` portalSpawner<br>[M] `$6b:$0b/v$00` miscellaneous1 |
| `1:14` | [M] `$33:$83/v$00` miscNpcs<br>[M] `$33:$84/v$00` miscNpcs<br>[M] `$33:$85/v$00` miscNpcs |
| `1:15` | [M] `$30:$19/v$00` subrosian |
| `1:17` | [M] `$32:$02/v$00` subrosianWithBuckets |
| `1:1a` | [M] `$32:$00/v$00` subrosianWithBuckets |
| `1:1b` | [M] `$6b:$19/v$00` miscellaneous1<br>[M] `$4c:$02/v$00` miscStatusObjects |
| `1:1c` | [M] `$4c:$02/v$00` miscStatusObjects<br>[M] `$4c:$03/v$00` miscStatusObjects |
| `1:1d` | [M] `$6b:$19/v$00` miscellaneous1<br>[M] `$4c:$02/v$00` miscStatusObjects<br>[M] `$4c:$03/v$00` miscStatusObjects |
| `1:20` | [M] `$6b:$09/v$00` miscellaneous1<br>[M] `$e1:$00/v$00` portalSpawner<br>[M] `$30:$1a/v$00` subrosian |
| `1:22` | [M] `$30:$1b/v$00` subrosian |
| `1:25` | [M] `$30:$1c/v$00` subrosian<br>[M] `$c8:$00/v$00` boomerangSubrosian<br>[M] `$32:$03/v$00` subrosianWithBuckets |
| `1:26` | [M] `$30:$1d/v$00` subrosian |
| `1:29` | [M] `$23:$40/v$00` seasonSpiritsScripts |
| `1:2a` | [M] `$22:$08/v$00` makuCutscenes |
| `1:2b` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:2c` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:2d` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:30` | [M] `$51:$00/v$00` smallVolcano |
| `1:31` | [M] `$30:$1e/v$00` subrosian<br>[M] `$51:$00/v$00` smallVolcano |
| `1:34` | [M] `$51:$01/v$00` smallVolcano |
| `1:37` | [M] `$32:$04/v$00` subrosianWithBuckets |
| `1:3a` | [M] `$6b:$09/v$00` miscellaneous1 |
| `1:3b` | [M] `$93:$00/v$00` makuSeed<br>[M] `$4c:$02/v$00` miscStatusObjects |
| `1:3c` | [M] `$93:$00/v$00` makuSeed<br>[M] `$6b:$19/v$00` miscellaneous1<br>[M] `$4c:$02/v$00` miscStatusObjects |
| `1:3d` | [M] `$4c:$02/v$00` miscStatusObjects<br>[M] `$4c:$03/v$00` miscStatusObjects<br>[M] `$4c:$03/v$00` miscStatusObjects |
| `1:3e` | [M] `$4c:$02/v$00` miscStatusObjects<br>[M] `$4c:$03/v$00` miscStatusObjects<br>[M] `$4c:$03/v$00` miscStatusObjects |
| `1:40` | [M] `$6b:$18/v$00` miscellaneous1 |
| `1:41` | [M] `$30:$1f/v$00` subrosian |
| `1:42` | [M] `$6b:$09/v$00` miscellaneous1<br>[M] `$30:$20/v$00` subrosian<br>[M] `$6b:$0b/v$00` miscellaneous1<br>[M] `$51:$00/v$00` smallVolcano |
| `1:43` | [M] `$30:$0e/v$00` subrosian |
| `1:44` | [M] `$6b:$0b/v$00` miscellaneous1 |
| `1:47` | [M] `$6b:$09/v$00` miscellaneous1<br>[M] `$32:$05/v$00` subrosianWithBuckets |
| `1:48` | [M] `$6b:$0b/v$00` miscellaneous1 |
| `1:4a` | [M] `$e1:$00/v$00` portalSpawner |
| `1:4b` | [M] `$4c:$02/v$00` miscStatusObjects<br>[M] `$4c:$03/v$00` miscStatusObjects |
| `1:4c` | [M] `$4c:$02/v$00` miscStatusObjects<br>[M] `$4c:$03/v$00` miscStatusObjects |
| `1:4d` | [M] `$4c:$02/v$00` miscStatusObjects |
| `1:4e` | [M] `$4c:$02/v$00` miscStatusObjects |
| `1:51` | [M] `$6d:$00/v$00` subrosiansHiding |
| `1:52` | [M] `$6e:$02/v$00` stealingFeather<br>[M] `$30:$21/v$00` subrosian |
| `1:53` | [M] `$e1:$00/v$00` portalSpawner<br>[M] `$6e:$01/v$00` stealingFeather |
| `1:54` | [M] `$30:$0f/v$00` subrosian |
| `1:55` | [M] `$30:$22/v$00` subrosian<br>[M] `$6b:$0b/v$00` miscellaneous1<br>[M] `$6b:$0b/v$00` miscellaneous1 |
| `1:56` | [M] `$30:$0a/v$00` subrosian<br>[M] `$30:$0b/v$00` subrosian |
| `1:57` | [M] `$6b:$09/v$00` miscellaneous1<br>[M] `$e1:$00/v$00` portalSpawner<br>[M] `$51:$01/v$00` smallVolcano |
| `1:5b` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:5c` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:5d` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:5e` | [M] `$87:$00/v$00` makuTree<br>[M] `$e1:$01/v$00` portalSpawner |
| `1:65` | [M] `$30:$07/v$00` subrosian |
| `1:66` | [M] `$30:$08/v$00` subrosian<br>[M] `$31:$02/v$00` datingRosaEvent |
| `1:67` | [M] `$30:$24/v$00` subrosian |
| `1:69` | [M] `$6f:$00/v$00` bombFlower<br>[M] `$30:$25/v$00` subrosian |
| `1:6a` | [M] `$51:$01/v$00` smallVolcano |
| `1:72` | [M] `$e1:$00/v$00` portalSpawner<br>[M] `$40:$09/v$00` piratian<br>[M] `$dc:$0b/v$00` miscPuzzles |
| `1:74` | [M] `$41:$0b/v$00` piratian<br>[M] `$40:$0d/v$00` piratian<br>[M] `$40:$0e/v$00` piratian |
| `1:75` | [M] `$30:$09/v$00` subrosian |
| `1:76` | [M] `$30:$06/v$00` subrosian |
| `1:77` | [M] `$31:$00/v$00` datingRosaEvent |
| `1:7b` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:80` | [M] `$2f:$80/v$00` miscNpcs |
| `1:82` | [M] `$9d:$01/v$00` impa<br>[M] `$44:$09/v$00` zelda |
| `1:83` | [M] `$3f:$80/v$00` miscNpcs |
| `1:84` | [M] `$2a:$08/v$00` bird<br>[M] `$2a:$09/v$00` bird<br>[M] `$2a:$06/v$00` bird<br>[M] `$2a:$07/v$00` bird<br>[M] `$2a:$04/v$00` bird<br>[M] `$2a:$05/v$00` bird<br>[M] `$2a:$02/v$00` bird<br>[M] `$2a:$03/v$00` bird<br>[M] `$2a:$00/v$00` bird<br>[M] `$2a:$01/v$00` bird |
| `1:85` | [M] `$c2:$00/v$00` mayorsHouseUnlinkedGirl<br>[M] `$24:$03/v$00` miscNpcs |
| `1:86` | [M] `$24:$00/v$00` miscNpcs |
| `1:87` | [M] `$6b:$19/v$00` miscellaneous1 |
| `1:88` | [M] `$cb:$03/v$00` linkedGameGhini |
| `1:8a` | [M] `$41:$00/v$00` piratian<br>[M] `$40:$01/v$00` piratian<br>[M] `$40:$02/v$00` piratian<br>[M] `$40:$03/v$00` piratian<br>[M] `$40:$04/v$00` piratian<br>[M] `$40:$05/v$00` piratian<br>[M] `$40:$06/v$00` piratian<br>[M] `$42:$01/v$00` pirateHouseSubrosian<br>[M] `$b8:$04/v$00` ambi |
| `1:8b` | [M] `$40:$07/v$00` piratian<br>[M] `$40:$08/v$00` piratian<br>[M] `$42:$00/v$00` pirateHouseSubrosian |
| `1:8e` | [M] `$53:$01/v$00` headSmelter<br>[M] `$30:$04/v$00` subrosian<br>[M] `$30:$05/v$00` subrosian<br>[M] `$6b:$22/v$00` miscellaneous1 |
| `1:8f` | [M] `$5c:$00/v$00` lavaSoupSubrosian |
| `1:91` | [M] `$89:$00/v$00` vasu<br>[M] `$89:$01/v$00` vasu<br>[M] `$89:$06/v$00` vasu<br>[M] `$e5:$00/v$00` ringHelpBook<br>[M] `$e5:$01/v$00` ringHelpBook |
| `1:92` | [M] `$ac:$00/v$00` bipinBlossomFamilySpawner |
| `1:93` | [M] `$4c:$01/v$00` miscStatusObjects<br>[M] `$4c:$01/v$00` miscStatusObjects<br>[M] `$ac:$01/v$00` bipinBlossomFamilySpawner |
| `1:94` | [M] `$8f:$00/v$00` oldManWithJewel |
| `1:95` | [M] `$6a:$00/v$00` danceHallMinigame |
| `1:96` | [M] `$30:$23/v$00` subrosian |
| `1:97` | [M] `$34:$00/v$00` subrosianSmiths<br>[M] `$34:$01/v$00` subrosianSmiths<br>[M] `$a4:$00/v$00` subrosianSmithy |
| `1:98` | [M] `$7f:$00/v$00` essence |
| `1:99` | [M] `$57:$00/v$00` ingo<br>[M] `$4c:$09/v$00` miscStatusObjects |
| `1:9a` | [M] `$99:$00/v$00` oldManWithRupees |
| `1:9c` | [M] `$43:$80/v$00` syrup<br>[M] `$49:$80/v$00` syrupCucco |
| `1:9d` | [M] `$8a:$00/v$00` sunkenCityNpcs<br>[M] `$8a:$02/v$00` sunkenCityNpcs<br>[M] `$8a:$03/v$00` sunkenCityNpcs |
| `1:9e` | [M] `$30:$02/v$00` subrosian<br>[M] `$30:$03/v$00` subrosian |
| `1:9f` | [M] `$39:$00/v$00` miscNpcs<br>[M] `$39:$01/v$00` miscNpcs<br>[M] `$39:$02/v$00` miscNpcs<br>[M] `$39:$03/v$00` miscNpcs<br>[M] `$39:$04/v$00` miscNpcs |
| `1:a0` | [M] `$30:$0c/v$00` subrosian<br>[M] `$81:$00/v$00` subrosianShop<br>[M] `$81:$04/v$00` subrosianShop<br>[M] `$81:$0a/v$00` subrosianShop<br>[M] `$81:$0c/v$00` subrosianShop<br>[M] `$81:$0d/v$00` subrosianShop |
| `1:a1` | [M] `$30:$0d/v$00` subrosian<br>[M] `$6b:$19/v$00` miscellaneous1 |
| `1:a3` | [M] `$70:$00/v$00` holly<br>[M] `$db:$02/v$00` linkedSecredGivers |
| `1:a4` | [M] `$2c:$00/v$00` miscNpcs |
| `1:a6` | [M] `$47:$01/v$00` shopItem<br>[M] `$47:$03/v$00` shopItem<br>[M] `$47:$04/v$00` shopItem<br>[M] `$46:$00/v$00` shopkeeper |
| `1:a8` | [M] `$e1:$00/v$00` portalSpawner |
| `1:a9` | [M] `$30:$26/v$00` subrosian |
| `1:aa` | [M] `$95:$00/v$00` kingMoblin |
| `1:ab` | [M] `$e1:$00/v$00` portalSpawner<br>[M] `$6b:$17/v$00` miscellaneous1 |
| `1:ac` | [M] `$4c:$0a/v$00` miscStatusObjects<br>[M] `$4c:$0b/v$00` miscStatusObjects<br>[M] `$4c:$0d/v$00` miscStatusObjects<br>[M] `$4c:$0c/v$00` miscStatusObjects<br>[M] `$e6:$00/v$00` getRodOfSeasons |
| `1:ad` | [M] `$d5:$00/v$00` greatFairy |
| `1:af` | [M] `$47:$0e/v$00` shopItem<br>[M] `$47:$0f/v$00` shopItem<br>[M] `$47:$10/v$00` shopItem<br>[M] `$46:$02/v$00` shopkeeper<br>[M] `$37:$86/v$00` miscNpcs |
| `1:b0` | [M] `$47:$00/v$00` shopItem<br>[M] `$47:$02/v$00` shopItem<br>[M] `$47:$05/v$00` shopItem<br>[M] `$46:$01/v$00` shopkeeper |
| `1:b2` | [M] `$30:$12/v$00` subrosian<br>[M] `$30:$13/v$00` subrosian |
| `1:b3` | [M] `$29:$00/v$00` miscNpcs<br>[M] `$b8:$00/v$00` ambi |
| `1:b4` | [M] `$5a:$00/v$00` blainoScript |
| `1:b5` | [M] `$6b:$00/v$00` miscellaneous1<br>[M] `$6b:$01/v$00` miscellaneous1<br>[M] `$6b:$04/v$00` miscellaneous1<br>[M] `$db:$01/v$00` linkedSecredGivers |
| `1:b6` | [M] `$8b:$00/v$00` sunkenCityNpcs<br>[M] `$8b:$01/v$00` sunkenCityNpcs<br>[M] `$cd:$00/v$00` linkedMasterDiver |
| `1:b9` | [M] `$ab:$01/v$00` moblinKeepScenes |
| `1:ba` | [M] `$cb:$00/v$00` linkedGameGhini |
| `4:01` | [M] `$6b:$1f/v$00` miscellaneous1 |
| `4:03` | [R] `$20:$01/v$00` dungeonScript |
| `4:04` | [R] `$12:$00/v$00` dungeonStuff |
| `4:05` | [R] `$13:$01/v$00` pushblockTrigger<br>[R] `$1e:$08/v$00` doorController<br>[R] `$1e:$0a/v$00` doorController<br>[R] `$1e:$0b/v$00` doorController |
| `4:06` | [R] `$c6:$00/v$00` herosCaveSwordChest |
| `4:07` | [R] `$13:$01/v$00` pushblockTrigger<br>[R] `$12:$04/v$00` dungeonStuff |
| `4:08` | [R] `$1e:$04/v$00` doorController |
| `4:09` | [M] `$6b:$20/v$00` miscellaneous1 |
| `4:0b` | [M] `$20:$00/v$00` dungeonScript<br>[M] `$7e:$00/v$00` minibossPortal<br>[M] `$1e:$0b/v$00` doorController<br>[M] `$1e:$0a/v$00` doorController |
| `4:0e` | [M] `$13:$01/v$00` pushblockTrigger<br>[M] `$1e:$0b/v$00` doorController |
| `4:0f` | [M] `$78:$01/v$00` switchTileToggler |
| `4:11` | [M] `$20:$02/v$00` dungeonScript |
| `4:12` | [M] `$20:$03/v$00` dungeonScript<br>[M] `$1e:$09/v$00` doorController<br>[M] `$1e:$0a/v$00` doorController |
| `4:13` | [M] `$7f:$00/v$00` essence |
| `4:14` | [M] `$12:$02/v$00` dungeonStuff |
| `4:15` | [M] `$1e:$0a/v$00` doorController<br>[M] `$1e:$15/v$00` doorController<br>[M] `$7e:$00/v$00` minibossPortal<br>[M] `$c7:$08/v$00` createObjectAtEachTileindex |
| `4:16` | [M] `$38:$00/v$00` miscNpcs |
| `4:19` | [M] `$1e:$08/v$00` doorController<br>[M] `$1e:$09/v$00` doorController<br>[M] `$12:$02/v$00` dungeonStuff |
| `4:1a` | [M] `$13:$01/v$00` pushblockTrigger<br>[M] `$1e:$08/v$00` doorController<br>[M] `$1e:$09/v$00` doorController<br>[M] `$1e:$0a/v$00` doorController |
| `4:1b` | [M] `$1e:$14/v$00` doorController<br>[M] `$12:$01/v$00` dungeonStuff<br>[M] `$1e:$0b/v$00` doorController<br>[M] `$c7:$08/v$00` createObjectAtEachTileindex |
| `4:1c` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$e2:$01/v$00` statueEyeball |
| `4:1f` | [M] `$7a:$00/v$00` roller<br>[M] `$7a:$00/v$00` roller |
| `4:20` | [M] `$7a:$02/v$00` roller<br>[M] `$7a:$01/v$00` roller |
| `4:21` | [M] `$7e:$00/v$00` minibossPortal<br>[M] `$20:$01/v$00` dungeonScript<br>[M] `$1e:$08/v$00` doorController<br>[M] `$1e:$09/v$00` doorController |
| `4:25` | [M] `$1e:$09/v$00` doorController<br>[M] `$c7:$04/v$00` createObjectAtEachTileindex |
| `4:26` | [R] `$7d:$00/v$00` spinner |
| `4:27` | [M] `$79:$05/v$00` movingPlatform<br>[M] `$79:$09/v$00` movingPlatform<br>[M] `$79:$15/v$00` movingPlatform<br>[M] `$79:$1b/v$00` movingPlatform |
| `4:28` | [M] `$79:$24/v$00` movingPlatform<br>[M] `$79:$2c/v$00` movingPlatform<br>[M] `$79:$34/v$00` movingPlatform |
| `4:29` | [M] `$20:$02/v$00` dungeonScript<br>[M] `$1e:$0b/v$00` doorController<br>[M] `$1e:$0a/v$00` doorController<br>[M] `$c7:$04/v$00` createObjectAtEachTileindex |
| `4:2a` | [M] `$12:$02/v$00` dungeonStuff<br>[M] `$c7:$04/v$00` createObjectAtEachTileindex |
| `4:2b` | [M] `$1e:$0b/v$00` doorController |
| `4:2c` | [M] `$7f:$00/v$00` essence |
| `4:2d` | [M] `$20:$00/v$00` dungeonScript |
| `4:2e` | [M] `$1d:$00/v$00` rupeeRoomRupees |
| `4:2f` | [M] `$1e:$09/v$00` doorController<br>[M] `$1e:$0a/v$00` doorController |
| `4:30` | [M] `$13:$01/v$00` pushblockTrigger<br>[M] `$1e:$09/v$00` doorController |
| `4:33` | [M] `$ce:$09/v$00` businessScrub<br>[M] `$dc:$0c/v$00` miscPuzzles |
| `4:34` | [M] `$12:$01/v$00` dungeonStuff<br>[M] `$1e:$0a/v$00` doorController |
| `4:36` | [M] `$13:$01/v$00` pushblockTrigger<br>[M] `$12:$02/v$00` dungeonStuff |
| `4:37` | [M] `$7a:$02/v$00` roller<br>[M] `$7a:$02/v$00` roller<br>[M] `$dc:$0c/v$00` miscPuzzles |
| `4:39` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$7e:$00/v$00` minibossPortal<br>[M] `$6b:$1d/v$00` miscellaneous1 |
| `4:3a` | [M] `$a1:$03/v$00` movingSidescrollPlatform |
| `4:3b` | [M] `$a1:$04/v$00` movingSidescrollPlatform<br>[M] `$a1:$05/v$00` movingSidescrollPlatform<br>[M] `$a1:$06/v$00` movingSidescrollPlatform |
| `4:3c` | [M] `$a1:$07/v$00` movingSidescrollPlatform |
| `4:3d` | [M] `$a1:$13/v$00` movingSidescrollPlatform<br>[M] `$a1:$14/v$00` movingSidescrollPlatform |
| `4:3e` | [M] `$7c:$00/v$00` trampoline |
| `4:3f` | [M] `$7c:$00/v$00` trampoline |
| `4:40` | [M] `$7f:$00/v$00` essence |
| `4:42` | [M] `$79:$09/v$00` movingPlatform |
| `4:43` | [M] `$7c:$00/v$00` trampoline<br>[M] `$20:$04/v$00` dungeonScript |
| `4:44` | [M] `$79:$01/v$00` movingPlatform |
| `4:45` | [M] `$1e:$06/v$00` doorController |
| `4:47` | [M] `$1e:$08/v$00` doorController |
| `4:48` | [M] `$1e:$09/v$00` doorController<br>[M] `$20:$02/v$00` dungeonScript<br>[M] `$7e:$00/v$00` minibossPortal |
| `4:49` | [M] `$20:$00/v$00` dungeonScript |
| `4:4b` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$e2:$01/v$00` statueEyeball<br>[M] `$7e:$00/v$00` minibossPortal |
| `4:4c` | [M] `$7a:$01/v$00` roller<br>[M] `$7a:$01/v$00` roller<br>[M] `$7a:$00/v$00` roller |
| `4:4e` | [M] `$7a:$00/v$00` roller<br>[M] `$7a:$01/v$00` roller<br>[M] `$7a:$01/v$00` roller<br>[M] `$7a:$00/v$00` roller |
| `4:50` | [M] `$12:$02/v$00` dungeonStuff |
| `4:52` | [M] `$79:$11/v$00` movingPlatform |
| `4:53` | [M] `$1e:$0a/v$00` doorController<br>[M] `$20:$03/v$00` dungeonScript |
| `4:54` | [M] `$12:$02/v$00` dungeonStuff |
| `4:58` | [M] `$1e:$07/v$00` doorController<br>[M] `$20:$01/v$00` dungeonScript |
| `4:5c` | [M] `$a1:$08/v$00` movingSidescrollPlatform<br>[M] `$a1:$09/v$00` movingSidescrollPlatform |
| `4:5d` | [M] `$a3:$00/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$00/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$00/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$00/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$01/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$01/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$01/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$01/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$02/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$02/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$02/v$00` disappearingSidescrollPlatform<br>[M] `$a3:$02/v$00` disappearingSidescrollPlatform |
| `4:5e` | [M] `$79:$05/v$00` movingPlatform |
| `4:5f` | [M] `$20:$04/v$00` dungeonScript<br>[M] `$1e:$0b/v$00` doorController<br>[M] `$1e:$09/v$00` doorController |
| `4:60` | [M] `$7f:$00/v$00` essence |
| `4:61` | [M] `$20:$07/v$00` dungeonScript |
| `4:63` | [M] `$ce:$0a/v$00` businessScrub |
| `4:64` | [M] `$20:$02/v$00` dungeonScript<br>[M] `$c7:$08/v$00` createObjectAtEachTileindex |
| `4:6a` | [M] `$1e:$08/v$00` doorController<br>[M] `$1e:$09/v$00` doorController<br>[M] `$1e:$0a/v$00` doorController<br>[M] `$20:$05/v$00` dungeonScript<br>[M] `$7e:$00/v$00` minibossPortal<br>[M] `$c7:$04/v$00` createObjectAtEachTileindex |
| `4:6b` | [M] `$38:$00/v$00` miscNpcs |
| `4:6c` | [M] `$20:$03/v$00` dungeonScript |
| `4:6e` | [M] `$1e:$08/v$00` doorController |
| `4:6f` | [M] `$78:$01/v$00` switchTileToggler |
| `4:70` | [M] `$78:$02/v$00` switchTileToggler<br>[M] `$78:$04/v$00` switchTileToggler |
| `4:73` | [M] `$c5:$00/v$00` d4HolesFloortrapRoom |
| `4:75` | [M] `$12:$01/v$00` dungeonStuff |
| `4:76` | [M] `$78:$08/v$00` switchTileToggler |
| `4:78` | [M] `$20:$00/v$00` dungeonScript |
| `4:79` | [M] `$1e:$0a/v$00` doorController |
| `4:7b` | [M] `$20:$06/v$00` dungeonScript |
| `4:7d` | [M] `$1e:$04/v$00` doorController |
| `4:7e` | [M] `$78:$10/v$00` switchTileToggler |
| `4:81` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$e2:$01/v$00` statueEyeball<br>[M] `$7e:$00/v$00` minibossPortal |
| `4:83` | [M] `$13:$01/v$00` pushblockTrigger<br>[M] `$12:$02/v$00` dungeonStuff |
| `4:84` | [M] `$a1:$0b/v$00` movingSidescrollPlatform<br>[M] `$a1:$0c/v$00` movingSidescrollPlatform |
| `4:87` | [M] `$a2:$00/v$00` movingSidescrollConveyor |
| `4:88` | [M] `$7f:$00/v$00` essence |
| `4:89` | [M] `$1e:$05/v$00` doorController |
| `4:8b` | [M] `$20:$00/v$00` dungeonScript<br>[M] `$a2:$01/v$00` movingSidescrollConveyor<br>[M] `$a2:$02/v$00` movingSidescrollConveyor |
| `4:8c` | [M] `$1e:$08/v$00` doorController<br>[M] `$1e:$0a/v$00` doorController<br>[M] `$20:$03/v$00` dungeonScript |
| `4:8f` | [M] `$12:$02/v$00` dungeonStuff |
| `4:91` | [M] `$62:$00/v$00` unicornsCave4ChestPuzzle |
| `4:92` | [M] `$1e:$0b/v$00` doorController |
| `4:93` | [M] `$1e:$06/v$00` doorController<br>[M] `$63:$00/v$00` unicornsCaveReverseMovingArmos |
| `4:95` | [M] `$1e:$0b/v$00` doorController<br>[M] `$1e:$09/v$00` doorController |
| `4:9a` | [M] `$1e:$04/v$00` doorController<br>[M] `$13:$01/v$00` pushblockTrigger<br>[M] `$20:$01/v$00` dungeonScript |
| `4:9c` | [M] `$1e:$04/v$00` doorController |
| `4:9e` | [M] `$1e:$0b/v$00` doorController |
| `4:9f` | [M] `$7b:$00/v$00` magnetSpinner |
| `4:a0` | [M] `$78:$01/v$00` switchTileToggler<br>[M] `$78:$02/v$00` switchTileToggler |
| `4:a2` | [M] `$1e:$08/v$00` doorController<br>[M] `$1e:$0b/v$00` doorController<br>[M] `$20:$02/v$00` dungeonScript<br>[M] `$7e:$00/v$00` minibossPortal |
| `4:a7` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$e2:$01/v$00` statueEyeball<br>[M] `$7e:$00/v$00` minibossPortal |
| `4:a9` | [M] `$a1:$0d/v$00` movingSidescrollPlatform<br>[M] `$a1:$0e/v$00` movingSidescrollPlatform |
| `4:aa` | [M] `$20:$01/v$00` dungeonScript |
| `4:ab` | [M] `$20:$00/v$00` dungeonScript |
| `4:ac` | [M] `$20:$02/v$00` dungeonScript |
| `4:ad` | [M] `$79:$00/v$00` movingPlatform<br>[M] `$79:$08/v$00` movingPlatform<br>[M] `$79:$18/v$00` movingPlatform<br>[M] `$79:$20/v$00` movingPlatform<br>[M] `$79:$28/v$00` movingPlatform<br>[M] `$79:$30/v$00` movingPlatform<br>[M] `$79:$38/v$00` movingPlatform |
| `4:ae` | [M] `$20:$0c/v$00` dungeonScript |
| `4:af` | [M] `$1e:$04/v$00` doorController |
| `4:b1` | [R] `$7d:$00/v$00` spinner |
| `4:b4` | [M] `$79:$40/v$00` movingPlatform<br>[M] `$79:$48/v$00` movingPlatform<br>[M] `$20:$04/v$00` dungeonScript<br>[M] `$7c:$00/v$00` trampoline<br>[M] `$c7:$08/v$00` createObjectAtEachTileindex |
| `4:b5` | [M] `$13:$01/v$00` pushblockTrigger<br>[M] `$1e:$09/v$00` doorController |
| `4:b6` | [M] `$1e:$07/v$00` doorController |
| `4:b7` | [M] `$1e:$10/v$00` doorController |
| `4:b8` | [M] `$1e:$10/v$00` doorController |
| `4:ba` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$e2:$01/v$00` statueEyeball<br>[M] `$7e:$00/v$00` minibossPortal |
| `4:bb` | [M] `$1d:$01/v$00` rupeeRoomRupees |
| `4:bd` | [M] `$1e:$07/v$00` doorController<br>[M] `$1e:$06/v$00` doorController<br>[M] `$20:$05/v$00` dungeonScript |
| `4:c0` | [M] `$20:$06/v$00` dungeonScript<br>[M] `$1e:$06/v$00` doorController |
| `4:c1` | [M] `$7c:$00/v$00` trampoline |
| `4:c2` | [M] `$7c:$00/v$00` trampoline |
| `4:c4` | [M] `$20:$0e/v$00` dungeonScript |
| `4:c5` | [M] `$20:$0d/v$00` dungeonScript |
| `4:c7` | [M] `$1e:$08/v$00` doorController |
| `4:c8` | [M] `$20:$07/v$00` dungeonScript<br>[M] `$7e:$00/v$00` minibossPortal<br>[M] `$1e:$09/v$00` doorController<br>[M] `$1e:$0a/v$00` doorController |
| `4:ca` | [M] `$1e:$06/v$00` doorController |
| `4:cd` | [M] `$20:$08/v$00` dungeonScript |
| `4:ce` | [M] `$13:$01/v$00` pushblockTrigger<br>[M] `$1e:$08/v$00` doorController |
| `4:cf` | [M] `$1e:$10/v$00` doorController |
| `4:d0` | [M] `$1e:$0b/v$00` doorController |
| `4:d1` | [M] `$20:$09/v$00` dungeonScript<br>[M] `$1e:$06/v$00` doorController |
| `4:d2` | [M] `$1e:$0b/v$00` doorController |
| `4:d3` | [M] `$7c:$00/v$00` trampoline |
| `4:d5` | [M] `$1e:$0a/v$00` doorController<br>[M] `$20:$0b/v$00` dungeonScript |
| `4:d6` | [M] `$20:$0a/v$00` dungeonScript |
| `4:e7` | [M] `$dc:$00/v$00` miscPuzzles |
| `4:e8` | [M] `$10:$00/v$00` farore<br>[M] `$1c:$00/v$00` faroresMemory |
| `4:ef` | [M] `$55:$00/v$00` subrosianAtD8<br>[M] `$55:$01/v$00` subrosianAtD8 |
| `4:f0` | [M] `$cc:$00/v$00` goldenCaveSubrosian |
| `4:f4` | [M] `$1f:$0b/v$00` specialWarp |
| `4:f5` | [M] `$1f:$0c/v$00` specialWarp |
| `4:f8` | [M] `$5b:$80/v$00` lostWoodsDekuScrub |
| `4:fa` | [M] `$90:$04/v$00` jewelHelper |
| `4:fb` | [M] `$90:$05/v$00` jewelHelper |
| `5:01` | [M] `$99:$00/v$00` oldManWithRupees |
| `5:02` | [M] `$99:$01/v$00` oldManWithRupees |
| `5:03` | [M] `$99:$02/v$00` oldManWithRupees |
| `5:04` | [M] `$99:$03/v$00` oldManWithRupees |
| `5:05` | [M] `$99:$04/v$00` oldManWithRupees |
| `5:06` | [M] `$99:$05/v$00` oldManWithRupees |
| `5:07` | [M] `$99:$06/v$00` oldManWithRupees |
| `5:08` | [M] `$99:$07/v$00` oldManWithRupees |
| `5:0b` | [M] `$d6:$00/v$00` dekuScrub |
| `5:0c` | [M] `$d6:$02/v$00` dekuScrub |
| `5:0d` | [M] `$1f:$07/v$00` specialWarp<br>[M] `$d6:$01/v$00` dekuScrub |
| `5:0e` | [M] `$79:$08/v$00` movingPlatform<br>[M] `$79:$10/v$00` movingPlatform<br>[M] `$79:$08/v$00` movingPlatform |
| `5:0f` | [M] `$ca:$00/v$00` troy |
| `5:11` | [M] `$dd:$00/v$00` goldenBeastOldMan |
| `5:12` | [M] `$90:$06/v$00` jewelHelper<br>[M] `$90:$07/v$00` jewelHelper |
| `5:20` | [M] `$e1:$01/v$00` portalSpawner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner |
| `5:21` | [M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner |
| `5:22` | [M] `$20:$05/v$00` dungeonScript<br>[M] `$dc:$07/v$00` miscPuzzles |
| `5:23` | [M] `$dc:$06/v$00` miscPuzzles |
| `5:24` | [M] `$dc:$0f/v$00` miscPuzzles<br>[M] `$dc:$04/v$00` miscPuzzles<br>[M] `$7e:$02/v$00` minibossPortal<br>[M] `$e1:$01/v$00` portalSpawner |
| `5:25` | [M] `$20:$04/v$00` dungeonScript<br>[M] `$1e:$07/v$00` doorController |
| `5:26` | [M] `$dc:$03/v$00` miscPuzzles<br>[M] `$1e:$04/v$00` doorController<br>[M] `$e1:$01/v$00` portalSpawner |
| `5:27` | [M] `$dc:$09/v$00` miscPuzzles<br>[M] `$dc:$08/v$00` miscPuzzles<br>[M] `$e4:$00/v$00` linkedHerosCaveOldMan |
| `5:28` | [M] `$20:$06/v$00` dungeonScript<br>[M] `$dc:$04/v$00` miscPuzzles<br>[M] `$7e:$02/v$00` minibossPortal<br>[M] `$e1:$01/v$00` portalSpawner<br>[M] `$c7:$08/v$00` createObjectAtEachTileindex |
| `5:29` | [M] `$e1:$01/v$00` portalSpawner<br>[M] `$20:$02/v$00` dungeonScript<br>[M] `$c7:$08/v$00` createObjectAtEachTileindex |
| `5:2a` | [M] `$12:$01/v$00` dungeonStuff |
| `5:2b` | [M] `$1e:$15/v$00` doorController<br>[M] `$c7:$08/v$00` createObjectAtEachTileindex |
| `5:2c` | [M] `$dc:$0d/v$00` miscPuzzles<br>[M] `$dc:$03/v$00` miscPuzzles<br>[M] `$1e:$05/v$00` doorController |
| `5:2d` | [M] `$dc:$03/v$00` miscPuzzles<br>[M] `$1e:$06/v$00` doorController<br>[M] `$7a:$01/v$00` roller<br>[M] `$7a:$01/v$00` roller |
| `5:2e` | [M] `$dc:$03/v$00` miscPuzzles<br>[M] `$1e:$05/v$00` doorController |
| `5:2f` | [M] `$20:$01/v$00` dungeonScript<br>[M] `$dc:$04/v$00` miscPuzzles<br>[M] `$7e:$02/v$00` minibossPortal<br>[M] `$e1:$01/v$00` portalSpawner<br>[M] `$7a:$01/v$00` roller |
| `5:30` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$7e:$01/v$00` minibossPortal<br>[M] `$38:$00/v$00` miscNpcs<br>[M] `$e2:$01/v$00` statueEyeball<br>[M] `$dc:$0a/v$00` miscPuzzles |
| `5:31` | [M] `$dc:$04/v$00` miscPuzzles<br>[M] `$7e:$02/v$00` minibossPortal<br>[M] `$e1:$01/v$00` portalSpawner<br>[M] `$dc:$05/v$00` miscPuzzles |
| `5:32` | [M] `$20:$00/v$00` dungeonScript |
| `5:33` | [M] `$e1:$01/v$00` portalSpawner<br>[M] `$e7:$00/v$00` loneZora |
| `5:34` | [M] `$dc:$04/v$00` miscPuzzles<br>[M] `$7e:$02/v$00` minibossPortal<br>[M] `$e1:$01/v$00` portalSpawner |
| `5:35` | [M] `$66:$00/v$00` explorersCrypt4ArmosButtonPuzzle |
| `5:37` | [M] `$20:$01/v$00` dungeonScript |
| `5:38` | [M] `$7c:$01/v$00` trampoline<br>[M] `$7c:$00/v$00` trampoline<br>[M] `$7b:$84/v$00` magnetSpinner<br>[M] `$7b:$84/v$00` magnetSpinner<br>[M] `$7b:$84/v$00` magnetSpinner<br>[M] `$7b:$84/v$00` magnetSpinner |
| `5:39` | [M] `$79:$05/v$00` movingPlatform |
| `5:3a` | [M] `$20:$00/v$00` dungeonScript |
| `5:3b` | [M] `$20:$0b/v$00` dungeonScript |
| `5:3c` | [M] `$79:$2b/v$00` movingPlatform |
| `5:3d` | [M] `$12:$01/v$00` dungeonStuff |
| `5:3e` | [M] `$6b:$23/v$00` miscellaneous1 |
| `5:3f` | [M] `$1e:$0a/v$00` doorController |
| `5:40` | [M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$7b:$00/v$00` magnetSpinner<br>[M] `$79:$09/v$00` movingPlatform |
| `5:41` | [M] `$1e:$05/v$00` doorController |
| `5:42` | [M] `$20:$02/v$00` dungeonScript<br>[M] `$7e:$00/v$00` minibossPortal<br>[M] `$1e:$0b/v$00` doorController<br>[M] `$1e:$09/v$00` doorController<br>[M] `$c7:$04/v$00` createObjectAtEachTileindex |
| `5:44` | [M] `$79:$13/v$00` movingPlatform |
| `5:45` | [M] `$20:$0c/v$00` dungeonScript<br>[M] `$20:$0e/v$00` dungeonScript |
| `5:46` | [M] `$1e:$06/v$00` doorController<br>[M] `$20:$05/v$00` dungeonScript |
| `5:47` | [M] `$20:$06/v$00` dungeonScript<br>[M] `$79:$1b/v$00` movingPlatform |
| `5:48` | [M] `$12:$02/v$00` dungeonStuff |
| `5:49` | [M] `$1e:$08/v$00` doorController<br>[M] `$7c:$00/v$00` trampoline |
| `5:4b` | [M] `$1e:$04/v$00` doorController<br>[M] `$6b:$24/v$00` miscellaneous1 |
| `5:4c` | [M] `$20:$0a/v$00` dungeonScript |
| `5:4d` | [M] `$7c:$00/v$00` trampoline |
| `5:4e` | [M] `$20:$08/v$00` dungeonScript<br>[M] `$c7:$04/v$00` createObjectAtEachTileindex |
| `5:4f` | [M] `$7f:$00/v$00` essence |
| `5:50` | [M] `$20:$03/v$00` dungeonScript<br>[M] `$1e:$09/v$00` doorController<br>[M] `$1e:$0b/v$00` doorController |
| `5:52` | [M] `$20:$0d/v$00` dungeonScript |
| `5:53` | [M] `$20:$09/v$00` dungeonScript<br>[M] `$7a:$01/v$00` roller<br>[M] `$7a:$00/v$00` roller<br>[M] `$7a:$00/v$00` roller<br>[M] `$7a:$00/v$00` roller |
| `5:54` | [M] `$12:$02/v$00` dungeonStuff |
| `5:56` | [M] `$20:$07/v$00` dungeonScript<br>[M] `$c7:$04/v$00` createObjectAtEachTileindex |
| `5:57` | [M] `$79:$25/v$00` movingPlatform |
| `5:5a` | [M] `$12:$02/v$00` dungeonStuff |
| `5:5b` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$e2:$01/v$00` statueEyeball<br>[M] `$7e:$00/v$00` minibossPortal |
| `5:5c` | [M] `$a1:$0f/v$00` movingSidescrollPlatform |
| `5:5f` | [M] `$7f:$00/v$00` essence |
| `5:61` | [M] `$a2:$03/v$00` movingSidescrollConveyor<br>[M] `$a2:$04/v$00` movingSidescrollConveyor |
| `5:64` | [M] `$1e:$0a/v$00` doorController<br>[M] `$1e:$08/v$00` doorController<br>[M] `$20:$08/v$00` dungeonScript |
| `5:65` | [M] `$69:$00/v$00` swordShieldMazeFreezingLavaEvent |
| `5:66` | [M] `$69:$00/v$00` swordShieldMazeFreezingLavaEvent |
| `5:6a` | [M] `$69:$00/v$00` swordShieldMazeFreezingLavaEvent |
| `5:6b` | [M] `$69:$00/v$00` swordShieldMazeFreezingLavaEvent |
| `5:6c` | [M] `$20:$04/v$00` dungeonScript |
| `5:6d` | [M] `$1e:$05/v$00` doorController |
| `5:6f` | [R] `$7d:$00/v$00` spinner |
| `5:71` | [M] `$20:$0a/v$00` dungeonScript |
| `5:72` | [M] `$1e:$0b/v$00` doorController<br>[M] `$1e:$08/v$00` doorController<br>[M] `$7e:$00/v$00` minibossPortal<br>[M] `$20:$07/v$00` dungeonScript<br>[M] `$c7:$04/v$00` createObjectAtEachTileindex |
| `5:73` | [M] `$20:$02/v$00` dungeonScript |
| `5:75` | [M] `$12:$01/v$00` dungeonStuff |
| `5:78` | [M] `$20:$09/v$00` dungeonScript |
| `5:7a` | [M] `$20:$00/v$00` dungeonScript |
| `5:7b` | [M] `$1e:$08/v$00` doorController |
| `5:7c` | [M] `$79:$15/v$00` movingPlatform<br>[M] `$79:$1d/v$00` movingPlatform<br>[M] `$1e:$0a/v$00` doorController<br>[M] `$1e:$06/v$00` doorController |
| `5:7d` | [M] `$20:$0b/v$00` dungeonScript |
| `5:7e` | [M] `$78:$01/v$00` switchTileToggler<br>[M] `$20:$05/v$00` dungeonScript |
| `5:7f` | [M] `$67:$00/v$00` swordShieldMazeArmosPatternPuzzle<br>[M] `$20:$05/v$00` dungeonScript |
| `5:80` | [M] `$1e:$08/v$00` doorController<br>[M] `$1e:$08/v$00` doorController |
| `5:82` | [M] `$20:$03/v$00` dungeonScript |
| `5:84` | [M] `$68:$00/v$00` swordShieldMazeGrabbableIce<br>[M] `$68:$00/v$00` swordShieldMazeGrabbableIce<br>[M] `$68:$00/v$00` swordShieldMazeGrabbableIce |
| `5:86` | [M] `$20:$06/v$00` dungeonScript |
| `5:87` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$e2:$01/v$00` statueEyeball<br>[M] `$7e:$00/v$00` minibossPortal |
| `5:88` | [M] `$20:$05/v$00` dungeonScript |
| `5:89` | [M] `$20:$05/v$00` dungeonScript |
| `5:8e` | [M] `$20:$01/v$00` dungeonScript |
| `5:91` | [M] `$1e:$12/v$00` doorController (room flag $80 clear) |
| `5:93` | [M] `$20:$00/v$00` dungeonScript |
| `5:94` | [M] `$20:$00/v$00` dungeonScript |
| `5:95` | [M] `$20:$00/v$00` dungeonScript |
| `5:96` | [M] `$1e:$09/v$00` doorController |
| `5:97` | [M] `$12:$00/v$00` dungeonStuff<br>[M] `$20:$01/v$00` dungeonScript |
| `5:98` | [M] `$1e:$0b/v$00` doorController |
| `5:9a` | [M] `$44:$00/v$00` zelda<br>[M] `$da:$00/v$00` zeldaApproachTrigger<br>[M] `$b0:$08/v$00` twinrovaFlame<br>[M] `$b0:$09/v$00` twinrovaFlame<br>[M] `$b0:$0a/v$00` twinrovaFlame |
| `5:9b` | [M] `$e2:$01/v$00` statueEyeball<br>[M] `$6b:$08/v$00` miscellaneous1 |
| `5:9c` | [M] `$e2:$03/v$00` statueEyeball |
| `5:9d` | [M] `$b5:$00/v$00` finalDungeonEnergy<br>[M] `$e1:$01/v$00` portalSpawner |
| `5:b2` | [M] `$6b:$0a/v$00` miscellaneous1 |
| `5:b3` | [M] `$d5:$01/v$00` greatFairy |
| `5:b6` | [M] `$45:$00/v$00` talon |
| `5:bb` | [M] `$79:$05/v$00` movingPlatform<br>[M] `$6b:$0d/v$00` miscellaneous1 |
| `5:bc` | [M] `$1f:$05/v$00` specialWarp<br>[M] `$6b:$0e/v$00` miscellaneous1 |
| `5:bd` | [M] `$1f:$06/v$00` specialWarp<br>[M] `$8d:$00/v$00` sunkenCityNpcs<br>[M] `$8d:$01/v$00` sunkenCityNpcs<br>[M] `$8d:$02/v$00` sunkenCityNpcs<br>[M] `$8d:$03/v$00` sunkenCityNpcs |
| `5:c5` | [M] `$3b:$16/v$00` goron |
| `5:c7` | [M] `$6b:$0a/v$00` miscellaneous1 |
| `5:c8` | [M] `$3b:$14/v$00` goron<br>[M] `$3b:$15/v$00` goron<br>[M] `$3b:$07/v$00` goron |
| `5:c9` | [M] `$3b:$20/v$00` goron<br>[M] `$3b:$11/v$00` goron<br>[M] `$3b:$02/v$00` goron<br>[M] `$3b:$13/v$00` goron |
| `5:cf` | [M] `$b1:$18/v$00` shipPiratian<br>[M] `$b1:$19/v$00` shipPiratian<br>[M] `$b1:$1a/v$00` shipPiratian |
| `5:d1` | [M] `$ce:$03/v$00` businessScrub |
| `5:d2` | [M] `$6b:$0c/v$00` miscellaneous1<br>[M] `$6b:$0f/v$00` miscellaneous1<br>[M] `$4d:$01/v$00` pirateSkull |
| `5:d4` | [M] `$b1:$12/v$00` shipPiratian<br>[M] `$b1:$13/v$00` shipPiratian<br>[M] `$b1:$14/v$00` shipPiratian<br>[M] `$b1:$15/v$00` shipPiratian<br>[M] `$b1:$16/v$00` shipPiratian |
| `5:e2` | [M] `$a1:$00/v$00` movingSidescrollPlatform<br>[M] `$a1:$01/v$00` movingSidescrollPlatform |
| `5:e3` | [M] `$6b:$19/v$00` miscellaneous1<br>[M] `$a1:$02/v$00` movingSidescrollPlatform |
| `5:e5` | [M] `$90:$01/v$00` jewelHelper |
| `5:e8` | [M] `$cd:$01/v$00` linkedMasterDiver |
| `5:ea` | [M] `$cd:$02/v$00` linkedMasterDiver |
| `5:f0` | [M] `$23:$30/v$00` seasonSpiritsScripts |
| `5:f2` | [M] `$23:$31/v$00` seasonSpiritsScripts |
| `5:f5` | [M] `$23:$01/v$00` seasonSpiritsScripts |
| `5:f8` | [M] `$23:$11/v$00` seasonSpiritsScripts |
| `5:fb` | [M] `$23:$21/v$00` seasonSpiritsScripts |

## Additional object-list records

These are all literal interaction rows not reached by the canonical room-root
expansion, including native-cutscene lists and dormant data. Each literal row
is listed once in its containing source list; this is not a reachability or
implementation claim. The dispatch table gives the supported family boundary.

| Source list | Records in source order |
| --- | --- |
| [`extraData1.s:objectData_faroreSparkle`](../../external/oracles-disasm/objects/seasons/extraData1.s#L4) | `$11:$01/v$00` farore<br>`$11:$11/v$00` farore<br>`$11:$21/v$00` farore<br>`$11:$31/v$00` farore<br>`$11:$41/v$00` farore<br>`$11:$51/v$00` farore<br>`$11:$61/v$00` farore<br>`$11:$71/v$00` farore |
| [`extraData1.s:objectData4022`](../../external/oracles-disasm/objects/seasons/extraData1.s#L16) | `$44:$00/v$00` zelda<br>`$b0:$08/v$00` twinrovaFlame<br>`$b0:$09/v$00` twinrovaFlame<br>`$b0:$0a/v$00` twinrovaFlame |
| [`extraData1.s:objectData402f`](../../external/oracles-disasm/objects/seasons/extraData1.s#L24) | `$b0:$0b/v$00` twinrovaFlame<br>`$b0:$0c/v$00` twinrovaFlame<br>`$b0:$0d/v$00` twinrovaFlame |
| [`extraData1.s:objectData4054`](../../external/oracles-disasm/objects/seasons/extraData1.s#L57) | `$c7:$a0/v$00` createObjectAtEachTileindex |
| [`extraData2.s:objectData575e`](../../external/oracles-disasm/objects/seasons/extraData2.s#L17) | `$a7:$00/v$00` endgameCutsceneBipsomFamily<br>`$a7:$01/v$00` endgameCutsceneBipsomFamily<br>`$a7:$02/v$00` endgameCutsceneBipsomFamily<br>`$4c:$07/v$00` miscStatusObjects<br>`$4c:$08/v$00` miscStatusObjects |
| [`extraData2.s:objectData5774`](../../external/oracles-disasm/objects/seasons/extraData2.s#L25) | `$a8:$00/v$00` a8<br>`$72:$01/v$00` blaino |
| [`extraData2.s:objectData578d`](../../external/oracles-disasm/objects/seasons/extraData2.s#L36) | `$a8:$03/v$00` a8<br>`$43:$80/v$00` syrup |
| [`extraData2.s:objectData5797`](../../external/oracles-disasm/objects/seasons/extraData2.s#L41) | `$a8:$04/v$00` a8<br>`$a5:$02/v$00` din<br>`$a9:$00/v$00` a9<br>`$a9:$01/v$00` a9<br>`$a9:$02/v$00` a9<br>`$a9:$03/v$00` a9<br>`$a9:$04/v$00` a9<br>`$a9:$05/v$00` a9<br>`$9c:$00/v$00` springBloomFlower |
| [`extraData2.s:objectData57c7`](../../external/oracles-disasm/objects/seasons/extraData2.s#L55) | `$a8:$13/v$00` a8<br>`$87:$01/v$00` makuTree<br>`$a8:$14/v$00` a8<br>`$a5:$03/v$00` din<br>`$a7:$03/v$00` endgameCutsceneBipsomFamily |
| [`extraData2.s:objectData57dd`](../../external/oracles-disasm/objects/seasons/extraData2.s#L63) | `$aa:$00/v$00` aa<br>`$a5:$04/v$00` din<br>`$a8:$24/v$00` a8 |
| [`extraData2.s:objectData57eb`](../../external/oracles-disasm/objects/seasons/extraData2.s#L69) | `$a8:$34/v$00` a8<br>`$a5:$05/v$00` din<br>`$ad:$00/v$00` ad<br>`$ad:$01/v$00` ad<br>`$ad:$02/v$00` ad<br>`$ad:$02/v$00` ad<br>`$ad:$03/v$00` ad<br>`$ad:$04/v$00` ad |
| [`extraData2.s:objectData580d`](../../external/oracles-disasm/objects/seasons/extraData2.s#L80) | `$a8:$44/v$00` a8<br>`$a5:$07/v$00` din<br>`$aa:$02/v$00` aa<br>`$aa:$03/v$00` aa<br>`$44:$01/v$00` zelda<br>`$87:$02/v$00` makuTree |
| [`extraData2.s:objectData5827`](../../external/oracles-disasm/objects/seasons/extraData2.s#L89) | `$a5:$03/v$00` din<br>`$b9:$00/v$00` horonDogCredits<br>`$b9:$01/v$00` horonDogCredits<br>`$b9:$02/v$00` horonDogCredits<br>`$b9:$03/v$00` horonDogCredits<br>`$b9:$04/v$00` horonDogCredits<br>`$b9:$05/v$00` horonDogCredits<br>`$b9:$06/v$00` horonDogCredits<br>`$b9:$07/v$00` horonDogCredits<br>`$4e:$09/v$00` dinDancingEvent<br>`$4e:$08/v$00` dinDancingEvent |
| [`extraData2.s:objectData5855`](../../external/oracles-disasm/objects/seasons/extraData2.s#L103) | `$df:$00/v$00` nayruRalphCredits<br>`$df:$01/v$00` nayruRalphCredits<br>`$bb:$01/v$00` bb<br>`$bb:$02/v$00` bb<br>`$bc:$03/v$00` bc_bd_be<br>`$4c:$07/v$00` miscStatusObjects<br>`$4c:$08/v$00` miscStatusObjects |
| [`extraData2.s:objectData5873`](../../external/oracles-disasm/objects/seasons/extraData2.s#L113) | `$a8:$54/v$00` a8<br>`$a8:$02/v$00` a8 |
| [`extraData2.s:objectData587d`](../../external/oracles-disasm/objects/seasons/extraData2.s#L118) | `$44:$05/v$00` zelda<br>`$c1:$00/v$00` c1 |
| [`extraData2.s:objectData5887`](../../external/oracles-disasm/objects/seasons/extraData2.s#L123) | `$d4:$00/v$00` linkShip<br>`$d4:$11/v$00` linkShip<br>`$d4:$31/v$00` linkShip<br>`$d4:$51/v$00` linkShip<br>`$d4:$31/v$00` linkShip<br>`$d4:$41/v$00` linkShip<br>`$d4:$71/v$00` linkShip<br>`$d4:$51/v$00` linkShip<br>`$d4:$21/v$00` linkShip<br>`$d4:$61/v$00` linkShip<br>`$d4:$02/v$00` linkShip |
| [`extraData3.s:objectData_leavingSamasaDesert`](../../external/oracles-disasm/objects/seasons/extraData3.s#L11) | `$74:$00/v$00` 74<br>`$b1:$07/v$00` shipPiratian |
| [`extraData3.s:objectData_pirateShipEnteringWestCoast`](../../external/oracles-disasm/objects/seasons/extraData3.s#L16) | `$74:$01/v$00` 74<br>`$74:$03/v$00` 74<br>`$74:$08/v$00` 74<br>`$74:$09/v$00` 74<br>`$74:$05/v$00` 74<br>`$74:$06/v$00` 74 |
| [`extraData3.s:objectData_insidePirateShipLeavingSubrosia`](../../external/oracles-disasm/objects/seasons/extraData3.s#L25) | `$b1:$01/v$00` shipPiratian<br>`$b2:$00/v$00` shipPiratian<br>`$b1:$03/v$00` shipPiratian<br>`$b1:$03/v$00` shipPiratian<br>`$b1:$03/v$00` shipPiratian<br>`$b1:$03/v$00` shipPiratian |
| [`extraData3.s:objectData_sickPiratiansInShip`](../../external/oracles-disasm/objects/seasons/extraData3.s#L34) | `$b1:$08/v$00` shipPiratian<br>`$b1:$09/v$00` shipPiratian<br>`$b1:$0d/v$00` shipPiratian<br>`$b1:$0e/v$00` shipPiratian |
| [`extraData3.s:objectData7e40`](../../external/oracles-disasm/objects/seasons/extraData3.s#L43) | `$b3:$01/v$00` linkedCutscene<br>`$b3:$03/v$00` linkedCutscene |
| [`extraData3.s:objectData7e4e`](../../external/oracles-disasm/objects/seasons/extraData3.s#L56) | `$4e:$00/v$00` dinDancingEvent<br>`$4e:$01/v$00` dinDancingEvent<br>`$4e:$02/v$00` dinDancingEvent<br>`$4e:$03/v$00` dinDancingEvent<br>`$4e:$04/v$00` dinDancingEvent<br>`$4e:$05/v$00` dinDancingEvent<br>`$4e:$06/v$00` dinDancingEvent |
| [`extraData3.s:objectData7e6c`](../../external/oracles-disasm/objects/seasons/extraData3.s#L68) | `$6a:$01/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame<br>`$6a:$02/v$00` danceHallMinigame |
| [`extraData3.s:objectData7e96`](../../external/oracles-disasm/objects/seasons/extraData3.s#L83) | `$1e:$14/v$00` doorController |
| [`extraData3.s:objectData7ea0`](../../external/oracles-disasm/objects/seasons/extraData3.s#L90) | `$9b:$00/v$00` 9b<br>`$96:$00/v$00` moblin |

## Literal script-created interactions

These commands are inventoried at their source sites, including scripts whose
native owner is not implemented. Script calls, jumps and execution gates still
determine whether a command runs; listing it does not establish reachability.
Coordinates are `(Y, X)`. The default var03 is `$00` for this command.

| Source script | Created interaction | Coordinates |
| --- | --- | --- |
| [`dungeonScripts.s:dungeonScript_minibossDeath`](../../external/oracles-disasm/scripts/seasons/dungeonScripts.s) | `$7e:$00/v$00` minibossPortal | `($00, $00)` |
| [`dungeonScripts.s:poisonMothsLairScript_minibossDeath`](../../external/oracles-disasm/scripts/seasons/dungeonScripts.s) | `$7e:$00/v$00` minibossPortal | `($00, $00)` |
| [`dungeonScripts.s:dancingDragonScript_spawnStairsToB1`](../../external/oracles-disasm/scripts/seasons/dungeonScripts.s) | `$05:$00/v$00` breakTileDebris | `($38, $98)` |
| [`scripts.s:makuTreeScript_remoteCutsceneDontSetRoomFlag`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$48:$00/v$00` makuLeaf | `($40, $50)` |
| [`scripts.s:malonScript`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$45:$01/v$00` talon | `($68, $78)` |
| [`scripts.s:mittensOwnerScript`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$5d:$06/v$00` tradeItem | `($44, $68)` |
| [`scripts.s:syrupScript_spawnShopItems`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$47:$0b/v$00` shopItem | `($28, $44)` |
| [`scripts.s:syrupScript_spawnShopItems`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$47:$07/v$00` shopItem | `($28, $4c)` |
| [`scripts.s:syrupScript_spawnShopItems`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$47:$08/v$00` shopItem | `($28, $74)` |
| [`scripts.s:caveTalonScript`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$5d:$08/v$00` tradeItem | `($68, $48)` |
| [`scripts.s:ingoScript_tradingVase`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$4c:$09/v$00` miscStatusObjects | `($08, $58)` |
| [`scripts.s:floodgateKeyholeScript_keyEntered`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$6b:$14/v$00` miscellaneous1 | `($00, $00)` |
| [`scripts.s:strangeBrother1Script_finishedScreen`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$6b:$16/v$00` miscellaneous1 | `($48, $28)` |
| [`scripts.s:moblinBulliesScript_spawnMoblins`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$73:$03/v$00` animalMoblinBullies | `($88, $30)` |
| [`scripts.s:moblinBulliesScript_spawnMoblins`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$73:$04/v$00` animalMoblinBullies | `($88, $50)` |
| [`scripts.s:moblinBulliesScript_spawnMoblins`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$73:$05/v$00` animalMoblinBullies | `($18, $b0)` |
| [`scripts.s:shipPirationScript_piratianComingDownHandler`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$b1:$02/v$00` shipPiratian | `($98, $78)` |
| [`scripts.s:shipPiratianScript_dizzyPirate1Spawner`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$b1:$0a/v$00` shipPiratian | `($98, $78)` |
| [`scripts.s:shipPiratianScript_1stDizzyPirateDescending`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$b1:$0b/v$00` shipPiratian | `($98, $78)` |
| [`scripts.s:shipPirationScript_2ndDizzyPirateDescending`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$b1:$0c/v$00` shipPiratian | `($98, $78)` |
| [`scripts.s:shipPirationScript_3rdDizzyPirateDescending`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$b2:$01/v$00` shipPiratian | `($98, $78)` |
| [`scripts.s:shipPiratianCaptainScript_arrivingInWestCoast`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$b2:$03/v$00` shipPiratian | `($68, $68)` |
| [`scripts.s:linkedGhiniScript_startRound`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$05:$00/v$00` breakTileDebris | `($68, $18)` |
| [`scripts.s:linkedGhiniScript_startRound`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$05:$00/v$00` breakTileDebris | `($68, $18)` |
| [`scripts.s:masterDiverScript_begunSecret`](../../external/oracles-disasm/scripts/seasons/scripts.s) | `$05:$00/v$00` breakTileDebris | `($58, $88)` |
| [`scripts2.s:startCrystalTrapRoomSequence`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$1e:$13/v$00` doorController | `($30, $00)` |
| [`scripts2.s:startCrystalTrapRoomSequence`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$05:$00/v$00` breakTileDebris | `($18, $48)` |
| [`scripts2.s:startCrystalTrapRoomSequence`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$05:$00/v$00` breakTileDebris | `($38, $48)` |
| [`scripts2.s:startCrystalTrapRoomSequence`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$05:$00/v$00` breakTileDebris | `($28, $48)` |
| [`scripts2.s:startCrystalTrapRoomSequence`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$05:$00/v$00` breakTileDebris | `($48, $48)` |
| [`scripts2.s:startCrystalTrapRoomSequence`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$65:$00/v$00` 65 | `($00, $00)` |
| [`scripts2.s:zelda_kidnapped`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$ba:$01/v$00` ba | `($18, $18)` |
| [`scripts2.s:talon_giveMushroomAfterWaking`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$5d:$07/v$00` tradeItem | `($68, $58)` |
| [`scripts2.s:headSmelterScript_blowUpRocks`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$6f:$01/v$00` bombFlower | `($38, $38)` |
| [`scripts2.s:danceLeaderScript_danceTutorial`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$6a:$03/v$00` danceHallMinigame | `($00, $00)` |
| [`scripts2.s:gettingRodOfSeasons_body`](../../external/oracles-disasm/scripts/seasons/scripts2.s) | `$e6:$02/v$00` getRodOfSeasons | `($38, $50)` |

## Supported opening, frontend and special-object routes

| Source entry | Supported boundary | Status |
| --- | --- | --- |
| `intro_titlescreen` | Native title, prompt, Start, shared RNG and file-select fade; attract replay unavailable | Partial |
| `fileSelectMode0-$05` | Standard name entry, message speed, file creation, copy and erase | Runtime route |
| Cutscene `$0d` | Standard pregame summons | Runtime route |
| Cutscene `$06` | Din dancing and capture | Runtime route |
| Cutscene `$07` | Din imprisoned by Onox | Runtime route |
| Cutscene `$08` | Temple sinking and season changes | Runtime route |
| Link `$08:$01` | Dance and tornado | Runtime route |
| Link `$08:$02` | Collapsed wake-up | Runtime route |
| Link `$08:$0a` | Pregame summons | Runtime route |
| Interaction `$a5:$09` | Din discovers Link | Runtime route |
| Interaction `$4e:$00-$07,$0b` | Standard troupe, dance and tornado | Runtime route |
| Interaction `$4f:$00-$04` | Standard imprisonment actors and effects | Runtime route |
| Interaction `$9d:$00-$01` | Standard Impa introduction and immediate repeat conversation | Runtime route |
| Interaction `$85:$00` | Nearby carnival music | Runtime route |
| Interaction `$88:$00-$01` | Castle lightning | Runtime route |
| Interaction `$77:$00-$01` | Temple scenery | Runtime route |
| Interaction `$86:$00-$04` | Temple explosions | Runtime route |
| Interaction `$9f:$00` | Exclamation effect | Opening and shared helper/hover runtime routes |
| Interaction `$05:$00` | Puff effect | Runtime route |
| Interaction `$08:$00` | Destruction debris | Runtime route |
| Special object `$0b` | Native state0/stateA waiting/force-mount presenter, common live body/punch/tornado, remembered admission and Flute stateC hops; quest controllers, scripted departure and remaining riding/hazard aliases incomplete | Partial |
| Special object `$0c` | Seasons state0/stateA presenter, common live body/mouth, remembered admission and Flute stateC arrival; remaining carry/hazard aliases and transient paths incomplete | Partial |
| Special object `$0d` | Seasons state0 and stateA waiting/force-mount presenter, common live body/stomp, remembered admission and Flute stateC arrival; scripted movement/departure states and transient aliases incomplete | Partial |

Implementation guidance is in [NPCs and room events](../npcs-and-events.md).
