# NPC and character interaction coverage

This is the room-by-room coverage snapshot for imported visible character
interactions as of July 27, 2026. It is an implementation inventory, not a
claim that every listed room has been compared exhaustively against a retail
playthrough.

The inventory covers:

- all 388 positioned or state-derived rows in
  `assets/oracle/objects/npcs.tsv`, spanning 211 rooms;
- all 72 conditional Bipin/Blossom family rows in
  `assets/oracle/objects/bipin_blossom_family.tsv`; and
- the explicit implementation classification selected for every record:
  ordinary NPC adapter, specialized native interaction, event-owned actor, or
  deliberately unsupported.

It does not enumerate enemies, tile interactions, invisible dungeon
mechanisms, treasures, dynamically spawned cutscene actors, or interaction
records that are not represented as `NpcRecord`. Those systems remain covered
by [Implementation status](implementation-status.md), the subsystem guides,
and their headless scenarios.

## Status definitions

| Code | Meaning |
| --- | --- |
| **I** | Implemented: the record has a traced ordinary, specialized, or event-owned production path and a named headless regression for its supported behavior. |
| **P** | Partial: useful presentation or a bounded specialized path works, but one or more native, script, state, or external-system branches remain unavailable or unproven. |
| **D** | Deliberately unsupported: the placed variant is explicitly suppressed because its original native/script behavior is not implemented. |

Record notation is `$id:$subid/v$var03`. The trailing name is the corresponding
`interactionCode` source handler in `oracles-disasm`. Rooms are sorted
numerically for navigation; records within a room retain generated source
order.

The distinction between **P** and **D** is intentionally conservative. A
record with a traced bounded production path can be partial. A row whose
native or script owner is unavailable is suppressed, even when the importer
can resolve graphics, initial text, or a visibility predicate. Neither status
can be promoted to **I** merely because the actor looks plausible in one room.

## Snapshot

### Positioned and state-derived rows

| Status | Records | Interpretation |
| --- | ---: | --- |
| **I** | 70 | Traced and covered by the current named NPC/event scenarios. |
| **P** | 44 | A traced ordinary, specialized, or event-owned path exists with a documented boundary. |
| **D** | 274 | Original native/script ownership is not implemented, so no actor is instantiated. |
| **Total** | **388** | **211 rooms and 294 unique ID/subid keys.** |

The separate family table contributes 72 conditional variants in rooms
`2:ea` and `2:eb`. Their selection, running Bipin, child naming, representative
stage/personality dialogue, and finished-game suppression are covered, but the
family remains **P** until its progression ownership and all per-variant
behavior are independently completed. All 72 rows are explicitly classified
as specialized native interactions.

The implementation manifest therefore contains 51 ordinary, 51 specialized,
14 event-owned, and 272 deliberately unsupported positioned/state-derived
rows. Including the family table, the typed runtime database contains 460
classified records and 123 specialized rows.

### Important partial and deferred boundaries

- Room `0:38` implements the Maku Tree disappearance and
  `wMakuTreeState=$02` Seed Satchel path. Later adult-tree states are not
  classified here and keep the record at **P**.
- Room `2:ee` implements Vasu, the snakes, help books, ring appraisal/list
  flows, and the original no-cable failure. Real Game Link transfer and linked
  secret input remain unavailable, so all five records remain **P**.
- Placed Forest Fairy `$49:$05-$10` phases are **D** and safely suppressed.
  The implemented Fairies' Woods hide-and-seek uses event-created `$49:$00`
  actors instead.
- Rooms that mix **I**, **P**, and **D** have only the marked records covered.
  One implemented actor does not make the other room interactions complete.

### Highest-value missing clusters

- Tokay interaction `$48` across the past and present trading/island rooms.
- Goron interaction `$66`, Goron Elders `$8b`, and the Goron/Biggoron
  shooting-gallery variants `$30:$01/$02` across groups 3 and 5.
- Zora `$ab`, King Zora `$9c`, Old Zora `$5a`, and Jabu-Jabu child `$ba`.
- Soldier `$40` and the surrounding palace, ending, and linked-game cast
  outside the implemented pre-Black-Tower and lower-Tower slices.
- Symmetry City `$bf`, carpenter `$9a`, Mamamu Yan/dog `$53/$54`, Tingle
  `$c8`, Bomb Upgrade Fairy `$83`, Rafton/Cheval `$69/$6a`, Toilet Hand `$5b`,
  Postman `$55`, Syrup `$5f`, and the remaining shopkeeper variants `$46`.
