namespace oracleofages;

internal sealed class ValidationSharedRoomEventHost(RoomEventContext context) : RoomEventHost
{
    protected override RoomEventContext EventContext => context;
    internal bool Capture(int? layer = null) => CaptureFullScreenFade(layer);
    internal void Release(bool color = true) => ReleaseFullScreenFade(color);
    internal int TakeChoice() => RequireDialogueChoice("validation/shared-event: absent choice");
}
