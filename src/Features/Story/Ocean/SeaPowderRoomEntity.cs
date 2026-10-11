using Godot;
using System.Collections.Generic;

namespace oracleofages;

// PART_SPARKLE $26: byte carry into Z, and a signed accelerating X component.
internal sealed partial class SeaPowderRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly SeaFairyDatabase _data;
    private readonly OracleRandom _random;
    private readonly bool _powder;
    private readonly EnemyAnimationPlayer _animation;
    private int _x, _z, _acceleration, _speedX, _speedZ, _counter, _reload;
    private bool _initialized;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int Height => (sbyte)(_z >> 8);
    internal SeaPowderRoomEntity(SeaPowderSpawn spawn, OracleRandom random)
    {
        _data = spawn.Data; _random = random; _powder = spawn.Powder; Position = spawn.Position;
        _x = (int)Position.X << 8;
        var visual = _data.Visual("powder", Position);
        _animation = new(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{visual.SpriteName}.png"),
            [visual.DownAnimation], visual.TileBase, visual.Palette);
        _animation.SetAnimation(0); Visible = false;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true; _z = ((_powder ? -16 : -(int)Position.Y - 8) & 255) << 8;
            var motion = _data.PowderMotion[(_random.Next().Value & 0x0c) >> 2];
            _acceleration = motion.Acceleration; _speedZ = motion.SpeedZ; _counter = motion.Counter;
            _reload = (_counter - 1) * 2;
            Visible = true; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex; return;
        }
        if (_counter != 0) _counter--;
        if (_counter == 0) { _counter = _reload; _acceleration = -_acceleration; }
        _x = (_x + _speedX) & 0xffff;
        _speedX = (short)(_speedX + _acceleration);
        Position = new(_x >> 8, Position.Y);
        _z = (_z + _speedZ) & 0xffff;
        if ((_z >> 8) == 0) { Finished = true; Visible = false; return; }
        if ((_z >> 8) >= 0xe8) Visible = !Visible;
        _animation.Advance(); QueueRedraw();
    }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(_animation.CurrentTexture,
            _animation.CurrentOffset + new Vector2(0, Height) + TransitionDrawOffset);
    }
}

internal sealed record SeaPowderSpawn(SeaFairyDatabase Data, Vector2 Position, bool Powder = false) : RoomEntitySpawn;
