using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// PART_WALL_ARROW_SHOOTER $25. An invisible native PART owns the cooldown;
// the arrow shares the ordinary enemy-arrow collision and bounce owner.
internal sealed partial class WallArrowShooterRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly WallArrowShooterProfile _data = EnemyBehaviorTables.Shared.WallArrowShooter;
    private readonly Func<EnemyArrowSpawn, bool> _shoot;
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal WallArrowShooterRoomEntity(RoomObjectRecord source, Func<EnemyArrowSpawn, bool> shoot)
    {
        if (source.Kind != RoomObjectKind.ReservingPart || source.Id != PartId.WallArrowShooter ||
            source.SubId is < 0 or > 3 || source.PackedPosition < 0)
            throw new NotSupportedException($"{source.Source}: unsupported PART$25 subid${source.SubId:x2} placement.");
        SubId = source.SubId; _shoot = shoot;
        Name = $"WallArrowShooter_{source.Order}_{SubId:x2}";
        Position = new((source.PackedPosition & 15) * 16 + 8, (source.PackedPosition >> 4) * 16 + 8);
        Visible = false;
    }
    public Node2D Node => this;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Update(frame.ScentSeedTarget ?? frame.Player.Position);
    private void Update(Vector2 target)
    {
        if (State == 0) { State = 1; Angle = SubId * 8; }
        if (Counter != 0) Counter--;
        if (Counter != 0) return;
        int axis = (SubId & 1) == 0 ? OracleObjectPosition.HighByte(Position.X) : OracleObjectPosition.HighByte(Position.Y);
        int targetAxis = (SubId & 1) == 0 ? OracleObjectPosition.HighByte(target.X) : OracleObjectPosition.HighByte(target.Y);
        if (unchecked((byte)(axis - targetAxis + _data.AlignmentRadius)) >= _data.AlignmentWidth) return;
        // Set before getFreePartSlot. A full pool drops this shot and still
        // waits the complete interval; it does not retry on the next update.
        Counter = _data.Interval;
        var offset = _data.SpawnOffsets[SubId];
        var point = new Vector2(unchecked((byte)(OracleObjectPosition.HighByte(Position.X) + offset.Second)),
            unchecked((byte)(OracleObjectPosition.HighByte(Position.Y) + offset.First)));
        _shoot(new(point, Angle, SubId: 1));
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("wallArrowShooter.s state0 requires the live enemy target during preload.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) Update(player?.Position ?? throw new InvalidOperationException("PART$25 preload requires Link."));
        return ScreenTransitionPresentation.Hidden;
    }
}
