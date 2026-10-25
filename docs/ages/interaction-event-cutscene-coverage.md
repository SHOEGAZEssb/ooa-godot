# Interaction, event and cutscene coverage

This ledger inventories the clean-US Ages source entry points beyond the
[positioned NPC ledger](npc-interaction-coverage.md). A mapped entry is not
a claim of exhaustive ROM parity. Missing variants remain open even when a
shared helper, generated visual, ordinary NPC or neighboring room works.

The inventory starts from the [interaction dispatch table](../../external/oracles-disasm/data/interactionCodeTable.s),
all six [object-table roots](../../external/oracles-disasm/objects/ages/pointers.s),
their pointer/alias expansions, all Ages
extra object data, symbolic native/script creation sites, the cutscene table,
both room-entry dispatch tables, and the scripted special-object dispatches.
Groups `$06/$07` alias object tables `$04/$05`; they are not counted twice.
Literal extra-data variants include dormant source rows and are not all
asserted reachable. Enemy/item/part state machines are dependencies here,
rather than a separate inventory of all combat and item behavior.

## Status definitions

| Status | Meaning |
| --- | --- |
| **R** | Runtime route located for the source family. This establishes implementation presence, not exhaustive branch, timing, lifecycle or ROM verification. |
| **P** | Runtime support exists, with a known missing variant, producer, placement or external-system branch listed below. |
| **M** | Required source entry has no production route. |
| **D** | The original clean-US dispatch deletes the object; no feature implementation is required. |
| **N** | No clean-US producer was identified. Retained source entry, not a confirmed missing playable feature. |

## Coverage totals

| Inventory | R | P | M | D | N | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Interaction IDs `$00-$e6` | 217 | 1 | 0 | 9 | 4 | 231 |
| Native cutscene states `$00-$21` | 33 | 0 | 0 | 0 | 1 | 34 |
| Room-specific code handlers `$00-$0c` | 13 | 0 | 0 | 0 | 0 | 13 |
| Room-specific tile-change handlers `$00-$37` | 56 | 0 | 0 | 0 | 0 | 56 |
| Link cutscene subids `$00-$0c` | 13 | 0 | 0 | 0 | 0 | 13 |
| Companion/Maple credit special objects `$0f-$12` | 4 | 0 | 0 | 0 | 0 | 4 |

| Source placement inventory | Count |
| --- | ---: |
| Expanded placed interactions, including shared room-label aliases | 1026 |
| Rooms with placed interactions | 524 |
| Distinct placed interaction ID/subid pairs | 592 |
| Literal interaction rows across all Ages object-data files | 1216 |
| Distinct literal object-data ID/subid pairs | 740 |
| Room-specific code bindings | 14 |
| Room-specific tile-change bindings | 58 |
| Open implementation checklist items | 2 |

These inventories overlap. A cutscene can create several interaction IDs;
a checklist item can affect several dispatch entries. Do not sum their counts.
The existing NPC ledger continues to describe only its imported records.

## Missing implementation checklist

A checked item requires the source-derived import/producer, actual gameplay
route, completion/cancellation/re-entry behavior and focused regression.
Visual-only import or a directly stepped helper does not close an item.

### Shared interactions and rewards

- [x] **INT-01** — `5:cf`, `$1c:$00`: Farore's Memory book, its linked/finished-game visibility, conversation and `MENU $0a` secrets list.
- [x] **INT-02** — `4:05`, `$20:$02`: Maku Path 30-Rupee script reward.
- [x] **INT-03** — `4:09`, `$20:$03`: Maku Path falling Small Key when exactly one torch is lit.
- [x] **INT-04** — `4:09`, `$20:$01`: Maku Path enemy-clear stairs, including its waits and room-flag completion.
- [x] **INT-05** — `4:53`, `$24:$21`: Switch-state bit 2 to active-trigger bit 2 translator.
- [x] **INT-06** — `4:66`, `$21:$18`: Restore D3 crystal switch bits from room flags for `4:5d`, `4:5f`, `4:61` and `4:63`.
- [x] **INT-07** — `0:5a`, `$39:$05`: Saved-Nayru monkey ensemble: the placed spawner, nine children, native movement and ordinary/bowtie dialogue.
- [x] **INT-08** — `0:38`, `0:67`, `1:67`, `$6b:$15`: Finished-game Link statues at the three placements outside the supported `1:38` owner.
- [x] **INT-09** — `0:22`, `0:24`, `$71:$0d`: Companion barrier to Symmetry City until the Tuni Nut is restored.
- [x] **INT-10** — `1:5b`, `1:6d`, `1:7c`, `5:c7`, `$82:$00/$01/$02`: Instantiate all nine sarcophagus placements outside D8 through the shared native owner.
- [x] **INT-11** — `4:d0`, `5:f9`, `$dc:$0f`: Collision-triggered Sea of No Return / Black Tower Turret entry text, with the original scrolling-entry gate.
- [x] **INT-12** — `0:38`, `$dc:$14`: Create decoration `$80:$00` when the Maku Tree final-dungeon portal tile is present.
- [x] **INT-13** — `0:e0`, `0:e1`, `0:e2`, `$90:$1b/$1c/$1d`: Room-load-created Maku Tree Gasha Seed rewards, retaining their distinct essence gates and positions.
- [ ] **INT-14** — `2:ee`, `$89:$01`: Blue Snake successful cable fortune and ring transfer; the no-cable timeout remains supported.
- [x] **INT-15** — `4:55`, `$20:$02`: D3 chest script when exactly two torches are lit.
- [x] **INT-16** — `0:09`, `$80:$01`: King Moblin's Keep flag decoration and its destruction-flag visibility gate.

### Hero's Cave

- [x] **HC-01** — `4:c2`, `$61:$30`, `$19:$04`, `$d8:$05`: Hero's Cave lever, colored cube and lava-filler placements through their shared owners.
- [x] **HC-02** — `4:c2`, `$90:$14` (three), `$90:$15`: Hero's Cave colored-cube target sensors and completed-puzzle staircase.
- [x] **HC-03** — `4:c5`, `$90:$16`: Hero's Cave chest-completion exit warp and control handoff.
- [x] **HC-04** — `4:c2`, `4:c3`, `4:c4`, `4:c5`, `4:c6`, `4:c9`, `4:ce`, `4:cf`, `$7e:$01`: All eight Hero's Cave portal placements and their item-flag / destination rules.
- [x] **HC-05** — `4:cd`, `$90:$18`: Hero's Cave pushed-statue Small Key puzzle.
- [x] **HC-06** — `4:cf`, `$90:$1a`: Hero's Cave colored-pushblock pattern chest.
- [x] **HC-07** — `4:c0`, `$90:$20`: Hero's Cave sidescrolling Rupee reward.
- [x] **HC-08** — `4:c8`, `$25:$00`, `$21:$11`: Hero's Cave floor-filling controller and completion chest placements.
- [x] **HC-09** — `4:c6`, `$20:$01`: Hero's Cave chest script requiring all four trigger bits.
- [x] **HC-10** — `4:c9`, `$20:$02`: Hero's Cave trigger-operated bridge and persistent completion.
- [x] **HC-11** — `4:c5`, `$20:$03`: Hero's Cave enemy-clear north staircase.
- [x] **HC-12** — `4:ce`, `$90:$17`: Hero's Cave entrance portal item-flag propagation from `4:c9`.

