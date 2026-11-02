
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateDebugSavestateRafts()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(0x26);
            LoadValidationRoom(1, 0xa7);
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 floor = (from y in Enumerable.Range(1, 6) from x in Enumerable.Range(1, 8)
                let point = new Vector2(x * 16 + 8.75f, y * 16 + 8.25f)
                where _currentRoom.GetTerrainInfo(point).Collision == 0 && !_collision.Collides(point) &&
                    !_entities.Entities<NpcCharacter>().Any(npc => npc.LinkBlockingBounds.HasPoint(point)) &&
                    point.DistanceSquaredTo(new(120, 83)) > 24 * 24
                select point).First();
            _player.WarpTo(floor); _player.Face(Vector2I.Down);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            RaftInteractionRoomEntity CreateInLastFreeSlot()
            {
                var fillers = new System.Collections.Generic.List<InteractionSlotValidationEntity>();
                while (_entities.InteractionSlotAvailable)
                {
                    var puff = new StationaryDebrisEffect(); puff.Initialize(_currentRoom.AssetRoot, new(24, 24), SoundId.MusNone);
                    var filler = new InteractionSlotValidationEntity(puff, _ => { });
                    _entities.AddEntity(filler); fillers.Add(filler);
                }
                var last = fillers.OrderBy(filler => _entities.NativeInteractionSlot(filler)).Last();
                last.Finished = true;
                var actor = new RaftInteractionRoomEntity(new RaftInteractionSpawn(new(120, 88), 1, 0xa7, Direction: 0),
                    _currentRoom, new RaftDatabase().Behavior, _saveData, _runtimeState, _entities);
                _entities.AddEntity(actor);
                foreach (var filler in fillers) filler.Finished = true;
                return actor;
            }
            _entities.Clear();
            var waiting = CreateInLastFreeSlot();
            Step(); Step(16);
            int waitingSlot = _entities.NativeInteractionSlot(waiting);
            FailIf(waitingSlot != 15, "Waiting debug snapshot must exercise saved slot$df, not default first-free$d2.");
            var animation = SomariaPrivate<EnemyAnimationPlayer>(waiting, "_animation");
            FailIf(animation.FrameIndex != 1 || animation.RemainingUpdates != 16 || !waiting.Visible,
                "Waiting snapshot must capture source frame1, not merely an initial sprite.");
            byte[] waitingBytes = Enumerable.Range(0xd040 + waitingSlot * 0x100, 0x40).Select(_runtimeState.ReadWramByte).ToArray();
            var waitingState = CaptureDebugSavestate();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                RestoreDebugSavestate(waitingState);
                waiting = _entities.Entities<RaftInteractionRoomEntity>().Single();
                animation = SomariaPrivate<EnemyAnimationPlayer>(waiting, "_animation");
                FailIf(_entities.NativeInteractionSlot(waiting) != waitingSlot || animation.FrameIndex != 1 ||
                    animation.RemainingUpdates != 16 || !waiting.Visible || _entities.RaftSpecialObjectPresent ||
                    !waitingBytes.SequenceEqual(Enumerable.Range(0xd040 + waitingSlot * 0x100, 0x40).Select(_runtimeState.ReadWramByte)),
                    "Waiting raft restore must bind its saved physical slot/cursor without allocating a placed duplicate.");
                Step();
                FailIf(animation.FrameIndex != 1 || animation.RemainingUpdates != 15,
                    "Restored waiting animation must consume exactly its next native visit.");
            }
            // Allocation/approach is proved by the native slot fixture. Begin
            // here at its published-header boundary to isolate reconstruction.
            _entities.Clear();
            CompanionRuntimeState.Begin(_runtimeState, 0x13, new(120.75f, 88.25f), 0);
            LoadValidationRoom(1, 0xa7);
            FailIf(!_entities.RaftSpecialObjectPresent || _entities.CanCaptureDebugState,
                "Pending native raft initialization must reject capture before the next D1 visit.");
            Step(); Step(16);
            var body = _entities.Entities<RaftRoomEntity>().Single();
            var parent = CreateInLastFreeSlot();
            Step(2); // state0, then mount the already-bound D1 without resetting it.
            FailIf(parent.State != 2 || _entities.Entities<RaftRoomEntity>().Single() != body,
                "A second source interaction must preserve the existing D1 body and retain its own state2 slot.");
            int parentSlot = _entities.NativeInteractionSlot(parent);
            int parentAt = 0xd040 + parentSlot * 0x100;
            byte[] parentBytes = Enumerable.Range(parentAt, 0x40).Select(_runtimeState.ReadWramByte).ToArray();
            _runtimeState.SetField("wDisabledObjects", 2);
            int remaining = _runtimeState.ReadWramByte(0xd120);
            int frame = _runtimeState.ReadWramByte(0xd131);
            byte[] bodyBytes = Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte).ToArray();
            var ridingState = CaptureDebugSavestate();
            FailIf(frame != 1 || !_player.RaftRideActive, "Riding snapshot must capture the source's second bob frame.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                RestoreDebugSavestate(ridingState);
                body = _entities.Entities<RaftRoomEntity>().Single();
                parent = _entities.Entities<RaftInteractionRoomEntity>().Single();
                FailIf(!_player.RaftRideActive || !body.LinkRiding || !body.Visible ||
                    parent.State != 2 || _entities.NativeInteractionSlot(parent) != parentSlot ||
                    !parentBytes.SequenceEqual(Enumerable.Range(parentAt, 0x40).Select(_runtimeState.ReadWramByte)) ||
                    !_entities.CanCaptureDebugState ||
                    !bodyBytes.SequenceEqual(Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte)),
                    "Riding restore must restore the shared player binding and native body without a fresh interaction.");
                Step();
                FailIf(!_player.RaftRideActive || _player.TopDownSwimming || _player.IsDrowning ||
                    _runtimeState.ReadWramByte(0xd120) != remaining - 1 || _runtimeState.ReadWramByte(0xd131) != frame ||
                    parent.State != 2 || parent.Finished,
                    "A restored mounted raft must execute one D1 visit and keep Link out of the water handler.");
                _runtimeState.SetField("wDisabledObjects", 0); Step();
                FailIf(!parent.Finished || _entities.Entities<RaftInteractionRoomEntity>().Any(),
                    "The restored mounting parent must delete on its next eligible interaction visit.");
            }
        }
        GD.Print("Validated repeated waiting/riding raft debug restoration, physical slots, native animation cursors, pending-state rejection, and immediate shared rider binding in both host schedules.");
    }
}
