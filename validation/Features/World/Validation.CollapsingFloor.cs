using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMoblinKeepCollapsingFloor()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        // Independently transcribed from miscellaneous2.s:@listOfTilesToBreak.
        int[] expected = [0x67,0x66,0x65,0x64,0x63,0x62,0x61,0x51,0x41,0x31,0x21,0x11,
            0x12,0x13,0x23,0x33,0x43,0x44,0x45,0x46,0x47,0x48,0x38,0x28,0x18,0x17,0x16];
        static Vector2 Point(int p) => new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            CollapsingFloorRoomEntity Enter()
            {
                LoadValidationRoom(2, 0x9f);
                _player.WarpTo(Point(0x61));
                FailIf(_currentRoom.IsSolid(_player.Position), "$dc:$0b regression entry must be real walkable ground.");
                var trap = _entities.EntityAdapters<CollapsingFloorRoomEntity>().Single();
                Step();
                FailIf(trap.State != 1, "$dc:$0b state 0 must only initialize.");
                _sound.ClearPlayRequestAudit();
                for (int i = 0; i < 100 && trap.State == 1; i++) Step(1, Vector2.Up);
                FailIf(trap.State != 2, $"$dc:$0b cannot be approached through room 2:9f geometry: {_player.Position}.");
                return trap;
            }
            var trap = Enter();
            var held = _player.Position;
            FailIf(trap.Counter != 30 || !_entities.PlayerUpdatesFrozen ||
                _entities.EntityAdapters<ExclamationMarkRoomEntity>().Count() != 1 ||
                _sound.PlayRequestsFor(0x50) != 2,
                "$dc:$0b collision must freeze Link and allocate the 30-update exclamation.");
            Step(29, Vector2.Up);
            FailIf(trap.State != 2 || trap.Counter != 1 || _player.Position != held,
                "$dc:$0b must retain Link's exact position for the first 29 paused updates.");
            Step(1, Vector2.Up);
            FailIf(trap.State != 3 || trap.Counter != 30 || _entities.PlayerUpdatesFrozen || _player.Position != held ||
                _entities.EntityAdapters<ExclamationMarkRoomEntity>().Any(),
                "$dc:$0b must release Link on update 30, after his frozen update.");
            // Verify the complete mini-script independently of Link's fall.
            // The actual gameplay handoff to the underground is exercised below.
            void Collapse(int count = 1)
            {
                if (batch) _entities.Update(count / 60.0, _player);
                else for (int i = 0; i < count; i++) _entities.Update(1.0 / 60, _player);
            }
            Collapse(29);
            FailIf(trap.ScriptIndex != 0 || trap.Counter != 1, "$dc:$0b first floor must wait another 30 updates.");
            _sound.ClearPlayRequestAudit();
            Collapse();
            for (int i = 0; i < expected.Length; i++)
            {
                FailIf(trap.ScriptIndex != i + 1 || _currentRoom.GetMetatile(Point(expected[i])) != 0x48,
                    $"$dc:$0b tile {i} must break at ${expected[i]:x2}; Link={_player.Position}, state={trap.State}.");
                FailIf(_sound.PlayRequestsFor(0xb3) != i + 1 || _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 0,
                    "$dc:$0b breakCrackedFloor must rumble once per tile and suppress the hole effect's sound.");
                Collapse(6);
                FailIf(trap.ScriptIndex != i + 1, "$dc:$0b must not advance its mini-script before seven updates.");
                Collapse();
            }
            FailIf(!trap.Finished || _entities.EntityAdapters<CollapsingFloorRoomEntity>().Count() != 0 ||
                expected.Any(p => _currentRoom.GetMetatile(Point(p)) != 0x48),
                "$dc:$0b must consume its zero terminator and delete after the last seven-update wait.");
            Collapse(40);
            FailIf(_entities.Entities<FallingDownHoleEffect>().Count != 0, "$dc:$0b floor effects must terminate.");
            // No save flag is written. Re-entry must restore the floor and allow
            // the same geometry-based activation after completion and cancellation.
            trap = Enter();
            FailIf(expected.Any(p => _currentRoom.GetMetatile(Point(p)) == 0x48), "$dc:$0b re-entry retained transient holes.");
            LoadValidationRoom(2, 0x9e);
            FailIf(_entities.PlayerUpdatesFrozen, "$dc:$0b cancellation leaked DISABLE_LINK.");
            trap = Enter();
            FailIf(trap.Counter != 30, "$dc:$0b cancelled visit did not restart its native counter.");
            // Leave Link where the collision triggered. Tile $31/$21 gives
            // way underneath him; his real hole animation must route to the
            // hardcoded underground, not the dungeon-floor lookup.
            Step(30);
            for (int i = 0; i < 400 && _rooms.ActiveGroup == 2; i++) Step();
            FailIf(_rooms.ActiveGroup != 7 || _currentRoom.Id != 0x01 ||
                _player.Position != new Vector2(0x38, 0x08),
                $"$dc:$0b fall must arrive in 7:01 at $03; got {_rooms.ActiveGroup:x1}:{_currentRoom.Id:x2}/{_player.Position}.");
            Step(80);
            FailIf(_entities.PlayerUpdatesFrozen || _transitions.IsTransitioning,
                "$dc:$0b underground arrival did not release the transition.");
            LoadValidationRoom(2, 0x9e);
        }
        // Destination interactions initialize during scrolling, but must not
        // trigger until the incoming room becomes active.
        var destination = _world.LoadRoom(2, 0x9f);
        _player.WarpTo(Point(0x11));
        _entities.BeginScreenTransition(2, destination, Vector2.Left * destination.Width);
        var incoming = _entities.EntityAdapters<CollapsingFloorRoomEntity>().Single();
        _entities.Update(100.0 / 60, _player);
        FailIf(incoming.State != 1 || _entities.PlayerUpdatesFrozen,
            "$dc:$0b advanced beyond initialization during destination scrolling.");
        _entities.FinishScreenTransition();
        var menu = typeof(RoomEntityManager).GetField("_linkCollisionsAndMenuDisabled", flags)!;
        menu.SetValue(_entities, true);
        _entities.Update(4.0 / 60, _player);
        FailIf(incoming.State != 1, "$dc:$0b ignored the collision/menu gate.");
        menu.SetValue(_entities, false);
        _entities.Update(1.0 / 60, _player);
        FailIf(incoming.State != 2, "$dc:$0b did not resume after scrolling and the collision gate cleared.");
        LoadValidationRoom(2, 0x9e);
    }
}
