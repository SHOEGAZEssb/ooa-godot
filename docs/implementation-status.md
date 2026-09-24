# Implementation status

The port is playable but incomplete. These are supported coverage boundaries,
not exhaustive clean-US ROM parity claims. Exact branches belong in code and
focused validations; NPC classifications live in the
[coverage ledger](npc-interaction-coverage.md).

## Foundations

- Room, tileset, graphics, collision, navigation, object, dialogue, menu, sprite,
  and sound data import.
- Original-resolution rendering, top-down and side-view rooms, scrolling,
  warps, time travel, persistent room state, deterministic RNG and 60 Hz updates.
- Boot/title, file select, three explicit-save slots with backup recovery,
  HUD, dialogue, inventory, maps, ring screens, save/quit, and game over.
- Imported music and SFX sequencing with original channel and RNG ownership.

## Gameplay coverage

- Core movement, sword combat, common terrain, hazards, treasures, drops,
  blocks, breakable objects, and a growing shared enemy roster.
- Substantial item and ring support, including seeds, bombs, shovel, Feather,
  Harp, Flippers, Switch Hook, and partial Cane of Somaria behavior.
- Spirit's Grave (`$01`) and Wing Dungeon (`$02`) are playable end to end.
- Moonlit Grotto (`$03`) has selected puzzles, both boss encounters, rewards,
  and its Essence/story handoff; full dungeon fidelity remains incomplete.
- Skull Dungeon (`$04`) has source-traced placed enemies and interactions in
  all 43 rooms, puzzles, bosses, rewards, and completion. A complete manual
  playthrough and exhaustive ROM comparison remain unperformed.
- Crown Dungeon (`$05`) has entrance, puzzle, item, boss, and reward paths.
  Complete combat, traversal, and dungeon parity remain unfinished.
- Selected overworld dialogue, shops/trades, Gasha and Seed Trees, Maple,
  early story sequences, King Moblin's keep, Goron quests/minigames, Symmetry
  Village restoration, and Patch's ceremony.
- Raft travel and wreck/theft events; partial Tokay Island progression and
  minigames; fairy fountains and Tokkey's song lesson.
- Ricky, Dimitri, and Moosh riding and forest quests, flute summoning, and
  the three Nuun Highlands layouts with carpenter/bridge progression.

## Major limitations

- Full story/world progression, remaining dungeons and bosses, enemy variants,
  NPC scripts, and room mechanisms.
- Remaining item upgrades and terrain-specific player states, including
  top-down Mermaid Suit movement, deep-water transitions, Roc's Cape, and
  incomplete seed and grabbable-object consumers.
- Remaining companion terrain states, thrown-NPC collisions, and exhaustive
  native initialization, slot reuse, and cross-object signal parity.
- Ring effects whose underlying gameplay systems are not yet supported.
- Partial linked-secret paths, linked-game transport, and external Game Link
  functionality.
- Graphics and sound behavior owned by unported gameplay objects.

Focused headless coverage does not establish complete minigame, dungeon, or
world playthrough parity. Unsupported imported behavior must fail with source
context or be explicitly and safely suppressed.
