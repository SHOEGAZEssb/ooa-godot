
namespace Oracles;

internal sealed class SeasonsTalkNpcRoomEntity : NativeNpcRoomEntity,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private readonly NativeObjectInstruction _source;
    private readonly OracleRoomData _room;
    private int _z, _speedZ;
    private int _selectedState;
    public override bool UpdatesDuringDialogue => _source.Id is 0x2a or 0xe5;
    public override bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {if(UpdatesDuringDialogue)UpdateFrame(frame,[]);}
    internal SeasonsTalkNpcRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : this(session, data, source, room, data.Create(source, room, session.Data.World.SpritePalettes,
            source.Id==0x2a?new KnowItAllBirdCharacter():null)) { }
    private SeasonsTalkNpcRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room, NpcCharacter npc)
        : base(npc, session.Memory, session.Link, index => data.Animation(source.Id, index), data.Text)
    {
        _session = session; _data = data; _source = source; _room = room;
        if(npc is KnowItAllBirdCharacter bird)
        {
            int text=0x3200+source.SubId;
            string[] visual=[data.Animation(0x2a,0),data.Animation(0x2a,1),data.Animation(0x2a,2),data.Animation(0x2a,3)];
            bird.InitializeBirdBody(npc.Record with {TextId=text,Message=data.Text(text)},visual,SeasonsGameContent.AssetRoot,
                session.Random,player=>Script?.AdvanceObjectUpdate(player),()=>{});
            bird.ShowInitializedPresentation();
        }
    }
    internal void SetSpeedZ(int value) => _speedZ = value;
    protected override void ObjectByteWritten(int offset, int value)
    {
        if (Entity is not KnowItAllBirdCharacter bird) return;
        if (offset == 0x48) bird.Direction = value;
        if (offset == 0x77) bird.TalkingSignal = value != 0;
    }
    internal bool NativeTalk => _source.Id == 0x3b && _source.SubId >> 4 == 2;
    internal override bool TryNativeTalk(NpcInteractionTarget target)
    {
        if (!NativeTalk) return false;
        WriteByte(0x71, 1); target.Begin(); return true;
    }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if(_source.Id==0x27&&!_session.Save.HasGlobalFlag(SeasonsGlobalFlag.FinishedGame))
            _session.Save.SetRoomFlag(_room.Group,_room.Id,0x40,(_session.Save.ReadField("wObtainedSeasons")&2)==0);
        var selected = _data.Select(Entity.Record, _session.Save, _session.Memory);
        if (!selected.Visible) { Delete(); return false; }
        WriteByte(0x44, 1);
        int id=_source.Id;
        if(id == 0x80) WriteByte(0x42, SeasonsNpcStoryState.HoronStage(_session.Save,_session.Memory));
        if(id is 0x3e or 0x3b)
        {WriteByte(0x43,_source.SubId>>4);WriteByte(0x42,id==0x3b?_source.SubId&15:_source.SubId>>4==3?SeasonsNpcStoryState.SunkenStage(_session.Save):SeasonsNpcStoryState.HoronStage(_session.Save,_session.Memory));}
        if(id is 0x8a or 0x8b or 0x8d)WriteByte(0x49,4);
        if(id==0x99){WriteByte(0x73,0x1f);WriteByte(0x4b,0x38);WriteByte(0x4d,0x80);}
        if (_source.Id is 0x30 or 0x32 or 0xb1) WriteByte(0x49,0xff);
        _selectedState = _source.Id == 0x3e ? SeasonsNpcStoryState.HoronStage(_session.Save,_session.Memory) : 0;
        Entity.SetScriptVisible(true);
        Entity.ZIndex=id==0xb8?ObjectDrawPriority.FixedHighPriorityZIndex:ObjectDrawPriority.BehindLinkZIndex;
        Script = new SeasonsTalkNpcScriptHost(this, _session, _data, selected.Script);
        if(_source.Id==0xe5)
        {
            Entity.SetBasePalette(Entity.Record.Palette+_source.SubId);
            Script.InitializeActorCollisionRadii("Self");Script.SetActorButtonSensitive("Self");return false;
        }
        if(_source.Id is 0x25 or 0x26)
        {WriteByte(0x44,_source.Id==0x26?2:1);SetAnimation(2);Entity.AnimateAsNpcOneUpdate(frame.Player);return false;}
        if(_source.Id==0x27){Script.AdvanceObjectUpdate(frame.Player);Script.AdvanceObjectUpdate(frame.Player);return false;}
        if (_source.Id == 0x2a)
        {
            ((KnowItAllBirdCharacter)Entity).InitializeNative();return false;
        }
        if (_source.Id == 0x2e)
        {
            int direction=_room.Id==0x6d?0:1;WriteByte(0x48,direction);if(direction!=0)SetAnimation(direction);
            var offset=FickleOffset();
            _session.Entities.TryAllocateInteraction(()=>new SeasonsFickleGirlEffectRoomEntity(_session,_data,
                _source with {Id=0x8e,SubId=0,X=(int)offset.Position.X,Y=(int)offset.Position.Y},_room,this));
            UpdateFickleGirl(frame.Player,false);return false;
        }
        if (_source.Id == 0xc8)
        {
            WriteByte(0x78,0x28);_room.SetPackedLayoutWithoutGraphics((byte)_room.GetPackedPosition(Entity.Position),0);
            Entity.SetAnimationCounter(1);Entity.AdvanceAnimationUpdates(1);UpdateBoomerangSubrosian(frame.Player);return false;
        }
        if (_source.Id == 0x42) SetAnimation(2);
        if (_source.Id == 0xa5)
        {
            SetAnimation(6);
            _session.Memory.SetWramByte(0xcc1d,0xa5);_session.Memory.SetField("wLoadedTreeGfxIndex",0xa5);
            return false;
        }
        if (_source.Id == 0x4e)
        {WriteByte(0x45,1);UpdateTroupe(frame.Player);return false;}
        if (_source.Id == 0xb8)
        {SetAnimation(2);Entity.AdvanceAnimationUpdates(1);return false;}
        if (_source.Id == 0x40)
        {
            _room.SetPackedLayoutWithoutGraphics((byte)_room.GetPackedPosition(Entity.Position),0);
            SetAnimation(4);Script.AdvanceObjectUpdate(frame.Player);UpdatePiratian(frame.Player);return false;
        }
        if (_source.Id == 0x3b) { UpdateGoron(frame.Player); return false; }
        if (_source.Id == 0x3e)
        {
            Script.AdvanceObjectUpdate(frame.Player);
            if (_source.SubId >> 4 == 0)
            {
                _session.Entities.TryAllocateInteraction(() => new SeasonsBoyPlaymateRoomEntity(_session, _data, _room,
                    _source with {Id=0x83,SubId=0,X=(_source.X-3)&255}));
                WriteByte(0x76, (_session.Random.Next().Value & 0x3f) + 0x78);
            }
            else if (_source.SubId >> 4 == 2 && _session.Memory.ReadField("wRoomStateModifier") == 0)
            { WriteByte(0x49,8); WriteByte(0x50,0x28); WriteByte(0x4b,0x62); WriteByte(0x4d,0x28); SetAnimation(6); }
            return false;
        }
        if (_source.Id is 0x80 or 0xc2)
        { Entity.AnimateAsNpcOneUpdate(frame.Player); return false; }
        if (_source.Id is 0x30 or 0x32 or 0xb1)
        { Script.AdvanceObjectUpdate(frame.Player); Script.AdvanceObjectUpdate(frame.Player); }
        else if (_source.Id != 0x9d) Script.AdvanceObjectUpdate(frame.Player);
        if (_source.Id == 0x99) Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
        if(_source.Id is 0x8a or 0x8b or 0x8d)Entity.AnimateAsNpcOneUpdate(frame.Player);
        return false;
    }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity is KnowItAllBirdCharacter bird) { bird.UpdateBird(frame.Player); return true; }
        if(_source.Id==0xe5){Script?.AdvanceObjectUpdate(frame.Player);Entity.UpdateDrawPriority(frame.Player.Position,withTerrainEffects:true);return true;}
        if(_source.Id==0x25){UpdateMittens(frame.Player);return true;}
        if(_source.Id==0x27)
        {
            if((_session.DisabledObjects&1)==0&&(_session.FrameCounter&0x3f)==0)
                FloatingMusicNoteRoomEntity.Spawn(_session.Entities,Entity.Position+new Vector2(-4,-6),true,"SokraSnore",subid:0);
            Entity.AnimateAsNpcOneUpdate(frame.Player);Script?.AdvanceObjectUpdate(frame.Player);return true;
        }
        if (_source.Id == 0x2e) { UpdateFickleGirl(frame.Player,true);return true; }
        if (_source.Id == 0xc8) { UpdateBoomerangSubrosian(frame.Player);return true; }
        if (_source.Id == 0x4e) {UpdateTroupe(frame.Player);return true;}
        if (_source.Id == 0xa5)
        {
            Script?.AdvanceObjectUpdate(frame.Player);
            if(ReadByte(0x78)==0)Entity.AdvanceAnimationUpdates(1);
            Entity.PreventPlayerPassing(frame.Player);Entity.UpdateDrawPriority(frame.Player.Position,withTerrainEffects:true);
            return true;
        }
        if (_source.Id is 0x30 or 0x32 or 0xb1)
        {
            if (_source.Id != 0x32)
            { OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20);Entity.SetScriptDrawOffset(new(0,unchecked((sbyte)(_z>>8)))); }
            if (_source.Id is 0x30 or 0x32 && _room.Group == 1)
                _room.SetPackedLayoutWithoutGraphics((byte)_room.GetPackedPosition(Entity.Position), 0);
        }
        if (_source.Id == 0x3b) { UpdateGoron(frame.Player); return true; }
        if (_source.Id == 0x3e && _source.SubId >> 4 == 0)
        {
            if (ReadByte(0x45) == 0)
            {
                WriteByte(0x76, ReadByte(0x76) - 1);
                if (ReadByte(0x76) == 0)
                { Entity.SetAnimationCounter(1); WriteByte(0x45,1); _session.Memory.SetWramByte(0xcceb,1); Entity.AdvanceAnimationUpdates(1); }
            }
            else if (_session.Memory.ReadWramByte(0xcceb) == 2)
            { WriteByte(0x76,(_session.Random.Next().Value&0x3f)+0x78); WriteByte(0x45,0); SetAnimation(8); }
            Script?.AdvanceObjectUpdate(frame.Player); Entity.PushPlayerAwayAndUpdateDrawPriority(frame.Player); return true;
        }
        if (_source.Id == 0x3e && _source.SubId >> 4 == 2 && _session.Memory.ReadField("wRoomStateModifier") == 0)
        { UpdateSpringBoy(frame.Player); return true; }
        if (_source.Id == 0x3e && _source.SubId >> 4 == 1 && _selectedState >= 2)
        {
            if (ReadByte(0x45) == 0) { WriteByte(0x45,1); _z=0; _speedZ=-0xc0; }
            OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20);
            Entity.SetScriptDrawOffset(new(0,unchecked((sbyte)(_z>>8))));
        }
        return false;
    }
    protected override void AdvanceNpc(RoomEntityFrame frame)
    {
        if (_source.Id == 0x40) {UpdatePiratian(frame.Player);return;}
        if (_source.Id == 0x32)
        {
            Vector2 delta=OracleObjectMath.ToPixelPosition(frame.Player.Position)-OracleObjectMath.ToPixelPosition(Entity.Position);
            int direction=System.Math.Abs(delta.X)+System.Math.Abs(delta.Y)>=40?4:
                System.Math.Abs(delta.X)>=System.Math.Abs(delta.Y)?delta.X<0?6:2:delta.Y<0?0:4;
            if(ReadByte(0x49)!=direction){WriteByte(0x49,direction);SetAnimation(direction>>1);}
        }
        if (_source.Id==0xb8 && ReadByte(0x7f)!=0) Entity.AnimateAsNpcOneUpdate(frame.Player);
        else base.AdvanceNpc(frame);
    }
    private void UpdatePiratian(Player player)
    {
        Entity.AdvanceAnimationUpdates(1);
        if(Entity.CurrentAnimationCounter==1)
        {
            int counter=_data.NativeTable("piratian-counters")[_session.Random.Next().Value&0x1f];
            if(counter!=0)Entity.SetAnimationCounter(counter);
        }
        Entity.PushPlayerAwayAndUpdateDrawPriority(player);
    }
    private void UpdateTroupe(Player player)
    {
        Entity.AdvanceAnimationUpdates(1);Script?.AdvanceObjectUpdate(player);
        Entity.PushPlayerAwayAndUpdateDrawPriority(player);
    }
    private void UpdateMittens(Player player)
    {
        Script?.AdvanceObjectUpdate(player);
        if(_session.Memory.ReadWramByte(0xcceb)==0){Entity.FaceLinkAndAnimateOneUpdate(player);return;}
        switch(ReadByte(0x78))
        {
            case 0:
                WriteByte(0x78,1);WriteByte(0x49,8);WriteByte(0x50,0x28);_speedZ=-0x200;WriteByte(0x79,4);SetAnimation(7);break;
            case 1:
                WriteByte(0x79,ReadByte(0x79)-1);
                if(ReadByte(0x79)==0){WriteByte(0x78,2);WriteByte(0x79,4);SetAnimation(8);_session.Sound.PlaySound(0x53);}break;
            case 2:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x28)){Move(ReadByte(0x50),ReadByte(0x49));break;}
                WriteByte(0x78,3);WriteByte(0x79,4);SetAnimation(7);_session.Sound.PlaySound(0x57);break;
            case 3:
                WriteByte(0x79,ReadByte(0x79)-1);
                if(ReadByte(0x79)==0){_session.Memory.SetWramByte(0xcceb,0);SetAnimation(2);}break;
        }
        Entity.SetScriptDrawOffset(new(0,unchecked((sbyte)(_z>>8))));Entity.AnimateAsNpcOneUpdate(player);
    }
    private void UpdateBoomerangSubrosian(Player player)
    {
        Script?.AdvanceObjectUpdate(player);Entity.PushPlayerAwayAndUpdateDrawPriority(player);
        if(ReadByte(0x45)==0)
        {
            WriteByte(0x78,ReadByte(0x78)-1);if(ReadByte(0x78)!=0)return;
            WriteByte(0x45,1);
            _session.Entities.TryAllocateInteraction(()=>new SeasonsNpcBoomerangRoomEntity(_session,_data,
                _source with {Id=0xc9,SubId=0,X=(int)Entity.Position.X,Y=(int)Entity.Position.Y},_room,this));
            WriteByte(0x77,1);Entity.SetAnimationCounter(1);Entity.AdvanceAnimationUpdates(1);return;
        }
        if(ReadByte(0x77)!=0)return;
        WriteByte(0x45,0);Entity.SetAnimationCounter(1);Entity.AdvanceAnimationUpdates(1);
        WriteByte(0x78,(_session.Random.Next().Value&0x3f)+0x3c);
    }
    private (Vector2 Position,int Angle) FickleOffset() => (Entity.Position+new Vector2((ReadByte(0x48)&1)==0?-8:10,2),(ReadByte(0x48)&1)==0?0x1c:6);
    private void UpdateFickleGirl(Player player,bool runScript)
    {
        if(runScript)Script?.AdvanceObjectUpdate(player);
        Entity.AdvanceAnimationUpdates(1);
        int mode=ReadByte(0x43),parameter=Entity.CurrentAnimationParameter;
        void SelectMode(){WriteByte(0x76,1);WriteByte(0x43,_session.Random.Next().Value&3);}
        bool CreateBubble(int subid)
        {var offset=FickleOffset();return _session.Entities.TryCreatePart(new SeasonsNpcBubbleSpawn(offset.Position,subid,offset.Angle));}
        if(mode==0)
        {if(parameter==0xff)SelectMode();Entity.PushPlayerAwayAndUpdateDrawPriority(player);return;}
        if(mode==1 && parameter==0 || mode>=2 && parameter!=2)
        {Entity.PushPlayerAwayAndUpdateDrawPriority(player);return;}
        SetAnimation(ReadByte(0x48)&1);SelectMode();
        int count;
        if(mode==1){count=_session.Random.Next().Value&3;if(count==0)count=1;}
        else {CreateBubble(1);count=(_session.Random.Next().Value&3)-2;if(count<0)return;count++;}
        for(int index=0;index<count;index++)if(!CreateBubble(0))return;
    }
    private void UpdateSpringBoy(Player player)
    {
        int state=ReadByte(0x45);
        bool Wait(int address) { WriteByte(address,ReadByte(address)-1); return ReadByte(address)!=0; }
        void Advance() => WriteByte(0x45,state+1);
        void MoveBoy() => Move(ReadByte(0x50),ReadByte(0x49));
        void Run(bool animate)
        { if(animate)Entity.AdvanceAnimationUpdates(1); Script?.AdvanceObjectUpdate(player); Entity.UpdateDrawPriority(player.Position,withTerrainEffects:true); }
        switch(state)
        {
            case 0: case 7: case 9: case 11:
                if(player.OverlapsMinibossPortal(Entity.ObjectCollisionBounds))WriteByte(0x77,12);
                if(ReadByte(0x77)!=0 && Wait(0x77)){Run(false);return;}
                MoveBoy();
                if(state==0 && (byte)Entity.Position.X>=0x4b)
                { Advance();_speedZ=-0x180;WriteByte(0x50,0x14);SetAnimation(9); }
                else if(state==7 && (byte)Entity.Position.Y<0x28)
                { Advance();WriteByte(0x76,6);WriteByte(0x49,0x18); }
                else if(state==9 && (byte)Entity.Position.X<0x18)
                { Advance();WriteByte(0x76,6);WriteByte(0x49,0x10); }
                else if(state==11 && (byte)Entity.Position.Y>=0x62)
                { Advance();WriteByte(0x76,6);WriteByte(0x49,8); }
                Run(true);return;
            case 1:
                if(_session.Memory.ReadWramByte(0xccc3)!=0)return;
                _session.Memory.SetWramByte(0xccc3,1);Advance();return;
            case 2:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)){MoveBoy();break;}
                Advance();WriteByte(0x76,0x28);
                Entity.Position=new(((int)Entity.Position.X&0xf0)+8,((int)Entity.Position.Y&0xf0)+3);SetAnimation(6);break;
            case 3:
                if(Wait(0x76))return;Advance();SetAnimation(5);return;
            case 4:
                int high=_session.Memory.ReadWramByte(0xccc3);_z=unchecked((short)((_z&255)|(high<<8)));
                if(high==0)
                { Advance();_speedZ=-0x2c0;_z=unchecked((short)((_z&255)|0xf600));WriteByte(0x50,0x28);WriteByte(0x49,0);_session.Sound.PlaySound(0x53); }
                break;
            case 5:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)){MoveBoy();break;}
                Advance();WriteByte(0x76,0x10);WriteByte(0x71,0);break;
            case 6:
                if(Wait(0x76))return;Advance();return;
            case 8: case 10:
                if(Wait(0x76))return;SetAnimation((ReadByte(0x49)>>3)+5);Advance();return;
            case 12:
                if(Wait(0x76))return;WriteByte(0x45,0);SetAnimation(6);return;
        }
        Entity.SetScriptDrawOffset(new(0,unchecked((sbyte)(_z>>8))));
    }
    private void UpdateGoron(Player player)
    {
        int variant = _source.SubId >> 4, state = ReadByte(0x45);
        if (variant == 0) { Script?.AdvanceObjectUpdate(player); Entity.FaceLinkAndAnimateOneUpdate(player); return; }
        if (variant == 1)
        {
            Script?.AdvanceObjectUpdate(player);
            Vector2 delta = OracleObjectMath.ToPixelPosition(Entity.Position) - OracleObjectMath.ToPixelPosition(player.Position);
            bool near = System.Math.Abs(delta.X) + System.Math.Abs(delta.Y) < 0x28;
            if (state == 0 && near) { WriteByte(0x45, 1); SetAnimation((Entity.Position.X < player.Position.X ? 2 : 0) + 6); }
            else if (state == 1 && Entity.CurrentAnimationParameter == 0xff) { WriteByte(0x45, 2); WriteByte(0x49, 0xff); }
            else if (state == 2)
            {
                if (near) { Entity.FaceLinkAndAnimateOneUpdate(player); return; }
                WriteByte(0x45, 3); SetAnimation((Entity.Position.X < player.Position.X ? 2 : 0) + 7);
            }
            else if (state == 3 && Entity.CurrentAnimationParameter == 0xff) WriteByte(0x45, 0);
            Entity.AnimateAsNpcOneUpdate(player); return;
        }
        Entity.AdvanceAnimationUpdates(2);
        if (state == 0 && ReadByte(0x71) != 0)
        {
            WriteByte(0x71, 0);
            int angle = OracleObjectMovement.Shared.RelativeAngle(Entity.Position, player.Position);
            SetAnimation(((angle + 4) >> 3) & 3);
            _session.Dialogue.ShowGameplayMessage(_data.Text(0x3700), _session.Entities.WorldToScreen(player.Position).Y,sourceTextId:0x3700);
            WriteByte(0x45, 1);
        }
        else if (state != 0) { SetAnimation(ReadByte(0x76)); WriteByte(0x45, 0); }
        Script?.AdvanceObjectUpdate(player);
        Entity.PushPlayerAwayAndUpdateDrawPriority(player);
    }
    public override bool BlocksLink(Vector2 center) =>
        Entity is not KnowItAllBirdCharacter {Talking:true} && base.BlocksLink(center);
}
