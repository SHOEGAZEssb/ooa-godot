using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceActorMovementPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); var data = new DekuForestPalaceEventDatabase(); var record = data.Record;
            _saveData.SetGlobalFlag(record.EntranceFlag, false); _saveData.SetGlobalFlag(record.CompletionFlag, false);
            _inventory.GiveTreasure(TreasureId.MysterySeeds, 0x20);
            var owner = _roomEvents.Get<DekuForestPalaceEvent>();
            var positions = SomariaPrivate<Dictionary<string, Vector2>>(owner, "_precisePositions");
            var beforePositions = new Dictionary<string, Vector2>();
            var commands = new[] { data.EntranceCommands, data.CorridorCommands, data.RewardGuardCommands,
                data.EscortGuardCommands, data.AmbiCommands, data.NayruCommands, data.ExitGuardCommands }
                .SelectMany(lane => lane).ToDictionary(command => command.Source);
            var trace = new ScriptMovementPublicationTrace(_runtimeState, source =>
            {
                if (!commands.TryGetValue(source, out var command)) return null;
                string actor = command switch { CutsceneMoveCommand move => move.Actor, CutsceneApplySpeedCommand move => move.Actor, _ => "" };
                if (actor.Length == 0) return null;
                var runner = SomariaPrivate<CutsceneCommandRunner>(owner, "_singleRunner");
                if (runner.CurrentCommand?.Source != source)
                {
                    var lanes = (IEnumerable)SomariaPrivate<object>(SomariaPrivate<CutsceneCommandLaneScheduler>(owner, "_lanes"), "_lanes");
                    runner = lanes.Cast<object>().Select(lane => (CutsceneCommandRunner)lane.GetType().GetProperty("Runner")!.GetValue(lane)!)
                        .Single(lane => lane.CurrentCommand?.Source == source);
                }
                Vector2 position = positions[actor];
                int speed = runner.ActorSpeed(new(actor));
                if (actor == "CorridorGuard")
                {
                    Vector2 before = beforePositions.GetValueOrDefault(actor, position);
                    // soldier.s compares the layout byte with the literal
                    // TILEINDEX_STAIRS=$d0, independently of terrain labels.
                    int packed = (OracleObjectPosition.HighByte(before.Y) & 0xf0) | (OracleObjectPosition.HighByte(before.X) >> 4);
                    speed = _currentRoom.ReadNativeLayoutByte((byte)packed, _runtimeState) == 0xd0 ? 0x19 : 0x28;
                }
                beforePositions[actor] = position;
                int angle = command is CutsceneMoveCommand cardinal ? cardinal.Angle : runner.ActorAngle(new(actor));
                return (speed, angle, position);
            });
            _roomEvents.CommandTraceSink = trace;
            LoadValidationRoom(1, record.EntranceRoom); _player.WarpTo(new(72, 56), recordSafe: false);
            int textHold = 0;
            for (int limit = 0; !(owner.Stage == DekuForestPalaceStage.GenericGuards && owner.DirectExit) && limit < 6000; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    FailIf(_player.IsDying, $"Palace movement fixture unexpectedly entered death: room={_currentRoom.Id:x2}, Link={_player.Position}.");
                    if (_dialogue.IsOpen && ++textHold >= 3) { _dialogue.Close(); textHold = 0; }
                });
            FailIf(owner.Stage != DekuForestPalaceStage.GenericGuards || !owner.DirectExit || owner.BlocksGameplay ||
                !_saveData.HasGlobalFlag(record.CompletionFlag) || trace.Visited.Count != 30,
                $"Palace actor comparison reached {trace.Visited.Count}/30 source operations; stage={owner.Stage}, room={_rooms.CurrentRoom.Id:x2}, Link={_player.Position}, signal=${owner.Signal:x2}.");
            _roomEvents.CommandTraceSink = null;
        }
    }

    private void ValidatePalaceRewardFlightPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool textPause in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); var data = new DekuForestPalaceEventDatabase(); var record = data.Record;
            _saveData.SetGlobalFlag(record.CompletionFlag, false); LoadValidationRoom(1, record.ThroneRoom);
            var owner = _roomEvents.Get<DekuForestPalaceEvent>();
            SomariaPrivate<CutsceneCommandRunner>(owner, "_singleRunner").Clear();
            SomariaPrivate<CutsceneCommandLaneScheduler>(owner, "_lanes").Clear();
            var actors = SomariaPrivate<Dictionary<string, NpcCharacter>>(owner, "_actors");
            var guard = actors["RewardGuard"];
            foreach (string key in actors.Keys.Where(key => key != "RewardGuard").ToArray()) actors.Remove(key);
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(actor => ReferenceEquals(actor.Node, guard)));
            guard.SetStatePosition(new(72, 104));
            SomariaPrivate<Dictionary<string, Vector2>>(owner, "_precisePositions")["RewardGuard"] = new(72, 104);
            F1Set(owner, "_signal", 6); F1Set(owner, "_playerInput", PalacePlayerInput.None); F1Set(owner, "_throneLinkGateReached", true);
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1;
            int at = 0xd040 + _entities.InteractionSlot(guard) * 256;
            native[at] = 1; native[at + 1] = 0x40; native[at + 2] = 4; native[at + 11] = 104; native[at + 13] = 72;
            native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); native[0xcfd1] = 6;
            byte[] sentinel = [0xa5, 0x5a, 0x3c, 0xc3], expected = new byte[4];
            var sounds = _sound.AttachPlayRequestAudit(); int soundStart = native.Sounds.Count, update = 0, movements = 0;
            void Seed() { for (int i = 0; i < 4; i++) { _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]); native[0xcec0 + i] = sentinel[i]; } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                bool moved = native[at] != 0 && native[at + 5] == 4 && native[0xcba0] == 0 && (native[0xcc8a] & 2) == 0;
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); update++;
                if (moved) movements++;
                var stage = SomariaPrivate<RewardGuardFlightStage>(owner, "_rewardFlightStage");
                FailIf(guard.Active != (native[at] != 0) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]) ||
                    native[at] != 0 && ((int)stage != native[at + 5] ||
                        guard.Position != new Vector2(native[at + 13], native[at + 11]) ||
                        SomariaPrivate<int>(owner, "_rewardCounter") != native[at + 6] ||
                        SomariaPrivate<int>(owner, "_rewardZFixed") != unchecked((short)native.Word(at + 14)) ||
                        SomariaPrivate<int>(owner, "_rewardSpeedZ") != unchecked((short)native.Word(at + 0x14))) ||
                    !sounds.Requests.SequenceEqual(native.Sounds.Skip(soundStart)),
                    $"Palace reward flight visit{update}: phase={stage}/{native[at + 5]}, active={guard.Active}/{native[at]:x2}, counter={SomariaPrivate<int>(owner, "_rewardCounter")}/{native[at + 6]}, XY={guard.Position}/{native[at + 13]},{native[at + 11]}, native scratch/Z/cue handoff differs.");
                Seed();
            });
            void Pause(bool paused)
            {
                if (textPause) { if (paused) _dialogue.ShowMessage("Reward guard pause.", _player.Position.Y); else _dialogue.Close(); native[0xcba0] = paused ? (byte)1 : (byte)0; }
                else { _runtimeState.SetWramByte(WramAddress.wDisabledObjects, paused ? (byte)3 : (byte)1); native[0xcc8a] = paused ? (byte)3 : (byte)1; }
            }
            Seed(); Step(4); Pause(true); Step(3); Pause(false);
            for (int limit = 0; native[at + 5] != 4 && limit < 100; limit++) Step(1);
            FailIf(native[at + 5] != 4, "Reward guard did not complete the source signal/delay/jump/land-delay handoff.");
            Step(2); Pause(true); Step(3); Pause(false);
            for (int limit = 0; native[at] != 0 && limit < 30; limit++) Step(1);
            Step(2);
            FailIf(guard.Active || movements != 16 || SomariaPrivate<RewardGuardFlightStage>(owner, "_rewardFlightStage") != RewardGuardFlightStage.Deleted,
                "Source guard flight must execute16 two-pixel steps from Y=$68 and delete at screen Y=$88, preserving pauses and post-retirement scratch.");
            owner.Cancel(); _entities.Clear();
        }
    }

    private void ValidateForestSoldierMovementPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); var data = new DekuForestSoldierEventDatabase();
            _saveData.SetRoomFlag(1, 0x81, 0x40, false); _inventory.GiveTreasure(TreasureId.MysterySeeds, 0x20);
            var owner = _roomEvents.Get<DekuForestSoldierEvent>();
            var runner = SomariaPrivate<CutsceneCommandRunner>(owner, "_runner");
            var trace = new ScriptMovementPublicationTrace(_runtimeState, source =>
            {
                if (data.Commands[source.CommandIndex] is not CutsceneMoveCommand move) return null;
                return (runner.ActorSpeed("Soldier"), move.Angle, SomariaPrivate<Vector2>(owner, "_precisePosition"));
            });
            _roomEvents.CommandTraceSink = trace; LoadValidationRoom(1, 0x81); _player.WarpTo(new(88, 42));
            int textHold = 0;
            for (int limit = 0; (_rooms.CurrentRoom.Id == 0x81 || IsTransitioning) && limit < 800; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    if (_dialogue.IsOpen && ++textHold >= 3) { _dialogue.Close(); textHold = 0; }
                });
            FailIf(_rooms.CurrentRoom.Id != 0x46 || IsTransitioning || trace.Visited.Count != 2 ||
                !_saveData.HasRoomFlag(1, 0x81, 0x40), "Forest soldier must publish both native motion routes and complete the palace warp through gameplay.");
            _roomEvents.CommandTraceSink = null;
        }
    }
}
