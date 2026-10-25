using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaRevealPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl();
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1); _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled, 1);
            var context = _roomEvents.Get<MakuSeedEvent>().Context;
            var actor = new TwinrovaRevealRoomEntity(context, new MakuSeedDatabase(), 0x93, 2, entity => _entities.AddEntity(entity));
            _entities.AddEntity(actor);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcc6e] = 1;
            int at = 0xd040 + _entities.InteractionSlot(actor.Node) * 256;
            native[at] = 1; native[at + 1] = 0x93; native[at + 2] = 2;
            int updates = 0, movements = 0; byte[] expected = new byte[4];
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int beforeX = native.Word(at + 12), beforeY = native.Word(at + 10);
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); updates++;
                bool live = native[at] != 0;
                if (live && (beforeX != native.Word(at + 12) || beforeY != native.Word(at + 10))) movements++;
                FailIf(actor.Finished == live || live &&
                    (SomariaPrivate<Vector2>(actor, "_precise") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                        SomariaPrivate<int>(actor, "_substate") != native[at + 5] || SomariaPrivate<int>(actor, "_counter") != native[at + 6]) ||
                    _runtimeState.ReadWramByte(0xcfd5) != native[0xcfd5] || _runtimeState.ReadWramByte(0xcfd6) != native[0xcfd6] ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]),
                    $"Twinrova $93:$02 visit{updates}: native preset/counter/position/publication differs.");
                Seed();
            });
            Seed(); Step(5);
            _dialogue.ShowMessage("Twinrova keeps flying during text.", _player.Position.Y); native[0xcba0] = 1;
            Step(3); _dialogue.Close(); native[0xcba0] = 0;
            for (int limit = 0; native[at + 5] != 2 && limit < 160; limit += 2) Step(2);
            FailIf(native[at + 5] != 2, "Twinrova arrival must exhaust every source preset.");
            // Declare the post-dialogue departure boundary. Native helper loads
            // its own preset table; the unrelated dialogue script is isolated.
            SomariaPrivate<CutsceneCommandRunner>(actor, "_runner").Clear();
            F1Set(actor, "_substate", 3); F1Set(actor, "_presetIndex", 0);
            typeof(TwinrovaRevealRoomEntity).GetMethod("LoadPreset", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actor, null);
            native[at + 5] = 3; native[at + 0x3a] = 5; native[at + 7] = 0; native[0xffae] = 0x40;
            byte[] caller = [0x16, (byte)(at >> 8), 0xcd, 0xb4, 0x76, 0xc9]; // bank$0a twinrova_loadAngleAndCounterPreset
            for (int i = 0; i < caller.Length; i++) native[0xc100 + i] = caller[i];
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 0x0a); native[0xffae] = 0;
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 3); native[0xcc8a] = 3;
            for (int limit = 0; !actor.Finished && limit < 220; limit += 2) Step(2);
            Step(2);
            FailIf(!actor.Finished || movements < 160 || _runtimeState.ReadWramByte(0xcfd0) != 9 || native[0xcfd0] != 9,
                "Twinrova must complete both preset lists and publish09 on departure retirement.");
            _entities.Clear();
        }
    }

    private void ValidateDefeatedMoblinPublicationRom()
    {
        // Source scripts.s incoming registers; direction helpers change the
        // host's object angle while the generic runner can retain an old one.
        var cases = new[] { (0, 0, 7319, 0x3c, 0x10, 0), (2, 0, 7331, 0x14, 0, 0),
            (2, 0, 7334, 0x28, 0, 0), (2, 0, 7337, 0x28, 0x10, 2),
            (2, 1, 7342, 0x14, 0x18, 0), (2, 1, 7345, 0x28, 0x18, 0), (2, 1, 7348, 0x28, 8, 1),
            (2, 2, 7353, 0x14, 0, 0), (2, 2, 7356, 0x28, 0, 0), (2, 2, 7358, 0x28, 0x18, 3),
            (2, 2, 7362, 0x28, 8, 1), (2, 2, 7364, 0x28, 0x10, 2), (2, 3, 7369, 0x14, 0, 0),
            (2, 3, 7372, 0x28, 0, 0), (2, 3, 7382, 0x28, 0x10, 2) };
        foreach (var (subId, index, line, speed, angle, helper) in cases)
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(AgesGlobalFlag.MoblinsKeepDestroyed);
            _saveData.SetRoomFlag(0, 9, 0x40, false); LoadValidationRoom(0, 9);
            var owner = _roomEvents.Get<DefeatedMoblinEvent>(); owner.Cancel(); _entities.Clear();
            var npc = _entities.Spawn<NpcCharacter>(new DefeatedMoblinActorSpawn(owner.Database.Actor(subId, index, 8, 8)));
            npc.SetStatePosition(new(8.25f, 8.5f));
            var host = new DefeatedMoblinScriptHost(owner, npc, _entities.InteractionSlot(npc)) { Initialized = true, Angle = helper == 0 ? angle : index == 1 ? 0x18 : 0 };
            int runnerAngle = host.Angle;
            if (helper != 0) host.RunNativeHandler($"Direction{helper}");
            FailIf(host.Angle != angle, $"Defeated Moblin source direction helper{helper} at line{line} changed.");
            SomariaPrivate<List<DefeatedMoblinScriptHost>>(owner, "_actors").Add(host); F1Set(owner, "_active", true);
            var original = owner.Database.Commands.Single(command => command.Source.SourceLine == line);
            var command = original with { Source = original.Source with { CommandIndex = 0 } };
            int frames = ((CutsceneApplySpeedCommand)command).Counter;
            host.Runner.Start([command, new CutsceneWaitCommand(command.Source with { CommandIndex = 1, Opcode = "wait" }, 0xff),
                new CutsceneEndCommand(command.Source with { CommandIndex = 2, Opcode = "scriptend" })]);
            host.Runner.SetInitialMotionRegisters("Actor", speed, runnerAngle);
            _player.WarpTo(new(120, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1); _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled, 1);
            var trace = new ScriptMovementPublicationTrace(_runtimeState, source => source == command.Source ? (speed, angle, npc.Position) : null);
            _roomEvents.CommandTraceSink = trace;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            int before = trace.MotionVisits; _dialogue.ShowMessage("Defeated Moblin movement hold.", _player.Position.Y);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched); _dialogue.Close();
            FailIf(trace.MotionVisits != before, "Defeated Moblin ordinary text must retain its movement counter.");
            StepGameplayUpdates(frames + 2, Vector2.Zero, batched: batched);
            FailIf(trace.MotionVisits != frames - 1 || trace.Visited.Count != 1,
                $"Defeated Moblin line{line}: source counter must execute every native movement/publication.");
            _roomEvents.CommandTraceSink = null; owner.Cancel(); _entities.Clear();
        }
    }
}
