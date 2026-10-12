using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuLongHookRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
            _inventory.GiveTreasure(TreasureId.Flippers, 0); _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, 0);
            LoadValidationRoom(5, 0x4e); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(40, 40)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetPackedStorageMetatile(0x12) != 0xf1 ||
                _rooms.CurrentDungeonIndex != 7, $"Long Hook chest must occupy the native $5:$4e/$12 alcove with a floor approach: solid={_collision.Collides(_player.Position)}, chest=${_currentRoom.GetPackedStorageMetatile(0x12):x2}, dungeon={_rooms.CurrentDungeonIndex}.");
            var seed = _random.CaptureState(); var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 40, 40);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 7;
            int update = 0;
            void Step(int count = 1, int angle = 0xff, bool press = false) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                press ? ["attack"] : [], press ? ["attack"] : [], batch, () => {
                    rom.UpdateGameplay(press ? 1 : 0, (press ? 1 : 0) | (angle == 0 ? 0x40 : 0), angle, _entities.FrameCounter);
                    press = false; rom.AdvanceTileGraphics(); update++;
                    CompareSomariaMotionRom(rom, $"Long Hook chest update{update}");
                    for (int a = 0xc672; a < 0xc6cc; a++)
                        FailIf(_saveData.ReadWramByte(a) != rom[a], $"Long Hook update{update}: inventory ${a:x4} differs.");
                    FailIf(_saveData.GetRoomFlags(5, 0x4e) != rom[0xca4e] ||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a],
                        $"Long Hook update{update}: native chest flag or reward input gate differs.");
                });
            Step(24, 0); Step(press: true);
            FailIf(!_interactions.ChestRewardActive || _currentRoom.GetPackedStorageMetatile(0x12) != 0xf0,
                $"The actual A-button update must open Long Hook from its south floor: Link={_player.PrecisePosition}, tile=${_currentRoom.GetPackedStorageMetatile(0x12):x2}, native push=${rom[0xcc65]:x2}, reward=${rom[0xd041]:x2}, state={rom[0xd004]}/{rom[0xd005]}, angle=${rom[0xd009]:x2}, mask=${rom[0xcc8a]:x2}, collisions=${rom[0xcc33]:x2}, flags=${rom[0xcc34]:x2}, disabled=${rom[0xcbca]:x2}.");
            Step(33);
            FailIf(!_dialogue.IsOpen || _inventory.SwitchHookLevel != 2 || rom[0xc6b6] != 2 ||
                !_dialogue.CurrentMessage.Contains("Long", StringComparison.Ordinal),
                "Source TREASURE_OBJECT_SWITCH_HOOK_01 must upgrade the existing Hook to level2 after its chest rise.");
            Step(4); _dialogue.Close(); rom[0xcba0] = 0; Step(); Step(press: true);
            FailIf(_interactions.ChestRewardActive || _inventory.SwitchHookLevel != 2,
                "Repeating the completed chest action must retain Long Hook without a second reward.");
            LoadValidationRoom(0, 0x60); LoadValidationRoom(5, 0x4e);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x12) != 0xf0 || _inventory.SwitchHookLevel != 2,
                "The Long Hook upgrade and opened chest must persist on room re-entry.");
        }
    }
}
