using Godot;

namespace oracleofages;

/// <summary>
/// ITEM_BRACELET ($16) child used for a lifted metatile. While held it is a
/// child of Link and uses updateGrabbedObjectPosition's weight-0 offsets.
/// Once released, the controller reparents it to world space and advances the
/// original 8.8 vertical motion.
/// </summary>
internal partial class BraceletLiftedObject : Node2D
{
    private Texture2D _texture = null!;
    private int _zFixed;
    private int _speedZ;

    internal int GroundX { get; private set; }
    internal int GroundY { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int SpeedRaw { get; private set; }
    internal Texture2D Texture => _texture;
    internal Vector2I ThrowDirection { get; private set; }
    internal bool Thrown { get; private set; }

    internal void Initialize(Texture2D texture)
    {
        _texture = texture;
        ZIndex = 11;
        QueueRedraw();
    }

    internal void SetHeldOffset(Vector2I offset)
    {
        Position = offset;
        QueueRedraw();
    }

    internal void Release(
        Node worldRoot,
        Vector2 groundPosition,
        int zFixed,
        int speedZ,
        Vector2I direction,
        int speedRaw)
    {
        Reparent(worldRoot, keepGlobalTransform: true);
        GroundX = Mathf.RoundToInt(groundPosition.X * 256.0f);
        GroundY = Mathf.RoundToInt(groundPosition.Y * 256.0f);
        _zFixed = zFixed;
        _speedZ = speedZ;
        ThrowDirection = direction;
        SpeedRaw = speedRaw;
        Thrown = true;
        SyncPosition();
    }

    internal void AdvanceLateral()
    {
        GroundX += ThrowDirection.X * SpeedRaw * 256 / 40;
        GroundY += ThrowDirection.Y * SpeedRaw * 256 / 40;
        SyncPosition();
    }

    internal bool AdvanceVertical(int gravity)
    {
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, gravity);
        SyncPosition();
        return landed;
    }

    internal Vector2 GroundPosition =>
        new(GroundX / 256.0f, GroundY / 256.0f);

    internal Rect2 CollisionBounds(int radiusX, int radiusY)
    {
        // checkObjectCollisions reads Item.yh/xh for the planar test and
        // compares Item.zh separately. The sprite's Z draw offset is not part
        // of its room-space collision center.
        Vector2 center = OracleObjectMath.ToPixelPosition(GroundPosition);
        return new Rect2(
            center - new Vector2(radiusX, radiusY),
            new Vector2(radiusX * 2, radiusY * 2));
    }

    public override void _Draw()
    {
        DrawTexture(_texture, new Vector2(-8, -8));
    }

    private void SyncPosition()
    {
        Vector2 ground = OracleObjectMath.ToPixelPosition(GroundPosition);
        Position = ground + new Vector2(0, _zFixed >> 8);
        ZIndex = 10;
        QueueRedraw();
    }
}
