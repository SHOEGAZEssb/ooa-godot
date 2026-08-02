using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_MISCELLANEOUS_1 $6b:$0a-$0c and its shared source script. Unlike
/// an ordinary $60 treasure, it can be touched on its first update and keeps
/// input disabled for a source-defined wait after the get-item command ends.
/// </summary>
internal sealed class MiscellaneousTreasureRoomEntity(
    GroundTreasurePickup pickup,
    Func<bool> collectionAllowed,
    Action<GroundTreasurePickup, Player> collected,
    int postGrantWait,
    int pickupDistance,
    Action? postGrantCompleted = null)
    : RoomEntityAdapter<GroundTreasurePickup>(
        pickup, pickup.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IPlayerRestriction
{
    private bool _initialized;
    private bool _collectionStarted;
    private bool _postGrantHandled;
    private int _postGrantCounter;

    public bool Finished { get; private set; }
    public bool DisablesSword => _collectionStarted && !Finished;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => DisablesSword;
    public bool DisablesRingTransformations => DisablesSword;
    public bool DisablesScreenTransitions => DisablesSword;
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue => true;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (!_initialized)
        {
            // $6b state 0 loads the script, then falls through to state 1.
            Entity.UpdateFrame(frame.Player);
            Entity.UpdateFrame(frame.Player);
            _initialized = true;
        }
        else
        {
            Entity.UpdateFrame(frame.Player);
        }

        if (!_collectionStarted || !Entity.Finished)
            return;
        if (!_postGrantHandled)
        {
            // giveitem blocks until its held-item presentation finishes. Any
            // following script command executes before the wait counter.
            postGrantCompleted?.Invoke();
            _postGrantHandled = true;
        }
        _postGrantCounter++;
        if (_postGrantCounter >= postGrantWait)
            Finished = true;
    }

    public void HandleLinkContact(Player player)
    {
        // objectCheckLinkWithinDistance subtracts the absolute Y difference
        // from c before comparing X, using the objects' high-byte positions.
        int deltaX = Math.Abs(
            Mathf.FloorToInt(player.PrecisePosition.X) -
            Mathf.FloorToInt(Entity.Position.X));
        int deltaY = Math.Abs(
            Mathf.FloorToInt(player.PrecisePosition.Y) -
            Mathf.FloorToInt(Entity.Position.Y));
        if (deltaX + deltaY >= pickupDistance ||
            !collectionAllowed() ||
            !Entity.TryCollectAfterSourceCheck(player))
        {
            return;
        }
        _collectionStarted = true;
        collected(Entity, player);
        Entity.UpdateFrame(player);
    }
}
