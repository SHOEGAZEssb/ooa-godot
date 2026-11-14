# NPC and character interaction coverage

This is the current room-by-room coverage for clean-US Seasons character
interactions. It is a source implementation inventory, not a claim that every
listed room has been compared exhaustively against a retail playthrough.

This ledger is maintained alongside NPC implementation work. Any change that
implements, extends, partially supports, suppresses, or reclassifies a listed
NPC record must update the affected table entries and counts in the same
change. Keep diary entries, bugfix descriptions, and regression details out of
this ledger, including its tables.

The inventory covers:

- character rows reached from every distinct room-object table in
  [pointers.s](../../external/oracles-disasm/objects/seasons/pointers.s), including shared labels,
  pointer expansions and conditional event lists;
- the seven troupe actors and ten Subrosian dancers selected by placed
  ensemble spawners from `objectData7e4e` and `objectData7e6c` in
  [extraData3.s](../../external/oracles-disasm/objects/seasons/extraData3.s); and
- all conditional Bipin/Blossom family rows selected by the shared
  [family spawner](../../external/oracles-disasm/object_code/common/interactions/bipinBlossomFamilySpawner.s).

Character identities come from Seasons' own interaction dispatch and native
handlers. Characters whose positions are initialized by their handler remain
listed even when the placement has no coordinates. The opening campfire and
ring-help books retain the same character/scenery boundary as the existing
ledger. Invisible controllers, shop merchandise, dungeon mechanisms, effects
and other dynamically created actors are inventoried in the
[interaction, event and cutscene ledger](interaction-event-cutscene-coverage.md).
Enemies, items and parts are dependencies, not NPC records.

The complete source inventory is broader than the current generated assets.
`assets/oracle/seasons/opening/troupe.tsv` and the opening actor data support
the standard opening; the rest of these source records are not claimed imported.

## Status definitions

| Code | Meaning |
| --- | --- |
| **I** | Implemented: the record has a traced ordinary, specialized, or event-owned production path and a named headless regression for its supported behavior. |
| **P** | Partial: useful presentation or a bounded specialized path works, but one or more native, script, state, or external-system branches remain unavailable or unproven. |
| **D** | Deliberately unsupported: the placed variant is explicitly suppressed because its original native/script behavior is not implemented. |
| **M** | Missing: the source record has no Seasons production route for its placed context; it is not a deliberately suppressed imported actor. |

Record notation is `$id:$subid/v$var03`. The trailing name is the file containing
the corresponding `interactionCode` source handler in `oracles-disasm`.
Values are the original placement inputs, before native subid/var03 rewriting.
Rooms are sorted numerically; records within a room retain source order.
Groups `$02/$03` share table `$01`, and `$06/$07` share `$04/$05`; aliases are
counted once under the canonical table group. Conditional alternatives are
listed together and are not simultaneous actors. Explicit room/season gates
describe object-list selection; handlers can impose further story-state gates.

A record with a traced bounded production path can be partial. A source row
without a production owner remains missing even if shared Ages code or graphics
exist. Neither status can be promoted to **I** merely because an actor looks
plausible in one room. The implemented rows cover the standard opening only;
linked-game and later-progression branches remain outside that supported boundary.

## Coverage totals

### Positioned and state-derived rows

| Status | Records | Interpretation |
| --- | ---: | --- |
| **I** | 159 | Standard-opening, talk-only, cave-gift and Vasu ring-service records with traced source scripts, visibility gates and focused NPC scenarios. |
| **P** | 8 | Maku `$87:$00` supports the first story stage; Maku-room Impa `$9d:$03` and Zelda `$44:$08` support unlinked deletion; Impa `$9d:$04` linked activation, spring boy `$3e:$20` shared-flower Link launch, Mittens `$25:$00` trade-owner producer, Sokra `$27:$01` autumn portal dialogue and Blue Snake `$89:$01` successful cable services remain incomplete. |
| **D** | 0 | No listed source record is classified as a deliberately suppressed import. |
| **M** | 139 | Quest, trade, shop, secret, minigame and event records without a Seasons production route in their placed context. |
| **Total** | **306** | **150 rooms and 265 unique ID/subid keys.** |

