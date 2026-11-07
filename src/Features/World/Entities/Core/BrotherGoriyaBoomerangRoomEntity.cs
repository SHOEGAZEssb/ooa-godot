namespace Oracles;

// seasons/parts/38.s: pause, fly to the captured target, then to the other brother.
internal sealed partial class BrotherGoriyaBoomerangRoomEntity : TransitionOffsetNode2D,IRoomEntity,IFixedRoomEntity,
    IRoomEntityLifetime,IPostObjectLinkContactRoomEntity,IObjectCollisionHeightRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,INativePartHealthRoomEntity
{
    private readonly RoomEntityManager _world;
    private readonly int _parentSlot,_receiverSlot;
    private readonly Vector2 _target;
    private readonly EnemyAnimationPlayer _animation;
    private int _slot=-1,_invincibility;
    private bool _collisionEnabled=true;
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Angle {get;private set;}
    public Node2D Node=>this;
    public bool Finished {get;private set;}
    public int CollisionZ=>0;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal Rect2 Bounds=>new(Position-new Vector2(2,2),new(4,4));
    internal BrotherGoriyaBoomerangRoomEntity(BrotherGoriyaBoomerangSpawn spawn,EnemyProjectileVisualRecord visual,RoomEntityManager world)
    {
        _world=world;_parentSlot=spawn.Parent.Slot;_receiverSlot=spawn.Receiver.Slot;_target=spawn.Target.Floor();
        AssetRoot=visual.AssetRoot;Position=spawn.Parent.Position.Floor();Angle=spawn.Parent.Angle;Visible=false;
        _animation=new(this,1);_animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites,AssetRoot),visual.Animations,
            visual.TileBase,visual.Palette,sourceGrayscaleInverted:visual.SourceGrayscaleInverted);_animation.SetAnimation(0);
    }
    internal void BindSlot(int slot)
    {
        _slot=slot;_world.RuntimeState.SetWramByte(0xd0d7+slot*0x100,(byte)(0xd0+_parentSlot));
        Parent!.BoomerangHigh=(byte)(0xd0+slot);
    }
    private BrotherGoriyaCharacter? Parent=>_world.ResolveEnemySlot(_parentSlot) switch
    {
        null=>null, // enemyDelete clears the entire related ENEMY struct.
        {Node:BrotherGoriyaCharacter actor}=>actor,
        var entity=>throw new NotSupportedException($"part38.s: relatedObj1 ENEMY slot${_parentSlot:x2} now contains {entity.GetType().Name}; its var18/var19 alias is not represented.")
    };
    public void ClearHealthAndCollision()=>_collisionEnabled=false;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(_slot<0)throw new InvalidOperationException("PART$38 has no physical slot.");
        if(_world.RuntimeState.ReadWramByte(0xd0d7+_slot*0x100)==0){Finished=true;Visible=false;return;}
        if(_invincibility!=0)_invincibility++;
        if(State==0)
        {
            State=1;Counter=20;Visible=true;
            ZIndex=ObjectDrawPriority.FromVisible((Parent?.AnimationIndex??0)==0?0x82:0x81);return;
        }
        if(State==1)
        {
            if(Counter!=0&&--Counter!=0)return;
            State=2;ZIndex=ObjectDrawPriority.FromVisible(0x81);
        }
        Vector2 target=State==2?_target:_world.ResolveEnemySlot(_receiverSlot)?.Node.Position??Vector2.Zero;
        Angle=OracleObjectMovement.Shared.RelativeAngle(Position,target);
        if(State==2)
        {
            if(BrotherGoriyaCharacter.Near(Position,target,8))State=3;
        }
        else if(BrotherGoriyaCharacter.Near(Position,target,4))
        {
            if(Parent is {} parent)parent.BoomerangHigh=0;
            _world.RuntimeState.SetWramByte(0xd098+_parentSlot*0x100,0);
            _world.RuntimeState.SetWramByte(0xd099+_parentSlot*0x100,0);
            Finished=true;Visible=false;return;
        }
        Position=NativeObjectMovement.ApplySpeed(_world.RuntimeState,OracleObjectPosition.FromPixels(Position),0x50,Angle).PrecisePosition;
        _animation.Advance();QueueRedraw();
    }
    public void HandleLinkContact(Player player)
    {
        if(State==0||Finished||!_collisionEnabled||_invincibility!=0||!player.EnemyContactHeightOverlaps(0))return;
        if(player.IsUsingShield&&player.Inventory.ShieldLevel is >=1 and <=3 && player.CanAcceptShieldCollision &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Bounds,player.ShieldCollisionBounds))
        {_invincibility=-28;_world.OnSoundRequested(SoundId.SndClink2);return;}
        if(player.NativeObjectVulnerable&&player.OverlapsEnemyCollision(Bounds,0))
            player.ApplyEnemyContactDamage(Position,2,RingDamageSource.Generic,34,15);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public override void _Draw(){if(Visible&&!Finished)DrawTexture(_animation.CurrentTexture,new Vector2(-16,-16)+TransitionDrawOffset);}
}
