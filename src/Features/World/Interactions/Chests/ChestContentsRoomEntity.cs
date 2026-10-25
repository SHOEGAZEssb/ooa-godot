using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// miscellaneous2.s:$dc:$15/$16. State zero only checks ROOMFLAG_ITEM;
// state one samples linked-game status, publishes the shared override and deletes.
internal sealed partial class ChestContentsRoomEntity(
    OracleRuntimeState runtime, Func<bool> itemFlagSet, Func<TreasureObjectRecord> contents)
    : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
      IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private bool _initialized;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    private void Advance()
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            Finished = itemFlagSet();
            return;
        }
        var reward = contents();
        runtime.SetWramByte(AgesWramAddress.wChestContentsOverride, checked((byte)reward.TreasureId));
        runtime.SetWramByte(AgesWramAddress.wChestContentsOverride + 1, checked((byte)reward.SubId));
        Finished = true;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Advance();
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) { if (!_initialized) Advance(); }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
