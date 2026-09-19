using System.Collections.Generic;

namespace oracleofages;

// INTERAC_EXCLAMATION_MARK $9f; state zero initializes without consuming counter1.
internal sealed class ExclamationMarkRoomEntity(NpcCharacter actor,int frames)
    : NpcCharacterRoomEntityAdapter(actor,actor.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity
{
    private bool _initialized;
    private int _counter=frames;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized) {_initialized=true; return;}
        if(_counter!=255 && --_counter==0) {Finished=true; return;}
        Entity.AdvanceAnimationUpdates(1);
    }
}
