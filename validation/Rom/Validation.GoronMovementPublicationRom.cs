using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGoronScriptPublicationRom()
    {
        // scripts.s source command lines, with independently traced incoming
        // motion registers. scriptHelper.s:5259 has the final half-speed move.
        var cases = new[] { (5579, 0x50, 0x18, 0x11), (5586, 0x50, 8, 0x11),
            (5605, 0x14, 0x18, 0), (5782, 0x28, 0x10, 0), (5792, 0x28, 8, 0),
            (5821, 0x28, 0x18, 0), (5830, 0x28, 0, 0), (5995, 0x28, 0, 0x11),
            (6004, 0x28, 0, 0x11), (6010, 0x28, 8, 0x11), (6010, 0x28, 0x18, 0x11),
            (5259, 0x14, 0x18, 0x21) };
        foreach (var (line, speed, angle, frames) in cases)
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0xc3);
            var owner = _roomEvents.Get<GoronCaveEvent>();
            var host = owner.Actors.First(actor => actor.Actor.Record.SubId >= 3 && actor.Actor.Record.Id == 0x66);
            foreach (var other in owner.Actors.Where(actor => actor != host)) other.Cancel();
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(entity => ReferenceEquals(entity.Node, host.Actor)));
            F1Set(host, "_initialized", true); F1Set(host, "_position", new Vector2(8.25f, 8.5f));
            F1Set(host, "_speed", speed); F1Set(host, "_angle", angle); host.Actor.SetStatePosition(new(8, 8));
            var command = host.Database.Commands.Single(command => command.Source.SourceLine == line &&
                (frames == 0 ? command is CutsceneNativeCommand { Handler: "objectApplySpeed" } : command is CutsceneNativeBlockingCommand));
            if (command is CutsceneNativeBlockingCommand blocking)
                FailIf(blocking.Frames != frames, $"Goron source line{line} counter changed.");
            host.StartCommands([command with { Source = command.Source with { CommandIndex = 0 } },
                new CutsceneWaitCommand(command.Source with { CommandIndex = 1, Opcode = "wait" }, 0xff),
                new CutsceneEndCommand(command.Source with { CommandIndex = 2, Opcode = "scriptend" })]);
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1);
            var native = new ObjectMotionRom(); const int at = ObjectMotionRom.Object + 0x40;
            native.Word(at + 10, 0x0880); native.Word(at + 12, 0x0840);
            native[at + 9] = (byte)angle; native[at + 0x10] = (byte)speed;
            int update = 0, movements = 0;
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count, bool text = false) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                if (!text)
                {
                    if (frames == 0 ? update == 0 : update > 0 && update < frames)
                    { native.Call(ObjectMotionRom.ApplySpeed, slot: 0x40); movements++; }
                    update++;
                }
                FailIf(SomariaPrivate<Vector2>(host, "_position") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != native[0xcec0 + i]),
                    $"Goron source line{line} visit{update}: native motion/counter/freeze/publication differs.");
                Seed();
            });
            Seed(); Step(1);
            _dialogue.ShowMessage("Goron movement hold.", _player.Position.Y); Step(3, true); _dialogue.Close();
            Step(frames == 0 ? 2 : frames + 2);
            FailIf(movements != (frames == 0 ? 1 : frames - 1), $"Goron line{line} did not exercise its native motion count.");
            owner.Cancel(); _entities.Clear();
        }
    }

    private void ValidateGoronDebrisPublicationRom()
    {
        foreach (bool punching in new[] { false, true })
        foreach (bool stop in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0xc3);
            var owner = _roomEvents.Get<GoronCaveEvent>();
            foreach (var host in owner.Actors) host.Cancel();
            // Keep the real event owner enrolled, but isolate this declared
            // first-child/pattern0 boundary from its creation/RNG protocol.
            owner.CreateDebris(new(72.25f, 64.5f), punching);
            var rocks = SomariaPrivate<List<GoronRock>>(owner, "_rocks");
            var actor = rocks[0].Actor; int slot = rocks[0].Slot;
            foreach (var other in rocks.Skip(1)) other.Actor.SetActive(false);
            rocks.Clear();
            var rock = new GoronRock(actor, new(72.25f, 64.5f), punching ? 0x1a : 4,
                punching ? 0x28 : 0x3c, punching ? -0xe4 : -0xe8, 0, false, slot, _runtimeState);
            rocks.Add(rock);
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(entity => ReferenceEquals(entity.Node, actor)));
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1); _runtimeState.SetWramByte(0xcfde, 0);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcfde] = 0;
            int at = 0xd040 + slot * 256;
            native[at] = 1; native[at + 1] = 0x92; native[at + 2] = 2; native[at + 3] = punching ? (byte)1 : (byte)0;
            native.Word(at + 10, 0x4080); native.Word(at + 12, 0x4840);
            int update = 0, movements = 0; byte[] expected = new byte[4];
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int beforeY = native.Word(at + 10), beforeX = native.Word(at + 12);
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); update++;
                if (native[at] != 0 && (native.Word(at + 10) != beforeY || native.Word(at + 12) != beforeX)) movements++;
                FailIf(actor.Active != (native[at] != 0) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]) ||
                    actor.Active && (rock.Position != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                        SomariaPrivate<int>(rock, "<z>P") != unchecked((short)native.Word(at + 14)) ||
                        SomariaPrivate<int>(rock, "<speedZ>P") != unchecked((short)native.Word(at + 0x14))),
                    $"Goron debris $92:$02 visit{update}: native init/gravity/planar/publication/retirement differs.");
                Seed();
            });
            Seed(); Step(4);
            _dialogue.ShowMessage("Debris keeps moving during text.", _player.Position.Y); native[0xcba0] = 1;
            Step(3); _dialogue.Close(); native[0xcba0] = 0;
            if (stop) { _runtimeState.SetWramByte(0xcfde, 1); native[0xcfde] = 1; }
            for (int limit = 0; actor.Active && limit < 60; limit += 2) Step(2);
            Step(2);
            FailIf(actor.Active || movements < 5, "Debris must exercise airborne motion and source retirement.");
            owner.Cancel(); _entities.Clear();
        }
    }
}
