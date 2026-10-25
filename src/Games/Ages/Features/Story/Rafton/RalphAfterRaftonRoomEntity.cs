using Godot;

namespace Oracles;

internal sealed class RalphAfterRaftonRoomEntity(
    RalphAfterRaftonCharacter ralph)
    : NpcCharacterRoomEntityAdapter(ralph, ralph.SetTransitionDrawOffset),
        IRoomBlocker,
        IOrdinaryNpcEntity
{
    private bool _initialized;
    public NpcCharacter Npc => Entity;
    // ralph.s checks these flags only in state0. orroomflag yields one
    // update before scriptend deletes this initialized actor.
    public bool RefreshStoryState => !_initialized;
    public void CompleteStoryInitialization() => _initialized = true;

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);
}
