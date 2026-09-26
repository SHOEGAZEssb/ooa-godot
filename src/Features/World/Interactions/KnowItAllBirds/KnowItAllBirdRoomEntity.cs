using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KnowItAllBirdRoomEntity(KnowItAllBirdCharacter bird)
    : RoomEntityAdapter<KnowItAllBirdCharacter>(bird, bird.SetTransitionDrawOffset),
        IFixedRoomEntity, ITalkTarget, IRoomBlocker, IOrdinaryNpcEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IRoomEntityUpdateFreeze, IPlayerRestriction, IScreenTransitionPreloadRoomEntity,
        IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public NpcCharacter Npc => Entity;
    public bool UpdatesDuringDialogue => true; // interactionSetAlwaysUpdateBit
    public bool UpdatesDuringRoomEntityFreeze => true;
    public bool FreezesRoomEntities => Entity.Script.ObjectsDisabled;
    public bool FreezesInteractions => false;
    public bool FreezesPlayerUpdates => Entity.Script.ObjectsDisabled;
    public bool DisablesSword => Entity.Script.ObjectsDisabled;
    public bool DisablesItems => Entity.Script.ObjectsDisabled;
    public bool DisablesMovement => Entity.Script.ObjectsDisabled;
    // substate1 calls interactionAnimate, without the NPC collision tail.
    public bool BlocksLink(Vector2 center) => !Entity.Talking && Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.ScriptButtonSensitive && Entity.CanTalkTo(player) ? Entity : null;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateBird(frame.Player);
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Entity.UpdateBird(frame.Player);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeNative();
        return ScreenTransitionPresentation.Visible;
    }
}
