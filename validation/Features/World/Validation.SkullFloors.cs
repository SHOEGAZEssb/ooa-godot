using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonFloors()
    {
        var data = new SkullDungeonDatabase();
        var records = data.GetRoomRecords(4, 0x71);
        FailIf(records.Count != 2 || records[0] is not { Order: 2, Id: 0x22, SubId: 0, X: 0x78, Y: 0x58 } ||
            records[1] is not { Order: 3, Id: 0x15, SubId: 0 }, "4:71 lost its source floor-controller order/coordinates.");
        foreach (var (room, order) in new[] { (0x72, 0), (0x79, 1), (0x7b, 1) })
        {
            FailIf(data.GetRoomRecords(4, room).Single(record => record.Id == 0x15) is not { SubId: 0 } record || record.Order != order,
                $"4:{room:x2} lost its source toggle-floor controller.");
            LoadValidationRoom(4, room);
            FailIf(_entities.Entities<ToggleFloorRoomEntity>().Count != 1,
                $"4:{room:x2} did not instantiate its native floor controller.");
        }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Step(int count = 1, bool jump = false, Vector2 move = default) =>
            StepGameplayUpdates(count, move, jump ? ["attack"] : [], jump ? ["attack"] : [], batched: true);
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        _inventory.EquipA(InventoryState.ItemFeather);
        var random = CaptureOracleRandomForValidation();
        Vector2 control = new(120, 88);
        byte[] Underlying() => (byte[])((byte[])typeof(OracleRoomData).GetField("_underlyingLayout", flags)!.GetValue(_currentRoom)!).Clone();
        LoadValidationRoom(4, 0x71);
        byte[] initialLayout = (byte[])_currentRoom.Layout.Clone();
        byte[] initialUnderlying = Underlying();

        (byte[] Layout, byte[] Underlying, int Calls) Run(bool batch)
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x71);
            // Restore the same source fixture for the scheduler comparison;
            // normal room re-entry intentionally retains toggle-floor colors.
            for (int i = 0; i < initialLayout.Length; i++)
            {
                if ((i & 15) < _currentRoom.WidthInTiles)
                    _currentRoom.SetPositionTileAndCollision(new Vector2((i & 15) * 16 + 8, (i >> 4) * 16 + 8), initialLayout[i], null, 0);
                _currentRoom.SetUnderlyingStorageMetatile(i, initialUnderlying[i]);
            }
            var changer = _entities.Entities<FloorColorChangerRoomEntity>().Single();
            var toggle = _entities.Entities<ToggleFloorRoomEntity>().Single();
            Vector2 direction = Vector2.Zero;
            foreach (Vector2 candidate in new[] { Vector2.Right, Vector2.Left, Vector2.Down, Vector2.Up })
            {
                Vector2 origin = control + Vector2.Up * 5 - candidate * 24;
                Vector2 side = new(-candidate.Y, candidate.X);
                bool clear = Enumerable.Range(0, 73).All(i => Enumerable.Range(-5, 11).All(j =>
                {
                    Vector2 point = origin + candidate * i + side * j;
                    return point.X >= 8 && point.Y >= 8 && point.X < _currentRoom.Width - 8 && point.Y < _currentRoom.Height - 8 &&
                        !_currentRoom.IsSolid(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None &&
                        !_entities.Entities<EnemyCharacter>().Any(enemy => enemy.Position.DistanceTo(point) < 8);
                }));
                if (!clear) continue;
                direction = candidate;
                _player.WarpTo(origin);
                break;
            }
            FailIf(direction == Vector2.Zero, "4:71 lacks a clear native approach for the Feather floor jump.");
            Step();
            FailIf(_currentRoom.GetMetatile(control) != 0xad || changer.WorkerCount != 0,
                "4:71 must initialize on a red toggle tile without generating a color permutation.");
            Step(jump: true, move: direction);
            FailIf(!_player.TopDownAirborne, "Actual Feather input did not begin the floor-crossing jump.");
            int updates = 0;
            bool pending = false;
            while (_player.TopDownAirborne && updates++ < 80)
            {
                pending |= toggle.PendingCount > 0;
                Step(move: direction);
            }
            FailIf(!pending || _player.TopDownAirborne || toggle.PendingCount != 0 ||
                _currentRoom.GetMetatile(control) != 0xae || _currentRoom.GetUnderlyingMetatile(control) != 0xae || changer.WorkerCount != 0,
                $"Feather landing did not cycle the actual control tile before the next parent dispatch: pending={pending}, air={_player.TopDownAirborne}, tile={_currentRoom.GetMetatile(control):x2}, workers={changer.WorkerCount}, Link={_player.Position}.");
            byte[] layout = (byte[])_currentRoom.Layout.Clone();
            byte[] underlying = Underlying();
            var before = _random.CaptureState();
            var prediction = new OracleRandom();
            prediction.RestoreState(before);
            byte[] permutation = prediction.GeneratePermutation();
            Step();
            var after = _random.CaptureState();
            FailIf(changer.WorkerCount != 1 || after.Calls != before.Calls + 256 ||
                after.PlacementIndex != before.PlacementIndex || !after.PlacementBuffer.SequenceEqual(permutation) ||
                !Enumerable.Range(0, 256).All(i => _entities.RuntimeState.ReadWramByte(OracleRuntimeState.BigBufferAddress + i) == permutation[i]),
                "Floor child did not perform256 shared RNG calls, replace w4RandomBuffer without resetting its cursor, and copy wBigBuffer.");
            void ConvertExpected(int position)
            {
                if (position >= 0x9f || position == 0x57 || (position & 15) == 0 || (position & 0xf0) == 0) return;
                if (layout[position] is >= 0x9d and <= 0x9f) layout[position] = 0x9e;
                else underlying[position] = 0x9e;
            }
            for (int i = 255; i >= 252; i--) ConvertExpected(permutation[i]);
            FailIf(!_currentRoom.Layout.SequenceEqual(layout) || !Underlying().SequenceEqual(underlying),
                "First floor-worker dispatch lost four reverse-index conversions or the colored/obstacle buffer split.");
            if (batch) Step(62); else for (int i = 0; i < 62; i++) Step();
            FailIf(changer.WorkerCount != 1, "Floor worker deleted before its64th update.");
            Step();
            for (int i = 251; i >= 0; i--) ConvertExpected(permutation[i]);
            FailIf(changer.WorkerCount != 0 || !_currentRoom.Layout.SequenceEqual(layout) || !Underlying().SequenceEqual(underlying),
                "Floor worker must finish all256 entries on update64 without changing colored-floor underlying bytes.");

            // Repeat using real input from the landing side; still-airborne
            // crossings and landing on the far side must queue/consume again.
            // The first jump may land on the control tile. Walk off it before
            // jumping back: leaving one's takeoff tile alone never queues it.
            Step(16, move: direction);
            Vector2 repeatOrigin = _player.Position;
            Step(jump: true, move: -direction);
            bool repeatedJump = _player.TopDownAirborne;
            for (int i = 0; _player.TopDownAirborne && i < 80; i++) Step(move: -direction);
            FailIf(_currentRoom.GetMetatile(control) != 0xaf,
                $"Repeated Feather crossing did not cycle yellow to blue: from={repeatOrigin}, to={_player.Position}, direction={direction}, jumped={repeatedJump}, pending={toggle.PendingCount}, knockback={_player.KnockbackFrames}.");
            Step();
            FailIf(changer.WorkerCount != 1, "Repeated control change did not create a fresh floor worker.");
            LoadValidationRoom(4, 0x91);
            FailIf(_entities.Entities<FloorColorChangerRoomEntity>().Count != 0 || _entities.Entities<ToggleFloorRoomEntity>().Count != 0,
                "Leaving4:71 retained its pending floor worker/controller.");
            LoadValidationRoom(4, 0x71);
            FailIf(_currentRoom.GetMetatile(control) != 0xaf || _entities.Entities<FloorColorChangerRoomEntity>().Single().WorkerCount != 0,
                "Re-entry must retain the blue control tile and discard the old child state.");
            return (layout, underlying, after.Calls);
        }
        var single = Run(false);
        var batched = Run(true);
        FailIf(!single.Layout.SequenceEqual(batched.Layout) || !single.Underlying.SequenceEqual(batched.Underlying) || single.Calls != batched.Calls,
            "Batched floor propagation changed the source tile/buffer/RNG results.");

        // Explicit shared-buffer overwrite and cancellation probes complement
        // the actual gameplay traversal above.
        LoadValidationRoom(4, 0x71);
        var runtime = new OracleRuntimeState();
        var isolatedRandom = new OracleRandom();
        var raw = new FloorColorChangerRoomEntity(records[0], _currentRoom, new DungeonInteractionDatabase(),
            isolatedRandom, runtime, () => { }, () => 0);
        var frame = new RoomEntityFrame(_player, 0, false);
        var spawns = new List<RoomEntitySpawn>();
        raw.UpdateFrame(frame, spawns);
        _currentRoom.SetPositionTileAndCollision(control, 0xae, 0, 0);
        raw.UpdateFrame(frame, spawns);
        FailIf(isolatedRandom.Calls != 0, "Parent must only allocate; permutation generation belongs to the later child.");
        raw.UpdateChildren(frame, spawns);
        Vector2 sample = new(40, 40);
        _currentRoom.SetPositionTileAndCollision(sample, 0x9d, 0, 0);
        for (int i = 0; i < 256; i++) runtime.SetWramByte(OracleRuntimeState.BigBufferAddress + i, 0x22);
        raw.UpdateChildren(frame, spawns);
        FailIf(_currentRoom.GetMetatile(sample) != 0x9e, "Worker retained a private permutation instead of observing shared wBigBuffer writes.");
        _currentRoom.SetPositionTileAndCollision(control, 0xda, 0, 0);
        raw.UpdateFrame(frame, spawns);
        raw.UpdateChildren(frame, spawns);
        FailIf(raw.WorkerCount != 1 || isolatedRandom.Calls != 256, "Somaria must preserve the worker and not generate a new permutation.");
        _currentRoom.SetPositionTileAndCollision(control, 0xad, 0, 0);
        raw.UpdateChildren(frame, spawns); // A later placed interaction changed the controller tile.
        FailIf(raw.WorkerCount != 0, "A later interaction's control-tile change must cancel the old worker in the same pass.");
        raw.Free();

        // A pending subid1 retains its tile position even if another native
        // object replaces that tile while Link is airborne.
        foreach (var (replacement, expected) in new[] { (0xda, 0xad), (0x00, 0x01), (0xff, 0x00) })
        {
            _currentRoom.SetPositionTileAndCollision(control, 0xad, 0, 0);
            _currentRoom.SetUnderlyingMetatile(control, 0xad);
            var pendingToggle = new ToggleFloorRoomEntity(_currentRoom, new DungeonInteractionDatabase(), _ => { }, () => { }, () => 0);
            _player.WarpTo(control + new Vector2(-16, -5));
            pendingToggle.UpdateFrame(frame, spawns);
            _player.AdvanceTopDownAirUpdateForValidation(startJump: true);
            _player.ApplyMovingPlatformDisplacement(Vector2.Right * 16);
            pendingToggle.UpdateFrame(frame, spawns);
            FailIf(pendingToggle.PendingCount != 1, "Raw landing probe did not queue the native colored tile.");
            _currentRoom.SetPositionTileAndCollision(control, (byte)replacement, 0, 0);
            _player.ApplyMovingPlatformDisplacement(Vector2.Right * 16);
            for (int i = 0; _player.TopDownAirborne && i < 80; i++) _player.AdvanceTopDownAirUpdateForValidation();
            pendingToggle.UpdateFrame(frame, spawns);
            FailIf(pendingToggle.PendingCount != 0 || _currentRoom.GetMetatile(control) != expected ||
                _currentRoom.GetUnderlyingMetatile(control) != expected,
                $"Queued toggle of ${replacement:x2} did not preserve the child's byte increment/clamp to ${expected:x2}.");
            pendingToggle.Free();
        }
    }
}
