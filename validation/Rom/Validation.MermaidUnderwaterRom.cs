using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidUnderwaterRom() => ValidateMermaidUnderwaterRom(false);
    private void ValidateUnderwaterHoleRom() => ValidateMermaidUnderwaterRom(true);
    private void ValidateUnderwaterWaterRom() => ValidateMermaidUnderwaterRom(false, water: true);
    private void ValidateUnderwaterCliffCoastRom() => ValidateMermaidUnderwaterRom(false, cliffCoast: true);
    private void ValidateUnderwaterCurrentRom() => ValidateMermaidUnderwaterRom(false, currents: true);
    private void ValidateUnderwaterFloorRom() => ValidateMermaidUnderwaterRom(false, floors: true);
    private void ValidateUnderwaterConveyorRom() => ValidateMermaidUnderwaterRom(false, conveyors: true);
    private void ValidateUnderwaterCurrentEdgesRom() => ValidateMermaidUnderwaterRom(false, currents: true, edges: true);

    private void ValidateMermaidUnderwaterRom(bool holes, bool water = false, bool cliffCoast = false,
        bool currents = false, bool floors = false, bool conveyors = false, bool edges = false)
    {
        int hostCase1 = 0;
        foreach (int group in cliffCoast ? new[] { 2 } : holes || conveyors ? new[] { 5 } : new[] { 2, 3 })
        foreach (int ring in holes || water || cliffCoast || floors ? new[] { 0xff } :
            currents || conveyors ? new[] { 0xff, 0x23 } : new[] { 0xff, 0x15 })
        foreach (int tile in holes ? new[] { 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0x48, 0x49, 0x4a, 0x4b } :
            cliffCoast ? new[] { 0xff } : conveyors ? new[] { 0x54, 0x55, 0x56, 0x57 } :
            currents ? new[] { 0xe0, 0xe3, 0xe1, 0xe2 } :
            floors ? new[] { 0xea, 0xf8, 0xd0, 0xf9 } : water ? new[] { 0xfa, 0xfc, 0xfe, 0xff, 0xe9 } : new[] { 0xa0 })
        foreach (int direction in cliffCoast ? new[] { 0x10 } :
            Enumerable.Range(0, holes || water || currents || floors || conveyors ? 4 : 8)
                .Select(index => index * (holes || water || currents || floors || conveyors ? 8 : 4)))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            // Hole/water/floor IDs dispatch the same impulse rule. Retain all
            // directions on the first row, then rotate direction across IDs.
            int firstTile = holes ? 0xf3 : water ? 0xfa : floors ? 0xea : tile;
            if ((holes || water || floors) && tile != firstTile && direction / 8 != (tile & 3)) continue;
            ReinitializeGameplayForValidation();
            if (holes || conveyors) _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, 0x22);
            int sourceRoom = holes || conveyors ? 0x4c : 0x90;
            LoadValidationRoom(group, sourceRoom); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    $"Underwater fixture could not equip ring ${ring:x2}.");
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                // $ff is both water and a downward cliff in this collision
                // set. Ordinary basin walls use dry floor; the separate
                // cliff-coasting fixture retains $ff on the solid border.
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    (water || currents || floors || conveyors) && (x is 0 or 9 || y is 0 or 7) ? (byte)0xa0 : (byte)tile,
                    x is 0 or 9 || y is 0 or 7 ? (byte)0x0f : (byte)0, 0);
            if (edges)
                for (int y = 2; y < 6; y++)
                foreach (int x in new[] { 3, 6 })
                    _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0,
                        y % 2 == 0 ? (byte)0x05 : (byte)0x0a, 0);
            _player.WarpTo(new(80.25f, 64.5f)); _player.Face(Vector2I.Right);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, 80, 64);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay();
            rom[0xd004] = 0; // Execute native Link initialization, rather than injecting velocity.
            rom[0xd009] = 0; // Fresh room-load object memory; direction is a separate byte.
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousDirections = 0, wallStops = 0;
            int Field(string name) => (int)typeof(Player).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count = 1, int angle = 0xff)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int edge = held & ~previousDirections; previousDirections = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter); edge = 0;
                    string context = $"Underwater {group:x1}:{sourceRoom:x2} tile=${tile:x2} ring=${ring:x2} direction=${direction:x2} update={++update}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        _player.TopDownSwimming || _player.IsFallingInHole || _player.IsPullingIntoHole ||
                        _transitions.IsTransitioning || _currentRoom.IsSolid(_player.Position),
                        context + $": fixed XY/facing/dry underwater state differs: runtime={_player.PrecisePosition}, native={rom.Word(0xd00c):x4},{rom.Word(0xd00a):x4}.");
                    FailIf(Field("_topDownMovementAngle") != rom[0xd009] || Field("_topDownMovementSpeedRaw") != rom[0xd010] ||
                            Field("_topDownMovementTargetSpeedRaw") != rom[0xd011] || Field("_topDownMovementVelocityCounter") != rom[0xd012] ||
                            Field("_topDownMovementVelocityInterval") != rom[0xd013] || Field("_topDownMovementTerrainMode") != rom[0xd036] ||
                            Field("_topDownMermaidImpulseCounter") != rom[0xd03e] ||
                            Field("_linkWalkAnimationCounter") != rom[0xd020] ||
                            (Field("_linkWalkAnimationFrame") == 0 ? 0x54 : 0x80) != rom[0xd031],
                            context + $": raw velocity/impulse/WALK animation differ: runtime={Field("_topDownMovementAngle"):x2}/{Field("_topDownMovementSpeedRaw"):x2}/{Field("_topDownMovementTargetSpeedRaw"):x2}/{Field("_topDownMovementVelocityCounter"):x2}/{Field("_topDownMermaidImpulseCounter"):x2}/{Field("_linkWalkAnimationCounter")}, native={rom[0xd009]:x2}/{rom[0xd010]:x2}/{rom[0xd011]:x2}/{rom[0xd012]:x2}/{rom[0xd03e]:x2}/{rom[0xd020]}.");
                    FailIf(Field("_tilePushWalls") != rom[0xd033],
                        context + ": retained wall publication differs across terrain/input movement phases.");
                    FailIf(rom[0xd036] != 0x98 || rom[0xd013] != 5 || rom[0xd011] != (ring == 0x15 ? 0x37 : 0x2d) ||
                        rom[0xd034] != 0x28 || rom[0xd032] !=
                            ((Field("_linkWalkAnimationFrame") == 0 ? 0x7c : 0xa8) + rom[0xd008]),
                        context + ": source-derived Mermaid velocity row or underwater WALK graphics selection differs.");
                    if (!holes && !water && !cliffCoast && !currents && !floors && !conveyors)
                        ValidateUnderwaterWalkFrame(rom[0xd032], context);
                    if ((held != 0 || cliffCoast) && rom[0xd033] != 0 && rom[0xd009] < 0x80 && rom[0xd010] == 0) wallStops++;
                    FailIf(!sounds.Requests.Where(id => id == SoundId.SndSplash).SequenceEqual(rom.Sounds.Where(id => id == SoundId.SndSplash)),
                        context + ": direction-edge splash sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            // Native room initialization starts with angle $00. Let the
            // zero-speed convergence reach the stopped angle before input.
            Step(6);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, direction);
                FailIf(Field("_topDownMermaidImpulseCounter") != 4 || Field("_topDownMovementVelocityCounter") != 0x14 ||
                    Field("_topDownMovementSpeedRaw") != 0,
                    "The initial underwater edge must load $04/$14 while a stopped angle adopts input before acceleration.");
                _dialogue.ShowMessage("Underwater impulse pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, direction);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(32, direction); Step(60);
                FailIf(Field("_topDownMovementSpeedRaw") != 0 || Field("_topDownMovementAngle") != 0xff ||
                    Field("_topDownMermaidImpulseCounter") != 0xff,
                    "Underwater impulse did not stop after a held direction and release.");
                Step(1, (direction + 8) & 0x1f); Step(4, (direction + 12) & 0x1f); Step(60);
            }
            // Fresh pulses reach the real solid basin border. Reversal and
            // release must remain usable after collision, without a reset.
            for (int pulse = 0; pulse < (cliffCoast ? 6 : 32); pulse++)
            {
                Step(); Step(5, direction);
                if (!cliffCoast && wallStops != 0) break;
            }
            if (cliffCoast)
            {
                // Release immediately before coasting reaches the $ff wall.
                // checkLinkJumpingOffCliff rejects wLinkAngle=$ff even though
                // both downward wall probes and the previous angle are $10.
                Step(60);
                FailIf(wallStops == 0 || rom[0xd004] != 1 || rom[0xcc5c] != 0,
                    "Released underwater cliff approach did not stop without a native ledge transition.");
                continue;
            }
            FailIf(wallStops == 0, "Underwater wall fixture did not execute a native blocked-motion speed reset.");
            Step(60);
            Step(1, (direction + 16) & 0x1f); Step(5, (direction + 16) & 0x1f); Step(60);
        }
        GD.Print($"Validated clean-US underwater Mermaid impulses holes={holes}, water={water}, directions/turns/coasting/walls, fixed XY/raw velocity/WALK clocks, dialogue, sound/RNG and reuse in split/batched gameplay.");
    }
}
