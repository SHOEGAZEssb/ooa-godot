using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ITEM_SWORD_BEAM ($27). State 0 performs setup without movement; subsequent
/// 60 Hz updates apply damage, move at SPEED_300, test tiles, flip palette on
/// global four-update boundaries, and enforce the original screen boundary.
/// </summary>
public partial class SwordBeamEffect : TransitionOffsetNode2D
{
    private SwordBeamDatabase _database = null!;
    private SwordBeamDatabaseRecord _record;
    private OracleRoomData _room = null!;
    private Func<Vector2, Vector2> _worldToScreen = null!;
    private Vector2 _precisePosition;
    private bool _initialized;
    private int _palettePhase;

    public bool Finished { get; private set; }
    internal bool CollisionEnabled => _initialized && !Finished;
    internal int Damage => _record.Damage;
    internal int PalettePhase => _palettePhase;
    internal Vector2 PrecisePosition => _precisePosition;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_record.RadiusX, _record.RadiusY),
        new Vector2(_record.RadiusX * 2, _record.RadiusY * 2));

    internal void Initialize(
        SwordBeamDatabase database,
        OracleRoomData room,
        Vector2 linkPosition,
        int direction,
        Func<Vector2, Vector2> worldToScreen,
        Action<int> playSound)
    {
        _database = database;
        _record = database.Get(direction);
        _room = room;
        _worldToScreen = worldToScreen;
        _precisePosition = linkPosition +
            new Vector2(_record.OffsetX, _record.OffsetY);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        Visible = false;
        playSound(_record.Sound);
        QueueRedraw();
    }

    internal void UpdateFrame(int globalFrame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
            QueueRedraw();
            return;
        }

        Vector2 direction = _record.Direction switch
        {
            0 => Vector2.Up,
            1 => Vector2.Right,
            2 => Vector2.Down,
            _ => Vector2.Left
        };
        _precisePosition += direction * (_record.SpeedRaw / 40.0f);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);

        if (_room.IsSolid(Position))
        {
            Collide(spawns);
            return;
        }

        if ((globalFrame & 3) == 0)
            _palettePhase ^= 1;

        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(
                _worldToScreen(Position)))
        {
            Finish();
            return;
        }
        QueueRedraw();
    }

    internal void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) =>
        Collide(spawns);

    public override void _Draw()
    {
        if (!Finished && Visible)
        {
            DrawTexture(
                _database.Texture(_record.Direction, _palettePhase),
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private void Collide(ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        spawns.Add(new SwordBeamClinkSpawn(Position));
        Finish();
    }

    private void Finish()
    {
        Finished = true;
        Visible = false;
        QueueRedraw();
    }
}