### Room-load tile events

- [x] **TILE-01** — `4:4c`: D3 room-load bridge layout selected by switch-state bit 0.
- [x] **TILE-02** — `4:4e`: D3 room-load bridge layout selected by switch-state bit 1.
- [x] **TILE-03** — `4:c7`: Hero's Cave room-load bridge layout selected by switch-state bit 0.
- [x] **TILE-04** — `0:2c`: Rolling Ridge vine growth/withered layout from sprout `$62:$00` at packed position `$17`.
- [x] **TILE-05** — `0:1c`: Rolling Ridge adjoining vine-edge substitutions for the same sprout and position.
- [x] **TILE-06** — `0:76`: Linked-game Black Tower progress-stage-1 room-layout substitution.

### Room-entry side effects

- [x] **ENTRY-01** — `5:d2`: Palace minimap-room `$06` and past-room `$06` flag `$10` writes.
- [x] **ENTRY-02** — `2:0e`: Sadness room-music override before Nayru is saved.
- [x] **ENTRY-03** — `0:39`: Ralph music on rightward entry before his portal-departure flag.
- [x] **ENTRY-04** — `0:3a`: Nayru music override after the finished-game flag.
- [x] **ENTRY-05** — `1:0e`: Present Mermaid's Cave exterior minimap location `$00:$3c`.
- [x] **ENTRY-06** — `3:0f`: Past Mermaid's Cave exterior minimap location `$01:$3c`.
- [x] **ENTRY-07** — `4:e6`: Change active music `$00` to `$ff` on the Vire/Black Tower room load.

### Linked-game entry flows

- [x] **MENU-01** — New-file Secrets option: Game-transfer / Hero's Secret file creation through the actual file menu.
- [ ] **MENU-02** — New-file Game Link option: Cable-based linked-file creation through the actual file menu.

## Interaction dispatch inventory

Each row accounts for one Ages dispatch-table entry. The source link opens
its handler; subids are the union of literal object-data rows, including
extra/cutscene data. Dynamic register/table-derived subids are also owned by
the corresponding family and are not bounded by this literal-row column.

