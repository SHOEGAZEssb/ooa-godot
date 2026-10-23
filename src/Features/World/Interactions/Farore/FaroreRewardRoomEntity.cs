using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_FARORE_GIVEITEM $d9 retains its own interaction slot.</summary>
internal sealed partial class FaroreRewardRoomEntity(FaroreScriptHost parent) : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private bool _initialized;
    public Node2D Node => this;
    public bool Finished => parent.RewardFinished;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Finished) { parent.AdvanceReward(frame.Player); _initialized = true; }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}

internal sealed record FaroreRewardSpawn(FaroreScriptHost Parent) : RoomEntitySpawn;
