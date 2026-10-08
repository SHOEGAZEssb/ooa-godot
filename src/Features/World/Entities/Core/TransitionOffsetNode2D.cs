using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Centralizes the presentation-only screen-scroll offset used by drawable
/// room entities. Logical room/world positions remain unchanged.
/// </summary>
public abstract partial class TransitionOffsetNode2D : Node2D
{
    private Func<Vector2, Vector2> _worldToScreen = static position => position;
    private Func<int> _shadowCounter = static () => 0;
    private Func<int> _shadowSlot = static () => 0;
    private Func<bool> _shadowTransition = static () => false;
    private OracleRoomData? _shadowRoom;

    internal bool TerrainShadowDrawn =>
        this is ITerrainShadowSource source &&
        _shadowRoom is not null &&
        TerrainShadow.ShouldDraw(
            source.TerrainShadowZHigh is not null && this is EnemyCharacter { GaleCollisionDisabled: true } enemy
                ? enemy.GaleDrawZ : source.TerrainShadowZHigh, Visible,
            _shadowRoom.TilesetFlags, TerrainShadowCameraByte,
            _shadowCounter(), _shadowSlot());

    // The transition position helper passes screen yh+$10 in B; the ordinary
    // helper passes hCameraY. Both are unsigned byte comparisons with $97.
    private int TerrainShadowCameraByte => _shadowTransition()
        ? ((int)_worldToScreen(Position + TransitionDrawOffset).Y + 0x10) & 0xff
        : (-(int)_worldToScreen(Vector2.Zero).Y) & 0xff;

    internal void BindTerrainShadow(OracleRoomData room, Func<int> counter,
        Func<int> slot, Func<bool> transition)
    {
        if (this is not ITerrainShadowSource) return;
        _shadowRoom = room;
        _shadowCounter = counter;
        _shadowSlot = slot;
        _shadowTransition = transition;
        var shadow = new ObjectTerrainShadow();
        shadow.Initialize(() => TerrainShadowDrawn, () => SourceOamDrawOffset);
        AddChild(shadow);
    }

    public Vector2 TransitionDrawOffset { get; private set; }
    internal void SetTerrainShadowRoom(OracleRoomData room) => _shadowRoom = room;
    internal Vector2 SourceOamWrapOffset =>
        OracleObjectMath.SourceOamWrapOffset(_worldToScreen(Position));
    protected Vector2 SourceOamDrawOffset =>
        TransitionDrawOffset + SourceOamWrapOffset;

    internal void SetWorldToScreen(Func<Vector2, Vector2> worldToScreen)
    {
        _worldToScreen = worldToScreen ??
            throw new ArgumentNullException(nameof(worldToScreen));
        QueueRedraw();
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (TransitionDrawOffset.IsEqualApprox(offset))
            return;
        TransitionDrawOffset = offset;
        QueueRedraw();
    }
}
