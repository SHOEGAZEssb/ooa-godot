using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSharedRoomEventHosts()
    {
        RoomEventContext context = _roomEvents.Get<HarpOfAgesEvent>().Context;
        var host = new RoomEventResources(context, new object());
        ColorRect fade = context.Fade;
        var original = (fade.Position, fade.Size, fade.ZIndex, fade.Color);
        try
        {
            fade.Position = new Vector2(3, 7);
            fade.Size = new Vector2(80, 60);
            fade.ZIndex = 6;
            fade.Color = new Color(0.2f, 0.3f, 0.4f, 0.5f);
            var room = (fade.Position, fade.Size, fade.ZIndex, fade.Color);
            FailIf(!host.CaptureFullScreenFade(48) || fade.Position != Vector2.Zero ||
                fade.Size != new Vector2(160, 144) || fade.ZIndex != 48,
                "A full-screen event fade did not cover the viewport at its requested layer.");
            fade.Color = Colors.Black;
            FailIf(host.CaptureFullScreenFade(99) || fade.ZIndex != 48,
                "A second fade phase overwrote its original presentation capture.");
            host.ReleaseFullScreenFade();
            FailIf((fade.Position, fade.Size, fade.ZIndex, fade.Color) != room,
                "Event cancellation did not restore the captured rectangle and color.");

            // Nayru singing owns geometry only; its color and layer remain
            // under the native controller's control at the handoff.
            host.CaptureFullScreenFade();
            fade.ZIndex = 23;
            fade.Color = Colors.White;
            host.ReleaseFullScreenFade(restoreColor: false);
            FailIf(fade.Position != room.Position || fade.Size != room.Size ||
                fade.ZIndex != 23 || fade.Color != Colors.White,
                "Geometry-only fade cleanup restored a color or layer it did not own.");
            var handedOff = (fade.Position, fade.Size, fade.ZIndex, fade.Color);
            host.ReleaseFullScreenFade();
            FailIf((fade.Position, fade.Size, fade.ZIndex, fade.Color) != handedOff,
                "Repeated fade cleanup changed the new owner's presentation.");
            host.CaptureFullScreenFade(48);
            fade.Color = Colors.Black;
            host.ReleaseFullScreenFade();
            FailIf((fade.Position, fade.Size, fade.ZIndex, fade.Color) != handedOff,
                "A later fade cycle reused a stale capture.");

            // Source disableinput may run repeatedly. Cleanup ownership follows
            // the latest writer; an outgoing owner cannot unlock its successor.
            var ralph = _roomEvents.Get<RalphPortalEvent>();
            var arrival = _roomEvents.Get<EnterPastEvent>();
            ralph.SetInputEnabled(false);
            FailIf(!_player.IsCutsceneControlOwner(ralph),
                "Ralph failed to acquire authoritative Player control.");
            arrival.SetInputEnabled(false);
            ralph.Cancel();
            FailIf(!_player.IsCutsceneControlOwner(arrival),
                "Outgoing Ralph cancellation released EnterPast's control.");
            arrival.Cancel();
            arrival.Cancel();
            FailIf(_player.CutsceneControlled,
                "EnterPast cancellation retained input control.");
            ralph.SetInputEnabled(false);
            ralph.Cancel();
            FailIf(_player.CutsceneControlled,
                "Direct Ralph cancellation depended on controller cleanup.");

            ICutsceneCommandHost nayru = _roomEvents.Get<NayruIntroEvent>();
            var source = new CutsceneCommandSource(
                "validation/real-host", "Nayru", 0, 37, "setdisabledobjects");
            nayru.SetActiveCommandSource(source);
            Reject(() => nayru.SetDisabledObjects(0x11), "set disabled objects");
            source = source with { Opcode = "disablemenu" };
            nayru.SetActiveCommandSource(source);
            Reject(() => nayru.SetMenuEnabled(false), "set menu");
            source = source with { Opcode = "showtext" };
            nayru.SetActiveCommandSource(source);
            Reject(() => nayru.ShowText(0x5600, "position probe", 0), "position");
            nayru.SetActiveCommandSource(null);

            static void Reject(Action action, string operation)
            {
                try { action(); }
                catch (InvalidOperationException error) when (
                    error.Message.Contains("validation/real-host") &&
                    error.Message.Contains("line 37") &&
                    error.Message.Contains(operation))
                {
                    return;
                }
                throw new InvalidOperationException(
                    $"Nayru silently accepted unsupported {operation}.");
            }

            _dialogue.Close();
            context.ShowChoiceDialogue("Shared event choice");
            _dialogue.SubmitChoiceForValidation(1);
            FailIf(host.RequireDialogueChoice("validation/shared-event: absent choice") != 1, "The shared event host lost the completed choice.");
            bool rejected = false;
            try { host.RequireDialogueChoice("validation/shared-event: absent choice"); }
            catch (InvalidOperationException error)
            {
                rejected = error.Message == "validation/shared-event: absent choice";
            }
            FailIf(!rejected, "The shared event host reused a consumed choice or lost its diagnostic.");
        }
        finally
        {
            host.ReleaseFullScreenFade();
            (fade.Position, fade.Size, fade.ZIndex, fade.Color) = original;
            _dialogue.Close();
        }
        GD.Print("Validated shared event fade ownership, repeated capture/cleanup, " +
            "geometry-only handoff, fresh captures, and consumed-choice diagnostics.");
    }
}
