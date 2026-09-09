using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSharedRoomEventHosts()
    {
        RoomEventContext context = ((ICutsceneCommandHost)_roomEvents.Get<HarpOfAgesEvent>()).Context;
        var host = new ValidationSharedRoomEventHost(context);
        ColorRect fade = context.Fade;
        var original = (fade.Position, fade.Size, fade.ZIndex, fade.Color);
        try
        {
            fade.Position = new Vector2(3, 7);
            fade.Size = new Vector2(80, 60);
            fade.ZIndex = 6;
            fade.Color = new Color(0.2f, 0.3f, 0.4f, 0.5f);
            var room = (fade.Position, fade.Size, fade.ZIndex, fade.Color);
            FailIf(!host.Capture(48) || fade.Position != Vector2.Zero ||
                fade.Size != new Vector2(160, 144) || fade.ZIndex != 48,
                "A full-screen event fade did not cover the viewport at its requested layer.");
            fade.Color = Colors.Black;
            FailIf(host.Capture(99) || fade.ZIndex != 48,
                "A second fade phase overwrote its original presentation capture.");
            host.Release();
            FailIf((fade.Position, fade.Size, fade.ZIndex, fade.Color) != room,
                "Event cancellation did not restore the captured rectangle and color.");

            // Nayru singing owns geometry only; its color and layer remain
            // under the native controller's control at the handoff.
            host.Capture();
            fade.ZIndex = 23;
            fade.Color = Colors.White;
            host.Release(color: false);
            FailIf(fade.Position != room.Position || fade.Size != room.Size ||
                fade.ZIndex != 23 || fade.Color != Colors.White,
                "Geometry-only fade cleanup restored a color or layer it did not own.");
            var handedOff = (fade.Position, fade.Size, fade.ZIndex, fade.Color);
            host.Release();
            FailIf((fade.Position, fade.Size, fade.ZIndex, fade.Color) != handedOff,
                "Repeated fade cleanup changed the new owner's presentation.");
            host.Capture(48);
            fade.Color = Colors.Black;
            host.Release();
            FailIf((fade.Position, fade.Size, fade.ZIndex, fade.Color) != handedOff,
                "A later fade cycle reused a stale capture.");

            _dialogue.Close();
            context.ShowChoiceDialogue("Shared event choice");
            _dialogue.SubmitChoiceForValidation(1);
            FailIf(host.TakeChoice() != 1, "The shared event host lost the completed choice.");
            bool rejected = false;
            try { host.TakeChoice(); }
            catch (InvalidOperationException error)
            {
                rejected = error.Message == "validation/shared-event: absent choice";
            }
            FailIf(!rejected, "The shared event host reused a consumed choice or lost its diagnostic.");
        }
        finally
        {
            host.Release();
            (fade.Position, fade.Size, fade.ZIndex, fade.Color) = original;
            _dialogue.Close();
        }
        GD.Print("Validated shared event fade ownership, repeated capture/cleanup, " +
            "geometry-only handoff, fresh captures, and consumed-choice diagnostics.");
    }
}