| ID | Source handler | Literal object-data subids | Status | Open items |
| --- | --- | --- | --- | --- |
| `$00` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$01` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$02` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$03` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$04` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$05` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$06` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$07` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$08` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$09` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$0a` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$0b` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$0c` | [breakTileDebris](../../external/oracles-disasm/object_code/common/interactions/breakTileDebris.s) | — | R | — |
| `$0d` | `interactionDelete` | — | D | — |
| `$0e` | `interactionDelete` | — | D | — |
| `$0f` | [fallDownHole](../../external/oracles-disasm/object_code/common/interactions/fallDownHole.s) | — | R | — |
| `$10` | [farore](../../external/oracles-disasm/object_code/common/interactions/farore.s) | $00 | R | — |
| `$11` | [faroreMakeChest](../../external/oracles-disasm/object_code/common/interactions/faroreMakeChest.s) | $01, $11, $21, $31, $41, $51, $61, $71 | R | — |
| `$12` | [dungeonStuff](../../external/oracles-disasm/object_code/common/interactions/dungeonStuff.s) | $00-$02, $04 | R | — |
| `$13` | [pushblockTrigger](../../external/oracles-disasm/object_code/common/interactions/pushblockTrigger.s) | $01 | R | — |
| `$14` | [pushblock](../../external/oracles-disasm/object_code/common/interactions/pushblock.s) | — | R | — |
| `$15` | [toggleFloor](../../external/oracles-disasm/object_code/ages/interactions/toggleFloor.s) | $00 | R | — |
| `$16` | [minecart](../../external/oracles-disasm/object_code/common/interactions/minecart.s) | — | R | — |
| `$17` | [dungeonKeySprite](../../external/oracles-disasm/object_code/common/interactions/dungeonKeySprite.s) | — | R | — |
| `$18` | [overworldKeySprite](../../external/oracles-disasm/object_code/common/interactions/overworldKeySprite.s) | — | R | — |
| `$19` | [coloredCube](../../external/oracles-disasm/object_code/ages/interactions/coloredCube.s) | $00-$01, $03-$05 | R | — |
| `$1a` | [coloredCubeFlame](../../external/oracles-disasm/object_code/ages/interactions/coloredCubeFlame.s) | $00 | R | — |
| `$1b` | [minecartGate](../../external/oracles-disasm/object_code/ages/interactions/minecartGate.s) | $00-$01, $04, $25 | R | — |
| `$1c` | [faroresMemory](../../external/oracles-disasm/object_code/common/interactions/faroresMemory.s) | $00 | R | — |
| `$1d` | `interactionDelete` | — | D | — |
| `$1e` | [doorController](../../external/oracles-disasm/object_code/common/interactions/doorController.s) | $04-$0b, $10-$11, $14 | R | — |
| `$1f` | [specialWarp](../../external/oracles-disasm/object_code/ages/interactions/specialWarp.s) | $00-$02 | R | — |
| `$20` | [dungeonScript](../../external/oracles-disasm/object_code/ages/interactions/dungeonScript.s) | $00-$06 | R | — |
| `$21` | [dungeonEvents](../../external/oracles-disasm/object_code/ages/interactions/dungeonEvents.s) | $01-$0a, $0c-$19 | R | — |
| `$22` | [floorColorChanger](../../external/oracles-disasm/object_code/ages/interactions/floorColorChanger.s) | $00 | R | — |
| `$23` | [extendableBridge](../../external/oracles-disasm/object_code/ages/interactions/extendableBridge.s) | $00-$01 | R | — |
| `$24` | [triggerTranslator](../../external/oracles-disasm/object_code/ages/interactions/triggerTranslator.s) | $02, $21 | R | — |
| `$25` | [tileFiller](../../external/oracles-disasm/object_code/ages/interactions/tileFiller.s) | $00 | R | — |
| `$26` | `interactionDelete` | — | D | — |
| `$27` | `interactionDelete` | — | D | — |
| `$28` | [bipin](../../external/oracles-disasm/object_code/common/interactions/bipin.s) | $0a | R | — |
| `$29` | [adlar](../../external/oracles-disasm/object_code/ages/interactions/adlar.s) | $00 | R | — |
| `$2a` | [librarian](../../external/oracles-disasm/object_code/ages/interactions/librarian.s) | $00 | R | — |
| `$2b` | [blossom](../../external/oracles-disasm/object_code/common/interactions/blossom.s) | — | R | — |
| `$2c` | [veranCutsceneWallmaster](../../external/oracles-disasm/object_code/ages/interactions/veranCutsceneWallmaster.s) | — | R | — |
| `$2d` | [veranCutsceneFace](../../external/oracles-disasm/object_code/ages/interactions/veranCutsceneFace.s) | $00 | R | — |
| `$2e` | [oldManWithRupees](../../external/oracles-disasm/object_code/ages/interactions/oldManWithRupees.s) | $00-$01 | R | — |
| `$2f` | [playNayruMusic](../../external/oracles-disasm/object_code/ages/interactions/playNayruMusic.s) | $00 | R | — |
| `$30` | [shootingGallery](../../external/oracles-disasm/object_code/ages/interactions/shootingGallery.s) | $00-$01 | R | — |
| `$31` | [impaInCutscene](../../external/oracles-disasm/object_code/ages/interactions/impaInCutscene.s) | $00-$07, $09-$0a | R | — |
| `$32` | [fakeOctorok](../../external/oracles-disasm/object_code/ages/interactions/fakeOctorok.s) | $00, $02 | R | — |
| `$33` | [smogBoss](../../external/oracles-disasm/object_code/ages/interactions/smogBoss.s) | $ff | R | — |
| `$34` | [triforceStone](../../external/oracles-disasm/object_code/ages/interactions/triforceStone.s) | $00 | R | — |
| `$35` | [child](../../external/oracles-disasm/object_code/common/interactions/child.s) | — | R | — |
| `$36` | [nayru](../../external/oracles-disasm/object_code/ages/interactions/nayru.s) | $00-$06, $08, $0a-$0e, $10-$11, $13 | R | — |
| `$37` | [ralph](../../external/oracles-disasm/object_code/ages/interactions/ralph.s) | $00, $02-$12 | R | — |
| `$38` | [pastGirl](../../external/oracles-disasm/object_code/ages/interactions/pastGirl.s) | $00 | R | — |
| `$39` | [monkey](../../external/oracles-disasm/object_code/ages/interactions/monkey.s) | $00-$07 | R | — |
| `$3a` | [villager](../../external/oracles-disasm/object_code/ages/interactions/villager.s) | $00, $02-$0d | R | — |
| `$3b` | [femaleVillager](../../external/oracles-disasm/object_code/ages/interactions/femaleVillager.s) | $00-$08 | R | — |
| `$3c` | [boy](../../external/oracles-disasm/object_code/ages/interactions/boy.s) | $00-$0e, $10 | R | — |
| `$3d` | [oldLady](../../external/oracles-disasm/object_code/ages/interactions/oldLady.s) | $00-$05 | R | — |
| `$3e` | [ghostVeran](../../external/oracles-disasm/object_code/ages/interactions/ghostVeran.s) | — | R | — |
| `$3f` | [boy2](../../external/oracles-disasm/object_code/ages/interactions/boy2.s) | $00-$03 | R | — |
| `$40` | [soldier](../../external/oracles-disasm/object_code/ages/interactions/soldier.s) | $00-$09, $0b-$0d | R | — |
| `$41` | [miscMan](../../external/oracles-disasm/object_code/ages/interactions/miscMan.s) | $00-$06 | R | — |
| `$42` | [mustacheMan](../../external/oracles-disasm/object_code/ages/interactions/mustacheMan.s) | $00-$01 | R | — |
| `$43` | [pastGuy](../../external/oracles-disasm/object_code/ages/interactions/pastGuy.s) | $00-$06 | R | — |
| `$44` | [miscMan2](../../external/oracles-disasm/object_code/ages/interactions/miscMan2.s) | $00-$04 | R | — |
| `$45` | [pastOldLady](../../external/oracles-disasm/object_code/ages/interactions/pastOldLady.s) | $00-$01 | R | — |
| `$46` | [shopkeeper](../../external/oracles-disasm/object_code/common/interactions/shopkeeper.s) | $00-$02 | R | — |
| `$47` | [shopItem](../../external/oracles-disasm/object_code/common/interactions/shopItem.s) | $00-$04, $0e-$10, $15 | R | — |
| `$48` | [tokay](../../external/oracles-disasm/object_code/ages/interactions/tokay.s) | $00-$1f | R | — |
| `$49` | [forestFairy](../../external/oracles-disasm/object_code/ages/interactions/forestFairy.s) | $05-$10 | R | — |
| `$4a` | [introSprites1](../../external/oracles-disasm/object_code/common/interactions/introSprites1.s) | — | R | — |
| `$4b` | [rabbit](../../external/oracles-disasm/object_code/ages/interactions/rabbit.s) | $00, $02-$07 | R | — |
| `$4c` | [bird](../../external/oracles-disasm/object_code/ages/interactions/bird.s) | $00-$04 | R | — |
| `$4d` | [ambi](../../external/oracles-disasm/object_code/ages/interactions/ambi.s) | $00-$04, $06, $08-$0a | R | — |
| `$4e` | [subrosian](../../external/oracles-disasm/object_code/ages/interactions/subrosian.s) | $00-$04 | R | — |
| `$4f` | [impaNpc](../../external/oracles-disasm/object_code/ages/interactions/impaNpc.s) | $00-$02 | R | — |
| `$50` | `interactionDelete` | — | D | — |
| `$51` | [dumbellMan](../../external/oracles-disasm/object_code/ages/interactions/dumbellMan.s) | $00 | R | — |
| `$52` | [oldMan](../../external/oracles-disasm/object_code/ages/interactions/oldMan.s) | $00-$06 | R | — |
| `$53` | [mamamuYan](../../external/oracles-disasm/object_code/ages/interactions/mamamuYan.s) | $00 | R | — |
| `$54` | [mamamuDog](../../external/oracles-disasm/object_code/ages/interactions/mamamuDog.s) | $00-$01 | R | — |
| `$55` | [postman](../../external/oracles-disasm/object_code/ages/interactions/postman.s) | $00 | R | — |
| `$56` | [explosion](../../external/oracles-disasm/object_code/common/interactions/explosion.s) | — | R | — |
| `$57` | [pickaxeWorker](../../external/oracles-disasm/object_code/ages/interactions/pickaxeWorker.s) | $00-$03 | R | — |
| `$58` | [hardhatWorker](../../external/oracles-disasm/object_code/ages/interactions/hardhatWorker.s) | $00-$03 | R | — |
| `$59` | [poe](../../external/oracles-disasm/object_code/ages/interactions/poe.s) | $00 | R | — |
| `$5a` | [oldZora](../../external/oracles-disasm/object_code/ages/interactions/oldZora.s) | $00 | R | — |
| `$5b` | [toiletHand](../../external/oracles-disasm/object_code/ages/interactions/toiletHand.s) | $00 | R | — |
| `$5c` | [maskSalesman](../../external/oracles-disasm/object_code/ages/interactions/maskSalesman.s) | $00 | R | — |
| `$5d` | [bear](../../external/oracles-disasm/object_code/ages/interactions/bear.s) | $00-$02 | R | — |
| `$5e` | [sword](../../external/oracles-disasm/object_code/ages/interactions/sword.s) | — | R | — |
| `$5f` | [syrup](../../external/oracles-disasm/object_code/common/interactions/syrup.s) | $80 | R | — |
| `$60` | [treasure](../../external/oracles-disasm/object_code/common/interactions/treasure.s) | — | R | — |
| `$61` | [lever](../../external/oracles-disasm/object_code/ages/interactions/lever.s) | $06, $30-$31, $46 | R | — |
| `$62` | [makuConfetti](../../external/oracles-disasm/object_code/ages/interactions/makuConfetti.s) | — | R | — |
| `$63` | [accessory](../../external/oracles-disasm/object_code/ages/interactions/accessory.s) | — | R | — |
| `$64` | [raftwreckCutsceneHelper](../../external/oracles-disasm/object_code/ages/interactions/raftwreckCutsceneHelper.s) | — | R | — |
| `$65` | [comedian](../../external/oracles-disasm/object_code/ages/interactions/comedian.s) | $00 | R | — |
| `$66` | [goron](../../external/oracles-disasm/object_code/ages/interactions/goron.s) | $00-$01, $04-$10 | R | — |
| `$67` | [companionSpawner](../../external/oracles-disasm/object_code/common/interactions/companionSpawner.s) | $00-$05 | R | — |
| `$68` | [rosa](../../external/oracles-disasm/object_code/ages/interactions/rosa.s) | $00-$01 | R | — |
| `$69` | [rafton](../../external/oracles-disasm/object_code/ages/interactions/rafton.s) | $00-$01 | R | — |
| `$6a` | [cheval](../../external/oracles-disasm/object_code/ages/interactions/cheval.s) | $00 | R | — |
| `$6b` | [miscellaneous1](../../external/oracles-disasm/object_code/ages/interactions/miscellaneous1.s) | $00-$03, $05-$06, $08, $0a-$15 | R | — |
| `$6c` | [fairyHidingMinigame](../../external/oracles-disasm/object_code/ages/interactions/fairyHidingMinigame.s) | $00-$02 | R | — |
| `$6d` | [possessedNayru](../../external/oracles-disasm/object_code/ages/interactions/possessedNayru.s) | $00 | R | — |
| `$6e` | [nayruSavedCutscene](../../external/oracles-disasm/object_code/ages/interactions/nayruSavedCutscene.s) | — | R | — |
| `$6f` | [bombFlower](../../external/oracles-disasm/object_code/common/interactions/bombFlower.s) | — | D | — |
| `$70` | [wildTokayController](../../external/oracles-disasm/object_code/ages/interactions/wildTokayController.s) | — | R | — |
| `$71` | [companionScripts](../../external/oracles-disasm/object_code/ages/interactions/companionScripts.s) | $00-$0d | R | — |
| `$72` | [kingMoblinDefeated](../../external/oracles-disasm/object_code/ages/interactions/kingMoblinDefeated.s) | $00 | R | — |
| `$73` | [ghiniHarassingMoosh](../../external/oracles-disasm/object_code/ages/interactions/ghiniHarassingMoosh.s) | $00-$02 | R | — |
| `$74` | [rickysGloveSpawner](../../external/oracles-disasm/object_code/ages/interactions/rickysGloveSpawner.s) | $00 | R | — |
| `$75` | [introSprite](../../external/oracles-disasm/object_code/ages/interactions/introSprite.s) | $00, $05-$06 | R | — |
| `$76` | [makuGateOpening](../../external/oracles-disasm/object_code/ages/interactions/makuGateOpening.s) | — | R | — |
| `$77` | [smallKeyOnEnemy](../../external/oracles-disasm/object_code/ages/interactions/smallKeyOnEnemy.s) | $31 | R | — |
| `$78` | [switchTileToggler](../../external/oracles-disasm/object_code/common/interactions/switchTileToggler.s) | $02, $04, $08 | R | — |
| `$79` | [movingPlatform](../../external/oracles-disasm/object_code/common/interactions/movingPlatform.s) | $03, $05, $08, $11, $1a, $23, $29 | R | — |
| `$7a` | [roller](../../external/oracles-disasm/object_code/common/interactions/roller.s) | — | R | — |
| `$7b` | [stonePanel](../../external/oracles-disasm/object_code/ages/interactions/stonePanel.s) | $00-$01 | R | — |
| `$7c` | [screenDistortion](../../external/oracles-disasm/object_code/ages/interactions/screenDistortion.s) | — | R | — |
| `$7d` | [spinner](../../external/oracles-disasm/object_code/common/interactions/spinner.s) | $00-$01 | R | — |
| `$7e` | [minibossPortal](../../external/oracles-disasm/object_code/common/interactions/minibossPortal.s) | $00-$01 | R | — |
| `$7f` | [essence](../../external/oracles-disasm/object_code/common/interactions/essence.s) | $00 | R | — |
| `$80` | [decoration](../../external/oracles-disasm/object_code/ages/interactions/decoration.s) | $00-$01, $04-$0a | R | — |
| `$81` | [tokayShopItem](../../external/oracles-disasm/object_code/ages/interactions/tokayShopItem.s) | $00-$01, $04 | R | — |
| `$82` | [sarcophagus](../../external/oracles-disasm/object_code/ages/interactions/sarcophagus.s) | $00-$02 | R | — |
| `$83` | [bombUpgradeFairy](../../external/oracles-disasm/object_code/ages/interactions/bombUpgradeFairy.s) | $00 | R | — |
| `$84` | [sparkle](../../external/oracles-disasm/object_code/ages/interactions/sparkle.s) | $08 | R | — |
| `$85` | `interactionDelete` | — | D | — |
| `$86` | [makuFlower](../../external/oracles-disasm/object_code/ages/interactions/makuFlower.s) | — | R | — |
| `$87` | [makuTree](../../external/oracles-disasm/object_code/ages/interactions/makuTree.s) | $00, $03-$05 | R | — |
| `$88` | [makuSprout](../../external/oracles-disasm/object_code/ages/interactions/makuSprout.s) | $00, $02 | R | — |
| `$89` | [vasu](../../external/oracles-disasm/object_code/common/interactions/vasu.s) | $00-$01, $06 | P | INT-14 |
| `$8a` | [remoteMakuCutscene](../../external/oracles-disasm/object_code/ages/interactions/remoteMakuCutscene.s) | $00-$01 | R | — |
| `$8b` | [goronElder](../../external/oracles-disasm/object_code/ages/interactions/goronElder.s) | $01-$02 | R | — |
| `$8c` | [tokayMeat](../../external/oracles-disasm/object_code/ages/interactions/tokayMeat.s) | — | R | — |
| `$8d` | [cloakedTwinrova](../../external/oracles-disasm/object_code/ages/interactions/cloakedTwinrova.s) | $02 | R | — |
| `$8e` | [octogonSplash](../../external/oracles-disasm/object_code/ages/interactions/octogonSplash.s) | — | R | — |
| `$8f` | [tokayCutsceneEmberSeed](../../external/oracles-disasm/object_code/ages/interactions/tokayCutsceneEmberSeed.s) | — | R | — |
| `$90` | [miscPuzzles](../../external/oracles-disasm/object_code/ages/interactions/miscPuzzles.s) | $00-$04, $07-$1a, $1e-$20 | R | — |
| `$91` | [bubble](../../external/oracles-disasm/object_code/common/interactions/bubble.s) | — | R | — |
| `$92` | [fallingRock](../../external/oracles-disasm/object_code/ages/interactions/fallingRock.s) | — | R | — |
| `$93` | [twinrova](../../external/oracles-disasm/object_code/ages/interactions/twinrova.s) | $04, $06 | R | — |
| `$94` | [patch](../../external/oracles-disasm/object_code/ages/interactions/patch.s) | $00-$02 | R | — |
| `$95` | [ball](../../external/oracles-disasm/object_code/ages/interactions/ball.s) | $00 | R | — |
| `$96` | [moblin](../../external/oracles-disasm/object_code/ages/interactions/moblin.s) | $00-$01 | R | — |
| `$97` | [97](../../external/oracles-disasm/object_code/ages/interactions/97.s) | — | R | — |
| `$98` | [woodenTunnel](../../external/oracles-disasm/object_code/common/interactions/woodenTunnel.s) | — | N | — |
| `$99` | [explosionWithDebris](../../external/oracles-disasm/object_code/ages/interactions/explosionWithDebris.s) | — | R | — |
| `$9a` | [carpenter](../../external/oracles-disasm/object_code/ages/interactions/carpenter.s) | $00-$04, $09, $b2-$b4, $c2-$c4, $d2-$d4, $ff | R | — |
| `$9b` | [raftwreckCutscene](../../external/oracles-disasm/object_code/ages/interactions/raftwreckCutscene.s) | $00 | R | — |
| `$9c` | [kingZora](../../external/oracles-disasm/object_code/ages/interactions/kingZora.s) | $00-$01 | R | — |
| `$9d` | [tokkey](../../external/oracles-disasm/object_code/ages/interactions/tokkey.s) | $00 | R | — |
| `$9e` | [waterPushblock](../../external/oracles-disasm/object_code/ages/interactions/waterPushblock.s) | $00-$01 | R | — |
| `$9f` | [exclamationMark](../../external/oracles-disasm/object_code/common/interactions/exclamationMark.s) | — | R | — |
| `$a0` | [floatingImage](../../external/oracles-disasm/object_code/common/interactions/floatingImage.s) | — | R | — |
| `$a1` | [movingSidescrollPlatform](../../external/oracles-disasm/object_code/common/interactions/movingSidescrollPlatform.s) | $00-$0e | R | — |
| `$a2` | [movingSidescrollConveyor](../../external/oracles-disasm/object_code/common/interactions/movingSidescrollConveyor.s) | — | N | — |
| `$a3` | [disappearingSidescrollPlatform](../../external/oracles-disasm/object_code/ages/interactions/disappearingSidescrollPlatform.s) | — | N | — |
| `$a4` | [circularSidescrollPlatform](../../external/oracles-disasm/object_code/ages/interactions/circularSidescrollPlatform.s) | $00-$02 | R | — |
| `$a5` | [touchingBook](../../external/oracles-disasm/object_code/ages/interactions/touchingBook.s) | — | R | — |
| `$a6` | [makuSeed](../../external/oracles-disasm/object_code/ages/interactions/makuSeed.s) | — | R | — |
| `$a7` | [endgameCutsceneBipsomFamily](../../external/oracles-disasm/object_code/common/interactions/endgameCutsceneBipsomFamily.s) | — | N | — |
| `$a8` | [a8](../../external/oracles-disasm/object_code/ages/interactions/a8.s) | $02-$04, $14, $54, $64 | R | — |
| `$a9` | [twinrovaFlame](../../external/oracles-disasm/object_code/common/interactions/twinrovaFlame.s) | $00-$08 | R | — |
| `$aa` | [din](../../external/oracles-disasm/object_code/ages/interactions/din.s) | $00-$01 | R | — |
| `$ab` | [zora](../../external/oracles-disasm/object_code/ages/interactions/zora.s) | $00-$0e, $10-$1b | R | — |
| `$ac` | [bipinBlossomFamilySpawner](../../external/oracles-disasm/object_code/common/interactions/bipinBlossomFamilySpawner.s) | $00-$01 | R | — |
| `$ad` | [zelda](../../external/oracles-disasm/object_code/ages/interactions/zelda.s) | $00-$02, $04-$05, $07-$08 | R | — |
| `$ae` | [creditsTextHorizontal](../../external/oracles-disasm/object_code/common/interactions/creditsTextHorizontal.s) | — | R | — |
| `$af` | [creditsTextVertical](../../external/oracles-disasm/object_code/common/interactions/creditsTextVertical.s) | — | R | — |
| `$b0` | [twinrovaInCutscene](../../external/oracles-disasm/object_code/ages/interactions/twinrovaInCutscene.s) | $00-$03 | R | — |
| `$b1` | [tuniNut](../../external/oracles-disasm/object_code/ages/interactions/tuniNut.s) | $00 | R | — |
| `$b2` | [volcanoHandler](../../external/oracles-disasm/object_code/ages/interactions/volcanoHandler.s) | — | R | — |
| `$b3` | [harpOfAgesSpawner](../../external/oracles-disasm/object_code/ages/interactions/harpOfAgesSpawner.s) | $00 | R | — |
| `$b4` | [bookOfSealsPodium](../../external/oracles-disasm/object_code/ages/interactions/bookOfSealsPodium.s) | $00 | R | — |
| `$b5` | [finalDungeonEnergy](../../external/oracles-disasm/object_code/common/interactions/finalDungeonEnergy.s) | $00 | R | — |
| `$b6` | [gashaSpot](../../external/oracles-disasm/object_code/common/interactions/gashaSpot.s) | $00-$0f | R | — |
| `$b7` | [kissHeart](../../external/oracles-disasm/object_code/common/interactions/kissHeart.s) | — | R | — |
| `$b8` | [vire](../../external/oracles-disasm/object_code/ages/interactions/vire.s) | $00-$02 | R | — |
| `$b9` | [horonDogCredits](../../external/oracles-disasm/object_code/common/interactions/horonDogCredits.s) | $00-$07 | R | — |
| `$ba` | [childJabu](../../external/oracles-disasm/object_code/ages/interactions/childJabu.s) | $00 | R | — |
| `$bb` | [humanVeran](../../external/oracles-disasm/object_code/ages/interactions/humanVeran.s) | — | R | — |
| `$bc` | [twinrova3](../../external/oracles-disasm/object_code/ages/interactions/twinrova3.s) | — | R | — |
| `$bd` | [pushblockSynchronizer](../../external/oracles-disasm/object_code/ages/interactions/pushblockSynchronizer.s) | $00 | R | — |
| `$be` | [ambisPalaceButton](../../external/oracles-disasm/object_code/ages/interactions/ambisPalaceButton.s) | $00-$03 | R | — |
| `$bf` | [symmetryNpc](../../external/oracles-disasm/object_code/ages/interactions/symmetryNpc.s) | $00, $02, $04, $06-$0c | R | — |
| `$c0` | [banana](../../external/oracles-disasm/object_code/common/interactions/banana.s) | — | R | — |
| `$c1` | [c1](../../external/oracles-disasm/object_code/common/interactions/c1.s) | $00 | R | — |
| `$c2` | [pirateShip](../../external/oracles-disasm/object_code/ages/interactions/pirateShip.s) | $01-$02 | R | — |
| `$c3` | [pirateCaptain](../../external/oracles-disasm/object_code/ages/interactions/pirateCaptain.s) | $00 | R | — |
| `$c4` | [pirate](../../external/oracles-disasm/object_code/ages/interactions/pirate.s) | $00-$04 | R | — |
| `$c5` | [playHarpSong](../../external/oracles-disasm/object_code/ages/interactions/playHarpSong.s) | — | R | — |
| `$c6` | [blackTowerDoorHandler](../../external/oracles-disasm/object_code/ages/interactions/blackTowerDoorHandler.s) | $00 | R | — |
| `$c7` | [createObjectAtEachTileindex](../../external/oracles-disasm/object_code/common/interactions/createObjectAtEachTileindex.s) | $04, $08, $a0 | R | — |
| `$c8` | [tingle](../../external/oracles-disasm/object_code/ages/interactions/tingle.s) | $00 | R | — |
| `$c9` | [syrupCucco](../../external/oracles-disasm/object_code/common/interactions/syrupCucco.s) | $80 | R | — |
| `$ca` | [troy](../../external/oracles-disasm/object_code/ages/interactions/troy.s) | $00-$01 | R | — |
| `$cb` | [linkedGameGhini](../../external/oracles-disasm/object_code/ages/interactions/linkedGameGhini.s) | $00 | R | — |
| `$cc` | [plen](../../external/oracles-disasm/object_code/ages/interactions/plen.s) | $00 | R | — |
| `$cd` | [masterDiver](../../external/oracles-disasm/object_code/ages/interactions/masterDiver.s) | $00 | R | — |
| `$ce` | [businessScrub](../../external/oracles-disasm/object_code/common/interactions/businessScrub.s) | $00, $03, $06 | R | — |
| `$cf` | [cf](../../external/oracles-disasm/object_code/common/interactions/cf.s) | — | R | — |
| `$d0` | [companionTutorial](../../external/oracles-disasm/object_code/common/interactions/companionTutorial.s) | $00-$01, $03-$04 | R | — |
| `$d1` | [gameCompleteDialog](../../external/oracles-disasm/object_code/common/interactions/gameCompleteDialog.s) | — | R | — |
| `$d2` | [titlescreenClouds](../../external/oracles-disasm/object_code/common/interactions/titlescreenClouds.s) | — | R | — |
| `$d3` | [introBird](../../external/oracles-disasm/object_code/common/interactions/introBird.s) | — | R | — |
| `$d4` | [linkShip](../../external/oracles-disasm/object_code/common/interactions/linkShip.s) | $00, $02, $11, $21, $31, $41, $51, $61, $71 | R | — |
| `$d5` | [greatFairy](../../external/oracles-disasm/object_code/ages/interactions/greatFairy.s) | $00 | R | — |
| `$d6` | [dekuScrub](../../external/oracles-disasm/object_code/ages/interactions/dekuScrub.s) | $00 | R | — |
| `$d7` | [makuSeedAndEssences](../../external/oracles-disasm/object_code/ages/interactions/makuSeedAndEssences.s) | — | R | — |
| `$d8` | [leverLavaFiller](../../external/oracles-disasm/object_code/ages/interactions/leverLavaFiller.s) | $00-$05 | R | — |
| `$d9` | [faroreGiveItem](../../external/oracles-disasm/object_code/common/interactions/faroreGiveItem.s) | — | R | — |
| `$da` | [zeldaApproachTrigger](../../external/oracles-disasm/object_code/common/interactions/zeldaApproachTrigger.s) | $00 | R | — |
| `$db` | [slateSlot](../../external/oracles-disasm/object_code/ages/interactions/slateSlot.s) | $00-$03 | R | — |
| `$dc` | [miscellaneous2](../../external/oracles-disasm/object_code/ages/interactions/miscellaneous2.s) | $00-$17 | R | — |
| `$dd` | [timewarp](../../external/oracles-disasm/object_code/ages/interactions/timewarp.s) | — | R | — |
| `$de` | [timeportal](../../external/oracles-disasm/object_code/ages/interactions/timeportal.s) | — | R | — |
| `$df` | [nayruRalphCredits](../../external/oracles-disasm/object_code/common/interactions/nayruRalphCredits.s) | $00-$01 | R | — |
| `$e0` | [eraOrSeasonInfo](../../external/oracles-disasm/object_code/common/interactions/eraOrSeasonInfo.s) | — | R | — |
| `$e1` | [timeportalSpawner](../../external/oracles-disasm/object_code/ages/interactions/timeportalSpawner.s) | $00-$02, $40 | R | — |
| `$e2` | [statueEyeball](../../external/oracles-disasm/object_code/common/interactions/statueEyeball.s) | $01, $03 | R | — |
| `$e3` | [knowItAllBird](../../external/oracles-disasm/object_code/ages/interactions/knowItAllBird.s) | $00-$09 | R | — |
| `$e4` | `interactionDelete` | — | D | — |
| `$e5` | [ringHelpBook](../../external/oracles-disasm/object_code/common/interactions/ringHelpBook.s) | $00-$01 | R | — |
| `$e6` | [raft](../../external/oracles-disasm/object_code/ages/interactions/raft.s) | $00-$01 | R | — |

