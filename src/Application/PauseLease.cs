using System;

namespace oracleofages;
internal sealed class PauseLease : IDisposable
{
    private GameplayPauseController? _controller;
    internal object Owner { get; }
    internal bool PlayerProcessEnabled { get; }
    internal bool PlayerPhysicsProcessEnabled { get; }
    internal bool RoomDebugVisible { get; }

    internal PauseLease(GameplayPauseController controller, object owner, bool playerProcessEnabled, bool playerPhysicsProcessEnabled, bool roomDebugVisible)
    {
        _controller = controller;
        Owner = owner;
        PlayerProcessEnabled = playerProcessEnabled;
        PlayerPhysicsProcessEnabled = playerPhysicsProcessEnabled;
        RoomDebugVisible = roomDebugVisible;
    }

    public void Dispose()
    {
        GameplayPauseController? controller = _controller;
        if (controller is null)
            return;
        _controller = null;
        controller.Release(this);
    }
}
