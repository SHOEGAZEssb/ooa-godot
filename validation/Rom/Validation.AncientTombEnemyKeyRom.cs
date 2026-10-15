using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombEnemyKeyRom()
    {
        ValidateAncientTombEnemyKeyScrollRom();
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0x98);
            _inventory.SetScriptedEquippedItems(0, 0);
            var key = _entities.Entities<EnemyAttachedKeyRoomEntity>().Single();
            var enemies = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities, "_enemySlots");
            FailIf(enemies.Count != 5 || enemies.Any(pair => pair.Key.Node is not StalfosCharacter),
                "Source room$5:$98 has five ordered ENEMY_STALFOS $31:$00 placements.");
            // Freeze initialized enemy movement through its actual mask. The
            // fixture declares slot-coordinate writes and deletion, while the
            // original Link, item, enemy initialization and key walks stay live.
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 4);
            _player.WarpTo(new(136, 136)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Attached-key fixture must begin on original room$5:$98 floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 136, 136) { HostileEnemiesEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 8; rom[0xcc8a] = 4;
            rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x31 * 4];
            foreach (var (entity, slot) in enemies)
            {
                int address = 0xd080 + slot * 256;
                rom[address] = (byte)(1 | ((slot == 0 ? 0 : slot) << 4)); rom[address + 1] = 0x31;
                rom.Word(address + 10, (int)entity.Node.Position.Y * 256);
                rom.Word(address + 12, (int)entity.Node.Position.X * 256);
            }
            int keySlot = 0xd040 + _entities.InteractionSlot(key) * 256;
            rom[keySlot] = 1; rom[keySlot + 1] = 0x77; rom[keySlot + 2] = 0x31;
            int update = 0;
            void Step(int count = 1, int angle = 0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle), batched: batch, afterUpdate: () => {
                    rom.UpdateGameplay(0, angle == 0 ? 0x40 : 0, angle, _entities.FrameCounter);
                    rom.AdvanceTileGraphics(); update++;
                    string context = $"D8 attached-key update{update}, batch={batch}";
                    CompareSomariaMotionRom(rom, context);
                    FailIf(key.Finished != (rom[keySlot] == 0 || rom[keySlot + 1] != 0x77) || !key.Finished &&
                        (key.State != rom[keySlot + 4] || key.Position != new Vector2(rom[keySlot + 13], rom[keySlot + 11]) ||
                        unchecked((ushort)key.ZFixed) != rom.Word(keySlot + 14) || key.SpeedZ != unchecked((short)rom.Word(keySlot + 20)) ||
                        key.Visible != ((rom[keySlot + 26] & 0x80) != 0)), $"{context}: linked slot, high-byte XYZ, flicker or falling motion differs: runtime {key.State}/{key.Position}/{key.ZFixed}/{key.SpeedZ}/{key.Visible}/{key.Finished}, ROM id${rom[keySlot+1]:x2} {rom[keySlot+4]}/{rom[keySlot+13]},{rom[keySlot+11]}/{rom.Word(keySlot+14)}/{unchecked((short)rom.Word(keySlot+20))}/{rom[keySlot+26]:x2}/{rom[keySlot]}.");
                    var treasures = _entities.Entities<GroundTreasurePickup>();
                    int[] native = Enumerable.Range(0xd2, 14).Select(page => page * 256 + 0x40)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x60).ToArray();
                    FailIf(treasures.Count != native.Length || _saveData.GetRoomFlags(5, 0x98) != rom[0xca98] ||
                        _inventory.GetDungeonSmallKeys(8) != rom[WramAddress.wDungeonSmallKeys + 8], $"{context}: small-key spawn, room item flag or inventory differs: runtime keys={_inventory.GetDungeonSmallKeys(8)}, treasures={treasures.Count}, flag=${_saveData.GetRoomFlags(5,0x98):x2}; ROM keys={rom[WramAddress.wDungeonSmallKeys+8]}, treasures={native.Length}, flag=${rom[0xca98]:x2}.");
                    if (treasures.Count != 0)
                    {
                        var treasure = treasures.Single(); int slot = native.Single();
                        FailIf(_entities.InteractionSlot(treasure) != (slot >> 8) - 0xd0 ||
                            treasure.Record.TreasureObject != "TREASURE_OBJECT_SMALL_KEY_00" ||
                            (int)treasure.State != rom[slot + 4] || treasure.SpawnSubstate != rom[slot + 5] ||
                            treasure.SpawnCounter != rom[slot + 6] || treasure.Visible != ((rom[slot + 26] & 0x80) != 0),
                            $"{context}: native $60/$30:$00 puff wait/physical slot differs.");
                    }
                });
            Step(3);
            var first = (StalfosCharacter)_entities.ResolveEnemySlot(0)!.Node;
            first.Position = new(136.5f, 104.25f);
            typeof(StalfosCharacter).GetField("_zFixed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(first, -8 * 256 + 0x55);
            rom.Word(0xd08a, 104 * 256 + 0x40); rom.Word(0xd08c, 136 * 256 + 0x80); rom.Word(0xd08e, -8 * 256 + 0x55);
            Step(4);
            FailIf(key.Position != new Vector2(136,104) || key.ZFixed != -8 * 256,
                "INTERAC$77 objectTakePosition must follow only the enemy's high XYZ bytes and flicker on odd updates.");
            var parent = _entities.ResolveEnemySlot(0)!;
            SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities").Remove(parent);
            typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_entities, [parent]);
            for (int offset = 0; offset < 64; offset++) rom[0xd080 + offset] = 0;
            // Reuse the related slot before its next interaction pass. The key
            // must follow its current occupant, rather than the deleted node.
            var replacement = _entities.ResolveEnemySlot(1)!;
            enemies.Remove(replacement);
            SomariaPrivate<HashSet<int>>(_entities, "_reservedEnemySlots").Remove(1);
            _entities.RegisterEnemySlot(replacement, 0);
            for (int offset = 0; offset < 64; offset++) { rom[0xd080 + offset] = rom[0xd180 + offset]; rom[0xd180 + offset] = 0; }
            Step(2);
            FailIf(key.State != 1 || key.Position != replacement.Node.Position.Floor(),
                "The key's retained native pointer must follow a reused enemy slot before deciding to fall.");
            replacement.Node.Position = new(136.5f,104.25f);
            typeof(StalfosCharacter).GetField("_zFixed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(replacement.Node, -8 * 256 + 0x55);
            rom.Word(0xd08a,104 * 256 + 0x40); rom.Word(0xd08c,136 * 256 + 0x80); rom.Word(0xd08e,-8 * 256 + 0x55);
            Step();
            SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities").Remove(replacement);
            typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_entities, [replacement]);
            for (int offset = 0; offset < 64; offset++) rom[0xd080 + offset] = 0;
            Step(50);
            FailIf(!key.Finished || _entities.RoomEnemyCount == 0 || _entities.Entities<GroundTreasurePickup>().Count != 1,
                "Deleting the linked slot must release exactly one small key while the other Stalfos remain alive.");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); rom[0xcc8a] = 0;
            Step(25, angle: 0); Step(3);
            FailIf(_inventory.GetDungeonSmallKeys(8) != 1 || !_saveData.HasRoomFlag(5, 0x98, OracleSaveData.RoomFlagItem),
                "Approaching the released key through original floor must grant one dungeon key and persist ROOMFLAG_ITEM.");
            LoadValidationRoom(5, 0x98); StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_entities.Entities<EnemyAttachedKeyRoomEntity>().Any(), "Collected enemy key must retire on re-entry.");
        }
        // The clean US carry bug is independent of the matching enemy in
        // slot1: a mismatch in slot0 immediately deletes the attachment.
        ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0x98); _entities.Clear();
        var record = new AncientTombDatabase().GetRoomRecords(5, 0x98).Single();
        var missed = new EnemyAttachedKeyRoomEntity(record, new DungeonInteractionVisualDatabase().Visual("enemy-attached-key"),
            () => false, () => new(EnemyId.Rope, new(136,72), 0), _ => throw new InvalidOperationException("A missed enemy must not create a key."));
        _entities.AddEntity(missed); _player.WarpTo(new(136,136)); _player.Face(Vector2I.Up);
        var missRom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0,136,136);
        missRom.InitializeLinkGameplay(); missRom.InitializeLinkWalkingAnimation(); missRom[0xcc39] = 8;
        int missSlot = 0xd040 + _entities.InteractionSlot(missed) * 256;
        missRom[missSlot] = 1; missRom[missSlot + 1] = 0x77; missRom[missSlot + 2] = 0x31;
        missRom[0xd080] = missRom[0xd180] = 1; missRom[0xd081] = 0x10; missRom[0xd181] = 0x31;
        StepGameplayUpdates(1, Vector2.Zero, afterUpdate: () => missRom.UpdateGameplay(0,0,0xff,_entities.FrameCounter));
        FailIf(!missed.Finished || missRom[missSlot] != 0,
            "Clean-US INTERAC$77 must preserve its first-enemy-slot mismatch bug despite a matching second Stalfos.");
        GD.Print("Validated clean-US D8 enemy-key slot attachment, XYZ/flicker, fall/puff, other-enemies-alive grant, re-entry and first-slot mismatch bug.");
    }

    private void ValidateAncientTombEnemyKeyScrollRom()
    {
        foreach (bool collected in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            if (collected) _saveData.SetRoomFlag(5, 0x98, OracleSaveData.RoomFlagItem);
            LoadValidationRoom(5, 0x97);
            _inventory.SetScriptedEquippedItems(0, 0);
            int y = Enumerable.Range(16, _currentRoom.Height - 32).First(y =>
                Enumerable.Range(_currentRoom.Width - 24, 24).All(x => !_collision.Collides(new(x, y))));
            _player.WarpTo(new(_currentRoom.Width - 24, y));
            void Enter(Vector2 direction, int target)
            {
                for (int update = 0; update < 40 && !_transitions.ScrollActive; update++)
                    StepGameplayUpdates(1, direction, batched: batch);
                FailIf(!_transitions.ScrollActive || _rooms.CurrentRoom.Id != target,
                    $"D8 key scroll must reach room$5:${target:x2} through original collision geometry: room${_currentRoom.Id:x2}, position={_player.Position}, active={_transitions.ScrollActive}.");
            }
            FailIf(_entities.Entities<EnemyAttachedKeyRoomEntity>().Any(),
                "Room$5:$97 must not contain the incoming INTERAC$77.");
            // Repeat after completion, then cancel through session teardown
            // during preload and enter again with the same item-flag branch.
            for (int entry = 0; entry < 3; entry++)
            {
                Enter(Vector2.Right, 0x98);
                var key = _entities.Entities<EnemyAttachedKeyRoomEntity>().SingleOrDefault();
                var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0,
                    (int)_player.Position.X, (int)_player.Position.Y);
                int slot = 0xd040 + (key is null ? 2 : _entities.InteractionSlot(key)) * 256;
                rom[slot] = 1; rom[slot + 1] = 0x77; rom[slot + 2] = 0x31;
                // Independent source placements: group5Map98EnemyObjectData
                // starts with ENEMY$31:$00 at Y$48/X$88 in slot $00.
                rom[0xd080] = 1; rom[0xd081] = 0x31;
                rom[0xd08b] = 0x48; rom[0xd08d] = 0x88;
                rom[0xcd00] = 8;
                rom.AdvanceInteractions(0);
                void Compare()
                {
                    FailIf((key is null) != (rom[slot] == 0) || key is not null &&
                        (key.State != rom[slot + 4] || key.Visible != ((rom[slot + 26] & 0x80) != 0) ||
                        key.Position != new Vector2(rom[slot + 13], rom[slot + 11]) ||
                        key.ZFixed != 0 || key.SpeedZ != 0 || key.Finished),
                        $"INTERAC$77 preload/freeze differs from clean US, collected={collected}, batch={batch}, entry={entry}.");
                    FailIf(_entities.Entities<GroundTreasurePickup>().Any() ||
                        _inventory.GetDungeonSmallKeys(8) != 0,
                        "Scrolling must not advance INTERAC$77 falling/key-grant states.");
                }
                Compare();
                if (entry == 1)
                {
                    ReinitializeGameplayForValidation();
                    if (collected) _saveData.SetRoomFlag(5, 0x98, OracleSaveData.RoomFlagItem);
                    LoadValidationRoom(5, 0x97);
                    _inventory.SetScriptedEquippedItems(0, 0);
                    _player.WarpTo(new(_currentRoom.Width - 24, y));
                    continue;
                }
                StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batch, afterUpdate: () =>
                {
                    rom.AdvanceInteractions(1);
                    Compare();
                });
                FailIf(_transitions.ScrollActive, "D8 attached-key scroll did not complete.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(key is not null && (key.State != 1 || key.Visible != ((_entities.FrameCounter & 1) != 0)),
                    "INTERAC$77 must resume attachment flicker on the first ordinary update after scrolling.");
                if (entry == 0)
                {
                    Enter(Vector2.Left, 0x97);
                    StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batch);
                }
            }
        }
    }
}
