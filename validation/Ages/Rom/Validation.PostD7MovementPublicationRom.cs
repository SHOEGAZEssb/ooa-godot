using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePostD7MovementPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        // scripts.s: Ambi down$64/SPEED_080, down$40/SPEED_040,
        // boy left$21/SPEED_180; rabbitMain.s substate1 applies speed before Z.
        foreach (var spec in new[] {
            (Id: 0x4d, Sub: 3, Script: "ascent", Index: 2, Speed: 0x14, Angle: 0x10, Counter: 0x64),
            (Id: 0x4d, Sub: 3, Script: "ascent", Index: 4, Speed: 0x0a, Angle: 0x10, Counter: 0x40),
            (Id: 0x3c, Sub: 6, Script: "boy", Index: 25, Speed: 0x3c, Angle: 0x18, Counter: 0x21),
            (Id: 0x4b, Sub: 1, Script: "", Index: 0, Speed: 0x3c, Angle: 0x18, Counter: 0) })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33);
            _entities.Clear(); _inventory.SetScriptedEquippedItems(0, 0);
            var context = SomariaPrivate<RoomEventContext>(_roomEvents, "_context");
            var data = new PostD7EventDatabase(); var owner = _roomEvents.Get<PostD7Event>();
            var actor = new PostD7ActorRoomEntity(context, data, owner, spec.Id, spec.Sub, new(72, 56));
            _entities.AddEntity(actor); _player.WarpTo(new(40, 104));
            StepGameplayUpdates(1, Vector2.Zero);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 104)
                { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay();
            int at = 0xd040 + _entities.InteractionSlot(actor.Node) * 256;
            native[at] = 1; native[at + 1] = (byte)spec.Id; native[at + 2] = (byte)spec.Sub;
            native[at + 11] = 56; native[at + 13] = 72;
            native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            // Declare a post-setup motion boundary. Original handlers and
            // their category/text guards still own every compared update.
            Vector2 initial = spec.Id == 0x4d ? new(72.25f, 248.5f) : new(8.25f, 56.5f);
            F1Set(actor, "_precise", initial); actor.Node.Position = initial.Floor();
            native.Word(at + 10, (int)(initial.Y * 256)); native.Word(at + 12, (int)(initial.X * 256));
            native[at + 9] = (byte)spec.Angle; native[at + 0x10] = (byte)spec.Speed;
            var runner = SomariaPrivate<CutsceneCommandRunner>(actor, "_runner");
            if (spec.Counter != 0)
            {
                var commands = data.Commands(spec.Script);
                FailIf(commands[spec.Index] is not CutsceneMoveCommand move || move.Counter != spec.Counter || move.Angle != spec.Angle,
                    $"Post-D7 ${spec.Id:x2}: source movement boundary changed.");
                runner.Start(commands, spec.Index); runner.SetInitialMotionRegisters("Actor", spec.Speed, spec.Angle); runner.AdvanceFrame();
                native[at + 6] = 0; native[at + 7] = (byte)spec.Counter;
            }
            else
            {
                F1Set(actor, "<Substate>k__BackingField", 1); F1Set(actor, "_z", -0x800); F1Set(actor, "_speedZ", -0x180);
                native[at + 5] = 1; native.Word(at + 14, 0xf800); native.Word(at + 0x14, 0xfe80);
            }
            _player.BeginCutsceneControl(owner: actor); _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1); native[0xcc8a] = 1;
            byte[] observed = new byte[4], expected = new byte[4], sentinel = [0xa5, 0x5a, 0x3c, 0xc3];
            _entities.AddEntity(new PartMovementScratchProbe(() =>
            { for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i); }));
            void Seed()
            { for (int i = 0; i < 4; i++) { _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]); native[0xcec0 + i] = sentinel[i]; } }
            int update = 0, movements = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                var previous = new Vector2(native.Word(at + 12), native.Word(at + 10));
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); update++;
                if (native[at] != 0 && previous != new Vector2(native.Word(at + 12), native.Word(at + 10))) movements++;
                FailIf(!observed.SequenceEqual(expected) || actor.Finished != (native[at] == 0) ||
                    native[at] != 0 && (
                    actor.PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                    actor.Node.Position != new Vector2(native[at + 13], native[at + 11]) ||
                    actor.ZFixed != unchecked((short)native.Word(at + 14)) ||
                    spec.Counter != 0 && runner.Counter != native[at + 7]),
                    $"Post-D7 ${spec.Id:x2}:${spec.Sub:x2} speed${spec.Speed:x2} visit{update}: XY={actor.PrecisePosition}/{native.Word(at + 12) / 256f},{native.Word(at + 10) / 256f}, Z={actor.ZFixed}/{(short)native.Word(at + 14)}, counter={runner.Counter}/{native[at + 7]}, scratch={Convert.ToHexString(observed)}/{Convert.ToHexString(expected)}.");
                Seed();
            });
            Seed(); Step(4);
            _dialogue.ShowMessage("Post-D7 movement pause.", _player.Position.Y); native[0xcba0] = 1; Step(3);
            _dialogue.Close(); native[0xcba0] = 0;
            Step(spec.Counter == 0 ? 4 : spec.Counter - 4);
            FailIf(movements != (spec.Counter == 0 ? 6 : spec.Counter - 1), "Post-D7 movement comparison must observe every eligible motion, including the zero-counter yield or rabbit wrap/deletion.");
            _player.EndCutsceneControl(actor); _entities.Clear();
        }
    }
}
