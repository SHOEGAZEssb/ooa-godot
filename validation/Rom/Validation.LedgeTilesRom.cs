using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOutdoorLedgeTilesRom() => ValidateLedgeTilesRom(false, false);
    private void ValidateIndoorLedgeTilesRom() => ValidateLedgeTilesRom(false, true);
    private void ValidateUnderwaterLedgeTilesRom() => ValidateLedgeTilesRom(true, false);
    private void ValidateUnderwaterIndoorLedgeTilesRom() => ValidateLedgeTilesRom(true, true);

    private void ValidateLedgeTilesRom(bool underwater, bool indoors)
    {
        var cliffs = indoors ? new[] { (0xb0, 0x10), (0xb1, 0x18), (0xb2, 0), (0xb3, 8),
            (0xc1, 0x10), (0xc2, 0x18), (0xc3, 0), (0xc4, 8) } :
            new[] { (5, 0x10), (6, 0x10), (7, 0x10), (0x0a, 0x18), (0x0b, 8), (0x64, 0x10), (0xff, 0x10) };
        int hostCase1 = 0;
        foreach (var (tile, direction) in cliffs)
        foreach (int thickness in new[] { 1, 2 })
        foreach (int repeat in Enumerable.Range(0, 2))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            int group = indoors ? underwater ? 5 : 4 : underwater ? 2 : 0;
            int roomId = indoors ? underwater ? 0x4c : 0 : 0x90;
            if (underwater && indoors) _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, 0x22);
            LoadValidationRoom(group, roomId); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (underwater) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool cliff = direction switch
                {
                    0x10 => y >= 4 && y < 4 + thickness && x is > 0 and < 9,
                    0 => y <= 4 && y > 4 - thickness && x is > 0 and < 9,
                    8 => x >= 6 && x < 6 + thickness && y is > 0 and < 7,
                    _ => x <= 3 && x > 3 - thickness && y is > 0 and < 7
                };
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    cliff ? (byte)tile : (byte)0xa0,
                    cliff || x is 0 or 9 || y is 0 or 7 ? (byte)0x0f : (byte)0, 0);
            }
            Vector2 start = direction switch
            {
                0x10 => new(80.25f, 56.5f), 0 => new(80.25f, 83.5f),
                8 => new(92.25f, 56.5f), _ => new(68.25f, 56.5f)
            };
            _player.WarpTo(start); _player.Face(Vector2I.Right);
            FailIf(_currentRoom.IsSolid(_player.Position), "Cliff fixture must begin on reachable dry floor.");
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, (int)start.X, (int)start.Y);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousDirections = 0;
            int Field(string name) => (int)typeof(Player).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count = 1, int angle = 0xff)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int edge = held & ~previousDirections; previousDirections = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter - 1); edge = 0; update++;
                    string context = $"Ledge {group:x1}:{roomId:x2} tile=${tile:x2} angle=${direction:x2} thickness={thickness} repeat={repeat} update={update} batch={batched}";
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    int z = unchecked((short)rom.Word(0xd00e));
                    FailIf(_player.PrecisePosition != expected || Field("_ledgeZFixed") != z ||
                        _player.NativeNormalStateForInteraction != (rom[0xd004] == 1) ||
                        _player.NativeInAirForInteraction != (rom[0xcc5c] != 0) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        _transitions.IsTransitioning,
                        context + $": fixed XY/Z/state differs: runtime={_player.PrecisePosition}, native={expected}, Z={Field("_ledgeZFixed")}/{z}, state=${rom[0xd004]:x2}/${rom[0xd005]:x2}.");
                    if (rom[0xd004] == 0x12)
                        FailIf(_player.LedgeSpeedRaw != rom[0xd010] ||
                            _player.LedgeSpeedZ != unchecked((short)rom.Word(0xd014)),
                            context + ": planar/vertical ledge speed differs.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": jump/land sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            for (int approach = 0; approach < 64 && rom[0xd004] != 0x12; approach++) Step(1, direction);
            FailIf(rom[0xd004] != 0x12 || _player.LedgeJumpPhase != LedgeJumpState.Airborne,
                "Cliff approach must start native state $12 without a screen transition.");
            Step(10);
            _dialogue.ShowMessage("Cliff pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(6); _dialogue.Close(); rom[0xcba0] = 0;
            Step(30); Step(4, (direction + 8) & 0x1f); Step(8);
            FailIf(rom[0xd004] != 1 || _player.LedgeJumpPhase != LedgeJumpState.None,
                "Cliff landing must restore normal control and accept fresh movement.");
        }
        GD.Print($"Validated clean-US cliff tile/direction entry gates, two cliff widths, full fixed XY/Z, velocity/state, pause/landing, sound/RNG and repeated split/batched gameplay, underwater={underwater}, indoors={indoors}.");
    }
}
