using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TargetCartCrystalRoomEntity(NpcCharacter actor,int subid,
    OracleRuntimeState state,GoronCaveDatabase data,Action<int> sound)
    : RoomEntityAdapter<NpcCharacter>(actor,actor.SetTransitionDrawOffset),
      IFixedRoomEntity,IRoomEntityLifetime,IRoomEnemyCounterEntity,ISeedCollisionTarget,
      IPostObjectItemCollisionRoomEntity,IObjectCollisionHeightRoomEntity,IScreenTransitionPreloadRoomEntity
{
    private int _state, _behaviour, _counter, _angle;
    private Vector2 _position;
    public bool Finished {get;private set;}
    public bool CountsAsEnemy=>!Finished;
    public int CollisionZ=>0;
    internal int SubId=>subid;
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // bank0.updateEnemies dispatches state zero during scrolling.
        // ENEMY $63 loads its configuration position before objectSetVisible80;
        // its state-one movement and counters remain frozen until the scroll ends.
        Initialize();
        return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible;
    }
    private void Initialize()
    {
        if(_state!=0||Finished) return;
        int configuration=state.ReadWramByte(0xcfd4);
        if(configuration>2) throw new InvalidOperationException($"ENEMY $63 configuration ${configuration:x2} is outside its source tables.");
        int[] positions=data.Bytes("crystal-configuration"+configuration);
        _position=new(positions[subid*2+1],positions[subid*2]);
        _behaviour=data.Bytes("crystal-behaviourTable")[configuration*16+subid];
        _counter=0x20; _angle=_behaviour==2?0x18:0; _state=1;
        Entity.SetAnimationRate(0); Entity.Position=_position;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished) return;
        if(_state==0)
        {
            Initialize(); return;
        }
        if(_state==2)
        {
            state.SetWramByte(0xcfde,(byte)(state.ReadWramByte(0xcfde)+1));
            if(subid<5) state.SetWramByte(0xcfdd,(byte)(state.ReadWramByte(0xcfdd)|(1<<subid)));
            sound(0x90); // SND_GALE_SEED
            for(int i=3;i>=0;i--) spawns.Add(new TargetCartDebrisSpawn(data.Effect(0x92,3),_position,i));
            Finish(); return;
        }
        if(_behaviour!=0)
        {
            if(--_counter==0) { _counter=0x40; _angle^=0x10; }
            OracleObjectMovement.Shared.ApplySpeed(ref _position,0x14,_angle);
        }
        if(subid<5&&state.ReadWramByte(0xcfdf)!=0) { Finish(); return; }
        Entity.Position=_position; Entity.AdvanceAnimationUpdates(1);
    }
    private bool Hit(Rect2 hitbox)
    {
        if(Finished||_state!=1||!hitbox.Intersects(new Rect2(_position-new Vector2(6,6),new Vector2(12,12)))) return false;
        _state=2; return true;
    }
    public SeedHitResult ApplySeedHit(Rect2 hitbox,Vector2 source,int seed,ICollection<RoomEntitySpawn> spawns)=>
        Hit(hitbox)?SeedHitResult.Consume:SeedHitResult.None;
    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox,Vector2 source,SeedRecord seed,int collisionType,ICollection<RoomEntitySpawn> spawns)=>
        Hit(hitbox)?new(true,SeedHitResult.Consume,true):default;
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 hitbox,Vector2 source,int damage,ICollection<RoomEntitySpawn> spawns)=>
        item==RoomEntityItemCollision.SwordBeam&&Hit(hitbox);
    internal void Finish() { Finished=true; Entity.SetActive(false); }
}