## Native cutscene dispatch

The source denominator is [cutsceneIndices.s](../../external/oracles-disasm/constants/common/cutsceneIndices.s)
and the [bank1.s](../../external/oracles-disasm/code/bank1.s) jump table.
`$00-$05` and `$16` are infrastructure states, retained
here so the original dispatcher has no omitted rows. R includes linked and
unlinked owners; it does not certify every internal state or allocation case.

| Index | Original behavior | Production ownership | Status |
| --- | --- | --- | --- |
| `$00` | Room loading | Room transition owner | R |
| `$01` | In-game dispatch | Application fixed-update scheduler | R |
| `$02` | Toggle blocks | Dungeon toggle / transition owner | R |
| `$03` | Ordinary fade warp | Room transition owner | R |
| `$04` | Mounted fade warp | Room transition owner | R |
| `$05` | Room-pack fade/load | Room transition owner | R |
| `$06` | Nayru singing / possession | Nayru intro event | R |
| `$07` | Maku Tree disappearance | Maku Tree disappearance event | R |
| `$08` | Black Tower explanation, stages 0-2 | Black Tower entrance / post-D3 / post-D7 events | R |
| `$09` | Black Tower escape / ending | Black Tower ending event | R |
| `$0a` | Credits, linked and unlinked | Black Tower ending event | R |
| `$0b` | Wall retraction, D6/D8 | Wall retraction event | R |
| `$0c` | Nayru rescue warp | Nayru rescue warp event | R |
| `$0d` | Pregame Triforce summons | New-game intro controller | R |
| `$0e` | Twinrova reveal | Maku Seed event | R |
| `$0f` | Room of Rites collapse | Black Tower ending event | R |
| `$10` | Turn to stone | Post-D7 event | R |
| `$11` | Flame of Sorrow | Black Tower ending linked lead-in | R |
| `$12` | Zelda kidnapped | Black Tower ending linked lead-in | R |
| `$13` | Fairy hide and seek | Fairies' Woods event | R |
| `$14` | Booted from palace | Palace boot event | R |
| `$15` | Legacy wave-scroll warp | No clean-US producer identified | N |
| `$16` | Gale Seed menu | Gale Seed menu / transition owner | R |
| `$17` | Warp to Twinrova fight | Room of Rites Zelda event | R |
| `$18` | Flames flicker / Twinrova lead-in | Room of Rites battle event | R |
| `$19` | Twinrova sacrifice / Ganon revival | Room of Rites battle / Ganon owners | R |
| `$1a` | Present D2 collapse | Wing Dungeon collapse event | R |
| `$1b` | Timewarp | Room transition / timewarp effects | R |
| `$1c` | Ambi passage opens | Palace passage event | R |
| `$1d` | Jabu opens | Jabu exterior event | R |
| `$1e` | Clean seas | Clean Seas event | R |
| `$1f` | Black Tower escape attempt | Black Tower escape-attempt event | R |
| `$20` | Flame of Despair | Black Tower ending event | R |
| `$21` | Black Tower complete | Post-D7 event | R |

