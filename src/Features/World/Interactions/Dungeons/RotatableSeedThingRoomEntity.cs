using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART_ROTATABLE_SEED_THING $33:$0a and its automatically created $03 child.
/// The parent watches the imported toggle-state mask and rotates one quarter
/// turn whenever either selected bit changes. The child mirrors its collision
/// orientation at Y+$0c and Z=$f2, so it does not extend the ground-level
/// seed-shooter target below the visible parent.
/// </summary>
internal sealed partial class RotatableSeedThingRoomEntity :
    TransitionOffsetNode2D, IRoomEntity, IFixedRoomEntity,
    ISeedHittableRoomEntity, ISeedBounceTarget,
    ISeedHeightAwareHittableRoomEntity,
    ISeedPreMovementCollisionTarget
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRuntimeState _runtime;
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _rotationStep;
    private readonly Vector2 _childOffset;
    private readonly int _childZ;
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
        _childOffset = new Vector2(
            data.SeedBouncerChildX, data.SeedBouncerChildY);
        _childZ = data.SeedBouncerChildZ;
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
            (byte)data.SeedBouncerBackgroundTile,
            (byte)data.SeedBouncerTileCollision,
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
        ICollection<RoomEntitySpawn> spawns) => ApplySeedHitAtHeight(
            hitbox, sourcePosition, sourceZ: 0, seedItem, spawns);

    public SeedHitResult ApplySeedHitAtHeight(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int sourceZ,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        Vector2 radii = CollisionRadii;
        bool parentHit = RoomEntityManager.ObjectCollisionZOverlaps(
                targetZ: 0, sourceZ, radius: 7) &&
            SourceCollisionIntersects(hitbox, Position, radii);
        bool childHit = RoomEntityManager.ObjectCollisionZOverlaps(
                _childZ, sourceZ, radius: 7) &&
            SourceCollisionIntersects(hitbox, Position + _childOffset, radii);
        return parentHit || childHit
            ? SeedHitResult.Bounce
            : SeedHitResult.None;
    }

    // Shooter seeds remain at Z 0. The source child is at Z $f2 (-14), so it
    // fails the ordinary +/-7 item overlap and must not extend their target.
    public bool IntersectsSeed(Rect2 hitbox) =>
        SourceCollisionIntersects(hitbox, Position, CollisionRadii);

    private static bool SourceCollisionIntersects(
        Rect2 itemBounds,
        Vector2 partPosition,
        Vector2 partRadii)
    {
        // checkObjectsCollidedFromVariables adds the radii in byte arithmetic
        // before comparing against twice their sum. For item - part, the
        // accepted interval is [-sum, sum): the upper/left touching edge is a
        // collision, while the lower/right touching edge is not. Rect2's
        // symmetric edge exclusion loses the valid approach beside solid
        // bouncer tiles in rooms such as 4:4e.
        Vector2 sums = itemBounds.Size / 2 + partRadii;
        Vector2 delta = itemBounds.GetCenter() - partPosition;
        return delta.Y >= -sums.Y && delta.Y < sums.Y &&
            delta.X >= -sums.X && delta.X < sums.X;
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