- Old-lady linked-secret records `$3d:$04/$05`: their complete linked-secret
  interaction graphs are not implemented like the Graveyard Ghini `$cb:$00`,
  so their placements are deliberately suppressed.

## Bipin and Blossom family variants

| Room | Coverage |
| --- | --- |
| `2:ea` | **[P] 38 generated variants:** Bipin `$28`, Blossom `$2b`, and child `$35`; state/personality selection, naming, and representative dialogue are covered. |
| `2:eb` | **[P] 34 generated variants:** Bipin `$28`, Blossom `$2b`, and child `$35`; stage advancement and running Bipin are covered. |

These are mutually selected alternatives, not 72 simultaneous actors.
`NpcDatabase.GetRoomNpcs` currently owns family progression side effects as
well as record selection. The ownership/consolidation work in
[TODO](../TODO.md) remains applicable.

## Room inventory

| Room | Imported records in source order |
| --- | --- |
| `0:09` | [D] `$72:$00/v$00` kingMoblinDefeated |
| `0:16` | [D] `$9a:$b2/v$00` carpenter<br>[D] `$9a:$d2/v$00` carpenter |
| `0:17` | [D] `$9a:$c2/v$00` carpenter<br>[D] `$9a:$d3/v$00` carpenter |
| `0:25` | [D] `$9a:$00/v$00` carpenter<br>[D] `$9a:$01/v$00` carpenter<br>[D] `$9a:$02/v$00` carpenter<br>[D] `$9a:$03/v$00` carpenter<br>[D] `$9a:$04/v$00` carpenter |
| `0:26` | [D] `$9a:$b3/v$00` carpenter<br>[D] `$9a:$c3/v$00` carpenter |
| `0:27` | [D] `$9a:$b4/v$00` carpenter<br>[D] `$9a:$d4/v$00` carpenter |
| `0:37` | [D] `$9a:$c4/v$00` carpenter |
| `0:38` | [P] `$87:$00/v$00` makuTree |
| `0:39` | [I] `$37:$0d/v$00` ralph<br>[D] `$5d:$02/v$00` bear |
| `0:3a` | [D] `$36:$13/v$00` nayru<br>[D] `$37:$11/v$00` ralph<br>[D] `$3c:$10/v$00` boy<br>[D] `$39:$07/v$01` monkey<br>[D] `$5d:$02/v$01` bear |
| `0:45` | [I] `$3f:$01/v$00` boy2 |
| `0:46` | [D] `$41:$02/v$00` miscMan<br>[D] `$41:$05/v$00` miscMan<br>[P] `$3d:$02/v$00` oldLady |
| `0:48` | [I] `$3a:$03/v$00` villager<br>[D] `$54:$01/v$03` mamamuDog |
| `0:50` | [D] `$83:$00/v$00` bombUpgradeFairy |
| `0:55` | [D] `$54:$01/v$01` mamamuDog |
| `0:56` | [I] `$3a:$04/v$00` villager<br>[I] `$65:$00/v$00` comedian |
| `0:57` | [I] `$41:$01/v$00` miscMan |
| `0:58` | [I] `$41:$04/v$00` miscMan |
| `0:5a` | [I] `$39:$02/v$00` monkey<br>[I] `$39:$03/v$00` monkey |
| `0:5d` | [I] `$cb:$00/v$00` linkedGameGhini |
| `0:65` | [D] `$31:$07/v$00` impaInCutscene<br>[D] `$4c:$04/v$00` bird |
| `0:66` | [I] `$3b:$01/v$00` femaleVillager |
| `0:67` | [D] `$41:$03/v$00` miscMan |
| `0:68` | [I] `$44:$02/v$00` miscMan2<br>[I] `$3a:$05/v$00` villager<br>[I] `$3b:$02/v$00` femaleVillager<br>[I] `$3c:$02/v$00` boy |
| `0:6a` | [I] `$31:$00/v$00` impaInCutscene |
| `0:70` | [D] `$49:$0b/v$00` forestFairy |
| `0:71` | [D] `$49:$07/v$00` forestFairy |
| `0:72` | [D] `$49:$0c/v$00` forestFairy |
| `0:77` | [D] `$44:$03/v$00` miscMan2 |
| `0:78` | [D] `$3d:$04/v$00` oldLady |
| `0:79` | [D] `$c8:$00/v$00` tingle |
| `0:7b` | [I] `$3c:$03/v$00` boy<br>[I] `$3c:$04/v$00` boy<br>[I] `$3f:$02/v$00` boy2 |
| `0:7c` | [I] `$59:$00/v$00` poe<br>[I] `$59:$00/v$02` poe |
| `0:80` | [D] `$49:$06/v$00` forestFairy |
| `0:81` | [D] `$49:$0d/v$00` forestFairy |
| `0:82` | [D] `$49:$05/v$00` forestFairy<br>[D] `$49:$05/v$00` forestFairy<br>[D] `$49:$08/v$00` forestFairy<br>[D] `$49:$09/v$00` forestFairy<br>[D] `$49:$0a/v$00` forestFairy<br>[D] `$49:$0a/v$00` forestFairy<br>[D] `$49:$0e/v$00` forestFairy<br>[D] `$49:$0f/v$00` forestFairy<br>[D] `$49:$10/v$00` forestFairy |
| `0:83` | [I] `$d5:$00/v$00` greatFairy<br>native non-character `$dc:$02` Wing Dungeon collapse |
| `0:86` | [D] `$54:$01/v$00` mamamuDog |
| `0:88` | [D] `$54:$01/v$02` mamamuDog |
| `0:a0` | [D] `$cd:$00/v$00` masterDiver |
| `0:a7` | [D] `$41:$06/v$00` miscMan |
| `0:aa` | [D] `$48:$0f/v$00` tokay<br>[D] `$48:$10/v$00` tokay |
| `0:bb` | [D] `$48:$1e/v$00` tokay |
| `0:bd` | [D] `$48:$12/v$00` tokay |
| `0:cd` | [D] `$48:$13/v$00` tokay |
| `0:dd` | [D] `$48:$14/v$00` tokay |
| `1:03` | [P] `$bf:$0c/v$00` symmetryNpc |
| `1:05` | [D] `$40:$0d/v$02` soldier |
| `1:06` | [D] `$40:$0d/v$03` soldier<br>[D] `$40:$0d/v$04` soldier |
| `1:07` | [D] `$40:$0d/v$05` soldier |
| `1:16` | [D] `$40:$04/v$00` soldier<br>[D] `$40:$06/v$00` soldier<br>[D] `$4d:$00/v$00` ambi<br>[D] `$36:$01/v$00` nayru<br>[D] `$40:$0d/v$01` soldier |
| `1:20` | [D] `$37:$0b/v$00` ralph |
| `1:25` | [D] `$40:$0d/v$00` soldier |
| `1:26` | [D] `$40:$05/v$00` soldier |
| `1:27` | [D] `$40:$0d/v$06` soldier |
| `1:2d` | [D] `$4e:$04/v$00` subrosian |
| `1:36` | [D] `$40:$05/v$00` soldier<br>[D] `$40:$03/v$00` soldier<br>[D] `$40:$03/v$00` soldier<br>[D] `$40:$03/v$00` soldier<br>[D] `$40:$03/v$00` soldier |
| `1:38` | [I] `$88:$00/v$00` makuSprout |
| `1:39` | [I] `$3a:$0d/v$00` villager |
| `1:45` | [I] `$43:$01/v$00` pastGuy |
| `1:46` | [D] `$40:$02/v$00` soldier<br>[D] `$40:$09/v$00` soldier<br>[D] `$37:$09/v$00` ralph<br>[D] `$40:$0b/v$00` soldier |
| `1:47` | [P] `$3a:$07/v$00` villager<br>[D] `$4f:$01/v$00` impaNpc<br>[D] `$ad:$08/v$00` zelda |
| `1:48` | [I] `$57:$00/v$00` pickaxeWorker<br>[I] `$3a:$06/v$00` villager<br>[I] `$38:$00/v$00` pastGirl |
| `1:49` | [I] `$3c:$0e/v$00` boy<br>[I] `$3a:$0c/v$00` villager<br>[I] `$43:$06/v$00` pastGuy |
| `1:57` | [I] `$3b:$05/v$00` femaleVillager |
| `1:58` | [I] `$44:$04/v$00` miscMan2<br>[I] `$4f:$02/v$00` impaNpc<br>[I] `$36:$0d/v$00` nayru |
| `1:65` | [P] `$3b:$04/v$00` femaleVillager<br>[P] `$4d:$0a/v$00` ambi<br>[P] `$37:$12/v$00` ralph |
| `1:66` | [P] `$3a:$08/v$00` villager |
| `1:68` | [P] `$3b:$03/v$00` femaleVillager<br>[I] `$43:$02/v$00` pastGuy<br>[D] `$4e:$00/v$00` subrosian<br>[D] `$36:$0c/v$00` nayru |
| `1:70` | [D] `$ce:$06/v$00` businessScrub |
| `1:72` | [P] `$40:$00/v$00` soldier |
| `1:73` | [P] `$40:$00/v$01` soldier |
| `1:74` | [P] `$45:$00/v$00` pastOldLady |
| `1:75` | [I] `$37:$0a/v$00` ralph<br>[I] `$31:$04/v$00` impaInCutscene<br>[I] `$31:$05/v$00` impaInCutscene<br>[I] `$36:$0a/v$00` nayru<br>[I] `$ad:$04/v$00` zelda<br>[I] `$58:$01/v$00` hardhatWorker<br>[I] `$58:$01/v$01` hardhatWorker |
| `1:77` | [P] `$45:$01/v$00` pastOldLady |
| `1:79` | [D] `$37:$10/v$00` ralph |
| `1:81` | [D] `$ce:$03/v$00` businessScrub |
| `1:82` | [D] `$44:$00/v$00` miscMan2<br>[P] `$3f:$00/v$00` boy2 |
| `1:83` | [D] `$41:$00/v$00` miscMan |
| `1:84` | [D] `$4b:$06/v$00` rabbit<br>[D] `$4b:$06/v$00` rabbit<br>[D] `$4b:$06/v$00` rabbit<br>[P] `$40:$01/v$00` soldier |
| `1:86` | [I] `$58:$02/v$00` hardhatWorker |
| `1:90` | [D] `$d6:$00/v$00` dekuScrub<br>[D] `$ce:$00/v$00` businessScrub |
| `1:92` | [P] `$43:$00/v$00` pastGuy |
| `1:93` | [D] `$42:$00/v$00` mustacheMan<br>[P] `$40:$01/v$01` soldier |
| `1:94` | [P] `$43:$00/v$01` pastGuy |
| `1:96` | [D] `$3b:$06/v$00` femaleVillager |
| `1:97` | [D] `$37:$03/v$00` ralph |
| `1:aa` | [D] `$48:$00/v$00` tokay<br>[D] `$48:$01/v$00` tokay<br>[D] `$48:$02/v$00` tokay<br>[D] `$48:$03/v$00` tokay<br>[D] `$48:$04/v$00` tokay<br>[D] `$48:$1f/v$00` tokay |
| `1:ac` | [D] `$48:$11/v$00` tokay |
| `1:ad` | [D] `$48:$15/v$00` tokay<br>[D] `$48:$15/v$00` tokay |
| `1:ba` | [D] `$c4:$04/v$00` pirate |
| `1:bb` | [D] `$48:$0a/v$00` tokay<br>[D] `$48:$0b/v$00` tokay |
| `1:bc` | [D] `$ce:$00/v$00` businessScrub |
| `1:bd` | [D] `$48:$16/v$00` tokay |
| `1:cb` | [D] `$48:$07/v$00` tokay<br>[D] `$68:$00/v$00` rosa |
| `1:cd` | [D] `$48:$17/v$00` tokay |
| `1:da` | [D] `$48:$08/v$00` tokay |
| `1:dd` | [D] `$48:$18/v$00` tokay |
| `2:0e` | [I] `$3c:$0d/v$00` boy<br>[I] `$3d:$00/v$00` oldLady |
| `2:0f` | [D] `$6a:$00/v$00` cheval |
| `2:1e` | [D] `$69:$00/v$00` rafton |
| `2:1f` | [D] `$69:$01/v$00` rafton |
| `2:2e` | [I] `$59:$00/v$01` poe |
| `2:2f` | [D] `$55:$00/v$00` postman |
| `2:3e` | [D] `$5b:$00/v$00` toiletHand |
| `2:3f` | [D] `$48:$05/v$00` tokay |
| `2:4e` | [D] `$2e:$01/v$00` oldManWithRupees |
| `2:5e` | [I] `$46:$00/v$00` shopkeeper |
| `2:7e` | [D] `$46:$01/v$00` shopkeeper |
| `2:a0` | [D] `$ab:$10/v$00` zora<br>[D] `$ab:$14/v$00` zora |
| `2:b1` | [D] `$ab:$13/v$00` zora |
| `2:d1` | [D] `$ab:$1b/v$00` zora |
| `2:d7` | [D] `$ab:$12/v$00` zora |
| `2:de` | [D] `$48:$0d/v$00` tokay |
| `2:e4` | [D] `$48:$0e/v$00` tokay |
| `2:e5` | [D] `$48:$19/v$00` tokay<br>[D] `$48:$1a/v$00` tokay<br>[D] `$48:$1b/v$00` tokay<br>[D] `$48:$1c/v$00` tokay |
| `2:e6` | [I] `$5c:$00/v$00` maskSalesman |
| `2:e7` | [D] `$53:$00/v$00` mamamuYan<br>[D] `$54:$00/v$00` mamamuDog |
| `2:e8` | [D] `$51:$00/v$00` dumbellMan |
| `2:e9` | [I] `$30:$00/v$00` shootingGallery |
| `2:ee` | [P] `$89:$00/v$00` vasu<br>[P] `$89:$01/v$00` vasu<br>[P] `$89:$06/v$00` vasu<br>[P] `$e5:$00/v$00` ringHelpBook<br>[P] `$e5:$01/v$00` ringHelpBook |
| `2:ef` | [D] `$66:$00/v$00` goron |
| `2:f3` | [D] `$3c:$07/v$00` boy |
| `2:f5` | [D] `$5a:$00/v$00` oldZora |
| `2:f6` | [D] `$66:$0f/v$00` goron |
| `2:f7` | [D] `$66:$07/v$00` goron |
| `2:fb` | [D] `$66:$0e/v$03` goron |
| `2:fd` | [D] `$66:$08/v$00` goron<br>[D] `$66:$10/v$00` goron<br>[D] `$66:$0c/v$03` goron<br>[P] `$68:$01/v$00` rosa<br>[P] `$68:$01/v$00` rosa |
| `2:ff` | [D] `$66:$08/v$00` goron<br>[D] `$66:$0c/v$04` goron<br>[D] `$66:$0c/v$05` goron<br>[D] `$4e:$02/v$00` subrosian<br>[D] `$4e:$02/v$01` subrosian<br>[D] `$66:$10/v$00` goron<br>[D] `$66:$0c/v$04` goron<br>[D] `$66:$0c/v$05` goron<br>[D] `$4e:$02/v$00` subrosian<br>[D] `$4e:$02/v$01` subrosian |
| `3:1f` | [D] `$66:$0a/v$00` goron |
| `3:2e` | [D] `$66:$0e/v$04` goron |
| `3:3e` | [D] `$66:$0b/v$00` goron |
| `3:5e` | [D] `$4e:$03/v$00` subrosian |
| `3:5f` | [D] `$66:$0e/v$06` goron |
| `3:6e` | [D] `$bf:$06/v$00` symmetryNpc |
| `3:6f` | [D] `$bf:$07/v$00` symmetryNpc |
| `3:7e` | [P] `$bf:$0a/v$00` symmetryNpc |
| `3:7f` | [P] `$bf:$0b/v$00` symmetryNpc |
| `3:8e` | [P] `$bf:$04/v$00` symmetryNpc |
| `3:8f` | [D] `$9d:$00/v$00` tokkey |
| `3:90` | [D] `$ba:$00/v$00` childJabu |
| `3:9e` | [I] `$36:$0b/v$00` nayru<br>[I] `$ad:$07/v$00` zelda<br>[I] `$4f:$00/v$00` impaNpc<br>[I] `$4f:$00/v$01` impaNpc<br>[I] `$4f:$00/v$02` impaNpc<br>[I] `$4f:$00/v$05` impaNpc<br>[I] `$4f:$00/v$09` impaNpc<br>[I] `$4f:$00/v$0a` impaNpc<br>[I] `$4f:$00/v$0b` impaNpc<br>[I] `$4f:$00/v$0d` impaNpc<br>[I] `$4f:$00/v$0e` impaNpc |
| `3:a0` | [D] `$ab:$17/v$00` zora |
| `3:b1` | [D] `$ab:$18/v$00` zora |
| `3:be` | [D] `$94:$00/v$00` patch |
| `3:c0` | [D] `$ab:$19/v$00` zora |
| `3:ce` | [D] `$3b:$08/v$00` femaleVillager |
| `3:cf` | [D] `$42:$01/v$00` mustacheMan |
| `3:d1` | [D] `$ab:$1a/v$00` zora |
| `3:d6` | [D] `$ab:$11/v$00` zora |
| `3:df` | [D] `$ab:$16/v$00` zora |
| `3:e3` | [D] `$ab:$15/v$00` zora |
| `3:e7` | [D] `$30:$01/v$00` shootingGallery<br>[D] `$8b:$02/v$00` goronElder |
| `3:e9` | [D] `$2e:$00/v$00` oldManWithRupees |
| `3:ea` | [P] `$bf:$00/v$00` symmetryNpc |
| `3:eb` | [P] `$bf:$02/v$00` symmetryNpc |
| `3:ec` | [P] `$bf:$02/v$00` symmetryNpc |
| `3:ed` | [D] `$5f:$80/v$00` syrup |
| `3:f7` | [D] `$e3:$08/v$00` knowItAllBird<br>[D] `$e3:$09/v$00` knowItAllBird<br>[D] `$e3:$06/v$00` knowItAllBird<br>[D] `$e3:$07/v$00` knowItAllBird<br>[D] `$e3:$04/v$00` knowItAllBird<br>[D] `$e3:$05/v$00` knowItAllBird<br>[D] `$e3:$02/v$00` knowItAllBird<br>[D] `$e3:$03/v$00` knowItAllBird<br>[D] `$e3:$00/v$00` knowItAllBird<br>[D] `$e3:$01/v$00` knowItAllBird |
| `3:f8` | [D] `$cc:$00/v$00` plen<br>[D] `$3d:$05/v$00` oldLady |
| `3:fa` | [D] `$29:$00/v$00` adlar |
| `3:fb` | [I] `$ca:$01/v$00` troy |
| `3:fc` | [I] `$28:$0a/v$00` bipin |
| `3:fe` | [D] `$46:$02/v$00` shopkeeper |
| `4:e0` | [I] `$3a:$02/v$00` villager |
| `4:e1` | [I] `$58:$00/v$00` hardhatWorker<br>[I] `$40:$0c/v$00` soldier<br>[I] `$57:$03/v$00` pickaxeWorker<br>[I] `$57:$03/v$01` pickaxeWorker |
| `4:e2` | [I] `$40:$0c/v$00` soldier<br>[I] `$58:$00/v$01` hardhatWorker<br>[I] `$58:$03/v$03` hardhatWorker<br>[I] `$57:$03/v$02` pickaxeWorker<br>[I] `$57:$03/v$03` pickaxeWorker |
| `4:e7` | [I] `$40:$0c/v$00` soldier<br>[I] `$58:$03/v$00` hardhatWorker<br>[I] `$58:$03/v$01` hardhatWorker<br>[I] `$57:$03/v$04` pickaxeWorker<br>[I] `$57:$03/v$05` pickaxeWorker |
| `4:e8` | [I] `$58:$03/v$02` hardhatWorker<br>[I] `$57:$03/v$06` pickaxeWorker<br>[I] `$57:$03/v$07` pickaxeWorker |
| `4:f3` | [D] `$58:$03/v$04` hardhatWorker |
| `4:f6` | [D] `$4d:$03/v$00` ambi |
| `4:fc` | [D] `$4d:$06/v$00` ambi |
| `4:fe` | [D] `$37:$0c/v$00` ralph |
| `5:ab` | [D] `$9c:$00/v$00` kingZora<br>[D] `$ab:$08/v$00` zora<br>[D] `$ab:$09/v$00` zora |
| `5:ac` | [D] `$ab:$05/v$00` zora<br>[D] `$ab:$06/v$00` zora<br>[D] `$ab:$07/v$00` zora |
| `5:ad` | [D] `$9c:$01/v$00` kingZora<br>[D] `$ab:$03/v$00` zora<br>[D] `$ab:$04/v$00` zora |
| `5:ae` | [D] `$ab:$00/v$00` zora<br>[D] `$ab:$01/v$00` zora<br>[D] `$ab:$02/v$00` zora |
| `5:b9` | [D] `$66:$0e/v$00` goron |
| `5:bb` | [D] `$66:$0d/v$01` goron<br>[D] `$66:$0e/v$02` goron |
| `5:bc` | [D] `$66:$0d/v$00` goron |
| `5:bd` | [D] `$66:$0e/v$01` goron |
| `5:c0` | [D] `$66:$0c/v$00` goron |
| `5:c2` | [D] `$66:$0d/v$02` goron |
| `5:c3` | [D] `$66:$06/v$00` goron<br>[D] `$66:$06/v$01` goron<br>[D] `$66:$05/v$02` goron<br>[D] `$66:$05/v$03` goron<br>[D] `$66:$05/v$04` goron<br>[D] `$66:$04/v$00` goron |
| `5:c4` | [D] `$66:$05/v$00` goron<br>[D] `$66:$05/v$01` goron |
| `5:c6` | [D] `$66:$0d/v$03` goron<br>[D] `$66:$0e/v$07` goron |
| `5:c8` | [D] `$52:$01/v$00` oldMan |
| `5:ca` | [D] `$48:$06/v$00` tokay |
| `5:cc` | [D] `$48:$09/v$00` tokay |
| `5:ce` | [D] `$6d:$00/v$00` possessedNayru<br>[D] `$40:$0d/v$0e` soldier |
| `5:cf` | [D] `$10:$00/v$00` farore |
| `5:d0` | [D] `$3b:$07/v$00` femaleVillager<br>[D] `$ab:$0e/v$00` zora<br>[D] `$3c:$0b/v$00` boy<br>[D] `$2a:$00/v$00` librarian<br>[D] `$9a:$09/v$00` carpenter |
| `5:d1` | [D] `$40:$0d/v$08` soldier<br>[D] `$40:$0d/v$09` soldier |
| `5:d2` | [D] `$40:$0d/v$0a` soldier<br>[D] `$40:$0d/v$0b` soldier |
| `5:d3` | [D] `$40:$0d/v$0c` soldier<br>[D] `$40:$0d/v$0d` soldier |
| `5:d4` | [D] `$40:$0d/v$07` soldier |
| `5:d5` | [D] `$40:$0d/v$0f` soldier |
| `5:d8` | [D] `$ca:$00/v$00` troy<br>[D] `$66:$09/v$00` goron<br>[D] `$66:$09/v$01` goron |
| `5:dc` | [D] `$66:$0e/v$05` goron<br>[D] `$66:$0c/v$06` goron |
| `5:dd` | [D] `$66:$0c/v$07` goron |
| `5:de` | [D] `$8b:$01/v$00` goronElder<br>[D] `$66:$05/v$05` goron |
| `5:df` | [D] `$66:$0e/v$09` goron<br>[D] `$66:$0e/v$0a` goron |
| `5:e0` | [D] `$66:$0e/v$08` goron<br>[D] `$66:$0d/v$04` goron |
| `5:e2` | [D] `$66:$0c/v$01` goron<br>[D] `$66:$0c/v$02` goron |
| `5:e4` | [D] `$52:$00/v$00` oldMan<br>[D] `$52:$02/v$00` oldMan |
| `5:e8` | [D] `$94:$01/v$00` patch<br>[D] `$94:$02/v$00` patch |
| `5:e9` | [D] `$48:$1d/v$00` tokay |
| `5:ec` | [D] `$52:$03/v$00` oldMan<br>[D] `$52:$04/v$00` oldMan<br>[D] `$52:$05/v$00` oldMan<br>[D] `$52:$06/v$00` oldMan |
| `5:f1` | [D] `$ad:$00/v$00` zelda |
| `5:f6` | [D] `$bf:$08/v$00` symmetryNpc<br>[D] `$bf:$09/v$00` symmetryNpc |
| `5:f8` | [D] `$c3:$00/v$00` pirateCaptain<br>[P] `$c4:$00/v$00` pirate<br>[P] `$c4:$01/v$00` pirate<br>[P] `$c4:$02/v$00` pirate<br>[P] `$c4:$03/v$00` pirate |

## Maintenance

When implementing or tracing a row:

1. Follow its placement, ID/subid dispatch, script, native handler, and state
   predicates in the disassembly.
2. Replace the exact deliberately-unsupported manifest key with an ordinary,
   specialized, or event-owned classification only after identifying its
   production owner.
3. Add a canonical headless regression for the supported branches, including
   negative predicates and re-entry behavior.
4. Change the row to **I**, **P**, or **D** based on the resulting production
   path; never promote it based only on a visual room check.
5. Recount the snapshot from the generated tables and keep the room entries in
   source order.

The durable implementation rules remain in
[NPCs and room events](npcs-and-events.md). This file is the navigable coverage
ledger; it should not accumulate architectural rules or replace
[Implementation status](implementation-status.md).
