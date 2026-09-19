using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class KingMoblinMinion : EnemyCharacter
{
    internal KingMoblinBoss Boss { get; private set; } = null!;
    internal KingMoblinBomb? Bomb { get; set; }
    internal int State { get; private set; }
    internal int SubId { get; private set; }
    internal int ZFixed => _z;
    private int _counter, _counter2, _z, _speedZ, _direction;
    internal override bool CollisionEnabled => false;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset+new Vector2(0,_z>>8);
    internal void Initialize(KingMoblinBoss boss,int subid)
    {
        Boss=boss; SubId=subid; boss.Minions[subid]=this;
        InitializeEnemy(Vector2.Zero,EnemyCharacterConfiguration.FromImported(boss.Data.Actor(0x56,subid)),positionedOam:true);
        Visible=false;
    }
    internal void StartFight() => State=2;
    internal void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(IsDead) return;
        switch(State)
        {
            case 0:
                Boss.World.Random.Next();
                var data=Boss.Data.Bytes("minions"); int offset=SubId*4;
                _counter=data[offset]; _direction=data[offset+1]; Position=new(data[offset+3],data[offset+2]);
                State=1; Visible=true; RestartAnimation(2); break;
            case 1: AdvanceAnimation(); break;
            case 2: State=3; _counter2=12; RestartAnimation(_direction); break;
            case 3:
                if(_counter2>0) _counter2--;
                if(_counter2!=0) {AdvanceAnimation(); break;}
                if(!Boss.World.PartSlot()) break;
                spawns.Add(new KingMoblinBombSpawn(Boss,this)); State=4; RestartAnimation(2); break;
            case 4:
                if(--_counter==0) {State=5; _speedZ=-0x180;}
                else if(Boss.World.Shaking()) State=7;
                AdvanceAnimation(); break;
            case 5:
                if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)) {State=6; _counter=16; AdvanceAnimation();}
                else if(_speedZ==0) Bomb!.Throw(OracleObjectMovement.Shared.RelativeAngle(Position,frame.Player.Position),null);
                break;
            case 6:
                if(--_counter==0) {_counter=200; State=2;}
                AdvanceAnimation(); break;
            case 7:
                State=8; _counter=24;
                Bomb!.Throw(Boss.Data.Bytes("escape-angles")[SubId],-0x100,0x37);
                RestartAnimation(SubId*2+1); break;
            case 8:
                if(--_counter==0)
                {
                    State=9; _speedZ=-0x140;
                    if(Boss.World.InteractionSlot()) spawns.Add(new KingMoblinExclamationSpawn(Position+new Vector2(SubId==0?-12:12,-8)));
                }
                break;
            case 9:
                if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)) {State=10; _counter=12; _counter2=8; RestartAnimation(0);} break;
            case 10:
                if(_counter2>0) _counter2--;
                if(_counter2==0)
                {
                    if(--_counter==0) {Boss.EscapeSignal=true; Finish(); break;}
                    Position+=new Vector2(0,-2);
                }
                AdvanceAnimation(); break;
        }
        QueueRedraw();
    }
}
