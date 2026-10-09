using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDeepWaterDiveRom() => ValidateDeepWaterDiveRom(false);
    private void ValidateDeepWaterDungeonDiveRom() => ValidateDeepWaterDiveRom(true);

    private void ValidateDeepWaterDiveRom(bool dungeon)
    {
        int hostCase1 = 0;
        foreach (int group in dungeon ? new[] { 5 } : new[] { 0, 1 })
        foreach (int ring in new[] { 0xff, 0x3c })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            int sourceRoom = dungeon ? 0x5c : 0x90;
            int destinationRoom = dungeon ? 0x4c : 0x90;
            int destinationGroup = dungeon ? group : group + 2;
            LoadValidationRoom(group, sourceRoom); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    "Deep-water fixture could not equip ZORA_RING $3c.");
            }
            // A bounded shoreline using the native sea tile/collision bytes.
            // Link approaches through walkable geometry; no swimming state is injected.
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool wall = x is 0 or 9 || y is 0 or 7;
                bool water = !wall && !(x == 1 && y == 4);
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    water ? (byte)0xfc : (byte)0xa0, wall ? (byte)0x0f : water ? (byte)0x10 : (byte)0, 0);
            }
            _player.WarpTo(new(24.25f, 60.5f)); _player.Face(Vector2I.Right);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, 24, 60);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; rom.InitializeLinkGameplay();
            if (dungeon)
            {
                rom.CreateMenuView().LoadDungeon(7);
                FailIf(rom[0xcc3b] != 1 || rom[0xcc3a] != 0x1b,
                    $"Jabu-Jabu room 5:5c must occupy floor 1, map position $1b before levelDown; got ${rom[0xcc3b]:x2}/${rom[0xcc3a]:x2}.");
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousHeld = 0, captured = -1, fadeUpdates = 0;
            bool loaded = false;
            void Lock(int mask)
            {
                _runtimeState.SetWramByte(WramAddress.wDisableScreenTransitions, (byte)mask);
                rom[0xcc91] = (byte)mask;
            }
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~previousHeld; previousHeld = held;
                Vector2 movement = (held & 0x10) != 0 ? Vector2.Right : Vector2.Zero;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.AdvanceWarpPalette();
                    if (rom[0xc2ef] == 3)
                    {
                        rom.UpdateFadeOutWarp();
                        fadeUpdates++;
                        loaded = rom[0xcc2d] != group || rom[0xcc30] != sourceRoom;
                    }
                    else if (!loaded)
                    {
                        rom.UpdateGameplay(edge, held, movement == Vector2.Right ? 8 : 0xff, _entities.FrameCounter);
                        if (rom[0xcc4b] != 0)
                        {
                            captured = rom[0xcc4a];
                            FailIf(rom[0xcc47] != (0x80 | destinationGroup) || rom[0xcc48] != destinationRoom ||
                                rom[0xcc49] != 0 || rom[0xcc4b] != 3,
                                "Native deep-water levelDown did not request its source-derived direct destination/basic warp.");
                            if (dungeon)
                                FailIf(rom[0xcc3b] != 0 || rom[0xcc3a] != 0x1b,
                                    "Native dungeon levelDown must decrement the floor while retaining map position $1b.");
                            rom.ApplyRequestedWarp();
                        }
                    }
                    else
                    {
                        // Destination Link initializes its basic warp state
                        // through the same native object dispatch, not a write
                        // to its coordinates or timers in the fixture.
                        rom.UpdateGameplay(edge, held, movement == Vector2.Right ? 8 : 0xff, _entities.FrameCounter);
                    }
                    edge = 0;
                    string context = $"Deep-water {group:x1}:{sourceRoom:x2} ring=${ring:x2} batched={batched} update={++update}";
                    FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _currentRoom.Id != rom[0xcc30] ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        context + $": room/full fixed XY differ: runtime={_rooms.ActiveGroup:x1}:{_currentRoom.Id:x2}/{_player.PrecisePosition}, native={rom[0xcc2d]:x1}:{rom[0xcc30]:x2}/{rom.Word(0xd00c):x4},{rom.Word(0xd00a):x4}.");
                    if (!loaded)
                    {
                        FailIf(_player.TopDownDiving != ((rom[0xcc5d] & 0x80) != 0) ||
                            _player.TopDownSwimming && (_player.TopDownDiveCounter != rom[0xd007] ||
                            _player.TopDownSwimAngle != rom[0xd009] || _player.TopDownSwimSpeedRaw != rom[0xd010]),
                            context + ": dive counter/angle/speed differ before or during fade-out.");
                        FailIf(_transitions.IsTransitioning != (rom[0xc2ef] == 3), context +
                            $": fade dispatch differs: runtime={_transitions.TimeWarpPhaseName}/{_transitions.IsTransitioning}, native=${rom[0xc2ef]:x2}, tile=${_currentRoom.GetMetatile(_player.Position):x2}, lock=${_runtimeState.ReadWramByte(0xcc91):x2}, swim={_player.TopDownSwimmingState}, diving={_player.TopDownDiving}, captured=${captured:x2}.");
                        if (rom[0xc2ef] == 3)
                            FailIf(!Mathf.IsEqualApprox(_warpFade.Color.A, System.Math.Min((int)rom[0xc2ff], 31) / 31f),
                                context + ": fade-out palette offset differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": source RNG differs.");
                    }
                    else
                    {
                        FailIf(!Mathf.IsEqualApprox(_warpFade.Color.A,
                            System.Math.Min((int)rom[0xc2ff], 31) / 31f), context +
                            $": arrival fade offset differs: runtime={_warpFade.Color.A}, native=${rom[0xc2ff]:x2}, mode=${rom[0xc4ab]:x2}, Link=${rom[0xd004]:x2}/${rom[0xd005]:x2}.");
                        if (rom[0xd004] == 1)
                        {
                            FailIf(CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                                _player.TopDownSwimmingState != (rom[0xcc5d] & 0x0f),
                                context + ": initialized arrival facing/swimming differs.");
                            if ((rom[0xcc34] & 0x40) != 0)
                                ValidateUnderwaterWalkFrame(rom[0xd032], context + " arrival");
                        }
                    }
                    FailIf(sounds.Requests.Contains(SoundId.SndEnterCave) || rom.Sounds.Contains(SoundId.SndEnterCave),
                        context + ": direct levelDown incorrectly played the stair entrance sound $6e.");
                });
            }
            // Establish the shoreline entry and swim to the basin's middle.
            while (!_player.TopDownSwimming && update < 60) Step(1, 0x10);
            FailIf(!_player.TopDownSwimming, "Deep-water shoreline was unreachable.");
            Step(60);
            int began = update;
            while (_player.PrecisePosition.X < 80 && update - began < 180) { Step(); Step(6, 0x10); }
            FailIf(_player.PrecisePosition.X < 80, "Deep-water test area was unreachable.");
            Step(60);
            // A lock allows B to cancel the dive. Repeating without resetting
            // Link checks that cancellation leaves no pending transition behind.
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Lock(repeat == 0 ? 1 : 0x80);
                Step(1, 2); Step(4); Step(1, 2); Step(4);
                FailIf(_player.TopDownDiving || _transitions.IsTransitioning,
                    "Locked deep-water cancellation retained a dive or pending warp.");
            }
            Lock(0);
            Step(1, 2);
            FailIf(!_player.TopDownDiving || _transitions.IsTransitioning || _player.TopDownDiveCounter != 0x78,
                "B should begin diving before the following update requests deep-water travel.");
            _dialogue.ShowMessage("Deep-water handoff pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(6, 2);
            FailIf(_transitions.IsTransitioning || _player.TopDownDiveCounter != 0x78,
                "Active text did not freeze the pending deep-water dive before its next Link dispatch.");
            _dialogue.Close(); rom[0xcba0] = 0;
            Vector2 sourcePosition = _player.PrecisePosition;
            Step(1, 2); // Held B cannot cancel: levelDown precedes timer/input.
            FailIf(!_transitions.IsTransitioning || captured < 0 || _player.PrecisePosition != sourcePosition ||
                _player.TopDownDiveCounter != 0x78 || _warpFade.Color.A != 0,
                "Deep-water handoff advanced movement/timer/fade on its request update.");
            FailIf(captured != _terrain.GetActiveTerrain(_player.Position).PackedPosition ||
                captured == _currentRoom.GetPackedPosition(_player.Position),
                "Deep-water destination did not capture the distinct foot-probe tile instead of Link's center tile.");
            Step(31, 2);
            FailIf(loaded || _rooms.ActiveGroup != group || _warpFade.Color.A != 1,
                "Deep-water destination loaded before the terminal fade-out update.");
            Step(1, 2);
            FailIf(!loaded || fadeUpdates != 32 || _rooms.ActiveGroup != destinationGroup || _currentRoom.Id != destinationRoom ||
                _player.PrecisePosition != new Vector2((captured & 15) * 16 + 8, (captured >> 4) * 16 + 8),
                "Deep-water reload failed to clear coordinate fractions and apply the captured tile center.");
            Step(32);
            FailIf(!_transitions.IsTransitioning || !_transitions.PaletteFadeActive || rom[0xc4ab] == 0 ||
                _warpFade.Color.A != 0, "Visible arrival must finish before the terminal palette-thread update.");
            Step();
            FailIf(_transitions.IsTransitioning || _warpFade.Color.A != 0,
                "Deep-water arrival fade did not finish at its source-derived terminal update.");
            Step(4);
        }
        GD.Print($"Validated clean-US deep-water descent dungeon={dungeon}, both overworld eras or native Jabu floor/map destination, ZORA_RING, reachable shore/swimming, repeated locked cancellation, dialogue, request-before-input ordering, silent 32/33-update fades, native destination load and captured tile-center handoff in split/batched gameplay.");
    }
}
