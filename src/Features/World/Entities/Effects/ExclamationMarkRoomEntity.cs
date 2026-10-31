using System.Collections.Generic;

namespace Oracles;

// INTERAC_EXCLAMATION_MARK $9f; state zero initializes without consuming counter1.
internal sealed class ExclamationMarkRoomEntity(NpcCharacter actor,int frames)
    : NpcCharacterRoomEntityAdapter(actor,actor.SetTransitionDrawOffset), IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private bool _initialized;
    private int _counter=frames;
    internal int Counter => _counter;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized)Advance();
        return Finished || !Entity.Visible ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible;
    }
    private void Advance()
    {
        if(Finished)return;
        if(!_initialized) {_initialized=true;
            Entity.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex); // $9f state 0: objectSetVisible80
            return;}
        if(_counter!=255)
        {
            _counter=unchecked((byte)(_counter-1));
            if(_counter==0) {Finished=true; return;}
        }
        Entity.AdvanceAnimationUpdates(1);
    }
}

internal sealed record ExclamationMarkSpawn(Godot.Vector2 Position, bool InitializeThisFrame = false, int Duration = 30, NpcRecord? Record = null, bool PlaySound = true)
    : RoomEntitySpawn(InitializeThisFrame);
