using Godot;
using System.Collections.Generic;

namespace Oracles;

// springBloomFlower.s states $01/$05-$07 are the boy's independent native consumer.
internal sealed class SeasonsNpcSpringBloomRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, INativeInteractionRoomEntity, IRoomEntityLifetime
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private bool _initialized, _reportedLink;
    private int _state;
    public bool Finished { get; private set; }
    public bool NativeInitializationPending => !_initialized;
    internal SeasonsNpcSpringBloomRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : this(session,data,data.Create(source,room,session.Data.World.SpritePalettes)) { }
    private SeasonsNpcSpringBloomRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data, NpcCharacter npc)
        : base(npc,npc.SetTransitionDrawOffset) { _session=session;_data=data; }
    private void Animation(int index) => Entity.SetScriptAnimation(_data.Animation(0x9c,index));
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized)
        {
            _initialized=true;
            if(_session.Memory.ReadField("wRoomStateModifier")!=0){Finished=true;Entity.SetActive(false);return;}
            _state=1;Entity.SetScriptVisible(true);Entity.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex);
        }
        switch(_state)
        {
            case 1:
                if(_session.Memory.ReadWramByte(0xccc3)!=0){_state=5;Animation(1);return;}
                if(!_reportedLink && frame.Player.OverlapsMinibossPortal(Entity.ObjectCollisionBounds))
                {
                    // shortcut: Link launch stays inactive until native Link state $06 is bound.
                    _reportedLink=true;
                    GD.Print("springBloomFlower.s: INTERAC$9c Link-launch states $02-$04 require LINK_STATE$06; player activation remains unsupported.");
                }
                return;
            case 5:
                Entity.AdvanceAnimationUpdates(1);
                if(Entity.CurrentAnimationParameter==0)return;
                _session.Sound.PlaySound(0x52);_state=6;Animation(2);return;
            case 6:
                Entity.AdvanceAnimationUpdates(1);
                int parameter=Entity.CurrentAnimationParameter;
                if(parameter==0xff){_session.Memory.SetWramByte(0xccc3,0);_state=7;Entity.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex);return;}
                _session.Memory.SetWramByte(0xccc3,(byte)parameter);
                if(parameter>=2)Entity.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
                return;
            case 7:
                Entity.AdvanceAnimationUpdates(1);
                if(Entity.CurrentAnimationParameter!=0xff)return;
                _state=1;_session.Memory.SetWramByte(0xccc3,0);Animation(0);return;
        }
    }
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        bool visible=_session.Memory.ReadField("wRoomStateModifier")==0;
        Entity.SetScriptVisible(visible);
        return visible?ScreenTransitionPresentation.Visible:ScreenTransitionPresentation.Hidden;
    }
}
