using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSuctionPitsRom()
    {
        // Source-derived literals, independently of the runtime table reader.
        var data = SuctionPitDatabase.Shared;
        FailIf(!data.Speeds.SequenceEqual(new[] { 0x3c, 0x32, 0x28, 0x1e, 0x19, 0x14, 0x0f, 0x0a }) ||
            !data.Probes.SequenceEqual(new (byte, byte)[] { (0, 0xf7), (10, 0), (0, 9), (0xfd, 0xfb), (0, 0) }),
            "seaEffects.s: source suction speeds or cumulative probe order differ.");
        foreach (int mode in Enumerable.Range(0, 6))
        foreach (byte tile in new byte[] { 0x3b, 0x3c, 0x3d, 0x3e, 0x3f, 0x40, 0x48, 0xf3, 0xe9 })
            FailIf(data.Matches(mode, tile) != (mode is 2 or 5 && tile is >= 0x3c and <= 0x3f),
                $"seaEffectTiles2.s: suction dispatch differs for mode${mode:x2}, tile${tile:x2}.");

        int hostCase = 0;
        foreach (var fixture in new[] { (Room: 0x34, Packed: 0x17, Tile: 0x3c),
            (Room: 0x34, Packed: 0x27, Tile: 0x3f), (Room: 0x34, Packed: 0x37, Tile: 0x3d),
            (Room: 0x84, Packed: 0x16, Tile: 0x3e) })
        foreach (bool batch in RomHostSchedules(hostCase++))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            LoadValidationRoom(5, fixture.Room);
            FailIf(_entities.Entities<SuctionPitRoomEntity>().Count != 1 ||
                _currentRoom.GetPackedStorageMetatile((byte)fixture.Packed) != fixture.Tile,
                $"Suction room5:{fixture.Room:x2} must allocate PART$2e from its original tile${fixture.Tile:x2} at${fixture.Packed:x2}.");
            // Isolate this PART and Link while retaining the original geometry.
            _entities.Clear();
            var pit = new SuctionPitRoomEntity(_entities);
            _entities.AddEntity(pit);
            Vector2 center = new((fixture.Packed & 15) * 16 + 8, (fixture.Packed >> 4) * 16 + 8);
            Vector2 start = center + new Vector2(24.25f, 0.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Left);
            _player.SetLocalRespawnPosition(start, Vector2I.Left);
            FailIf(_collision.Collides(start) || _currentRoom.GetTerrainInfo(start).Hazard != HazardType.None,
                $"Suction room5:{fixture.Room:x2}: approach must start in original open geometry.");
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 3, (int)start.X, (int)start.Y)
                { HostilePartsEnabled = true };
            rom.Word(0xd00c, (int)(start.X * 256)); rom.Word(0xd00a, (int)(start.Y * 256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd009] = 0; // Full room load clears angle independently of facing.
            rom[0xcc21] = (byte)start.Y; rom[0xcc22] = (byte)start.X; rom[0xcc23] = 3;
            rom[0xd0c0] = 1; rom[0xd0c1] = 0x2e;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, lastHeld = 0, initialHealth = _player.HealthQuarters;
            bool sawPull = false, sawCapture = false, sawFall = false;
            void Step(int count = 1, int angle = 0xff)
            {
                int held = angle == 24 ? 0x20 : angle == 8 ? 0x10 : 0;
                int edge = held & ~lastHeld; lastHeld = held;
                StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    MenuRomActions(held), MenuRomActions(edge), batched: batch, afterUpdate: () =>
                    {
                        rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter); edge = 0;
                        string context = $"Suction room5:{fixture.Room:x2} tile${fixture.Tile:x2}, batch={batch}, update{++update}";
                        FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                            pit.State != rom[0xd0c4] || pit.Counter != rom[0xd0c6] ||
                            _runtimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != rom[0xcc91] ||
                            (SomariaPrivate<int>(_player, "_forcedRespawnPhase") == 1) != (rom[0xcc4f] == 2) ||
                            rom[0xcc4f] == 2 && SomariaPrivate<int>(_player, "_forcedRespawnParameter") != 0 ||
                            _player.NativeNormalStateForInteraction != (rom[0xd004] == 1) ||
                            _player.HealthQuarters != rom[0xc6aa] || _player.Visible != ((rom[0xd01a] & 0x80) != 0),
                            context + $": XY={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}; " +
                            $"PART={pit.State}:{pit.Counter:x2}/{rom[0xd0c4]}:{rom[0xd0c6]:x2}, Link state${rom[0xd004]:x2}:${rom[0xd005]:x2}, force${rom[0xcc4f]:x2}, " +
                            $"velocity=${SomariaPrivate<int>(_player, "_topDownMovementSpeedRaw"):x2}/${rom[0xd010]:x2}, angle=${SomariaPrivate<int>(_player, "_topDownMovementAngle"):x2}/${rom[0xd009]:x2}, impulse=${SomariaPrivate<int>(_player, "_topDownMermaidImpulseCounter"):x2}/${rom[0xd03e]:x2}.");
                        if (rom[0xcc91] != 0) sawPull = true;
                        if (rom[0xcc4f] == 2) sawCapture = true;
                        if (rom[0xd004] == 2 && rom[0xd005] == 1) sawFall = true;
                        FailIf(!sounds.Requests.Where(id => id == SoundId.SndLinkFall).SequenceEqual(rom.Sounds.Where(id => id == SoundId.SndLinkFall)),
                            context + ": fall cue boundary differs.");
                    });
            }
            Step(2);
            FailIf(pit.Counter != 0x20 || sawPull, "Suction PART$2e must initialize without pulling and retain $20 away from pits.");
            // Approach from the real open side, cancel while the weak pull can
            // still be escaped, then repeat and complete the native handoff.
            for (int wait = 0; !sawPull && wait < 480; wait++) Step(angle: wait % 12 is >= 1 and <= 3 ? 24 : 0xff);
            FailIf(!sawPull || sawCapture, $"Suction room5:{fixture.Room:x2} tile${fixture.Tile:x2}: approach did not reach a cancellable pull: XY={_player.PrecisePosition}, walls=${rom[0xd033]:x2}, speed=${rom[0xd010]:x2}, capture={sawCapture}.");
            if (fixture.Tile == 0x3c)
            {
                int retainedCounter = pit.Counter;
                Vector2 retainedPosition = _player.PrecisePosition;
                _dialogue.ShowMessage("Suction pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6);
                FailIf(pit.Counter != retainedCounter || _player.PrecisePosition != retainedPosition ||
                    _runtimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != 0xff,
                    "Text must freeze suction while retaining its shared transition signal.");
                _dialogue.Close(); rom[0xcba0] = 0;
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 8); rom[0xcc8a] = 8;
                Step(2);
                FailIf(pit.Counter != retainedCounter, "DISABLE_PARTS$08 must retain the suction counter.");
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); rom[0xcc8a] = 0;
            }
            for (int pulse = 0; pulse < 5; pulse++) { Step(); Step(5, 8); }
            FailIf(pit.Counter != 0x20 || _runtimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != 0,
                $"Suction room5:{fixture.Room:x2} tile${fixture.Tile:x2}: escape must restore counter$20 and clear lock: XY={_player.PrecisePosition}, counter=${pit.Counter:x2}, state=${rom[0xd004]:x2}, capture={sawCapture}.");
            sawPull = sawCapture = sawFall = false;
            for (int wait = 0; !sawCapture && wait < 180; wait++) Step(angle: wait % 6 == 0 ? 0xff : 24);
            FailIf(!sawPull || !sawCapture, "Repeated suction approach did not request LINK_STATE_RESPAWNING$02.");
            for (int wait = 0; _player.HealthQuarters == initialHealth && wait < 80; wait++) Step();
            FailIf(!sawFall || _player.HealthQuarters != initialHealth - 2,
                "Suction must execute the falling animation and half-heart respawn damage.");
            Step(18);
            FailIf(_player.IsFallingInHole || _player.PrecisePosition != new Vector2((int)start.X, (int)start.Y) ||
                pit.Counter != 0x20 || _runtimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != 0,
                "Suction respawn must finish at the retained safe position and reset on the next normal PART update.");
            sawCapture = false;
            for (int wait = 0; !sawCapture && wait < 180; wait++) Step(angle: wait % 6 == 0 ? 0xff : 24);
            FailIf(!sawCapture, "Suction pit must remain active after a completed respawn.");
            // A later interaction's parameter2 write must replace the PART's
            // pending parameter0 request, rather than lose to another owner.
            _player.RequestForcedRespawn(); rom[0xcc51] = 2;
            Step(22);
            FailIf(_player.IsFallingInHole || _player.HealthQuarters != initialHealth - 4 ||
                sounds.Requests.Count(id => id == SoundId.SndLinkFall) != 1,
                "A later forced-respawn writer must replace suction's pending fall without a second falling animation.");
        }
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            LoadValidationRoom(5, 0x35);
            _player.WarpTo(new(6.25f, 24.5f));
            FailIf(_collision.Collides(_player.Position) ||
                !_rooms.TryGetNeighbor(Vector2I.Left, out int target) || target != 0x34,
                "Suction scroll fixture requires original room5:35's open west edge and imported room5:34 neighbor.");
            _transitions.BeginScroll(_player, Vector2I.Left, 0x34);
            var incoming = _entities.Entities<SuctionPitRoomEntity>().Single();
            FailIf(incoming.State != 1 || incoming.Counter != 0x20,
                "Incoming PART$2e must initialize before the scrolling destination is exposed.");
            for (int wait = 0; _transitions.ScrollActive && wait < 256; wait++)
                StepGameplayUpdates(1, Vector2.Zero, batched: batch, afterUpdate: () =>
                    FailIf(incoming.Counter != 0x20 || _player.IsFallingInHole ||
                        _runtimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != 0,
                        "Initialized destination suction must remain frozen throughout room scrolling."));
            FailIf(_transitions.ScrollActive, "Suction scroll fixture did not complete.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(incoming.Counter != 0x20, "Destination suction must resume harmlessly away from pits after scrolling.");
        }
        GD.Print("Validated clean-US dungeon suction: all four tiles, real Mermaid/Ancient Tomb approaches, escape, modal/part masks, capture/fall/respawn/reuse, shared request overwrite, scroll initialization/freeze and split/batched gameplay.");
    }
}
