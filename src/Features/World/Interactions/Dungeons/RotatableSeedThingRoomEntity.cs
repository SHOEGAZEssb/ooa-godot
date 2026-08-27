using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART_ROTATABLE_SEED_THING $33:$0a and its automatically created $03 child.
/// The parent watches the imported toggle-state mask and rotates one quarter
/// turn whenever either selected bit changes; the invisible child mirrors its
/// collision orientation twelve pixels below it.
/// </summary>
internal sealed partial class RotatableSeedThingRoomEntity :
    TransitionOffsetNode2D, IRoomEntity, IFixedRoomEntity,
    ISeedHittableRoomEntity, ISeedBounceTarget,
    ISeedPreMovementCollisionTarget
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRuntimeState _runtime;
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _rotationStep;
    private bool _initialized;
    private byte _lastMaskedState;

    public Node2D Node => this;
    internal int Orientation { get; private set; }
    internal int ToggleMask => _record.Parameter;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;
    public int SeedBounceOrientation => _animation.CurrentParameter;
    internal Vector2 CollisionRadii => Orientation switch
    {
        0 => new Vector2(4, 6),
        1 or 3 => new Vector2(4, 4),
        2 => new Vector2(6, 4),
        _ => throw new InvalidOperationException()
    };

    internal RotatableSeedThingRoomEntity(
        DungeonMechanicDatabaseRecord record,
        DungeonMechanicDatabase data,
        DungeonInteractionVisual visual,
        OracleRoomData room,
        OracleRuntimeState runtime,
        Func<long> animationTick)
    {
        if (record is not { Id: 0x33, SubId: 0x0a } ||
            record.Parameter == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _runtime = runtime;
        Position = Point(record.PackedPosition);
        Name = $"RotatableSeedThing_{record.Order}";
        ZIndex = NpcCharacter.BehindLinkZIndex;
        Orientation = (record.SubId >> 2) & 0x03;
        _rotationStep = (record.SubId & 0x80) != 0 ? -1 : 1;
        if ((record.SubId & 0x40) != 0)
            _rotationStep *= 2;

        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(Orientation);

        room.SetPositionTileAndCollision(
            Position,
            room.GetMetatile(Position),
            (byte)data.MoonlitOrbCollision,
            animationTick(),
            preserveRenderedTile: true);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        byte masked = (byte)(StateSource() & ToggleMask);
        if (!_initialized)
        {
            _initialized = true;
            _lastMaskedState = masked;
            return;
        }
        if (masked == _lastMaskedState)
            return;
        _lastMaskedState = masked;
        Orientation = (Orientation + _rotationStep) & 0x03;
        _animation.SetAnimation(Orientation);
        QueueRedraw();
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns) =>
        IntersectsSeed(hitbox) ? SeedHitResult.Bounce : SeedHitResult.None;

    public bool IntersectsSeed(Rect2 hitbox)
    {
        Vector2 radii = CollisionRadii;
        Vector2 size = radii * 2;
        return hitbox.Intersects(new Rect2(Position - radii, size)) ||
            hitbox.Intersects(new Rect2(
                Position + new Vector2(0, 12) - radii,
                size));
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (!Visible)
            return;
        Vector2 offset = _animation.CurrentOffset + TransitionDrawOffset;
        DrawTexture(CurrentTexture, offset);
    }

    private byte StateSource() => _runtime.ReadWramByte(
        OracleRuntimeState.ToggleBlocksStateAddress);

    private static Vector2 Point(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
