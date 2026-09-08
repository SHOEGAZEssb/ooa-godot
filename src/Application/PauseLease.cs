using System;

namespace oracleofages;
internal sealed class PauseLease : IDisposable
{
    private GameplayPauseController? _controller;
    internal object Owner { get; private set; }
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

    internal void Transfer(object owner)
    {
        if (_controller is null) throw new InvalidOperationException("Cannot transfer a released pause lease.");
        Owner = owner;
    }
}
