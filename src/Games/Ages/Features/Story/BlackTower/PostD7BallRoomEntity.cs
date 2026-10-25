using Godot;
using System.Collections.Generic;

namespace Oracles;

/// <summary>Temporary INTERAC_BALL $95 uses the shared room $1:$49 flight.</summary>
internal sealed class PostD7BallRoomEntity : IRoomEntity, IFixedRoomEntity
{
    private readonly RoomEventContext _context;
    private readonly Room149Ball _ball = new();
    private bool _initialized;
    public Node2D Node => _ball;
    internal PostD7BallRoomEntity(RoomEventContext context)
    {
        _context = context;
        _ball.Initialize(new Room149FamilyDatabase().Visual("ball"));
        _ball.SetActive(true);
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _ball.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) { _initialized = true; return; }
        int signal = _context.Entities.RuntimeState.ReadWramByte(0xcfd3);
        if (signal != 0 && _ball.Idle)
        { _ball.Launch(signal); return; }
        bool moving = !_ball.Idle;
        _ball.UpdateFrame();
        if (moving && _ball.Idle) _context.Entities.RuntimeState.SetWramByte(0xcfd3, 0);
    }
}
