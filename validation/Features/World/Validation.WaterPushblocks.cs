using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom141WaterPushblocks()
    {
        var data = new WaterPushblockDatabase();
        var placements = data.GetRoom(1, 0x41);
        FailIf(placements.Count != 2 ||
            placements[0] is not { Order: 1, SubId: 0, X: 0x58, Y: 0x68, TileBase: 0x1c, Palette: 5 } ||
            placements[1] is not { Order: 2, SubId: 1, X: 0x38, Y: 0x68 } ||
            placements.Any(p => p.Animation != "127@8,0,0,0;8,8,2,0"),
            "Room 1:41 lost its source $9e:$00/$01 order, positions, graphics or static OAM.");
        int[] affected = [0x140, 0x141, 0x142, 0x150, 0x151, 0x152, 0x040, 0x041, 0x042, 0x050, 0x051, 0x052];
        FailIf(!data.RoomFlags.Select(f => f.Group * 0x100 + f.Room).SequenceEqual(affected) ||
            data.RoomFlags.Any(f => f.Mask != 1),
            "$9e @swapRoomLayouts lost source order or final fallthrough XOR of room 0:52.");
        static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        void CheckFlags(bool swapped)
        {
            foreach (int key in affected)
                FailIf((_saveData.GetRoomFlags(key >> 8, key & 255) & 0x21) != (swapped ? 0x21 : 0x20),
                    $"$9e layout XOR in {key >> 8:x}:{key & 255:x2} did not preserve other bits; swapped={swapped}.");
            FailIf(!_saveData.HasRoomFlag(0, 0x53, 1), "$9e XOR escaped the source room list into 0:53.");
        }
        foreach (bool batched in new[] { false, true })
        {
            foreach (int key in affected)
            {
                _saveData.SetRoomFlag(key >> 8, key & 255, 1, false);
                _saveData.SetRoomFlag(key >> 8, key & 255, 0x20);
            }
            _saveData.SetRoomFlag(0, 0x53, 1);
            for (int subid = 0; subid < 2; subid++)
            {
                bool reverse = subid == 1;
                Vector2 push = reverse ? Vector2.Right : Vector2.Left;
                LoadValidationRoom(1, 0x41);
                _player.WarpTo(new Vector2(reverse ? 0x18 : 0x78, 0x68));
                FailIf(_currentRoom.IsSolid(_player.Position), "Room 1:41 approach began in solid geometry.");
                void Step(int count, Vector2 input) => StepGameplayUpdates(count, input, batched: batched);
                Step(1, Vector2.Zero);
                var block = _entities.Entities<WaterPushblockRoomEntity>().Single();
                FailIf(block.Record.SubId != subid || block.State != 1 || block.Counter != 30 ||
                    block.Position != new Vector2(reverse ? 0x38 : 0x58, 0x68) || !block.Visible ||
                    block.CurrentAnimationOpaquePixels == 0 || block.ZIndex != NpcCharacter.BehindLinkZIndex ||
                    !_entities.TimeWarpPositionOccupied(new TimeWarpLandingDatabase(), reverse ? 0x63 : 0x65),
                    $"Room 1:41 $9e:${subid:x2} initialization, solid reservation or presentation failed.");
                FailIf(_entities.Entities<TimePortal>().Single().Position != new Vector2(0x78, 0x28),
                    "Room 1:41 lost the preceding $e1:$00 portal.");
                // Walk through the actual dry channel until the first native
                // collision. Never arrange Link inside the block's hitbox.
                for (int i = 0; i < 45 && block.Counter == 30; i++) Step(1, push);
                FailIf(block.Counter != 29 || block.State != 1 ||
                    _player.Position != block.Position - push * 12,
                    $"Room 1:41 $9e:${subid:x2} unreachable through collision: Link={_player.Position}, block={block.Position}, counter={block.Counter}.");
                Step(9, push);
                FailIf(block.Counter != 20, "$9e did not decrement once per centered push update.");
                Step(1, Vector2.Zero);
                FailIf(block.Counter != 30 || _player.IsPushing, "$9e interrupted push did not reset 30 and clear the transient push pose.");
                Step(29, push);
                FailIf(block.State != 1 || block.Counter != 1 || _entities.PlayerMenusDisabled,
                    "$9e started before the 30th consecutive centered push.");
                var drop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(ItemDropDatabase.OneRupee, new Vector2(0x78, 0x38)));
                var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new Vector2(0x78, 0x38), 0));
                var bomb = _entities.Spawn<BombEffect>(new BombSpawn(_player, new BombDatabase().Data, 1, _ => { }));
                _sound.ClearPlayRequestAudit();
                int randomBefore = _entities.RandomCalls;
                Vector2 initialBlock = block.Position;
                Vector2 initialLink = _player.Position;
                Step(1, push);
                FailIf(block.State != 2 || block.Counter != 0x40 || block.Position != initialBlock ||
                    !_entities.PlayerUpdatesFrozen || !_entities.PlayerMenusDisabled || !_entities.ScreenTransitionsDisabled ||
                    _sound.PlayRequestsFor(0xf0) != 1 || _sound.PlayRequestsFor(0x71) != 1,
                    "$9e push completion must lock $81/menu, stop music, sound $71, and defer movement to next update.");
                CheckFlags(reverse);
                int dropElapsed = drop.ElapsedFrames;
                int bombElapsed = bomb.ElapsedFrames;
                int puffElapsed = puff.ElapsedUpdates;
                _player.ApplyInteractionInvincibility(12);
                for (int movement = 1; movement <= 64; movement++)
                {
                    Step(1, -push);
                    // objectApplySpeed moves 8.8 values; xh truncates negative
                    // half-pixels downward. Link stays where pushing ended.
                    float expectedX = Mathf.Floor(initialBlock.X + push.X * movement / 2);
                    FailIf(block.Position.X != expectedX || _player.Position != initialLink || _player.IsPushing,
                        $"$9e:${subid:x2} movement {movement}: expected X={expectedX}, got {block.Position.X}; Link={_player.Position}.");
                    FailIf(drop.ElapsedFrames != dropElapsed || bomb.ElapsedFrames != bombElapsed,
                        "$9e $81 mask advanced a PART_ITEM_DROP or ITEM_BOMB during movement.");
                    if (movement == 4)
                        FailIf(puff.ElapsedUpdates != puffElapsed + 4, "$9e $81 mask froze an ordinary $05 interaction.");
                    FailIf(_player.InvincibilityFrames != -Math.Max(0, 12 - movement),
                        "$9e incorrectly froze the shared post-Link invincibility update.");
                }
                FailIf(block.State != 3 || block.Substate != 0 || block.Counter != 70,
                    "$9e must enter flood state with 70 updates after exactly 64 half-pixel movements.");
                byte[] initialRow = Enumerable.Range(0, 10).Select(x => (byte)_currentRoom.GetMetatile(Point(0x60 + x))).ToArray();
                byte[] mappings = Godot.FileAccess.GetFileAsBytes(
                    $"res://assets/oracle/layouts/tilesetMappings{_currentRoom.TilesetId:x2}.bin");
                void CheckInterleave(int position, byte tile1, byte priorCollision)
                {
                    // setInterleavedTile types $03/$01 use the opposite
                    // half of $f9 and preserve the prior collision byte.
                    int[] quarters = reverse ? [0, 0, 2, 2] : [1, 1, 3, 3];
                    for (int quarter = 0; quarter < 4; quarter++)
                    {
                        bool fromPuddle = (quarter % 2 == 1) == reverse;
                        int offset = fromPuddle ? 0xf9 * 8 + quarters[quarter] : tile1 * 8 + quarter;
                        int x = (position & 15) * 2 + quarter % 2;
                        int y = (position >> 4) * 2 + quarter / 2;
                        FailIf(_currentRoom.GetBackgroundSubtileForValidation(x, y) != mappings[offset] ||
                            _currentRoom.GetBackgroundAttributeForValidation(x, y) != mappings[offset + 4],
                            $"$9e:${subid:x2} interleaved ${position:x2} quarter {quarter} differs from source type ${(reverse ? 1 : 3):x2}.");
                    }
                    FailIf(_currentRoom.GetTerrainInfo(Point(position)).Collision != priorCollision,
                        $"$9e interleaved ${position:x2} changed collisions before the ordinary tile write.");
                }
                Step(69, Vector2.Zero);
                FailIf(block.Substate != 0 || block.Counter != 1 || _sound.PlayRequestsFor(0xc2) != 0,
                    "$9e floodgates started before the 70th wait update.");
                Step(1, Vector2.Zero);
                FailIf(block.Substate != 1 || block.Counter != 8 || _sound.PlayRequestsFor(0xc2) != 1 ||
                    _currentRoom.GetMetatile(Point(0x63)) != 0x1b || _currentRoom.GetMetatile(Point(0x65)) != 0x1b,
                    "$9e first flood phase lost the paired $63/$65 interleaved tiles or $c2 sound.");
                CheckInterleave(0x63, 0x1b, _currentRoom.Collisions[initialRow[3]]);
                CheckInterleave(0x65, 0x1b, _currentRoom.Collisions[initialRow[5]]);
                for (int phase = 1; phase <= 9; phase++)
                {
                    Step(7, Vector2.Zero);
                    FailIf(block.Substate != phase || block.Counter != 1, $"$9e flood phase {phase} shortened its eight-update wait.");
                    Step(1, Vector2.Zero);
                    FailIf(block.Substate != phase + 1, $"$9e flood phase {phase} missed its zero update.");
                    if (phase is 2 or 4 or 6 or 8)
                    {
                        int[] positions = phase switch { 2 => [0x62, 0x66], 4 => [0x61, 0x67], 6 => [0x60, 0x68], _ => [0x69] };
                        foreach (int position in positions)
                            CheckInterleave(position, position is 0x60 or 0x69 ? (byte)0xf3 : (byte)0x1b,
                                _currentRoom.Collisions[initialRow[position - 0x60]]);
                    }
                }
                byte[] expectedRow = reverse
                    ? [0xfa, 0xf9, 0xf9, 0xf9, initialRow[4], 0x1b, 0x1b, 0x1b, 0x1b, 0xf3]
                    : [0xf3, 0x1b, 0x1b, 0x1b, initialRow[4], 0xf9, 0xf9, 0xf9, 0xf9, 0xfa];
                byte[] actualRow = Enumerable.Range(0, 10).Select(x => (byte)_currentRoom.GetMetatile(Point(0x60 + x))).ToArray();
                FailIf(!actualRow.SequenceEqual(expectedRow),
                    $"$9e:${subid:x2} final channel differs from source: {Convert.ToHexString(actualRow)}.");
                Step(89, Vector2.Zero);
                FailIf(block.Substate != 10 || _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0,
                    "$9e puzzle jingle started before 90 updates.");
                Step(1, Vector2.Zero);
                FailIf(block.Substate != 11 || block.Counter != 72 || _sound.PlayRequestsFor(0xf1) != 1 ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
                    "$9e must stop flood sound and start the puzzle jingle on update 90.");
                Step(71, Vector2.Zero);
                CheckFlags(reverse);
                FailIf(!_entities.PlayerMenusDisabled || block.State != 3, "$9e released its final 72-update wait early.");
                Step(1, Vector2.Zero);
                CheckFlags(!reverse);
                FailIf(block.State != 4 || _entities.PlayerUpdatesFrozen || _entities.PlayerMenusDisabled ||
                    _sound.ActiveMusic == 0 || _entities.RandomCalls != randomBefore,
                    $"$9e completion failed: state={block.State}, frozen={_entities.PlayerUpdatesFrozen}, menu={_entities.PlayerMenusDisabled}, music=${_sound.ActiveMusic:x2}, RNG={_entities.RandomCalls}/{randomBefore}.");
                FailIf(drop.ElapsedFrames != dropElapsed || bomb.ElapsedFrames != bombElapsed,
                    "$9e prematurely resumed frozen parts/items on its interaction completion update.");
                Step(2, Vector2.Zero);
                FailIf(drop.ElapsedFrames != dropElapsed + 2 || bomb.ElapsedFrames != bombElapsed + 2,
                    "$9e did not resume existing parts/items on the following gameplay updates.");
                _entities.ClearPhysicalPlayerItems();
                // Approach again across the now traversable channel. State 4
                // remains a solid object until the room is parsed again.
                for (int i = 0; i < 100; i++) Step(1, push);
                FailIf(block.State != 4 || Mathf.Abs(_player.Position.X - block.Position.X) != 12,
                    "$9e state 4 moved again or stopped being solid on a repeated approach.");
                CheckFlags(!reverse);
                FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out var restored) ||
                    affected.Any(key => restored!.HasRoomFlag(key >> 8, key & 255, 1) != !reverse),
                    "$9e layout flags did not survive explicit save serialization and reload.");
                foreach (int key in affected)
                    FailIf(_rooms.GetRoom(key >> 8, key & 255).TilesetId !=
                        _rooms.World.LoadRoom(key >> 8, key & 255, (key >> 8) + (!reverse ? 2 : 0)).TilesetId,
                        $"$9e layout swap was not applied on loading {key >> 8:x}:{key & 255:x2}.");
            }
            ValidateWaterPushblockInterruption(batched);
        }
        GD.Print("Validated room 1:41 $9e water pushblocks: reachable pushes, both flood directions, " +
            "exact counters, channel tiles, input/music ownership, twelve XOR flags, re-entry and batched gameplay.");
    }

    private void ValidateWaterPushblockInterruption(bool batched)
    {
        void Step(int count, Vector2 input, string[]? held = null, string[]? pressed = null) =>
            StepGameplayUpdates(count, input, held, pressed, batched);
        LoadValidationRoom(1, 0x41);
        _player.WarpTo(new Vector2(0x38, 0x68)); // Real shallow-water approach from the wrong side.
        Step(1, Vector2.Zero);
        var block = _entities.Entities<WaterPushblockRoomEntity>().Single();
        for (int i = 0; i < 80 && block.Counter == 30; i++) Step(1, Vector2.Right);
        FailIf(block.Counter != 29, "$9e wrong-side approach did not reach the original contact gate.");
        Step(29, Vector2.Right);
        FailIf(block.State != 1 || block.Counter != 0, $"$9e wrong-side zero boundary: state={block.State}, counter={block.Counter}, Link={_player.PrecisePosition}, block={block.Position}.");
        Step(1, Vector2.Right);
        FailIf(block.Counter != 0xff || block.State != 1, "$9e wrong-side counter did not wrap $00->$ff.");
        Step(1, Vector2.Zero);
        FailIf(block.Counter != 30, "$9e wrong-side release did not restore its 30-update counter.");

        // Re-enter on the actual dry side, five pixels above the centerline.
        LoadValidationRoom(1, 0x41);
        _player.WarpTo(new Vector2(0x78, 0x63));
        Step(80, Vector2.Left);
        block = _entities.Entities<WaterPushblockRoomEntity>().Single();
        FailIf(block.Counter != 30 || block.State != 1 || _player.Position.Y != 0x63,
            $"$9e off-center approach: state={block.State}, counter={block.Counter}, Link={_player.PrecisePosition}, block={block.Position}.");
        Step(1, Vector2.Down); // Walk one pixel to the inclusive four-pixel boundary.
        Step(1, Vector2.Left);
        FailIf(block.Counter != 29, "$9e rejected the inclusive four-pixel centering boundary.");
        Step(1, Vector2.Left, ["attack"]);
        FailIf(block.Counter != 30, "$9e counted a push with A held.");
        Step(1, Vector2.Left, ["item"]);
        FailIf(block.Counter != 30, "$9e counted a push with B held.");

        // Text and the harp freeze ordinary interaction counters. The $e1
        // portal still receives the harp signal and activates after completion.
        LoadValidationRoom(1, 0x41);
        _player.WarpTo(new Vector2(0x78, 0x68));
        Step(1, Vector2.Zero);
        block = _entities.Entities<WaterPushblockRoomEntity>().Single();
        for (int i = 0; i < 40 && block.Counter == 30; i++) Step(1, Vector2.Left);
        int counter = block.Counter;
        _dialogue.ShowMessage("Water control validation.", 0x68);
        Step(10, Vector2.Left);
        FailIf(block.Counter != counter, "$9e counter advanced while text disabled ordinary interactions.");
        _dialogue.Close();
        EnsureHarpAndSongs();
        _inventory.SelectHarpSong(1);
        _inventory.EquipB(InventoryState.ItemHarp);
        Step(1, Vector2.Zero, ["item"], ["item"]);
        FailIf(!_harp.IsPlaying, "Room 1:41 Echoes did not start through the gameplay item route.");
        Step(60, Vector2.Zero);
        FailIf(block.Counter != counter || block.State != 1, "$9e advanced while ITEM_HARP disabled ordinary interactions.");
        for (int i = 0; i < 300 && _harp.IsPlaying; i++) Step(1, Vector2.Zero);
        Step(2, Vector2.Zero);
        FailIf(!_entities.Entities<TimePortal>().Single().Active || block.Counter != 30,
            "Room 1:41 portal did not activate after Echoes, or $9e retained an interrupted push.");
        _inventory.EquipB(InventoryState.ItemSword);
        Step(30, Vector2.Left);
        FailIf(block.State != 2, "$9e failed to push again after harp completion.");
        Step(150, Vector2.Zero);
        FailIf(block.State != 3 || !_entities.PlayerUpdatesFrozen, "$9e cancellation fixture did not reach active flooding.");
        LoadValidationRoom(1, 0x41);
        Step(1, Vector2.Zero);
        FailIf(_entities.PlayerUpdatesFrozen || _entities.PlayerMenusDisabled || _saveData.HasRoomFlag(1, 0x41, 1) ||
            _entities.Entities<WaterPushblockRoomEntity>().Single().Position != new Vector2(0x58, 0x68),
            "$9e cancellation committed the layout swap or retained movement/menu ownership.");

        _entities.BeginScreenTransition(1, _currentRoom, new Vector2(160, 0));
        block = _entities.Entities<WaterPushblockRoomEntity>().Single(b => !b.Finished);
        FailIf(!block.Visible || block.State != 1 || block.Counter != 30,
            "$9e scroll preload failed to resolve state 0 and its flag-selected graphics.");
        _entities.Update(30.0 / 60.0, _player);
        FailIf(block.State != 1 || block.Counter != 30 || block.Position != new Vector2(0x58, 0x68),
            "$9e advanced ordinary behavior during destination scrolling.");
        _entities.FinishScreenTransition();
        LoadValidationRoom(1, 0x41);
    }
}