### Implementation classifications

| Classification | Positioned/state-derived rows | Family variants | Total |
| --- | ---: | ---: | ---: |
| Ordinary NPC adapter | 31 | 0 | 31 |
| Specialized native interaction | 125 | 0 | 125 |
| Event-owned actor | 11 | 0 | 11 |
| Deliberately unsupported | 0 | 0 | 0 |
| No production owner | 139 | 72 | 211 |
| **Total** | **306** | **72** | **378** |

## Bipin and Blossom family variants

| Room | Coverage |
| --- | --- |
| `1:92` | **[M] 38 source variants:** Bipin `$28`, Blossom `$2b`, and child `$35`. |
| `1:93` | **[M] 34 source variants:** Bipin `$28`, Blossom `$2b`, and child `$35`. |

These are mutually selected alternatives, not 72 simultaneous actors.
The placed `$ac:$00/$01` spawners select by child stage and personality; the
finished-game gate suppresses the family. These source variants are not yet
generated Seasons NPC records.

## Room inventory

| Room | Source records in source order |
| --- | --- |
| `0:08` | [M] `$52:$00/v$00` biggoron |
| `0:0c` | [M] `$8c:$00/v$00` flyingRooster |
| `0:1f` | [M] `$8c:$01/v$00` flyingRooster |
| `0:2f` | [M] `$ce:$06/v$00` businessScrub |
| `0:4d` | [I] `$36:$81/v$00` miscNpcs (ordinary)<br>[I] `$36:$82/v$00` miscNpcs (ordinary)<br>[I] `$36:$84/v$00` miscNpcs (ordinary) |
| `0:5b` | [M] `$db:$00/v$00` linkedSecredGivers |
| `0:5d` | [I] `$36:$80/v$00` miscNpcs (ordinary)<br>[I] `$36:$83/v$00` miscNpcs (ordinary)<br>[I] `$8b:$01/v$00` sunkenCityNpcs<br>[I] `$8b:$02/v$00` sunkenCityNpcs<br>[I] `$8b:$03/v$00` sunkenCityNpcs<br>[I] `$8b:$04/v$00` sunkenCityNpcs<br>[I] `$2e:$00/v$00` fickleGirl<br>[I] `$2e:$01/v$00` fickleGirl<br>[I] `$2e:$04/v$00` fickleGirl |
| `0:5e` | [I] `$3e:$30/v$00` miscBoyNpcs<br>[I] `$3e:$31/v$00` miscBoyNpcs<br>[I] `$3e:$32/v$00` miscBoyNpcs<br>[I] `$3e:$33/v$00` miscBoyNpcs<br>[I] `$3e:$34/v$00` miscBoyNpcs<br>[I] `$8a:$01/v$00` sunkenCityNpcs<br>[I] `$b8:$01/v$00` ambi |
| `0:6d` | [I] `$2e:$02/v$00` fickleGirl<br>[I] `$2e:$03/v$00` fickleGirl<br>[M] `$76:$00/v$00` sunkenCityBullies<br>[M] `$76:$01/v$00` sunkenCityBullies<br>[M] `$76:$02/v$00` sunkenCityBullies |
| `0:6e` | [I] `$3e:$31/v$00` miscBoyNpcs |
| `0:71` | [I] `$27:$02/v$00` sokra |
| `0:80` | [M] `$ce:$00/v$00` businessScrub |
| `0:90` | [M] `$73:$00/v$00` animalMoblinBullies<br>[M] `$73:$01/v$00` animalMoblinBullies<br>[M] `$73:$02/v$00` animalMoblinBullies |
| `0:92` | [M] `$73:$00/v$00` animalMoblinBullies<br>[M] `$73:$01/v$00` animalMoblinBullies<br>[M] `$73:$02/v$00` animalMoblinBullies |
| `0:98` | [I] `$4e:$00/v$00` dinDancingEvent (room flag $40 clear)<br>[I] `$4e:$01/v$00` dinDancingEvent (room flag $40 clear)<br>[I] `$4e:$02/v$00` dinDancingEvent (room flag $40 clear)<br>[I] `$4e:$03/v$00` dinDancingEvent (room flag $40 clear)<br>[I] `$4e:$04/v$00` dinDancingEvent (room flag $40 clear)<br>[I] `$4e:$05/v$00` dinDancingEvent (room flag $40 clear)<br>[I] `$4e:$06/v$00` dinDancingEvent (room flag $40 clear) |
| `0:9b` | [P] `$27:$01/v$00` sokra (pre-autumn initialization) |
| `0:a7` | [I] `$a5:$09/v$00` din |
| `0:b6` | [I] `$9d:$00/v$00` impa<br>[P] `$9d:$04/v$00` impa |
| `0:b7` | [M] `$26:$00/v$00` mittensAndOwner<br>[I] `$26:$01/v$00` mittensAndOwner<br>[P] `$25:$00/v$00` mittensAndOwner<br>[I] `$25:$01/v$00` mittensAndOwner |
| `0:bf` | [M] `$4d:$00/v$00` pirateSkull |
| `0:c5` | [I] `$3c:$80/v$00` miscNpcs (ordinary)<br>[I] `$3e:$10/v$00` miscBoyNpcs |
| `0:c6` | [P] `$3e:$20/v$00` miscBoyNpcs<br>[I] `$2d:$80/v$00` miscNpcs (ordinary)<br>[I] `$2d:$86/v$00` miscNpcs (ordinary)<br>[I] `$37:$81/v$00` miscNpcs (ordinary)<br>[I] `$80:$02/v$00` fickleOldMan<br>[I] `$9d:$05/v$00` impa<br>[I] `$2a:$0b/v$00` bird |
| `0:d6` | [I] `$37:$80/v$00` miscNpcs (ordinary)<br>[I] `$2d:$81/v$00` miscNpcs (ordinary)<br>[I] `$2d:$83/v$00` miscNpcs (ordinary)<br>[I] `$2d:$85/v$00` miscNpcs (ordinary)<br>[I] `$3e:$04/v$00` miscBoyNpcs<br>[I] `$3e:$11/v$00` miscBoyNpcs<br>[I] `$a5:$08/v$00` din<br>[I] `$4e:$0a/v$00` dinDancingEvent |
| `0:d8` | [I] `$8a:$04/v$00` sunkenCityNpcs |
| `0:d9` | [I] `$2d:$84/v$00` miscNpcs (ordinary)<br>[I] `$37:$84/v$00` miscNpcs (ordinary) |
| `0:da` | [M] `$58:$00/v$00` guruguru<br>[M] `$bc:$04/v$00` bc_bd_be |
| `0:e2` | [I] `$b1:$10/v$00` shipPiratian |
| `0:e6` | [I] `$2d:$82/v$00` miscNpcs (ordinary) |
| `0:e7` | [I] `$3d:$80/v$00` miscNpcs (ordinary)<br>[M] `$d8:$00/v$00` linkedFountainLady |
| `0:e9` | [M] `$27:$00/v$00` sokra<br>[I] `$37:$83/v$00` miscNpcs (ordinary)<br>[I] `$80:$01/v$00` fickleOldMan<br>[I] `$3e:$00/v$00` miscBoyNpcs<br>[I] `$3e:$01/v$00` miscBoyNpcs<br>[I] `$3e:$03/v$00` miscBoyNpcs<br>[I] `$3e:$12/v$00` miscBoyNpcs<br>[I] `$3e:$13/v$00` miscBoyNpcs |
| `0:f2` | [I] `$b1:$11/v$00` shipPiratian |
| `0:f6` | [I] `$3e:$02/v$00` miscBoyNpcs |
| `0:f7` | [I] `$80:$00/v$00` fickleOldMan<br>[I] `$80:$03/v$00` fickleOldMan<br>[I] `$37:$85/v$00` miscNpcs (ordinary) |
| `0:f8` | [I] `$37:$82/v$00` miscNpcs (ordinary) |
| `0:fb` | [I] `$b8:$02/v$00` ambi |
| `0:fc` | [M] `$40:$0a/v$00` piratian |
| `1:03` | [I] `$30:$15/v$00` subrosian<br>[I] `$30:$16/v$00` subrosian |
| `1:05` | [I] `$30:$17/v$00` subrosian<br>[I] `$30:$18/v$00` subrosian |
| `1:06` | [I] `$33:$80/v$00` miscNpcs (ordinary)<br>[M] `$33:$81/v$00` miscNpcs<br>[M] `$33:$82/v$00` miscNpcs |
| `1:0a` | [M] `$53:$00/v$00` headSmelter<br>[M] `$30:$00/v$00` subrosian |
| `1:0b` | [P] `$87:$00/v$00` makuTree<br>[P] `$9d:$03/v$00` impa<br>[P] `$44:$08/v$00` zelda |
| `1:0c` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:11` | [I] `$32:$01/v$00` subrosianWithBuckets |
| `1:14` | [I] `$33:$83/v$00` miscNpcs (ordinary)<br>[M] `$33:$84/v$00` miscNpcs<br>[M] `$33:$85/v$00` miscNpcs |
| `1:15` | [I] `$30:$19/v$00` subrosian |
| `1:17` | [I] `$32:$02/v$00` subrosianWithBuckets |
| `1:1a` | [I] `$32:$00/v$00` subrosianWithBuckets |
| `1:20` | [I] `$30:$1a/v$00` subrosian |
| `1:22` | [I] `$30:$1b/v$00` subrosian |
| `1:25` | [I] `$30:$1c/v$00` subrosian<br>[I] `$c8:$00/v$00` boomerangSubrosian<br>[I] `$32:$03/v$00` subrosianWithBuckets |
| `1:26` | [I] `$30:$1d/v$00` subrosian |
| `1:2b` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:2c` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:2d` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:31` | [I] `$30:$1e/v$00` subrosian |
| `1:37` | [I] `$32:$04/v$00` subrosianWithBuckets |
| `1:41` | [I] `$30:$1f/v$00` subrosian |
| `1:42` | [I] `$30:$20/v$00` subrosian |
| `1:43` | [I] `$30:$0e/v$00` subrosian |
| `1:47` | [I] `$32:$05/v$00` subrosianWithBuckets |
| `1:52` | [I] `$30:$21/v$00` subrosian |
| `1:54` | [I] `$30:$0f/v$00` subrosian |
| `1:55` | [I] `$30:$22/v$00` subrosian |
| `1:56` | [I] `$30:$0a/v$00` subrosian<br>[I] `$30:$0b/v$00` subrosian |
| `1:5b` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:5c` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:5d` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:5e` | [M] `$87:$00/v$00` makuTree |
| `1:65` | [I] `$30:$07/v$00` subrosian |
| `1:66` | [I] `$30:$08/v$00` subrosian |
| `1:67` | [I] `$30:$24/v$00` subrosian |
| `1:69` | [M] `$30:$25/v$00` subrosian |
| `1:72` | [I] `$40:$09/v$00` piratian |
| `1:74` | [M] `$41:$0b/v$00` piratian<br>[M] `$40:$0d/v$00` piratian<br>[M] `$40:$0e/v$00` piratian |
| `1:75` | [I] `$30:$09/v$00` subrosian |
| `1:76` | [I] `$30:$06/v$00` subrosian |
| `1:77` | [M] `$31:$00/v$00` datingRosaEvent |
| `1:7b` | [M] `$87:$00/v$00` makuTree<br>[M] `$9d:$03/v$00` impa<br>[M] `$44:$08/v$00` zelda |
| `1:80` | [M] `$2f:$80/v$00` miscNpcs |
| `1:82` | [I] `$9d:$01/v$00` impa<br>[M] `$44:$09/v$00` zelda |
| `1:83` | [M] `$3f:$80/v$00` miscNpcs |
| `1:84` | [I] `$2a:$08/v$00` bird<br>[I] `$2a:$09/v$00` bird<br>[I] `$2a:$06/v$00` bird<br>[I] `$2a:$07/v$00` bird<br>[I] `$2a:$04/v$00` bird<br>[I] `$2a:$05/v$00` bird<br>[I] `$2a:$02/v$00` bird<br>[I] `$2a:$03/v$00` bird<br>[I] `$2a:$00/v$00` bird<br>[I] `$2a:$01/v$00` bird |
| `1:85` | [I] `$c2:$00/v$00` mayorsHouseUnlinkedGirl<br>[M] `$24:$03/v$00` miscNpcs |
| `1:86` | [M] `$24:$00/v$00` miscNpcs |
| `1:88` | [M] `$cb:$03/v$00` linkedGameGhini |
| `1:8a` | [M] `$41:$00/v$00` piratian<br>[M] `$40:$01/v$00` piratian<br>[M] `$40:$02/v$00` piratian<br>[M] `$40:$03/v$00` piratian<br>[M] `$40:$04/v$00` piratian<br>[M] `$40:$05/v$00` piratian<br>[M] `$40:$06/v$00` piratian<br>[I] `$42:$01/v$00` pirateHouseSubrosian<br>[I] `$b8:$04/v$00` ambi |
| `1:8b` | [M] `$40:$07/v$00` piratian<br>[M] `$40:$08/v$00` piratian<br>[I] `$42:$00/v$00` pirateHouseSubrosian |
| `1:8e` | [M] `$53:$01/v$00` headSmelter<br>[I] `$30:$04/v$00` subrosian<br>[I] `$30:$05/v$00` subrosian |
| `1:8f` | [M] `$5c:$00/v$00` lavaSoupSubrosian |
| `1:91` | [I] `$89:$00/v$00` vasu<br>[P] `$89:$01/v$00` vasu (no-cable timeout)<br>[I] `$89:$06/v$00` vasu<br>[I] `$e5:$00/v$00` ringHelpBook<br>[I] `$e5:$01/v$00` ringHelpBook |
| `1:94` | [M] `$8f:$00/v$00` oldManWithJewel |
| `1:95` | [M] `$6a:$01/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame<br>[M] `$6a:$02/v$00` danceHallMinigame |
| `1:96` | [I] `$30:$23/v$00` subrosian |
| `1:97` | [M] `$34:$00/v$00` subrosianSmiths<br>[M] `$34:$01/v$00` subrosianSmiths<br>[M] `$a4:$00/v$00` subrosianSmithy |
| `1:99` | [M] `$57:$00/v$00` ingo |
| `1:9a` | [M] `$99:$00/v$00` oldManWithRupees |
| `1:9c` | [M] `$43:$80/v$00` syrup<br>[M] `$49:$80/v$00` syrupCucco |
| `1:9d` | [I] `$8a:$00/v$00` sunkenCityNpcs<br>[I] `$8a:$02/v$00` sunkenCityNpcs<br>[I] `$8a:$03/v$00` sunkenCityNpcs |
| `1:9e` | [I] `$30:$02/v$00` subrosian<br>[I] `$30:$03/v$00` subrosian |
| `1:9f` | [I] `$39:$00/v$00` miscNpcs (ordinary)<br>[I] `$39:$01/v$00` miscNpcs (ordinary)<br>[I] `$39:$02/v$00` miscNpcs (ordinary)<br>[I] `$39:$03/v$00` miscNpcs (ordinary)<br>[I] `$39:$04/v$00` miscNpcs (ordinary) |
| `1:a0` | [I] `$30:$0c/v$00` subrosian |
| `1:a1` | [I] `$30:$0d/v$00` subrosian |
| `1:a3` | [M] `$70:$00/v$00` holly<br>[M] `$db:$02/v$00` linkedSecredGivers |
| `1:a4` | [M] `$2c:$00/v$00` miscNpcs |
| `1:a6` | [M] `$46:$00/v$00` shopkeeper |
| `1:a9` | [M] `$30:$26/v$00` subrosian |
| `1:aa` | [M] `$95:$00/v$00` kingMoblin |
| `1:ad` | [M] `$d5:$00/v$00` greatFairy |
| `1:af` | [M] `$46:$02/v$00` shopkeeper<br>[I] `$37:$86/v$00` miscNpcs (ordinary) |
| `1:b0` | [M] `$46:$01/v$00` shopkeeper |
| `1:b2` | [M] `$30:$12/v$00` subrosian<br>[M] `$30:$13/v$00` subrosian |
| `1:b3` | [M] `$29:$00/v$00` miscNpcs<br>[I] `$b8:$00/v$00` ambi |
| `1:b5` | [M] `$6b:$00/v$00` miscellaneous1<br>[M] `$db:$01/v$00` linkedSecredGivers |
| `1:b6` | [I] `$8b:$00/v$00` sunkenCityNpcs<br>[I] `$8b:$01/v$00` sunkenCityNpcs<br>[M] `$cd:$00/v$00` linkedMasterDiver |
| `1:ba` | [M] `$cb:$00/v$00` linkedGameGhini |
| `4:16` | [I] `$38:$00/v$00` miscNpcs (ordinary) |
| `4:33` | [M] `$ce:$09/v$00` businessScrub |
| `4:63` | [M] `$ce:$0a/v$00` businessScrub |
| `4:6b` | [I] `$38:$00/v$00` miscNpcs (ordinary) |
| `4:e8` | [M] `$10:$00/v$00` farore |
| `4:ef` | [M] `$55:$00/v$00` subrosianAtD8<br>[M] `$55:$01/v$00` subrosianAtD8 |
| `4:f0` | [M] `$cc:$00/v$00` goldenCaveSubrosian |
| `4:f8` | [M] `$5b:$80/v$00` lostWoodsDekuScrub |
| `5:01` | [M] `$99:$00/v$00` oldManWithRupees |
| `5:02` | [M] `$99:$01/v$00` oldManWithRupees |
| `5:03` | [I] `$99:$02/v$00` oldManWithRupees |
| `5:04` | [M] `$99:$03/v$00` oldManWithRupees |
| `5:05` | [M] `$99:$04/v$00` oldManWithRupees |
| `5:06` | [M] `$99:$05/v$00` oldManWithRupees |
| `5:07` | [M] `$99:$06/v$00` oldManWithRupees |
| `5:08` | [M] `$99:$07/v$00` oldManWithRupees |
| `5:0b` | [M] `$d6:$00/v$00` dekuScrub |
| `5:0c` | [M] `$d6:$02/v$00` dekuScrub |
| `5:0d` | [M] `$d6:$01/v$00` dekuScrub |
| `5:0f` | [M] `$ca:$00/v$00` troy |
| `5:11` | [M] `$dd:$00/v$00` goldenBeastOldMan |
| `5:27` | [M] `$e4:$00/v$00` linkedHerosCaveOldMan |
| `5:30` | [I] `$38:$00/v$00` miscNpcs (ordinary) |
| `5:33` | [M] `$e7:$00/v$00` loneZora |
| `5:9a` | [M] `$44:$00/v$00` zelda |
| `5:b3` | [M] `$d5:$01/v$00` greatFairy |
| `5:b6` | [M] `$45:$00/v$00` talon |
| `5:bd` | [M] `$8d:$00/v$00` sunkenCityNpcs<br>[I] `$8d:$01/v$00` sunkenCityNpcs<br>[I] `$8d:$02/v$00` sunkenCityNpcs<br>[I] `$8d:$03/v$00` sunkenCityNpcs |
| `5:c5` | [M] `$3b:$16/v$00` goron |
| `5:c8` | [I] `$3b:$14/v$00` goron<br>[I] `$3b:$15/v$00` goron<br>[M] `$3b:$07/v$00` goron |
| `5:c9` | [I] `$3b:$20/v$00` goron<br>[I] `$3b:$11/v$00` goron<br>[I] `$3b:$02/v$00` goron<br>[I] `$3b:$13/v$00` goron |
| `5:cf` | [M] `$b1:$18/v$00` shipPiratian |
| `5:d1` | [M] `$ce:$03/v$00` businessScrub |
| `5:d2` | [M] `$4d:$01/v$00` pirateSkull |
| `5:d4` | [I] `$b1:$12/v$00` shipPiratian<br>[I] `$b1:$13/v$00` shipPiratian<br>[I] `$b1:$14/v$00` shipPiratian<br>[I] `$b1:$15/v$00` shipPiratian<br>[I] `$b1:$16/v$00` shipPiratian |

Implementation guidance is in [NPCs and room events](../npcs-and-events.md).
