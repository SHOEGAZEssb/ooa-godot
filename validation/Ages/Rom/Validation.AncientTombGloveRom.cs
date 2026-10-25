using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombGloveRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Bracelet, 0);
            LoadValidationRoom(5, 0xa6);
            var active = SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var entity in active.Where(e => e is not TileFillerRoomEntity and not DungeonPuzzleChestRoomEntity).ToArray())
            { active.Remove(entity); free.Invoke(_entities, [entity]); }
            FailIf(_entities.Entities<TileFillerRoomEntity>().Count != 1 || _entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 1,
                "Source room$5:$a6 requires ordered INTERAC$25:$00 and $21:$11 before its enemy pointer.");
            // Begin this focused puzzle/item handoff with all but the final
            // original blue cell painted. The real filler performs the last
            // move; its later source slot creates the chest during that pass.
            for (int position = 0; position < _currentRoom.Layout.Length; position++)
                if (position != 0x16 && _currentRoom.Layout[position] == 0x9f)
                    _currentRoom.SetPositionTileAndCollision(new((position & 15) * 16 + 8, (position >> 4) * 16 + 8), 0x9d, null, 0);
            _inventory.SetScriptedEquippedItems(0, 0);
            _player.WarpTo(new(104, 40)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "D8 floor-fill fixture must start on source floor below its final blue cell.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 104, 40);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 8;
            foreach (var entity in active)
            {
                int slot = 0xd040 + _entities.InteractionSlot(entity.Node) * 256;
                rom[slot] = 1;
                rom[slot + 1] = (byte)(entity is TileFillerRoomEntity ? 0x25 : 0x21);
                rom[slot + 2] = (byte)(entity is TileFillerRoomEntity ? 0 : 0x11);
                rom[slot + 11] = (byte)entity.Node.Position.Y; rom[slot + 13] = (byte)entity.Node.Position.X;
            }
            int update = 0;
            void Step(int count = 1, int angle = 0xff, bool press = false) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                press ? ["attack"] : [], press ? ["attack"] : [], batch, () => {
                    rom.UpdateGameplay(press ? 1 : 0, (press ? 1 : 0) | (angle == 0 ? 0x40 : 0), angle, _entities.FrameCounter);
                    press = false; rom.AdvanceTileGraphics(); update++;
                    CompareSomariaMotionRom(rom, $"D8 Power Glove chest update{update}, chest={_interactions.ChestRewardActive}, native chest=${rom[0xd041]:x2}/${rom[0xd044]:x2}, masks=${_runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects):x2}/${rom[0xcc8a]:x2}");
                    for (int address = 0xc672; address < 0xc6cc; address++)
                        FailIf(_saveData.ReadWramByte(address) != rom[address],
                            $"D8 Power Glove chest update{update}: inventory byte${address:x4} differs.");
                    FailIf(_saveData.GetRoomFlags(5, 0xa6) != rom[0xcaa6] ||
                        _currentRoom.GetPackedStorageMetatile(0x37) != rom[0xcf37],
                        $"D8 Power Glove chest update{update}: room item flag or chest tile differs.");
                });
            Step(2);
            for (int walk = 0; _currentRoom.GetPackedStorageMetatile(0x37) != 0xf1 && walk < 64; walk++) Step(angle: 0);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x16) != 0x9e || _currentRoom.GetPackedStorageMetatile(0x37) != 0xf1,
                "Painting the last original blue cell must advance the yellow endpoint and spawn the Power Glove chest at$37.");
            // The chest consumer starts outside its solid tile after the
            // independently compared floor-fill completion boundary.
            _player.WarpTo(new(120, 72)); _player.Face(Vector2I.Up);
            rom.Word(0xd00a,72*256); rom.Word(0xd00c,120*256); rom[0xd008] = 0; rom[0xd009] = 0;
            for (int walk = 0; _player.Position.Y > 66 && walk < 64; walk++) Step(angle: 0);
            Step(12, angle: 0); // Publish both native chest wall probes before A.
            Step(angle: 0, press: true);
            for (int wait = 0; !_dialogue.IsOpen && wait < 80; wait++) Step();
            FailIf(!_dialogue.IsOpen || _inventory.BraceletLevel != 2 ||
                _dialogue.CurrentMessage != "You got the\nPower Glove!\nYou can now lift\nheavy objects." ||
                !_saveData.HasRoomFlag(5, 0xa6, OracleSaveData.RoomFlagItem),
                "Source TREASURE_OBJECT_BRACELET_02 must upgrade wBraceletLevel to2 and display TX_002f.");
            Step(4); _dialogue.Close(); rom[0xcba0] = 0; Step(3);
            LoadValidationRoom(5, 0xa6); StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x37) != 0xf0 || _inventory.BraceletLevel != 2,
                "The Power Glove chest and upgrade must persist on re-entry.");
        }
        GD.Print("Validated clean-US D8 Power Glove chest approach, upgrade grant, text, tile/item flag and re-entry through gameplay.");
    }
}
