using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// INTERAC_TOKAY $0c; catches only reserved ITEM C while Link is not grabbing.
internal sealed class WildTokayParticipantRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly WildTokayGameEvent _owner;
    private TokayCharacter Actor=>(TokayCharacter)Node;
    private Vector2 _position;
    private int _speed;
    private TokayAttachedVisualRoomEntity? _accessory;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal bool HoldingMeat { get; private set; }
    internal bool Red { get; private set; }
    public bool Finished=>!Actor.Active;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    private RoomEventContext Context=>_owner.Context;
    internal WildTokayParticipantRoomEntity(WildTokayGameEvent owner,NpcRecord record):base(Create(record),static _=>{})
    {_owner=owner;_position=new(record.X,record.Y);}
    private static TokayCharacter Create(NpcRecord record)
    {
        var actor=new TokayCharacter{Name="WildTokay_48_0c",Visible=false};actor.Initialize(record);
        actor.SetBlocksLink(false);actor.SetAnimationRate(0);return actor;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;Red=Context.Entities.RuntimeState.ReadWramByte(0xcfdf)!=0;
            Actor.SetBasePalette(Red?2:0);
            _speed=Context.Rooms.SaveData.ReadWramByte(_owner.Database.WildLevelAddress)<3?0x14:0x19;
            Actor.SetScriptAnimation(_owner.Interactions.Animation(2));Actor.ShowInitializedPresentation();DrawOffset();return;
        }
        if(Substate==1){if(--Counter==0)Substate=2;return;}
        if(Substate==0)
        {
            var meat=Context.Entities.Entities<WildTokayMeat>().SingleOrDefault(m=>m.Thrown);
            if(meat is not null&&!frame.Player.IsCarryingObject&&WildTokayGameEvent.CanParticipantCatch(Actor.Position,meat.ReservedThrow!.Position))
            {
                Substate=1;Counter=6;HoldingMeat=true;Actor.SetScriptAnimation(_owner.Interactions.Animation(Actor.Record.X==0x88?7:8));
                // cfda/cfdb is a raw pointer, not a search through meat history.
                int page=Context.Entities.RuntimeState.ReadWramByte(0xcfda);
                var active=Context.Entities.Entities<WildTokayMeat>().SingleOrDefault(m=>Context.Entities.InteractionSlot(m)+0xd0==page);
                if(active is null)throw new System.NotSupportedException($"tokay.s:wildTokayParticipant_checkGrabMeat: activeMeatObject points outside its live $8c owner ($cfda=${page:x2}).");
                meat.CancelReservedThrow();active.Catch();Context.Sound.PlaySound(_owner.Database.SoundOpenChest);
                CreateAccessory();
            }
        }
        Actor.SetStatePosition(NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _position,_speed,0x10));DrawOffset();
        if((((int)Actor.Position.Y+8)&255)<0x90){Actor.AdvanceAnimationUpdates(1);return;}
        if(!HoldingMeat)Context.Entities.RuntimeState.SetWramByte(0xcfde,0xff);
        else
        {
            _accessory?.DeleteNative();
            if(Red)Context.Entities.RuntimeState.SetWramByte(0xcfde,1);
        }
        Actor.SetActive(false);
    }
    private void DrawOffset()=>Actor.SetScriptDrawOffset(new(0,Actor.Position.Y>=0xf0?-256:0));
    private void CreateAccessory()
    {
        if(!Context.Entities.InteractionSlotAvailable)return;
        var visual=_owner.Database.MeatAccessory(Actor.CurrentAnimationParameter);
        var record=new NpcRecord(Actor.Record.Group,Actor.Record.Room,0x63,0x73,0,0,0,0,visual.Sprite,visual.TileBase,visual.Palette,0,false,visual.EncodedAnimation,visual.EncodedAnimation,visual.EncodedAnimation,visual.EncodedAnimation,"",NpcImplementationClassification.EventOwned);
        _accessory=new(Actor,record,new(visual.XOffset,visual.YOffset)){Visible=false};
        _accessory.NativeOffset=()=>{var v=_owner.Database.MeatAccessory(Actor.CurrentAnimationParameter);return new(v.XOffset,v.YOffset);};
        Context.Entities.AddEntity(_accessory);
    }
}
