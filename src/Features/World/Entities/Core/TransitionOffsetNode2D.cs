using Godot;
using System;

namespace Oracles;

/// <summary>
/// Centralizes the presentation-only screen-scroll offset used by drawable
/// room entities. Logical room/world positions remain unchanged.
/// </summary>
public abstract partial class TransitionOffsetNode2D : Node2D
{
    internal string AssetRoot { get; set; } = string.Empty;
    private Func<Vector2, Vector2> _worldToScreen = static position => position;
    private Func<bool> _usesTransitionProjection = static () => false;
    private Func<int> _shadowCounter = static () => 0;
    private Func<int> _shadowSlot = static () => 0;
    private Func<bool> _shadowTransition = static () => false;
    private OracleRoomData? _shadowRoom;
    private OracleRuntimeState _shadowMemory = null!;
    private LinkTerrainEffectDatabase? _groundTerrainEffects;

    private protected LinkTerrainEffectFrame? GetGroundTerrainEffect(int? zHigh)
    {
        // bank0.s:_drawObjectTerrainEffects samples the object's ground
        // position, five pixels below yh, before applying its draw height.
        if (!Visible || zHigh is null || ((byte)zHigh.Value & 0x80) != 0 ||
            _shadowRoom is null || (_shadowRoom.TilesetFlags & 0x20) != 0 ||
            TerrainShadowCameraByte >= 0x97 || _shadowTransition())
            return null;
        int xh = (int)Position.X & 0xff;
        int yh = (int)Position.Y & 0xff;
        byte position = (byte)(((yh + 5) & 0xf0) | (xh >> 4));
        _groundTerrainEffects ??= LinkTerrainEffectDatabase.For(AssetRoot);
        return _groundTerrainEffects.FrameFor(
            _shadowRoom.ReadNativeLayoutByte(position, _shadowMemory),
            new Vector2(xh, yh), _shadowCounter());
    }

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
        Func<int> slot, Func<bool> transition, OracleRuntimeState memory)
    {
        if (this is not ITerrainShadowSource) return;
        _shadowRoom = room;
        _shadowMemory = memory;
        _shadowCounter = counter;
        _shadowSlot = slot;
        _shadowTransition = transition;
        var shadow = new ObjectTerrainShadow();
        shadow.Initialize(AssetRoot, () => TerrainShadowDrawn, () => SourceOamDrawOffset);
        AddChild(shadow);
    }

    public Vector2 TransitionDrawOffset { get; private set; }
    internal Vector2 SourceScreenPosition => _worldToScreen(Position);
    internal void SetTerrainShadowRoom(OracleRoomData room) => _shadowRoom = room;
    internal Vector2 SourceOamWrapOffset =>
        // bank0's scroll projection retains 16-bit coordinates before culling.
        _usesTransitionProjection() ? Vector2.Zero :
            OracleObjectMath.SourceOamWrapOffset(SourceScreenPosition);
    protected Vector2 SourceOamDrawOffset =>
        TransitionDrawOffset + SourceOamWrapOffset;

    internal void SetWorldToScreen(Func<Vector2, Vector2> worldToScreen, Func<bool> usesTransitionProjection)
    {
        _worldToScreen = worldToScreen ??
            throw new ArgumentNullException(nameof(worldToScreen));
        _usesTransitionProjection = usesTransitionProjection ??
            throw new ArgumentNullException(nameof(usesTransitionProjection));
        QueueRedraw();
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        // Projection mode can change at scroll boundaries with a zero offset.
        TransitionDrawOffset = offset;
        QueueRedraw();
    }
}
