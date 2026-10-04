using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidSwimmingRom() => ValidateMermaidSwimmingRom(false);
    private void ValidateMermaidDivingRom() => ValidateMermaidSwimmingRom(true);
    private void ValidateMermaidSeaSwimmingRom() => ValidateMermaidSwimmingRom(false, sea: true);
    private void ValidateMermaidSeaDivingGatesRom() => ValidateMermaidSwimmingRom(true, sea: true);

    private void ValidateMermaidSwimmingRom(bool diving, bool sea = false)
    {
        int hostCase1 = 0;
        foreach (int ring in diving ? new[] { 0xff, 0x3c } : new[] { 0xff, 0x15 })
        foreach (int direction in diving ? new[] { 8 } : Enumerable.Range(0, 8).Select(index => index * 4))
        foreach (int transitionLock in diving && sea ? new[] { 0x01, 0x80, 0xff } : new[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    $"Mermaid fixture could not equip ring ${ring:x2}.");
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool wall = x is 0 or 9 || y is 0 or 7;
                bool water = !wall && !(x == 1 && y == 4);
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    water ? sea ? (byte)0xfc : (byte)0xfa : (byte)0xa0, wall ? (byte)0x0f : water ? (byte)0x10 : (byte)0, 0);
            }
            _player.WarpTo(new(24.25f, 64.5f)); _player.Face(Vector2I.Right);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, 24, 64);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; rom.InitializeLinkGameplay();
            // TILEINDEX_DEEP_WATER=$fc: an unlocked dive dispatches levelDown
            // before timer/input, initiating a separate destination transition.
            // Compare the independently gated swim/dive path here.
            _runtimeState.SetWramByte(0xcc91, (byte)transitionLock);
            rom[0xcc91] = (byte)transitionLock;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousDirections = 0;
            int Field(string name) => (int)typeof(Player).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count = 1, int angle = 0xff, int pressed = 0)
            {
                Vector2 input = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = (input.X > 0 ? 0x10 : input.X < 0 ? 0x20 : 0) | (input.Y > 0 ? 0x80 : input.Y < 0 ? 0x40 : 0);
                int edge = pressed | (held & ~previousDirections); previousDirections = held;
                held |= pressed;
                StepGameplayUpdates(count, input, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter); edge = 0;
                    string context = $"Mermaid sea={sea} lock=${transitionLock:x2} ring=${ring:x2} direction=${direction:x2} update={++update}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        _player.TopDownSwimmingState != (rom[0xcc5d] & 0x0f) ||
                        _player.TopDownDiving != ((rom[0xcc5d] & 0x80) != 0) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        context + ": full fixed Link position/swim/facing differs.");
                    if (_player.TopDownSwimming)
                    {
                        FailIf(_player.TopDownSwimAngle != rom[0xd009] || _player.TopDownSwimSpeedRaw != rom[0xd010] ||
                            _player.TopDownSwimTargetSpeedRaw != rom[0xd011] ||
                            _player.TopDownSwimmingEntryCounter != rom[0xd006] ||
                            Field("_topDownDiveCounter") != rom[0xd007] ||
                            Field("_topDownSwimVelocityCounter") != rom[0xd012] ||
                            _player.TopDownSwimAnimationCounter != rom[0xd020],
                            context + $": angle/speed/target/entry/velocity/animation counters differ: runtime={_player.TopDownSwimAngle:x2}/{_player.TopDownSwimSpeedRaw}/{_player.TopDownSwimTargetSpeedRaw}/{_player.TopDownSwimmingEntryCounter}/{Field("_topDownSwimVelocityCounter")}/{_player.TopDownSwimAnimationCounter}, native={rom[0xd009]:x2}/{rom[0xd010]}/{rom[0xd011]}/{rom[0xd006]}/{rom[0xd012]}/{rom[0xd020]}.");
                        if (_player.TopDownSwimmingState == 3)
                        {
                            FailIf(Field("_topDownMermaidImpulseCounter") != rom[0xd03e] ||
                                _player.TopDownSwimTargetSpeedRaw != (ring == 0x15 ? 0x37 : 0x2d),
                                context + ": source-derived Mermaid impulse counter/target differs.");
                        }
                    }
                    FailIf(!sounds.Requests.Where(id => id is SoundId.SndSplash or SoundId.SndLinkSwim)
                        .SequenceEqual(rom.Sounds.Where(id => id is SoundId.SndSplash or SoundId.SndLinkSwim)),
                        context + ": splash/swim sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                    FailIf(_currentRoom.IsSolid(_player.Position), context + ": approach entered solid room geometry.");
                });
            }
            int began = update;
            while (!_player.TopDownSwimming && update - began < 60) Step(1, 8);
            FailIf(!_player.TopDownSwimming || _player.TopDownSwimmingEntryCounter != 2,
                "Mermaid water entry did not retain the source-derived two-update speed lock.");
            Step(60);
            began = update;
            while (_player.PrecisePosition.X < 80 && update - began < 180)
            {
                Step(); Step(6, 8);
            }
            FailIf(_player.PrecisePosition.X < 80, "Mermaid fixture could not swim from the walkable shore to its test area.");
            Step(60);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                if (diving)
                {
                    Step(1, pressed: 2);
                    FailIf(!_player.TopDownDiving || _player.TopDownDiveCounter != 0x78,
                        "Native Mermaid dive did not initialize counter2=$78.");
                    Step(8);
                    int retained = ring == 0x3c ? 0x78 : 0x70;
                    _dialogue.ShowMessage("Mermaid dive pause.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(6);
                    _dialogue.Close(); rom[0xcba0] = 0;
                    Step(1, pressed: 2);
                    FailIf(_player.TopDownDiving || _player.TopDownDiveCounter != retained,
                        "Manual Mermaid surfacing cleared the native retained raw counter2.");
                    Step(retained);
                    FailIf(_player.TopDownDiveCounter != (ring == 0x3c ? retained : 0),
                        "Surfaced Mermaid raw counter did not drain or obey ZORA_RING $3c.");
                    Step();
                    FailIf(_player.TopDownDiveCounter != (ring == 0x3c ? retained : 0xff),
                        "Surfaced Mermaid raw counter2 wrap differs.");
                    Step(1, pressed: 2);
                    Step(0x78);
                    FailIf(_player.TopDownDiving != (ring == 0x3c) || _player.TopDownDiveCounter != (ring == 0x3c ? 0x78 : 0),
                        "Native Mermaid automatic surfacing/ZORA_RING timeout differs.");
                    if (ring == 0x3c) Step(1, pressed: 2);
                    Step(4);
                    continue;
                }
                Step(1, direction);
                _dialogue.ShowMessage("Mermaid impulse pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, direction);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, direction); Step(18, direction); // Held direction outlives the impulse.
                // SPEED_160=$37 decelerates by $05 on a five-update
                // cadence, so sixty release updates cover even maximum speed.
                Step(60);
                FailIf(_player.TopDownSwimSpeedRaw != 0 || _player.TopDownSwimAngle != 0xff || Field("_topDownMermaidImpulseCounter") != 0xff,
                    "Mermaid held/released direction failed to coast to its source-derived stopped state.");
                Step(1, (direction + 8) & 0x1f); Step(4, (direction + 12) & 0x1f);
                Step(60); // Releasing then pressing permits a fresh impulse.
            }
        }
        GD.Print($"Validated clean-US Mermaid Suit sea={sea}, two-update entry, direction-edge impulses/holds/turns, diving={diving}, SWIMMERS/ZORA rings, full fixed XY/angle/speed/target/raw counters/animation, dialogue, sound/RNG and repeat through split/batched gameplay.");
    }
}