## Room-specific code

Source: [roomSpecificCode.s](../../external/oracles-disasm/code/ages/roomSpecificCode.s).
Room bindings are listed independently of visible actors.

| Index | Rooms | Behavior | Status | Open item |
| --- | --- | --- | --- | --- |
| `$00` | `0:93` | Fairy scratch reset | R | — |
| `$01` | `4:60` | D3 spinner before crystals | R | — |
| `$02` | `4:52` | D3 spinner after crystals | R | — |
| `$03` | `1:81` | Mystery Seed palace soldier | R | — |
| `$04` | `5:d2` | Palace minimap/visit writes | R | — |
| `$05` | `2:0e` | Nayru-house music | R | — |
| `$06` | `0:38`, `1:38` | Set death respawn point | R | — |
| `$07` | `1:97` | Post-Rafton Ralph music | R | — |
| `$08` | `0:39` | Portal-departure Ralph music | R | — |
| `$09` | `0:3a` | Finished-game Nayru music | R | — |
| `$0a` | `1:0e` | Present D6 minimap | R | — |
| `$0b` | `3:0f` | Past D6 minimap | R | — |
| `$0c` | `4:e6` | Vire-room active-music reset | R | — |

## Room-specific tile-change dispatch

Source: [roomSpecificTileChanges.s](../../external/oracles-disasm/code/ages/roomSpecificTileChanges.s).
R accounts for a runtime layout route and its original actor-creation side effects.

