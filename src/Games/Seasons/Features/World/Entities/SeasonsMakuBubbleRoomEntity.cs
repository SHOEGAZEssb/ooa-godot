namespace Oracles;

// ENEMY$56: the parent's interaction script owns waking, dialogue and replacement.
internal sealed partial class SeasonsMakuBubbleRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, INativeEnemySlotRoomEntity, IRoomEntityLifetime,
    ISwordHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity,
    IObjectCollisionHeightRoomEntity, ILinkSwordStateAwareRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsMakuTreeRoomEntity _parent;
    private readonly byte[] _offsets;
    private int _sword=ItemCollisionType.L1Sword;
    internal int Slot {get;private set;}
    private int Address=>((0xd0+Slot)<<8)|0x80;
    private OracleRuntimeState Memory=>_session.Memory;
    internal int State=>Read(4);
    public Node2D Node=>this;
    public bool Finished=>Read(0)==0;
    public bool MeleeReportsContact=>false;
    public int CollisionZ=>-8;
    internal Vector2 DrawOffset=>CurrentDrawOffset+SourceOamDrawOffset+new Vector2(0,-8);
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal SeasonsMakuBubbleRoomEntity(SeasonsOpeningSession session,SeasonsTalkNpcDatabase data,SeasonsMakuTreeRoomEntity parent)
    {
        _session=session;_parent=parent;_offsets=data.NativeTable("maku-bubble-offsets");
        InitializeVisual(data.MakuBubbleVisual,parent.Npc.Position,positionedOam:true);
        ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;Visible=false;
    }
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve)
    {
        Slot=slot;
        for(int i=0;i<64;i++)Write(i,0);
        Write(0,1);Write(1,0x56);Write(0x24,0xd6);Write(0x25,2);Write(0x29,1);
        Write(0x1b,3);Write(0x1c,3);Write(0x1d,10);
        Write(0x18,0x40);Write(0x19,0xd0+_session.Entities.NativeInteractionSlot(_parent));
    }
    public void ReleaseEnemySlot()
    {
        // Room retirement clears live objects; enemyDelete already cleared enabled.
        if(Read(0)!=0)for(int i=0;i<64;i++)Write(i,0);
    }
    private int Read(int offset)=>Memory.ReadWramByte(Address+offset);
    private void Write(int offset,int value)=>Memory.SetWramByte(Address+offset,unchecked((byte)value));
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(State==1 && Read(0x29)==0)Write(4,2);
        switch(State)
        {
            case 0:
                Write(0x3d,_session.Random.Next().Value);Write(0x3e,1);
                Write(4,1);Visible=true;Follow();return;
            case 1:AdvanceAnimation();Follow();return;
            case 2:
                if(Read(5)==0)
                {
                    Write(5,1);
                    for(int i=0x24;i<0x2f;i++)Write(i,0);
                    Write(0x29,1);Write(0x1b,1);Write(0x1c,1);
                    var palettes=_session.Data.World.SpritePalettes;
                    SetNativePalette(new Dictionary<int,Color[]>{{3,Enumerable.Range(0,4).Select(shade=>palettes[1,shade]).ToArray()}});
                    SetAnimation(1);Memory.SetWramByte(0xcfc0,(byte)(Memory.ReadWramByte(0xcfc0)|0x80));
                    Follow();return;
                }
                if((AnimationParameter&0x80)==0){AdvanceAnimation();return;}
                ConsumeAnimationParameter();Write(0x21,0);Write(5,0);Write(4,3);Follow();return;
            case 3:
                AdvanceAnimation();
                if(AnimationParameter==0)return;
                if((AnimationParameter&0x80)!=0){Write(0,0);Visible=false;return;}
                Write(0x0b,Read(0x0b)+1);Position=new(Read(0x0d),Read(0x0b));
                // OAM wrapping depends on position and is cached in Godot's draw commands.
                QueueRedraw();return;
            default:throw new NotSupportedException($"makuTreeBubble.s: ENEMY$56 state${State:x2}.");
        }
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)UpdateFrame(new(_session.Link,_session.FrameCounter,false,null),spawns);
        return Visible?ScreenTransitionPresentation.Visible:ScreenTransitionPresentation.Hidden;
    }
    private void Follow()
    {
        int parameter=AnimationParameter;
        if(State==1 && parameter==0 && AnimationCounter==1)_session.Sound.PlaySound(0x92);
        int offset=parameter*4;
        if(offset+2>=_offsets.Length)throw new InvalidOperationException($"makuTreeBubble.s: invalid offset index ${parameter:x2}.");
        int y=unchecked((byte)((int)_parent.Npc.Position.Y+(sbyte)_offsets[offset]));
        int x=unchecked((byte)((int)_parent.Npc.Position.X+(sbyte)_offsets[offset+1]));
        Write(0x0b,y);Write(0x0d,x);Write(0x0f,0xf8);
        Write(0x26,_offsets[offset+2]);Write(0x27,_offsets[offset+2]);
        Position=new(x,y);Write(0x20,AnimationCounter);Write(0x21,parameter);
    }
    public override void _Draw()
    {if(Visible)DrawTexture(CurrentTexture,DrawOffset);}
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>_sword=collision??SwordCollision.Type(state,level);
    public bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds);
    private bool Hit(int item,Rect2 bounds)
    {
        if(State!=1 || Read(0x29)==0 || Read(0x26)==0)return false;
        var collisions=ObjectCollisionDatabase.For(AssetRoot);
        if(!collisions.EnemyEnabled(0x56,item))return false;
        int effect=collisions.Effect(Read(0x25),item);
        var center=new Vector2(Read(0x0d),Read(0x0b));
        var radius=new Vector2(Read(0x27),Read(0x26));
        if(!bounds.Intersects(new(center-radius,radius*2)))return false;
        if(effect==0)return true;
        // enemyCode56 treats any common status/damage reaction as state2.
        Write(0x29,0);return true;
    }
}
