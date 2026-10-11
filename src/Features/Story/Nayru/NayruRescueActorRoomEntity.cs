using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// The Maku arrival/reward actors retain independent INTERACTION dispatches.
internal sealed class NayruRescueActorRoomEntity : CutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IPlayerInteractable, IRoomBlocker
{
    private readonly RoomEventContext _context;
    private readonly NayruRescueWarpDatabase _data;
    private readonly CutsceneCommandRunner _runner;
    private readonly NpcCharacter _actor;
    private readonly NayruRescueActorRoomEntity? _parent;
    private readonly int _index;
    private Vector2 _precise;
    private int _counter,_counter2,_z,_speedZ,_direction,_palette,_motion,_preset,_presetIndex,_jumpSpeed,_airAnimation;
    private int _twinrovaAnimation=-1;
    private bool _scriptEnded,_pressed;
    private int _noteFrame=-1;
    private bool _flowerCreated;
    internal int Id {get;}
    internal int SubId {get;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal Vector2 PrecisePosition => _precise;
    internal int ZFixed => _z;
    public Node2D Node => _actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue => State==0 || Id is 0x87 or 0x93;
    public bool UpdatesDuringRoomEntityFreeze => State==0 || Id is 0x87 or 0x93;
    public override bool DialogueOpen => _context.DialogueOpen;
    public override bool ScriptExecutionBlocked => DialogueOpen || _context.Player.IsDying;
    public override bool IsLinkedGame => _context.Rooms.SaveData.IsLinkedGame;
    public override int FrameCounter => _context.Entities.FrameCounter;
    internal NayruRescueActorRoomEntity(RoomEventContext context,NayruRescueWarpDatabase data,int id,int subid,Vector2 position,
        int index=0,NayruRescueActorRoomEntity? parent=null)
    {
        _context=context;_data=data;Id=id;SubId=subid;_index=index;_parent=parent;_precise=position;
        _actor=data.CreateActor(id,subid,position);_runner=new(this);
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    private byte Read(int address) => _context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value) => _context.Entities.RuntimeState.SetWramByte(address,(byte)value);
    private void Start(string script) {_scriptEnded=false;_runner.Start(_data.Commands(script));_runner.SetInitialMotionRegisters(new("Actor"),0,0);}
    private void AdvanceScript()
    {
        _runner.AdvanceFrame();
    }
    private void SetAnimation(int index) {_actor.SetScriptAnimation(_data.Animation(Id,index));_noteFrame=-1;}
    private void SetPosition(Vector2 position) {_precise=position;_actor.SetStatePosition(position.Floor());}
    private void SetPalette(int palette)
    {
        _palette=palette;_actor.SetBasePalette(palette);_actor.SetScriptPaletteOverride(palette==6?_data.StonePalette:null);
    }
    private void Initialize()
    {
        State=1;_actor.ShowInitializedPresentation();
        switch(Id)
        {
            case 0x36:
                SetAnimation(Read(0xcfd0)==3?2:5);_actor.Visible=Read(0xcfd0)==3;
                Start(Read(0xcfd0)==3?"nayru-song":"nayru-arrival");break;
            case 0x37:
                SetAnimation(1);_actor.Visible=Read(0xcfd0)==3;
                Start(Read(0xcfd0)==3?"ralph-greeting":"ralph-arrival");break;
            case 0x87:
                SetAnimation(Read(0xcfd0)==3?1:0);Start("maku");
                _actor.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex);
                CreateFlower();break;
            case 0x8d: SetAnimation(3);SetPosition(new(0x88,0x40));_actor.Visible=false;Start("cloaked");break;
            case 0x3c: SetPalette(6);SetAnimation(3);_counter=60;break;
            case 0x3d: SetAnimation(3);_counter=220;break;
            case 0x39: InitializeMonkey();break;
            case 0x93: InitializeTwinrova();break;
            default:throw new NotSupportedException($"Nayru rescue: INTERAC${Id:x2}:${SubId:x2}.");
        }
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0) {Initialize();return;}
        switch(Id)
        {
            case 0x36:case 0x37: UpdateCompanion();break;
            case 0x87: _runner.AdvanceFrame();_actor.AdvanceAnimationUpdates(1);break;
            case 0x8d:
                AdvanceScript();if(_scriptEnded) {_context.Entities.TryCreateVirePuff(_precise,_z>>8,0);Finish();}
                else _actor.AdvanceAnimationUpdates(1);break;
            case 0x3c:UpdateBoy();break;
            case 0x3d:UpdateOldLady();break;
            case 0x39:UpdateMonkey();break;
            case 0x93:UpdateTwinrova();break;
        }
        _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
    private void AnimateMotion()
    {
        int speed=_runner.ActorSpeed(new("Actor"));
        int updates=_runner.MovementCounter(new("Actor"))==0||speed<0x28?1:speed<0x50?2:speed<0x78?3:4;
        _actor.AdvanceAnimationUpdates(updates);
    }
    private void UpdateCompanion()
    {
        if(Substate==0)
        {
            if(Read(0xcfd0)==7)
            {
                _direction=(_context.Entities.Random.Next().Value&2)|1;
                _counter=RandomDelay();Substate=1;return;
            }
            if(_actor.CurrentAnimationFrame!=_noteFrame)
            {
                _noteFrame=_actor.CurrentAnimationFrame;int parameter=_actor.CurrentAnimationParameter;
                if(parameter!=0)NayruRescueMusicNoteRoomEntity.Spawn(_context,_precise.Floor()+new Vector2(parameter==1?-6:8,-4),parameter==1);
            }
            AnimateMotion();AdvanceScript();return;
        }
        if(Substate==1)
        {
            if(Read(0xcfd0)!=8)
            {if(--_counter==0) {_direction^=2;SetAnimation(_direction);_counter=RandomDelay();}return;}
            Substate=2;Start(Id==0x36?"nayru-farewell":"ralph-farewell");
            if(Id==0x36) {SetAnimation(1);return;}
        }
        AnimateMotion();_runner.AdvanceFrame();if(_scriptEnded) Finish();
    }
    private int RandomDelay() => ((_context.Entities.Random.Next().Value&3)<<2)+16;
    private void UpdateBoy()
    {
        if(Substate==0)
        {if(--_counter==0) {_counter=120;Substate=1;_context.Sound.PlaySound(SoundId.SndEnergyThing);}return;}
        if(Substate==1)
        {
            if(--_counter==0) {Substate=2;SetPalette(2);Start("boy");return;}
            if((FrameCounter&7)==0) SetPalette(_palette^4);return;
        }
        AnimateMotion();_runner.AdvanceFrame();
    }
    private void UpdateOldLady()
    {
        if(Substate==0) {if(--_counter==0) {_speedZ=-0x1c0;Substate=1;}return;}
        if(Substate==1)
        {if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)) {Substate=2;_counter=180;Start("old-lady");}return;}
        _counter=(_counter-1)&255;if(_counter==0) Write(0xcfdf,0xff);_runner.AdvanceFrame();AnimateMotion();
    }
    private void InitializeMonkey()
    {
        if(_index==0)
            for(int index=9;index>0;index--)
                if(_context.Entities.InteractionSlotAvailable) _context.Entities.AddEntity(new NayruRescueActorRoomEntity(_context,_data,0x39,4,Vector2.Zero,index));
        var table=_data.Table("monkey-positions");int start=_index*4;
        SetPosition(new(table[start+1],table[start]));_counter=table[start+2];SetAnimation(table[start+3]);
        _counter2=_context.Entities.Random.Next().Value&15;
        _actor.AdjustInitialAnimationCounter(_counter2-7);
        _speedZ=_jumpSpeed=unchecked((short)_data.Table("monkey-jumps")[_context.Entities.Random.Next().Value&3]);
        _counter2=60;SetPalette(6);_actor.Visible=false;
        if(_index==9) {SetPosition(new(0x24,0x64));_motion=1;}
        if(_index==8) {_counter=250;SetAnimation(7);if(_context.Entities.InteractionSlotAvailable)_context.Entities.AddEntity(new NayruRescueBowtieRoomEntity(_data,this));}
    }
    private void UpdateMonkey()
    {
        switch(Substate)
        {
            case 0:if(--_counter2==0) Substate=1;return;
            case 1:if(--_counter==0) {_counter=60;Substate=2;_context.Sound.PlaySound(SoundId.SndGaleSeed);}return;
            case 2:
                if(--_counter==0) {_counter=60;Substate=3;_actor.Visible=true;}
                else _actor.Visible=(FrameCounter&1)!=0;return;
            case 3:
                if(--_counter==0) {Substate=4;SetPalette(_index==5?2:3);if(_index==8) {_counter=180;SetPalette(2);}}
                return;
            default:
                if(_index==9) {UpdateWalkingMonkey();return;}
                if(_index is 0 or 1 or 2 or 4)
                {if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x10)) _speedZ=_jumpSpeed;}
                _actor.AdvanceAnimationUpdates(1);
                if(_index==8 && --_counter==0) Write(0xcfdf,0xff);return;
        }
    }
    private void UpdateWalkingMonkey()
    {
        int airborne=(((_z>>8)-3)&255)<0xfa?1:0;
        if(airborne!=_airAnimation) {_airAnimation=airborne;_actor.SetAnimationCounter(1);_actor.AdvanceAnimationUpdates(1);}
        if(Substate==4) {_direction=8;Substate=5;_speedZ=-0x100;return;}
        if(Substate==5)
        {
            if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)) MoveRaw(0x28,_direction);
            else {_speedZ=-0x100;if(++_motion==3) {Substate=6;_counter=16;}}
        }
        else if(Substate==6 && --_counter==0)
        {_counter=16;Substate=7;_direction^=0x10;SetAnimation((_direction&0x10)==0?8:3);_speedZ=-0x100;}
        else if(Substate==7 && --_counter==0) {_motion=0;Substate=5;}
    }
    private void InitializeTwinrova()
    {
        if(SubId==1) {_actor.SetBasePalette(1);CopyTwinrova();return;}
        if(Read(0xcfd0)!=8) {State=0;_actor.Visible=false;return;}
        SetPalette(2);SetPosition(new(0x88,0xf8));_z=-0x800;
        if(_context.Entities.InteractionSlotAvailable) _context.Entities.AddEntity(new NayruRescueActorRoomEntity(_context,_data,0x93,1,Vector2.Zero,parent:this));
        LoadTwinrovaPreset();UpdateTwinrovaDirection();_context.Sound.PlaySound(SoundId.SndBeam2);PublishTarget();
    }
    private void UpdateTwinrova()
    {
        if(SubId==1) {_actor.AdvanceAnimationUpdates(1);if(_parent!.Finished) Finish();else CopyTwinrova();return;}
        if(Substate==1)
        {
            _actor.AdvanceAnimationUpdates(1);
            if((FrameCounter&7)==0) _z=unchecked((short)(_z+(unchecked((sbyte)_data.Table("z-oscillation")[(FrameCounter>>3)&7])<<8)));
            AdvanceScript();
            if(_scriptEnded) {_context.Sound.PlaySound(SoundId.SndBeam2);PublishTarget();Substate=2;_preset=1;_presetIndex=0;LoadTwinrovaPreset();}
            return;
        }
        PublishTarget();_actor.AdvanceAnimationUpdates(1);MoveRaw(0x50,_direction);
        if(--_counter!=0 || LoadTwinrovaPreset()) {UpdateTwinrovaDirection();return;}
        if(Substate==0) {Substate=1;Start("twinrova");}
        else {Write(0xcfd0,9);Finish();}
    }
    private bool LoadTwinrovaPreset()
    {
        var table=_data.Table(_preset==0?"twinrova-arrival":"twinrova-departure");int index=_presetIndex*2;
        _direction=table[index];int counter=table[index+1];if(counter==0)return false;
        _counter=counter;_presetIndex++;return true;
    }
    private void UpdateTwinrovaDirection() => SetTwinrovaAnimation(((_direction+4)&0x18)>>3);
    private void SetTwinrovaAnimation(int animation) {if(_twinrovaAnimation==animation)return;_twinrovaAnimation=animation;SetAnimation(animation);}
    private void CopyTwinrova()
    {SetPosition(new(0xa0-(int)_parent!._precise.X+_precise.X%1,(int)_parent._precise.Y+_precise.Y%1));_z=(_parent._z&~255)|(_z&255);_actor.Visible=_parent._actor.Visible;SetTwinrovaAnimation(_parent._twinrovaAnimation switch {1=>3,3=>1,var value=>value});}
    private void PublishTarget(int offset=0)
    {Write(0xcfd5,(int)_precise.Y);Write(0xcfd6,((int)_precise.X+offset)&255);}
    private void MoveRaw(int speed,int angle) => _actor.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(ref _precise,speed,angle));
    private void Finish() {Finished=true;_actor.SetActive(false);}
    public bool BlocksLink(Vector2 center) => _actor.BlocksLinkCenter(center);
    public bool TryInteract(Player player) {if(!_actor.ScriptButtonSensitive||!_actor.CanTalkTo(player)) return false;_pressed=true;return true;}
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value=="Actor";
    public override bool TryConsumeActorButton(CutsceneActorId actor) {bool pressed=_pressed;_pressed=false;return pressed;}
    public override void SetActorButtonSensitive(string actor) => _actor.SetScriptButtonSensitive(true);
    public override void SetActorCollisionRadii(string actor,int radiusY,int radiusX) => _actor.SetCollisionRadii(radiusY,radiusX);
    public override void SetActorAnimation(string actor,int index,string animation) {SetAnimation(index);if(Id==0x87)_motion=index;if(Id==0x93)_twinrovaAnimation=index;}
    public override void SetActorMovementAnimation(string actor,int angle,string animation) {SetAnimation((angle&0x18)>>3);_direction=(angle&0x18)>>3;}
    public override void MoveActorAtSpeed(string actor,int speed,int angle) => MoveRaw(speed,angle);
    public override void ShowText(int id,string message) => ShowText(id,message,null);
    public override void ShowText(int id,string message,int? position) => _context.ShowDialogue(message,position,sourceTextId:id);
    public override void ScriptEnded() => _scriptEnded=true;
    public override int ReadMemory(string binding) => binding switch {"RescueSignal"=>Read(0xcfd0),"Linked"=>IsLinkedGame?1:0,_=>throw UnsupportedCommand(binding)};
    public override bool MemoryEquals(string binding,int value) => ReadMemory(binding)==value;
    public override void WriteMemory(string binding,int value)
    {if(binding=="RescueSignal")Write(0xcfd0,value);else if(binding=="MakuMapText")_context.Rooms.SaveData.SetMakuMapTextPresent(value);else throw UnsupportedCommand(binding);}
    public override bool GateOpen(string gate) => gate switch {"PaletteDone"=>!_context.Transitions.NativePaletteFadeActive,"HarpDone"=>(Read(0xcfc0)&0x80)!=0,_=>throw UnsupportedCommand(gate)};
    public override void WriteObjectByte(string actor,int address,int value)
    {if(address==0x48)_direction=value;else if(address==0x60)_actor.SetAnimationCounter(value);else throw UnsupportedCommand($"object byte${address:x2}");}
    public override void SetInputEnabled(bool enabled)
    {Write(WramAddress.wDisabledObjects,enabled?0:0x81);Write(WramAddress.wMenuDisabled,enabled?0:0x80);if(enabled)_context.Player.EndCutsceneControl();else _context.Player.ClearNativeItemParents();}
    public override void PlaySound(int sound) => _context.Sound.PlaySound(sound);
    public override void GiveItem(int treasure,int parameter)
    {
        if (!_context.Entities.InteractionSlotAvailable) return;
        _context.GrantScriptTreasure(0,0x38,treasure,parameter,
            "TREASURE_OBJECT_TUNE_OF_AGES_00","scriptHelper.s:nayruScript02_part2",nativeDispatch:true);
        _runner.WaitForNativeCounter(3);
    }
    public override void RunNativeHandler(string handler)
    {
        var pieces=handler.Split(':');
        switch(pieces[0])
        {
            case "Reveal":_actor.Visible=true;break;
            case "LinkDirection":_context.Player.RequestNativeScriptFacing(this,int.Parse(pieces[1]));break;
            case "FaceTarget":if((FrameCounter&3)==0) {_direction=(((OracleObjectMovement.Shared.RelativeAngle(_precise.Floor(),new(Read(0xcfd6),Read(0xcfd5)))+4)&0x18)>>3);SetAnimation(_direction);}break;
            case "PublishTarget":PublishTarget(pieces.Length==1?0:unchecked((sbyte)int.Parse(pieces[1])));break;
            case "Disabled11":Write(WramAddress.wDisabledObjects,0x11);break;
            case "ResetMusic":_context.Sound.PlayRoomMusic(_context.Rooms.ActiveGroup,_context.Rooms.CurrentRoom.Id,_context.Rooms.SaveData);break;
            case "HarpSong":if(_context.Entities.InteractionSlotAvailable)_context.Entities.AddEntity(new NayruRescueHarpRoomEntity(_context));break;
            default:throw UnsupportedCommand(handler);
        }
    }
    internal bool FlowerFrowning => _motion==4;
    internal void CreateFlower()
    {
        if (_flowerCreated) return;
        _flowerCreated=true;
        _context.Entities.AddEntity(new NayruRescueFlowerRoomEntity(_data.Maku,this));
    }
    internal NpcCharacter Actor => _actor;
}
