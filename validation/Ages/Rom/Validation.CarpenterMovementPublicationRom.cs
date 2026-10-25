using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCarpenterScriptPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(false);
            var owner = _roomEvents.Get<CarpenterEvent>(); var data = owner.Database;
            _saveData.SetGlobalFlag(data.Constant("bridge-flag"), false);
            _runtimeState.SetWramByte(data.Constant("found-address"), 0x1c);
            LoadValidationRoom(0, 0x25); _player.WarpTo(new(120, 104));
            var actors = SomariaPrivate<List<CarpenterScriptHost>>(owner, "_actors");
            CarpenterScriptHost? activeHost = null;
            // Preserve each existing native callback and scheduler order;
            // observe which lane owns a shared subroutine's trace in validation.
            var lanes = (System.Collections.IList)SomariaPrivate<object>(
                SomariaPrivate<CutsceneCommandLaneScheduler>(owner, "_lanes"), "_lanes");
            for (int i = 0; i < lanes.Count; i++)
            {
                object original = lanes[i]!; Type type = original.GetType();
                var runner = (CutsceneCommandRunner)type.GetProperty("Runner")!.GetValue(original)!;
                var before = (Action?)type.GetProperty("BeforeAdvance")!.GetValue(original);
                var host = actors.Single(actor => ReferenceEquals(actor.Runner, runner));
                Action observed = () => { activeHost = host; before?.Invoke(); };
                lanes[i] = Activator.CreateInstance(type, type.GetProperty("Name")!.GetValue(original), runner, observed);
            }
            var trace = new ScriptMovementPublicationTrace(_runtimeState, source =>
            {
                var command = data.Commands[source.CommandIndex];
                if (command is not (CutsceneMoveCommand or CutsceneApplySpeedCommand)) return null;
                var host = activeHost ?? throw new InvalidOperationException("Carpenter trace has no active scheduler lane.");
                int angle = command is CutsceneMoveCommand cardinal ? cardinal.Angle : host.Runner.ActorAngle("Carpenter");
                return (host.Runner.ActorSpeed("Carpenter"), angle, host.Npc.Position);
            }, () => activeHost!);
            _roomEvents.CommandTraceSink = trace;
            int textHold = 0;
            for (int limit = 0; owner.HasState && limit < 2000; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    if (_dialogue.IsOpen && ++textHold >= 3) { _dialogue.Close(); textHold = 0; }
                });
            FailIf(owner.HasState || owner.BlocksGameplay || owner.MenusDisabled ||
                !_saveData.HasGlobalFlag(data.Constant("bridge-flag")) || trace.Visited.Count != 20,
                $"Carpenter bridge: actual gameplay reached {trace.Visited.Count}/20 source motions or retained control.");
            _roomEvents.CommandTraceSink = null;
            LoadValidationRoom(0, 0x25);
            FailIf(owner.HasState || _entities.Entities<NpcCharacter>().Any(npc => npc.Active), "Completed bridge actors must remain suppressed on re-entry.");
        }
    }

    private void ValidateCarpenterDeparturePublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(false);
            var owner = _roomEvents.Get<CarpenterEvent>(); var data = owner.Database;
            _saveData.SetGlobalFlag(data.Constant("bridge-flag"), false);
            _runtimeState.SetWramByte(data.Constant("found-address"), 4);
            LoadValidationRoom(0, 0x25); _player.WarpTo(new(120, 104));
            var actors = SomariaPrivate<List<CarpenterScriptHost>>(owner, "_actors");
            var host = actors.Single(actor => actor.Npc.Record.SubId == 2);
            foreach (var actor in actors) actor.Runner.Clear();
            actors.RemoveAll(actor => !ReferenceEquals(actor, host)); F1Set(owner, "_blocker", null!);
            var adapter = _entities.EntityAdapters<CarpenterRoomEntity>().Single(actor => ReferenceEquals(actor.Npc, host.Npc));
            F1Retain(adapter); host.Npc.SetStatePosition(new(8, 56));
            host.WriteObjectByte("Carpenter", 0x44, 2); owner.SetInputEnabled(false); owner.SetMenuEnabled(false);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 104) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); int at = 0xd040 + _entities.InteractionSlot(host.Npc) * 256;
            native[0xcfd1] = 4; native[at] = 1; native[at + 1] = 0x9a; native[at + 2] = 2;
            native[at + 11] = 56; native[at + 13] = 8;
            native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); native[at + 4] = 2;
            native[0xcc8a] = 0x81; native[0xcc02] = 1;
            _runtimeState.SetWramByte(data.Constant("found-address"), 0); native[0xcfd1] = 0;
            var sounds = _sound.AttachPlayRequestAudit(); int nativeSoundStart = native.Sounds.Count;
            byte[] sentinel = [0xa5, 0x5a, 0x3c, 0xc3], expected = new byte[4];
            void Seed() { for (int i = 0; i < 4; i++) { _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]); native[0xcec0 + i] = sentinel[i]; } }
            int update = 0, movements = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                bool moving = native[at] != 0;
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); update++;
                if (moving) movements++;
                FailIf(host.Npc.Active != (native[at] != 0) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]) ||
                    native[at] != 0 && (host.Npc.Position != new Vector2(native[at + 13], native[at + 11]) ||
                        SomariaPrivate<int>(host, "_z") != unchecked((short)native.Word(at + 14)) ||
                        SomariaPrivate<int>(host, "_speedZ") != unchecked((short)native.Word(at + 0x14))) ||
                    _runtimeState.ReadWramByte(data.Constant("found-address")) != native[0xcfd1] ||
                    !sounds.Requests.SequenceEqual(native.Sounds.Skip(nativeSoundStart)),
                    $"Carpenter departure visit{update}: active={host.Npc.Active}/{native[at]:x2}, XY={host.Npc.Position}/{native[at + 13]},{native[at + 11]}, Z={SomariaPrivate<int>(host, "_z")}/{(short)native.Word(at + 14)}, speedZ={SomariaPrivate<int>(host, "_speedZ")}/{(short)native.Word(at + 0x14)}, found={_runtimeState.ReadWramByte(data.Constant("found-address")):x2}/{native[0xcfd1]:x2}, scratch={string.Join(',', Enumerable.Range(0,4).Select(i=>_runtimeState.ReadWramByte(0xcec0+i).ToString("x2")))}/{Convert.ToHexString(expected)}, sound={string.Join(',',sounds.Requests)}/{string.Join(',',native.Sounds.Skip(nativeSoundStart))}.");
                Seed();
            });
            Seed(); Step(4);
            _dialogue.ShowMessage("Departure remains enabled during text.", _player.Position.Y); native[0xcba0] = 1; Step(3);
            _dialogue.Close(); native[0xcba0] = 0; Step(9);
            FailIf(movements != 16 || host.Npc.Active || owner.HasState || owner.BlocksGameplay ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 || native[0xcc8a] != 0,
                "Carpenter must take16 native state2 steps from X=$08 through wrapped$ff..$f8, release control and stop moving after deletion.");
            // Retain Link's mask for this separate post-retirement boundary,
            // so unrelated newly-enabled Link helpers cannot replace scratch.
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1); native[0xcc8a] = 1;
            Step(2);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); native[0xcc8a] = 0;
        }
    }
}
