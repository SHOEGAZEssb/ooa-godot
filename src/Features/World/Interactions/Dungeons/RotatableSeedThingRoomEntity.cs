using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART$33 timed and toggle-driven parents. Their $03 children are allocated
/// separately so native pool limits and update order remain observable.
/// </summary>
internal sealed partial class RotatableSeedThingRoomEntity :
    TransitionOffsetNode2D, IRoomEntity, IFixedRoomEntity,
    ISeedHittableRoomEntity, ISeedBounceTarget,
    ISeedHeightAwareHittableRoomEntity,
    ISeedPreMovementCollisionTarget, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRuntimeState _runtime;
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _rotationStep;
    private readonly Vector2 _childOffset;
    private readonly int _childZ;
    private bool _initialized;
    private bool _loadedProperties;
    private byte _lastMaskedState;
    private readonly int _period;
    private readonly Action _initializeTile;
    private readonly Func<RotatableSeedThingRoomEntity,Vector2,int,bool> _createChild;
    internal int Counter { get; private set; }
    internal bool Initialized => _initialized;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    public Node2D Node => this;
    internal int Orientation { get; private set; }
    internal int ToggleMask => _record.Parameter;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;
    public int SeedBounceOrientation => _animation.CurrentParameter;
    internal Vector2 CollisionRadii => !_loadedProperties ? Vector2.Zero : Orientation switch
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
        Func<long> animationTick,
        Func<RotatableSeedThingRoomEntity,Vector2,int,bool> createChild)
    {
        if (record.Id != InteractionId.SmogBoss || record.SubId is not (0x0a or 0x08 or 0x88) ||
            (record.SubId & 3) == 2 && record.Parameter == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _runtime = runtime;
        _createChild = createChild;
        _period = data.SeedBouncerPeriod * ((record.SubId & 0x40) != 0 ? 2 : 1);
        _childOffset = new Vector2(
            data.SeedBouncerChildX, data.SeedBouncerChildY);
        _childZ = data.SeedBouncerChildZ;
        Position = Point(record.PackedPosition);
        Name = $"RotatableSeedThing_{record.Order}";
        ZIndex = NpcCharacter.BehindLinkZIndex;
        Visible = false;
        _rotationStep = (record.SubId & 0x80) != 0 ? -1 : 1;
        if ((record.SubId & 3) == 2 && (record.SubId & 0x40) != 0)
            _rotationStep *= 2;

        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(Orientation);

        _initializeTile = () => room.SetPositionTileAndCollision(
            Position,
            (byte)data.SeedBouncerBackgroundTile,
            (byte)data.SeedBouncerTileCollision,
            animationTick(),
            preserveRenderedTile: true);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // updateParts still dispatches state0 under wScrollMode=$08.
        if (!_initialized) Initialize();
        return ScreenTransitionPresentation.Visible;
    }

    private void Initialize()
    {
        _loadedProperties = true;
        Counter = _period;
        Orientation = (_record.SubId >> 2) & 3;
        _animation.SetAnimation(Orientation);
        _initializeTile();
        Visible = true;
        _initialized = _createChild(this,_childOffset,_childZ);
        _lastMaskedState = (byte)(StateSource() & ToggleMask);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        byte masked = (byte)(StateSource() & ToggleMask);
        if (!_initialized)
        {
            Initialize();
            return;
        }
        if ((_record.SubId & 3) == 0)
        {
            if (Counter != 0) Counter--;
            if (Counter != 0) return;
            Counter = _period;
            Orientation = (Orientation + _rotationStep) & 3;
            _animation.SetAnimation(Orientation);
            QueueRedraw();
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
        return parentHit
            ? SeedHitResult.Bounce
            : SeedHitResult.None;
    }

    // Shooter seeds remain at Z 0. The source child is at Z $f2 (-14), so it
    // fails the ordinary +/-7 item overlap and must not extend their target.
    public bool IntersectsSeed(Rect2 hitbox) =>
        SourceCollisionIntersects(hitbox, Position, CollisionRadii);

    internal static bool SourceCollisionIntersects(
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
