using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC$c6 owns the chest signal, direct grant, $60 spin presentation and return.
internal sealed class SeasonsHerosCaveSwordRoomEntity : NpcCharacterRoomEntityAdapter,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsHerosCaveDatabase _data;
    private int _address;
    private bool _retired, _finished;
    private OracleRuntimeState Memory=>_session.Memory;
    internal int State=>_address==0?0:Read(4);
    internal int Counter=>Read(6);
    internal bool SequenceActive=>State>=2 && !Finished;
    public bool Finished=>_finished || _retired || _address!=0 && Read(0)==0;
    public bool NativeInitializationPending=>State==0;
    public bool UpdatesDuringDialogue=>State==0 || _address!=0 && (Read(0)&0x80)!=0;
    public bool UpdatesDuringRoomEntityFreeze=>UpdatesDuringDialogue;

    internal SeasonsHerosCaveSwordRoomEntity(SeasonsOpeningSession session,SeasonsHerosCaveDatabase data,NpcCharacter actor)
        :base(actor,actor.SetTransitionDrawOffset)
    {
        _session=session;_data=data;
        Entity.SetWorldToScreen(session.Transitions.WorldToGameplayScreen,()=>session.Scrolling);
    }
    public void BindNativeInteractionSlot(int slot)
    {
        _address=((0xd0+slot)<<8)|0x40;
        Write(0,1);Write(1,0xc6);Write(2,0);Write(4,0);
        Write(0x0b,(int)Entity.Position.Y);Write(0x0d,(int)Entity.Position.X);
    }
    public void ReleaseNativeInteractionSlot()
    {
        if(!_retired && _address!=0 && Read(1)==0xc6)
            for(int offset=0;offset<64;offset++)Write(offset,0);
        _retired=true;
    }
    private int Read(int offset)=>Memory.ReadWramByte(_address+offset);
    private void Write(int offset,int value)=>Memory.SetWramByte(_address+offset,unchecked((byte)value));
    private bool Tick(){Write(6,Read(6)-1);return Read(6)==0;}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame){if(State==0 || UpdatesDuringDialogue)Advance();}
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)Advance();
        return Entity.Visible?ScreenTransitionPresentation.Visible:ScreenTransitionPresentation.Hidden;
    }
    private void Advance()
    {
        if(Finished)return;
        switch(State)
        {
            case 0:
                Write(4,1);Memory.SetField("wcca1",1);return;
            case 1:
                if(Memory.ReadField("wcca2")==0)return;
                Memory.SetField("wDisableLinkCollisionsAndMenu",(byte)_data.Constant("input-mask"));
                Memory.SetField("wDisabledObjects",(byte)_data.Constant("input-mask"));
                Write(4,2);Write(0,Read(0)|0x80);Write(0x10,_data.Constant("move-speed"));Write(6,_data.Constant("move-wait"));
                Write(0x1a,0x80);Entity.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);Entity.SetScriptVisible(true);return;
            case 2:
                if(!Tick())
                {
                    Vector2 precise=new((Read(0x0d)*256+Read(0x0c))/256f,(Read(0x0b)*256+Read(0x0a))/256f);
                    Vector2 pixel=OracleObjectMovement.Shared.ApplySpeed(ref precise,Read(0x10),Read(9));
                    int x=(int)(precise.X*256),y=(int)(precise.Y*256);
                    Write(0x0c,x);Write(0x0d,x>>8);Write(0x0a,y);Write(0x0b,y>>8);Entity.SetStatePosition(pixel);return;
                }
                Write(4,3);_session.Inventory.GiveTreasure(TreasureId.Sword,1);
                _session.Sound.PlaySound(SoundId.SndGetItem);_session.ShowText(0x001c,_data.Text(0x001c));return;
            case 3:
                if(_session.Entities.TextActiveSource())return;
                Write(4,4);Write(0x1a,0);Entity.SetScriptVisible(false);Write(6,_data.Constant("return-wait"));
                var request=new GroundTreasureGrantRequest(_session.Rooms.ActiveGroup,_session.RoomId,0,
                    (int)_session.Link.Position.Y&255,(int)_session.Link.Position.X&255,"TREASURE_OBJECT_SWORD_03","herosCaveSwordChest.s:@state3")
                {GrabMode=TreasureGrabMode.SpinSlash,RoomFlagTiming=GroundTreasureRoomFlagTiming.Never,
                    ExpectedTreasureId=TreasureId.Sword,ExpectedSubId=3,ExpectedObjectParameter=1};
                if(_session.Entities.TryCreateGroundTreasure(request))_session.Sound.PlaySound(SoundId.SndCtrlMediumFadeOut);
                return;
            case 4:
                if(!Tick())return;
                _session.Save.SetRoomFlag(_session.Rooms.ActiveGroup,_session.RoomId,OracleSaveData.RoomFlagItem);
                _session.Entities.OnRoomWarpRequested(_data.ReturnWarp(_session.Rooms.ActiveGroup,_session.RoomId));
                _session.Sound.PlaySound(SoundId.SndFadeOut);_finished=true;return;
            default:throw new NotSupportedException($"herosCaveSwordChest.s: unsupported INTERAC$c6 state${State:x2}.");
        }
    }
}
