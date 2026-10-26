using Godot;

namespace Oracles;

// $37:$09 checks flag $32 during state 0. Publishing that flag near the
// script's end must not hide/freeze the actor before resetmusic/scriptend.
internal sealed class PalaceRalphRoomEntity(NpcCharacter actor) :
    NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset), IVariableRoomEntity
{
    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position, initializeVisibility: false);
}
