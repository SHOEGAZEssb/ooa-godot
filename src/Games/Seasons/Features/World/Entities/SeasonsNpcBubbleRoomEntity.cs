
namespace Oracles;

internal sealed partial class SeasonsNpcBubbleRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, INativePartHealthRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IPostObjectMeleeCollisionRoomEntity, ISwordHittableRoomEntity, ILinkSwordStateAwareRoomEntity,
    IPostObjectItemCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private readonly int _subid;
    private Vector2 _position;
    private int _state, _counter, _counter2, _speed, _angle, _sword=ItemCollisionType.L1Sword;
    private bool _hit;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => _state==0;
    public bool UpdatesDuringRoomEntityFreeze => _state==0;
    public bool MeleeReportsContact => false;
    internal SeasonsNpcBubbleRoomEntity(SeasonsOpeningSession session,SeasonsTalkNpcDatabase data,SeasonsNpcBubbleSpawn spawn)
    {
        _session=session;_data=data;_position=spawn.Position;_subid=spawn.Subid;_angle=spawn.Angle;
        InitializeVisual(data.BubbleVisual,_position,_subid,positionedOam:true);Visible=false;
        ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(_state==0)
        {
            _state=1;SetAnimation(_subid);
            _speed=_data.NativeTable("bubble-speeds")[_session.Random.Next().Value&3];
            _counter=(_session.Random.Next().Value&0x3f)+0x78;_counter2=16;Visible=true;return;
        }
        if(_state==2){AdvanceAnimation();if(AnimationParameter==0xff){Finished=true;Visible=false;}return;}
        if(_hit){Pop();_session.Sound.PlaySound(0xaf);return;}
        Position=NativeObjectMovement.ApplySpeed(_session.Memory,ref _position,_speed,_angle);
        if(_counter!=0)--_counter;
        if(_counter==0){Pop();return;}
        if(--_counter2!=0)return;
        _counter2=16;
        if((_session.Random.Next().Value&3)==0)_angle=(_angle-1)&255;
    }
    private void Pop(){_state=2;SetAnimation(2);}
    public void ClearHealthAndCollision()=>_hit=true;
    private bool Contact(int type,Rect2 bounds)
    {
        var collisions=ObjectCollisionDatabase.For(AssetRoot);
        if(_state!=1 || _hit || Finished || type is <0 or >=32 || !collisions.PartEnabled(0x32,type) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(new Rect2(Position-Vector2.One*2,Vector2.One*4),bounds))return false;
        int effect=collisions.Effect(0x72,type);
        if(effect==0x23)_hit=true;
        else if(effect!=0)throw new NotSupportedException($"poppableBubble.s: PART$32 item${type:x2} effect${effect:x2} is unbound.");
        return true;
    }
    public void SetLinkSwordState(SwordActionState state,int level,int? itemCollisionType=null)=>_sword=itemCollisionType??SwordCollision.Type(state,level);
    public bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Contact(_sword,bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision type,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)
    {
        var data=ObjectCollisionDatabase.For(AssetRoot);
        int collision=type switch
        {
            RoomEntityItemCollision.ThrownObject=>data.ItemType(ItemId.Bracelet),
            RoomEntityItemCollision.Boomerang=>data.ItemType(ItemId.Boomerang),
            RoomEntityItemCollision.Bomb=>data.ItemType(ItemId.Bomb),
            _=>(int)type
        };
        return Contact(collision,bounds);
    }
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns)=>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(0,item.ZHigh,7) && Contact(item.CollisionType,item.CollisionBounds)?new(true,false):default;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
}

internal sealed record SeasonsNpcBubbleSpawn(Vector2 Position,int Subid,int Angle) : RoomEntitySpawn;
