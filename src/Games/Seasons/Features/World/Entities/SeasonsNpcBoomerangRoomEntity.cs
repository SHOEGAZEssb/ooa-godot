using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class SeasonsNpcBoomerangRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, INativeInteractionRoomEntity, IRoomEntityLifetime
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcRoomEntity _owner;
    private bool _initialized, _returning;
    private int _counter=0x28;
    public bool NativeInitializationPending => !_initialized;
    public bool Finished { get; private set; }
    internal SeasonsNpcBoomerangRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room, SeasonsTalkNpcRoomEntity owner)
        : this(session,owner,data.Create(source,room,session.Data.World.SpritePalettes)) { }
    private SeasonsNpcBoomerangRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcRoomEntity owner,NpcCharacter npc)
        : base(npc,npc.SetTransitionDrawOffset) { _session=session;_owner=owner; }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized){_initialized=true;Entity.SetScriptVisible(true);return;}
        if(!_returning && --_counter==0)_returning=true;
        Entity.Position=NativeObjectMovement.ApplySpeed(_session.Memory,OracleObjectPosition.FromPixels(Entity.Position),0x41,_returning?8:0x18).PrecisePosition;
        if(_returning && unchecked((byte)((int)Entity.Position.X+8))>=unchecked((byte)_owner.Npc.Position.X))
        { _owner.WriteByte(0x77,0);Finished=true;Entity.SetActive(false);return; }
        if(Entity.ConsumeAnimationParameter()!=0)_session.Sound.PlaySound(0x78);
        Entity.AdvanceAnimationUpdates(1);
    }
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { _initialized=true;Entity.SetScriptVisible(true);return ScreenTransitionPresentation.Visible; }
}
