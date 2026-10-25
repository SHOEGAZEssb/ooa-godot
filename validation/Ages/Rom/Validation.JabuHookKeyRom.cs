using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateJabuHookKeyRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(AgesWramAddress.wJabuWaterLevel, 0);
            _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            LoadValidationRoom(5, 0x55);
            var parent = _entities.Entities<DungeonPatternKeyRoomEntity>().Single();
            _inventory.EquipA(TreasureId.SwitchHook); _inventory.EquipB(0);
            _player.WarpTo(new(136, 56)); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(_player.Position) ||
                _currentRoom.GetPackedStorageMetatile(0x78) != 0xdb ||
                _currentRoom.GetPackedStorageMetatile(0x76) != 0xa0,
                "The $5:$55 Hook puzzle must start on real floor$38 above diamond$78.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, 136, 56);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 7;
            int address = 0xd040 + _entities.InteractionSlot(parent) * 256;
            rom[address] = 1; rom[address + 1] = 0x90; rom[address + 2] = 0x0c;
            rom[address + 11] = 0x58; rom[address + 13] = 0x88;
            int update = 0; bool spawned = false;
            void Step(int count = 1, int angle = 0xff, bool press = false)
            {
                int held = (press ? 1 : 0) | (angle switch { 0 => 0x40, 8 => 0x10, 16 => 0x80, _ => 0 });
                int edge = press ? 1 : 0;
                StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    MenuRomActions(held), MenuRomActions(edge), batch, () => {
                        rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter); edge = 0;
                        rom.AdvanceTileGraphics(); update++;
                        CompareSomariaMotionRom(rom, $"Jabu Hook key update{update}");
                        foreach (int tile in new[] { 0x36, 0x37, 0x38, 0x3a, 0x76, 0x78, 0x7a })
                            FailIf(_currentRoom.GetPackedStorageMetatile((byte)tile) != rom[0xcf00 + tile],
                                $"Jabu Hook key update{update}: exchanged tile${tile:x2} differs.");
                        var keys = _entities.Entities<GroundTreasurePickup>();
                        int[] native = Enumerable.Range(0xd0, 16).Select(page => page * 256 + 0x40)
                            .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x60).ToArray();
                        FailIf(parent.Finished != (rom[address] == 0) || keys.Count != native.Length ||
                            _inventory.GetDungeonSmallKeys(7) != rom[AgesWramAddress.wDungeonSmallKeys + 7] ||
                            _saveData.GetRoomFlags(5, 0x55) != rom[0xca55],
                            $"Jabu Hook key update{update}: native allocation, pickup, or room flag differs: parent={parent.Finished}/{rom[address]==0}, slots={keys.Count}/{native.Length}, keys={_inventory.GetDungeonSmallKeys(7)}/{rom[AgesWramAddress.wDungeonSmallKeys+7]}, flag=${_saveData.GetRoomFlags(5,0x55):x2}/${rom[0xca55]:x2}, Link={_player.PrecisePosition}.");
                        if (keys.Count != 0)
                        {
                            spawned = true; var key = keys.Single(); int slot = native.Single();
                            FailIf(key.Position != new Vector2(rom[slot + 13], rom[slot + 11]) || (int)key.State != rom[slot + 4] ||
                                (int)key.State == 1 && key.SpawnSubstate != rom[slot + 5] ||
                                unchecked((ushort)key.ZFixed) != rom.Word(slot + 14),
                                $"Jabu Hook key update{update}: source falling-key position/state/Z differs: XY={key.Position}/{rom[slot+13]},{rom[slot+11]}, state={key.State}/{rom[slot+4]}, Z={key.ZFixed}/{rom.Word(slot+14)}.");
                        }
                        FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                            $"Jabu Hook key update{update}: shared RNG differs.");
                    });
            }
            Step(); Step(press: true); Step(90);
            void Exchange(Vector2 start, int direction)
            {
                // Each bounded approach starts on actual floor, never inside
                // a diamond or the dividing wall. No puzzle tiles are edited.
                FailIf(_collision.Collides(start), "Jabu Hook approach must use the original floor geometry.");
                _player.WarpTo(start); _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
                rom.Word(0xd00a, (int)start.Y * 256); rom.Word(0xd00c, (int)start.X * 256);
                rom[0xd008] = (byte)direction; rom[0xd009] = (byte)(direction * 8);
                Step(); Step(press: true); Step(90);
            }
            Exchange(new(120, 56), 1); // $38 -> $37
            Exchange(new(104, 120), 0); // $36 -> $76
            Exchange(new(104, 56), 1); // $37 -> $36, completing the pattern
            FailIf(!spawned || !parent.Finished || _player.IsUsingSwitchHook ||
                _currentRoom.GetPackedStorageMetatile(0x76) != 0xdb ||
                _player.Position != new Vector2(120, 56),
                $"A real Hook exchange must complete diamonds$36/$3a/$76/$7a and allocate one falling key: spawned={spawned}, parent={parent.Finished}, Link={_player.PrecisePosition}, tile76=${_currentRoom.GetPackedStorageMetatile(0x76):x2}, hook={_entities.SwitchHook?.Item?.State}/{_entities.SwitchHook?.Item?.Substate}, tiles36/3a/7a=${rom[0xcf36]:x2}/${rom[0xcf3a]:x2}/${rom[0xcf7a]:x2}, tile77=${rom[0xcf77]:x2} collision=${rom[0xce77]:x2}.");
            Step(100); Step(16, 8);
            for (int walk = 0; _inventory.GetDungeonSmallKeys(7) == 0 && walk < 40; walk++) Step(angle: 16);
            FailIf(_inventory.GetDungeonSmallKeys(7) != 1,
                $"The source floor approach must collect the puzzle key: Link={_player.PrecisePosition}, keys={_entities.Entities<GroundTreasurePickup>().Count}, native key={rom[0xd140]:x2}:{rom[0xd144]:x2}, native dungeon={rom[0xcc39]}, tile58=${rom[0xcf58]:x2}.");
            Step(60);
            FailIf(_inventory.GetDungeonSmallKeys(7) != 1 || _entities.Entities<GroundTreasurePickup>().Count != 0,
                "A completed pattern must not allocate another key after collection.");
            LoadValidationRoom(0, 0x60); LoadValidationRoom(5, 0x55); StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_inventory.GetDungeonSmallKeys(7) != 1 || _entities.Entities<GroundTreasurePickup>().Count != 0,
                "The native item flag must suppress a second key on room re-entry.");
        }
    }
}
