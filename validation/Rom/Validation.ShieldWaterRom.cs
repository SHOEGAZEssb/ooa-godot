using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShieldWaterRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.Shield, 1);
            _inventory.EquipA(primary ? TreasureId.Shield : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Shield);
            // Bounded terrain fixture: source water metatile $fa and collision
            // $10 in a basin approached from a walkable neighboring metatile.
            // Neither runtime nor native Link starts inside the water.
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool water = x is >= 4 and <= 7 && y is >= 2 and <= 5;
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    water ? (byte)0xfa : (byte)0xa0, water ? (byte)0x10 : (byte)0, 0);
            }
            _player.WarpTo(new(56, 64));
            _player.Face(Vector2I.Right);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 56, 64);
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2;
            int update = 0;
            void Step(int count = 1, int held = 0, int pressed = 0, Vector2? movement = null, int angle = 0xff, int direction = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, movement ?? Vector2.Zero, MenuRomActions(held | direction), MenuRomActions(pressed), batched, () =>
                {
                    rom.UpdateGameplay(edge, held | direction, angle, _entities.FrameCounter);
                    edge = 0;
                    FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                        _player.TopDownSwimmingState != (rom[0xcc5d] & 0x0f) ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f),
                        $"Shield water A={primary} update={++update}: shield/swim/position runtime={_player.IsUsingShield}/{_player.TopDownSwimmingState}/{_player.PrecisePosition}, ROM=${rom[0xcc6f]:x2}/${rom[0xcc5d]:x2}/{rom.Word(0xd00c) / 256.0f},{rom.Word(0xd00a) / 256.0f}.");
                    FailIf(!sounds.Requests.Where(id => id == SoundId.SndShield).SequenceEqual(rom.Sounds.Where(id => id == SoundId.SndShield)),
                        "Water entry/exit restarted or retained the shield initialization cue incorrectly.");
                });
            }
            Step(1, button, button);
            Step(16, button, movement: Vector2.Right, angle: 8, direction: 0x10);
            Step(48, button);
            FailIf(!_player.TopDownSwimming || _player.IsUsingShield || rom[0xd500] != 0,
                "Swimming retained the shield parent or failed to enter the basin.");
            Step(1);
            Step(32, button, button, Vector2.Left, 24, 0x20);
            FailIf(_player.TopDownSwimming || !_player.IsUsingShield,
                "Leaving water with the shield held failed to create its new parent.");
            Step(1);
            Step(1, button, button);
            Step(16, button, movement: Vector2.Right, angle: 8, direction: 0x10);
            Step(32, button);
            Step(32, button, movement: Vector2.Left, angle: 24, direction: 0x20);
            Step(1);
        }
        GD.Print("Validated clean-US Shield water entry cancellation, held A/B swim gates, silent in-water release, ground exit reinitialization and repeated basin traversal in split/batched actual gameplay updates.");
    }
}
