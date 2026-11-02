
namespace Oracles;

// INTERAC$8e follows the placed girl's native $76 animation restart signal.
internal sealed class SeasonsFickleGirlEffectRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, INativeInteractionRoomEntity
{
    private readonly SeasonsTalkNpcRoomEntity _owner;
    private readonly SeasonsTalkNpcDatabase _data;
    private bool _initialized;
    public bool NativeInitializationPending => !_initialized;
    internal SeasonsFickleGirlEffectRoomEntity(SeasonsOpeningSession session,SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source,OracleRoomData room,SeasonsTalkNpcRoomEntity owner)
        : this(data,owner,data.Create(source,room,session.Data.World.SpritePalettes)) { }
    private SeasonsFickleGirlEffectRoomEntity(SeasonsTalkNpcDatabase data,SeasonsTalkNpcRoomEntity owner,NpcCharacter npc)
        : base(npc,npc.SetTransitionDrawOffset) { _owner=owner;_data=data; }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized){_initialized=true;Entity.SetScriptVisible(true);Entity.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);return;}
        Entity.AdvanceAnimationUpdates(1);
        if(_owner.ReadByte(0x76)!=0){_owner.WriteByte(0x76,0);Entity.SetScriptAnimation(_data.Animation(0x8e,0));}
        Entity.SetScriptVisible(Entity.CurrentAnimationParameter==0);
    }
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { _initialized=true;Entity.SetScriptVisible(true);Entity.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);return ScreenTransitionPresentation.Visible; }
}
