using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateConveyorMovementRom()
    {
        int hostCase1 = 0;
        foreach (int ring in new[] { 0xff, 0x23 })
        foreach (int tile in new[] { 0x54, 0x55, 0x56, 0x57 })
        foreach (int direction in Enumerable.Range(0, 8).Select(index => index * 4))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x00); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    "Conveyor fixture could not equip QUICKSAND_RING $23.");
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    x is 0 or 9 || y is 0 or 7 ? (byte)0xa0 : (byte)tile,
                    x is 0 or 9 || y is 0 or 7 ? (byte)0x0f : (byte)0, 0);
            // Partial obstacles require tile-edge sliding without making the
            // basin escapable. Native probes retain their preceding publication
            // for the terrain step and refresh before input movement.
            for (int y = 2; y < 6; y++)
            foreach (int x in new[] { 3, 6 })
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0,
                    y % 2 == 0 ? (byte)0x05 : (byte)0x0a, 0);
            Vector2 start = new(80.25f, 64.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Right);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, 80, 64);
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
                    string context = $"Conveyor tile=${tile:x2} ring=${ring:x2} direction=${direction:x2}, update={update}, batch={batched}";
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(_player.PrecisePosition != expected ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        Field("_topDownMovementAngle") != rom[0xd009] ||
                        Field("_topDownMovementSpeedRaw") != rom[0xd010] ||
                        _player.TopDownSwimming || _player.TopDownAirborne ||
                        _player.IsFallingInHole || _transitions.IsTransitioning || _currentRoom.IsSolid(_player.Position),
                        context + $": fixed XY/angle/speed/facing/ground state differs: runtime={_player.PrecisePosition}, native={expected}, velocity={Field("_topDownMovementAngle"):x2}/{Field("_topDownMovementSpeedRaw"):x2} vs {rom[0xd009]:x2}/{rom[0xd010]:x2}.");
                    FailIf(Field("_tilePushWalls") != rom[0xd033],
                        context + ": retained wall publication differs across terrain/input movement phases.");
                    // Native Link observes cba0; this fixture does not execute
                    // the text renderer that emits the port's pause-message blips.
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": gameplay sound request order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            // Source SPEED_80 is exactly $0080 in the selected cardinal axis;
            // QUICKSAND_RING rejects the terrain handler before any movement.
            Step();
            Vector2 push = OracleObjectMath.StrictCardinalVector((tile - 0x54) * 8) * 0.5f;
            FailIf(_player.PrecisePosition != start + (ring == 0x23 ? Vector2.Zero : push),
                "A stationary conveyor update did not apply the independent native half-pixel step or ring gate.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(8);
                _dialogue.ShowMessage("Conveyor pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, direction); _dialogue.Close(); rom[0xcba0] = 0;
                Step(16, direction); Step(8);
                Step(16, (direction + 8) & 0x1f); Step(8);
                Step(16, (direction + 16) & 0x1f); Step(8);
            }
            for (int pulse = 0; pulse < 24; pulse++) { Step(); Step(5, direction); }
            Step(16); Step(8, (direction + 16) & 0x1f); Step(16);
        }
        GD.Print("Validated clean-US four-direction conveyors, eight input directions, Quicksand Ring, fixed XY/velocity, partial-wall sliding, pause, floor departure/re-entry, sounds/RNG and repeat in split/batched gameplay.");
    }
}