| Index | Rooms | Source handler | Status | Open item |
| --- | --- | --- | --- | --- |
| `$00` | `5:f5` | `tileReplacement_group5Mapf5` | R | — |
| `$01` | `4:1b` | `tileReplacement_group4Map1b` | R | — |
| `$02` | `2:7e` | `tileReplacement_group2Map7e` | R | — |
| `$03` | `4:4c` | `tileReplacement_group4Map4c` | R | — |
| `$04` | `4:4e` | `tileReplacement_group4Map4e` | R | — |
| `$05` | `4:59` | `tileReplacement_group4Map59` | R | — |
| `$06` | `4:60` | `tileReplacement_group4Map60` | R | — |
| `$07` | `4:52` | `tileReplacement_group4Map52` | R | — |
| `$08` | `0:38` | `tileReplacement_group0Map38` | R | — |
| `$09` | `1:38` | `tileReplacement_group1Map38` | R | — |
| `$0a` | `5:38` | `tileReplacement_group5Map38` | R | — |
| `$0b` | `5:25` | `tileReplacement_group5Map25` | R | — |
| `$0c` | `5:43` | `tileReplacement_group5Map43` | R | — |
| `$0d` | `5:4c` | `tileReplacement_group5Map4c` | R | — |
| `$0e` | `5:5c`, `5:71` | `tileReplacement_group5Map5c` | R | — |
| `$0f` | `5:4d` | `tileReplacement_group5Map4d` | R | — |
| `$10` | `5:5d`, `5:72` | `tileReplacement_group5Map5d` | R | — |
| `$11` | `7:4a` | `tileReplacement_group7Map4a` | R | — |
| `$12` | `5:95` | `tileReplacement_group5Map95` | R | — |
| `$13` | `5:c3` | `tileReplacement_group5Mapc3` | R | — |
| `$14` | `0:5c` | `tileReplacement_group0Map5c` | R | — |
| `$15` | `2:f7` | `tileReplacement_group2Mapf7` | R | — |
| `$16` | `0:73` | `tileReplacement_group0Map73` | R | — |
| `$17` | `0:48` | `tileReplacement_group0Map48` | R | — |
| `$18` | `0:ac` | `tileReplacement_group0Mapac` | R | — |
| `$19` | `0:2c` | `tileReplacement_group0Map2c` | R | — |
| `$1a` | `0:1c` | `tileReplacement_group0Map1c` | R | — |
| `$1b` | `0:ba` | `tileReplacement_group0Mapba` | R | — |
| `$1c` | `0:aa` | `tileReplacement_group0Mapaa` | R | — |
| `$1d` | `0:cc` | `tileReplacement_group0Mapcc` | R | — |
| `$1e` | `0:bc` | `tileReplacement_group0Mapbc` | R | — |
| `$1f` | `0:da` | `tileReplacement_group0Mapda` | R | — |
| `$20` | `0:ca` | `tileReplacement_group0Mapca` | R | — |
| `$21` | `0:61` | `tileReplacement_group0Map61` | R | — |
| `$22` | `0:51` | `tileReplacement_group0Map51` | R | — |
| `$23` | `0:54` | `tileReplacement_group0Map54` | R | — |
| `$24` | `0:25` | `tileReplacement_group0Map25` | R | — |
| `$25` | `0:3a` | `tileReplacement_group0Map3a` | R | — |
| `$26` | `0:0b` | `tileReplacement_group0Map0b` | R | — |
| `$27` | `5:b9` | `tileReplacement_group5Mapb9` | R | — |
| `$28` | `1:27` | `tileReplacement_group1Map27` | R | — |
| `$29` | `5:c2` | `tileReplacement_group5Mapc2` | R | — |
| `$2a` | `5:e3` | `tileReplacement_group5Mape3` | R | — |
| `$2b` | `2:90` | `tileReplacement_group2Map90` | R | — |
| `$2c` | `1:8c` | `tileReplacement_group1Map8c` | R | — |
| `$2d` | `4:c7` | `tileReplacement_group4Mapc7` | R | — |
| `$2e` | `4:c9` | `tileReplacement_group4Mapc9` | R | — |
| `$2f` | `2:9e` | `tileReplacement_group2Map9e` | R | — |
| `$30` | `0:e0` | `tileReplacement_group0Mape0` | R | — |
| `$31` | `0:e1` | `tileReplacement_group0Mape1` | R | — |
| `$32` | `0:e2` | `tileReplacement_group0Mape2` | R | — |
| `$33` | `4:ea` | `tileReplacement_group4Mapea` | R | — |
| `$34` | `1:58` | `tileReplacement_group1Map58` | R | — |
| `$35` | `0:98` | `tileReplacement_group0Map98` | R | — |
| `$36` | `0:76` | `tileReplacement_group0Map76` | R | — |
| `$37` | `0:a5` | `tileReplacement_group0Mapa5` | R | — |

