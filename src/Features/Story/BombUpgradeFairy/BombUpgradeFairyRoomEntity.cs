using Godot;
using System.Collections.Generic;

namespace oracleofages;

// $83 has no initcollisions/checkabutton or objectPreventLinkFromPassing call.
internal sealed class BombUpgradeFairyRoomEntity : NpcCharacterRoomEntityAdapter,
    IRoomEntityUpdateFreeze, IPlayerRestriction, IScreenTransitionPreloadRoomEntity
{
    private BombUpgradeFairyEvent? _owner;
    internal NpcCharacter Npc => Entity;
    internal BombUpgradeFairyRoomEntity(NpcCharacter npc) : base(npc, npc.SetTransitionDrawOffset)
    {
        npc.SetAnimationRate(0);
        npc.SetScriptVisible(false);
    }
    internal void Bind(BombUpgradeFairyEvent owner) => _owner = owner;
    public bool FreezesRoomEntities => _owner?.BlocksGameplay == true;
    public bool FreezesInteractions => false; // wDisabledObjects=$80
    public bool DisablesSword => FreezesRoomEntities;
    public bool DisablesItems => FreezesRoomEntities;
    public bool DisablesMovement => FreezesRoomEntities;
    public bool DisablesMenus => FreezesRoomEntities;
    public bool DisablesPlayerContact => FreezesRoomEntities;
    public bool DisablesCompanion => FreezesRoomEntities;
    ScreenTransitionPresentation IScreenTransitionPreloadRoomEntity.PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        ScreenTransitionPresentation.Hidden;
}
