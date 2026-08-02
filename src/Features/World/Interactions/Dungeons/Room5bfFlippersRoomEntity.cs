using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_MISCELLANEOUS_1 $6b:$0c and its source script. Unlike an ordinary
/// $60 treasure, it can be touched on its first update and releases input 30
/// updates after the get-item dialogue closes.
/// </summary>
internal sealed class Room5bfFlippersRoomEntity(
    GroundTreasurePickup pickup,
    Func<bool> collectionAllowed,
    Action<GroundTreasurePickup, Player> collected,
    int postGrantWait,
    int pickupRadius)
    : RoomEntityAdapter<GroundTreasurePickup>(
        pickup, pickup.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IPlayerRestriction
{
    private bool _initialized;
    private bool _collectionStarted;
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
        _postGrantCounter++;
        if (_postGrantCounter >= postGrantWait)
            Finished = true;
    }

    public void HandleLinkContact(Player player)
    {
        Vector2 delta = player.Position - Entity.Position;
        float combinedRadius = pickupRadius + NpcCharacter.LinkCollisionRadius;
        if (Mathf.Abs(delta.X) >= combinedRadius ||
            Mathf.Abs(delta.Y) >= combinedRadius ||
            !collectionAllowed() || !Entity.TryCollect(player))
        {
            return;
        }
        _collectionStarted = true;
        collected(Entity, player);
        Entity.UpdateFrame(player);
    }
}
