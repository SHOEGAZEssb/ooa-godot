using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class WaterfallWarpRoomEntity(WaterfallWarpRecord record,
    OracleSaveData save, OracleRuntimeState runtime, Func<bool> riding, Action<Warp> warpRequested)
    : Node2D, IRoomEntity, IFixedRoomEntity, IPlayerRestriction, IRoomEntityLifetime
{
    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int State { get; private set; }
    public bool DisablesSword => false;
    public bool DisablesScreenTransitions => !Finished && record.Subid == 2;
    public bool DisablesWarpTiles => !Finished && record.Subid == 1 && State == 2;
    public void SetTransitionDrawOffset(Vector2 offset) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        Player player = frame.Player;
        Vector2 position = riding() && CompanionRuntimeState.AnyActive(runtime)
            ? CompanionRuntimeState.Read(runtime).Position : player.Position;
        bool colliding = !player.IsDying && !player.IsCarryingObject && player.AcceptsRoomEntityContact &&
            Math.Abs(position.X - record.Position.X) < record.Radii.X + 6 &&
            Math.Abs(position.Y - record.Position.Y) < record.Radii.Y + 6;
        if (State == 0)
        {
            if (record.Subid == 1 && save.ReadWramByte(0xc610) != CompanionRuntimeState.DimitriId)
            { Finished = true; return; }
            State = record.Subid == 1 && !colliding ? 2 : 1;
            return;
        }
        if (record.Subid == 1 && State == 1)
        {
            if (!colliding) State = 2;
            return;
        }
        if (!riding() || !CompanionRuntimeState.IsActive(runtime, CompanionRuntimeState.DimitriId)) return;
        if (record.Subid == 1 ? !colliding :
            !player.AcceptsRoomEntityContact || CompanionRuntimeState.Read(runtime).Position.Y < record.ExitY) return;
        Finished = true;
        warpRequested(record.Warp);
    }
}
