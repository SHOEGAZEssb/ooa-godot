
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckRetiredMenuCallbacks(GameplaySession session, OracleSoundEngine sound,
        ValidationSoundRequestAudit sounds, Action gameOver)
    {
        var save=session.Rooms.SaveData;
        byte[] bytes=save.Serialize();
        int requests=sounds.Requests.Count;
        int active=sound.NativeActiveMusic, room=sound.NativeRoomMusic;
        Exception? failure=null;
        try
        {
            gameOver(); gameOver();
            FailIf(session.Menus.Pause.TryAcquire(session) is not null,
                $"{save.Game}: a retired pause owner admitted another lease.");
            FailIf(session.Menus.AdvanceNormal(ApplicationFixedUpdateScheduler.UpdateDelta,true,true,true),
                $"{save.Game}: retired menus claimed an original update.");
            using var input=new InputEventKey {PhysicalKeycode=Key.F6,Pressed=true};
            FailIf(session.Menus.HandleInput(input),$"{save.Game}: retired menus handled development input.");
            session.Menus.Dispose(); session.Menus.Pause.Dispose();
        }
        catch (Exception error) { failure=error; }
        FailIf(failure is not null || !bytes.SequenceEqual(save.Serialize()) || sounds.Requests.Count != requests ||
            sound.NativeActiveMusic != active || sound.NativeRoomMusic != room || session.Menus.Pause.IsLeased || session.Menus.TransitionActive,
            $"{save.Game}: retired menu callbacks must not touch UI, save, audio or pause ownership: {failure?.Message ?? "side effects"}.");
    }
}
