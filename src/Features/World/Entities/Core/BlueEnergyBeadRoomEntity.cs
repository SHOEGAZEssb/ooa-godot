using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>PART_BLUE_ENERGY_BEAD $53, inward swirl (var32=0).</summary>
internal sealed partial class BlueEnergyBeadRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, INativePartHealthRoomEntity
{
    private readonly OracleRandom _random;
    private readonly OracleRuntimeState _runtime;
    private readonly Vector2I _center;
    private OracleObjectPosition _position;
    internal int Index { get; }
    internal bool Initialized { get; private set; }
    internal int Delay { get; private set; }
    internal byte Duration { get; private set; }
    internal Vector2 PrecisePosition => _position.PrecisePosition;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    // State0 sets enabled bit7: updateParts retains these even under text,
    // scroll, or wDisabledObjects. State0 itself is always eligible too.
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void ClearHealthAndCollision() { } // partCode53 never checks status.

    internal BlueEnergyBeadRoomEntity(int index, Vector2 center, byte duration,
        DungeonInteractionVisual visual, OracleRandom random, OracleRuntimeState runtime)
    {
        Index = index; Duration = duration; _random = random; _runtime = runtime;
        _center = new(OracleObjectPosition.HighByte(center.X), OracleObjectPosition.HighByte(center.Y));
        InitializeVisual(visual, Vector2.Zero, index);
        Name = $"BlueEnergyBead_53_{index:x2}";
        ZIndex = NpcCharacter.FixedHighPriorityZIndex; // objectSetVisible preserves low bits0.
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        var data = BlueEnergyBeadDatabase.Shared;
        if (!Initialized)
        {
            Initialized = true;
            _runtime.SetWramByte(data.DeleteAddress, 0);
            ResetDelay();
            return;
        }
        if (_runtime.ReadWramByte(data.DeleteAddress) != 0 ||
            Duration != 0xff && --Duration == 0)
        {
            Finished = true; Visible = false; return;
        }
        if (!Visible)
        {
            if (--Delay != 0) return;
            SetAnimation(Index);
            // objectSetPositionInCircleArc writes only xh/yh. Fractions from
            // the previous flight survive; the initial allocation has zeroes.
            var offset = OracleObjectMovement.Shared.CircleArcOffset(data.Radius, Index * 4);
            _position = new(
                (ushort)((((_center.Y + offset.Y) & 255) << 8) | (_position.YFixed & 255)),
                (ushort)((((_center.X + offset.X) & 255) << 8) | (_position.XFixed & 255)));
            Position = _position.PixelPosition;
            Visible = true;
            return;
        }
        _position = OracleObjectMovement.Shared.ApplySpeed(_position, data.Speed, (Index * 4) ^ 0x10);
        Position = _position.PixelPosition;
        AdvanceAnimation();
        if (AnimationParameter == 0xff) { Visible = false; ResetDelay(); }
        QueueRedraw();
    }

    private void ResetDelay() => Delay = (_random.Next().Value & BlueEnergyBeadDatabase.Shared.DelayMask) + 1;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}

internal sealed record BlueEnergyBeadSpawn(int Index, Vector2 Center, byte Duration) : RoomEntitySpawn;
