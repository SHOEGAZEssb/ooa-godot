using Godot;
using System.Collections.Generic;

namespace oracleofages;

// PART $49 dispatches before INTERACTION $66. Its fuse pauses while carried;
// releasing it enters the explosion immediately, without a thrown-bomb fuse.
internal sealed class GoronBombRoomEntity : RoomEntityAdapter<NpcCharacter>,
    IFixedRoomEntity, IRoomEntityLifetime, IPostObjectLinkContactRoomEntity, IBraceletInteractableRoomEntity
{
    private readonly GoronBigBangController _owner;
    private readonly int _subid;
    private int _state, _counter, _wave, _angle, _speed, _z, _speedZ, _radius=3;
    private Vector2 _position;
    public bool Finished { get; private set; }
    internal int State => _state;
    internal int Counter => _counter;
    internal int Wave => _wave;
    internal GoronBombRoomEntity(NpcCharacter actor,GoronBigBangController owner,int subid)
        : base(actor,actor.SetTransitionDrawOffset)
    {
        _owner=owner; _subid=subid; actor.SetAnimationRate(0);
        actor.SetScriptVisible(subid!=0xff); owner.Parts.Add(this);
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished) return;
        var context=_owner.Context;
        if(_state==0)
        {
            if(_subid==0xff) { _state=5; SetWave(); return; }
            int[] xy=_owner.Database.Bytes("bomb-791b");
            _position=new(xy[_subid*2+1],xy[_subid*2]);
            _angle=(OracleObjectMovement.Shared.RelativeAngle(_position,frame.Player.Position)+
                _owner.Database.Bytes("bomb-790b")[context.Entities.NextRandomValue()&15])&ObjectAngle.Mask;
            _speed=_owner.Database.Bytes("bomb-79a2")[_owner.Database.Bytes("bomb-7962")[_subid*4+Quadrant()]];
            _speedZ=-0x280; _state=1; context.Sound.PlaySound(SoundId.SndPoof);
        }
        switch(_state)
        {
            case 1:
                var velocity=OracleObjectSpeedTable.Shared.GetRaw(_speed,_angle);
                _position=OracleObjectPosition.FromPixels(_position).Add(velocity.YFixed,velocity.XFixed).PrecisePosition;
                if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20))
                {
                    int rebound=(-_speedZ)>>1;
                    if(rebound> -0x80) { _state=3; _counter=0x14; }
                    else { _speedZ=rebound; _speed>>=1; }
                }
                Entity.AdvanceAnimationUpdates(1); break;
            case 2:
                if(!frame.Player.IsCarryingObject) { Explode(); break; }
                var motion=new CarriedObjectMotion(); motion.Hold(frame.Player);
                _position=motion.GroundPosition; _z=motion.ZFixed; break;
            case 3:
                if(--_counter==0) { Explode(); break; }
                Entity.AdvanceAnimationUpdates(1);
                _position+=context.Rooms.CurrentRoom.GetMetatile(_position) switch
                {0x54=>Vector2.Up,0x55=>Vector2.Right,0x56=>Vector2.Down,0x57=>Vector2.Left,_=>Vector2.Zero};
                break;
            case 4:
                Entity.AdvanceAnimationUpdates(1); _radius=Entity.CurrentAnimationParameter;
                if(_radius==0xff) Finish(); break;
            case 5:
                if(--_counter!=0) return;
                _wave++; if(!SetWave()) return;
                int used=0, quadrant=Quadrant();
                for(int i=0;i<_owner.Database.Bytes("bomb-780f")[_wave*2+1];i++)
                {
                    int candidate;
                    do { candidate=_owner.Database.Bytes("bomb-789d")[quadrant*8+(context.Entities.NextRandomValue()&7)]; }
                    while((used&(1<<candidate))!=0);
                    if(!context.Entities.PartSlotAvailable) continue;
                    context.Entities.Spawn<NpcCharacter>(new GoronBombSpawn(_owner,candidate));
                    used|=1<<candidate;
                }
                return;
        }
        Entity.Position=_position; Entity.SetScriptDrawOffset(new(0,_z>>8));
    }
    private int Quadrant() => (_owner.Context.Player.Position.X<0x50?0:2)+(_owner.Context.Player.Position.Y<0x40?0:1);
    private bool SetWave()
    {
        _counter=_owner.Database.Bytes("bomb-780f")[_wave*2];
        if(_counter!=0xff) return true;
        _owner.Context.Entities.RuntimeState.SetWramByte(WramAddress.wTmpcfc0,1); Finish(); return false;
    }
    private void Explode()
    {
        _state=4;
        string animation=_owner.Database.Animation(0x49,1);
        Entity.Initialize(_owner.Database.BombRecord with {TileBase=0x0c,Palette=2,
            UpAnimation=animation,RightAnimation=animation,DownAnimation=animation,LeftAnimation=animation});
        Entity.SetAnimationRate(0); Entity.SetScriptAnimation(animation);
        Entity.SetFixedDrawPriority(NpcCharacter.InFrontOfLinkZIndex);
        Entity.Position=_position; Entity.SetScriptDrawOffset(new(0,_z>>8));
        _owner.Context.Sound.PlaySound(SoundId.SndExplosion);
    }
    public bool TryUseBracelet(Player player,Vector2I releaseDirection)
    {
        if(_state==2) { player.EndCarriedObjectPose(); Explode(); return true; }
        if(_state!=3||Finished||player.IsCarryingObject||player.CutsceneControlled) return false;
        Vector2 delta=_position-(player.Position+(Vector2)player.FacingVector*6);
        if(System.Math.Abs(delta.X)>=13||System.Math.Abs(delta.Y)>=13) return false;
        _state=2; player.BeginCarriedObjectPose(); return true;
    }
    public void HandleLinkContact(Player player)
    {
        if(Finished||_state!=4||!RoomEntityManager.ObjectCollisionZOverlaps(_z>>8,player.EnemyContactZ,7)) return;
        Vector2 delta=player.EnemyContactPosition-_position;
        if(delta.X>=-_radius-5&&delta.X<_radius+5&&delta.Y>=-_radius-5&&delta.Y<_radius+5)
            player.ApplyEnemyContactDamage(_position,0,RingDamageSource.Generic,0x22,0x0f,allowZeroDamage:true);
    }
    internal void Finish() { Finished=true; Entity.SetActive(false); }
}
