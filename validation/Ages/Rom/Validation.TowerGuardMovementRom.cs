using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTowerGuardMovementPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            var database = new BlackTowerEntranceEventDatabase(); var record = database.Record;
            _saveData.SetRoomFlag(record.Group, record.Room, (byte)record.AftermathFlag, false);
            _saveData.SetRoomFlag(record.Group, record.Room, (byte)record.CompleteFlag, false);
            LoadValidationRoom(record.Group, record.Room);
            var owner = _roomEvents.Get<BlackTowerEntranceEvent>();
            var guard = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x58 && npc.Record.SubId == 2);
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(actor => ReferenceEquals(actor.Node, guard)));
            guard.SetStatePosition(new(72, 56)); F1Set(owner, "_guardPrecisePosition", new Vector2(72.25f, 56.5f));
            F1Set(owner, "_stage", BlackTowerEntranceEventStage.Aftermath);
            int index = Enumerable.Range(0, database.Aftermath.Count).Single(i => database.Aftermath[i] is CutsceneMoveCommand);
            var move = (CutsceneMoveCommand)database.Aftermath[index];
            FailIf(move.Counter != 0x21 || move.Angle != 8 || record.MoveSpeed != 0x14,
                "scriptHelper.s hardhatWorkerSubid02Script must retain SPEED_080/moveright$21.");
            var runner = SomariaPrivate<CutsceneCommandRunner>(owner, "_runner");
            runner.Start(database.Aftermath, index); runner.SetInitialMotionRegisters(move.ActorId, 0x14, 8);
            runner.AdvanceFrame(); // Declare the post-setup counter2=$21 boundary; setup does not move.
            _player.WarpTo(new(40, 104)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 104) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1;
            int at = 0xd040 + _entities.InteractionSlot(guard) * 256;
            native[at] = 1; native[at + 1] = 0x58; native[at + 2] = 2; native[at + 4] = 1;
            native[at + 7] = 0x21; native[at + 9] = 8; native[at + 0x10] = 0x14;
            native[at + 0x20] = 0x7f; native[at + 0x38] = 1;
            native.Word(at + 10, 0x3880); native.Word(at + 12, 0x4840);
            byte[] observed = new byte[4], expected = new byte[4], sentinel = [0xa5, 0x5a, 0x3c, 0xc3];
            int traceVisits = 0, motionVisits = 0, update = 0;
            _roomEvents.CommandTraceSink = new MovementCommandScratchTrace(entry =>
            {
                if (entry.Source != move.Source) return;
                traceVisits++;
                for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            });
            void Seed()
            {
                for (int i = 0; i < 4; i++) { _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]); native[0xcec0 + i] = sentinel[i]; }
            }
            void Step(int count, bool paused = false) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                {
                    for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i];
                }); update++;
                FailIf(runner.Counter != native[at + 7] || guard.Position != new Vector2(native[at + 13], native[at + 11]) ||
                    SomariaPrivate<Vector2>(owner, "_guardPrecisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                    $"Tower guard visit={update}: source script counter2/precise position differs.");
                if (!paused)
                {
                    FailIf(traceVisits != 1 || !observed.SequenceEqual(expected),
                        $"Tower guard visit={update}: script movement must publish native scratch before later consumers; setup/zero visits preserve it.");
                    if (runner.Counter != 0) motionVisits++;
                }
                else FailIf(traceVisits != 0, "Ordinary active text must retain the guard's script/movement counters.");
                traceVisits = 0; Seed();
            });
            Seed(); Step(8);
            _dialogue.ShowMessage("Tower guard movement pause.", _player.Position.Y); native[0xcba0] = 1;
            Step(3, true); _dialogue.Close(); native[0xcba0] = 0;
            Step(25);
            FailIf(motionVisits != 32 || runner.Counter != 0 || guard.Position != new Vector2(88, 56),
                $"Native moveright$21 must execute exactly32 half-pixel steps and retain the fractional endpoint: visits={motionVisits}, counter={runner.Counter}, guard={guard.Position}, precise={SomariaPrivate<Vector2>(owner, "_guardPrecisePosition")}.");
            _roomEvents.CommandTraceSink = null; owner.Cancel(); _entities.Clear();
        }
    }
}
