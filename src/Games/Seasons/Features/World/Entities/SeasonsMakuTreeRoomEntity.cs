namespace Oracles;

// Seasons INTERAC$87:$00 stage0 and INTERAC$22:$0a retain separate native dispatch.
internal sealed class SeasonsMakuTreeRoomEntity : NativeNpcRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    internal bool Gate => Npc.Record.Id == 0x22;
    public override bool UpdatesDuringDialogue => NativeInitializationPending || !Gate;
    public override bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;

    internal SeasonsMakuTreeRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : base(data.CreateMakuActor(source,room,session.Data.World.SpritePalettes),session.Memory,session.Link,
            index=>data.Animation(0x87,index),data.Text)
    {
        _session=session; _data=data;
        Npc.SetScriptVisible(false); Npc.SetScriptButtonSensitive(false); Npc.SetBlocksLink(false);
        Npc.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
    }

    protected override bool InitializeNpc(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        WriteByte(0x44,1);
        Npc.SetCollisionRadii(0,0);
        if(Gate)
        {
            if(Flag(0x80)){Delete();return false;}
            Script=new SeasonsMakuTreeScriptHost(this,_session,_data,"makuTreeScript_gateHit");
            Script.AdvanceObjectUpdate(frame.Player);
            return false;
        }
        if(_session.Save.HasTreasure(TreasureId.Essence) || _session.Save.HasGlobalFlag(0x28))
            throw new NotSupportedException($"makuTree.s: INTERAC$87:$00 in ${Npc.Record.Group:x1}:${Npc.Record.Room:x2} supports the first, no-Essence story stage only.");
        WriteByte(0x40,ReadByte(0x40)|0x80);
        _session.Memory.SetWramByte(0xcc39,0);
        Npc.SetScriptVisible(true);
        if(!Flag(0x40) && Flag(0x80)) SpawnKey(false);
        Script=new SeasonsMakuTreeScriptHost(this,_session,_data,"stage0");
        Script.AdvanceObjectUpdate(frame.Player);
        Script.AdvanceObjectUpdate(frame.Player);
        Script.AdvanceObjectUpdate(frame.Player);
        return false;
    }

    protected override bool UpdateNativeNpc(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Gate)
        {
            if(!Flag(0x40) && _session.Save.HasTreasure(0x42))
                _session.Save.SetRoomFlag(_session.Rooms.ActiveGroup,_session.RoomId,0x40);
            return false;
        }
        if(ReadByte(0x44)!=2)return false;
        byte[] tiles=_data.NativeTable("maku-gate-tiles");
        bool first=ReadByte(0x45)==0;
        if(first)
        {
            for(int i=0;i<tiles.Length;i+=4)
                _session.Room.SetInterleavedMetatile(Point(tiles[i]),tiles[i+1],tiles[i+2],tiles[i+3],
                    (long)_session.AnimationTicks,writeLayout:false);
            WriteByte(0x45,1);WriteByte(0x46,30);
        }
        else
        {
            WriteByte(0x46,ReadByte(0x46)-1);
            if(ReadByte(0x46)!=0)return true;
            for(int i=0;i<tiles.Length;i+=4)_session.Rooms.TrySetTile(tiles[i],tiles[i+1]);
            WriteByte(0x44,1);WriteByte(0x45,0);
        }
        byte[] puffs=_data.NativeTable("maku-gate-puffs");
        for(int i=first?0:8;i<(first?8:16);i+=2)
            if(_session.Entities.TryCreateInteraction(new KillEnemyPuffSpawn(new(puffs[i+1],puffs[i]))) is null)break;
        _session.Sound.PlaySound(0x73);
        _session.Entities.BeginScreenShake(6);
        _session.Sound.PlaySound(0x70);
        _session.Entities.OnRoomTileChanged();
        return true;
    }

    protected override void AdvanceNpc(RoomEntityFrame frame)
    {if(!Gate)Npc.AdvanceAnimationUpdates(1);}
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {if(NativeInitializationPending || !Gate)UpdateFrame(frame,new List<RoomEntitySpawn>());}
    private bool Flag(int flag)=>_session.Save.HasRoomFlag(_session.Rooms.ActiveGroup,_session.RoomId,(byte)flag);
    private static Vector2 Point(int packed)=>new((packed&15)*16,(packed>>4)*16);

    internal void SetMakuAnimation(int animation)
    {
        if(animation==0)
        {
            var bubble=(SeasonsMakuBubbleRoomEntity?)_session.Entities.TryAllocateEnemy(slot=>
                new SeasonsMakuBubbleRoomEntity(_session,_data,this));
            if(bubble is not null)
            {
                WriteByte(0x56,0x80);WriteByte(0x57,0xd0+bubble.Slot);
                _session.Memory.SetWramByte(0xcfc0,(byte)(_session.Memory.ReadWramByte(0xcfc0)&0x7f));
            }
        }
        WriteByte(0x77,animation);SetAnimation(animation);
    }

    internal void SpawnKey(bool falling)
    {
        int x=_session.Save.ReadWramByte(0xc6e0);
        if(falling)
        {
            int linkX=(int)_session.Link.Position.X;
            x=linkX is <0x3c or >=0x64?0x50:linkX>=0x50?0x40:0x60;
        }
        var request=new GroundTreasureGrantRequest(_session.Rooms.ActiveGroup,_session.RoomId,0,
            falling?0x60:0x58,x,falling?"TREASURE_OBJECT_GNARLED_KEY_00":"TREASURE_OBJECT_GNARLED_KEY_01",
            falling?"scriptHelper.s:makuTree_dropGnarledKey":"makuTree.s:makuTree_spawnGnarledKey")
        {
            SpawnMode=falling?TreasureSpawnMode.FromScreenTop:TreasureSpawnMode.Instant,
            GrabMode=TreasureGrabMode.OneHand,
            SpawnDelayFrames=falling?40:0,InitialZAboveScreen=falling,BounceCount=falling?2:0,
            Gravity=falling?0x10:0,BounceSpeed=falling?-0xaa:0,
            SpawnSound=falling?SoundId.SndSolvePuzzle:0,ExpectedTreasureId=0x42,ExpectedSubId=falling?0:1
        };
        if(_session.Entities.TryCreateGroundTreasure(request) && falling)_session.Save.WriteWramByte(0xc6e0,(byte)x);
    }
}