## Scripted special-object dispatch

Sources: [Link cutscene dispatch](../../external/oracles-disasm/object_code/ages/specialObjects/linkInCutscene.s)
and [companion/Maple credit dispatch](../../external/oracles-disasm/object_code/ages/specialObjects/companionCutscene.s).

| Object | Subid | Source behavior | Status |
| --- | --- | --- | --- |
| Link cutscene `$08` | `$00` | Attract-mode Triforce scene | R |
| Link cutscene `$08` | `$01` | Impa encounter | R |
| Link cutscene `$08` | `$02` | Impa following/stone positioning | R |
| Link cutscene `$08` | `$03` | Nayru possession scene | R |
| Link cutscene `$08` | `$04` | Nayru scene movement | R |
| Link cutscene `$08` | `$05` | Shared Maku Sprout / Goron elder / comedian paths | R |
| Link cutscene `$08` | `$06` | Triforce stone push | R |
| Link cutscene `$08` | `$07` | Tokay theft | R |
| Link cutscene `$08` | `$08` | Nayru rescue hidden/show handshake | R |
| Link cutscene `$08` | `$09` | Maku credits | R |
| Link cutscene `$08` | `$0a` | Ending reunion / kiss | R |
| Link cutscene `$08` | `$0b` | Pregame summons | R |
| Link cutscene `$08` | `$0c` | Room of Rites reaction | R |
| `$0f` | Source credit states | Ricky credits | R |
| `$10` | Source credit states | Dimitri credits | R |
| `$11` | Source credit states | Moosh credits | R |
| `$12` | Source credit states | Maple credits | R |

## Remaining fidelity proof

The following are proof or explicitly rejected execution boundaries, rather
than additional confirmed missing story sequences. Their reachability and
original effects must be established before claiming complete fidelity.

- [ ] **PROOF-01** — Reconcile the source-supported allocation-failure continuations currently rejected by cutscene guards: Zelda rescue `$ad:$06`, Twinrova reveal `$87:$06`, Room of Rites cleared-slot/retained-pointer aliases, and Ganon's unchecked shadow allocation.
- [ ] **PROOF-02** — Close branch and lifecycle evidence for the R inventory through source-derived or executed-ROM scenarios, including input/object masks, scrolling/warp entry, cancellation, re-entry, persistent completion, and individual versus batched gameplay updates. Existing registered scenarios cover bounded paths; their presence does not prove every source branch.

Maintenance guidance is in [NPCs and events](../npcs-and-events.md). Keep current
statuses, counts and outstanding work here; keep audit narratives and temporary
evidence outside tracked documentation.
