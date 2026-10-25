using Godot;

namespace Oracles;

internal sealed partial class VeranFinalCharacter
{
    private void Spider(RoomEntityFrame frame)
    {
        switch(State)
        {
            case 0:return;
            case 1:
                AdvanceAnimation();if(AnimationParameter!=255)return;
                InitializeForm(SpiderHealth,0x6c,16,16);RestartAnimation(5);SpiderRoam(resetAttack:true);return;
            case 2:
                if(Dec2()==0){Counter2=120;if(SpiderChooseAttack(frame.Player.Position))return;}
                if(Dec1()==0){SpiderRoam();return;}SpiderMove();AdvanceAnimation();return;
            case 3:
                if((_z&0x8000)!=0){OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20);return;}
                if(Dec1()!=0){if((Counter&1)==0)_z=unchecked((short)(_z^0x200));return;}
                _z&=255;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;SpiderCollision(false);SpiderRoam();return;
            case 4:
                switch(Attack)
                {
                    case 0:
                        if(Substate==0){Substate=1;Counter=60;Speed=0x3c;Toward(frame.EnemyTarget??frame.Player.Position);Angle=(Angle&0x18)+4;AdvanceAnimation();return;}
                        if(Dec1()==0){SpiderRoam(resetAttack:true);return;}
                        SpiderMove();AdvanceAnimation();AdvanceAnimation();return;
                    case 1:SpiderJump(frame);return;
                    case 2:SpiderGrab(frame.Player);return;
                    default:throw Unsupported();
                }
            default:throw Unsupported();
        }
    }
    private void SpiderRoam(bool resetAttack=false)
    {
        if(resetAttack)Counter2=120;
        State=2;Speed=0x1e;Counter=Data.Bytes("spider-counters")[World.Entities.Random.Next().Value&3];
        int offset=(Position.Y>=88?16:0)+(Position.X>=120?8:0)+(World.Entities.Random.Next().Value&7);
        Angle=Data.Bytes("spider-angles")[offset];AdvanceAnimation();
    }
    private void SpiderMove()
    {
        Angle=EnemyAdjacentWallResolver.Shared.BounceAngle(Position,Angle,
            point=>World.Room.IsSolidForEnemyMovement(point,holesAreWalls:true));
        Move();
    }
    private bool SpiderChooseAttack(Vector2 link)
    {
        int angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),link.Floor());
        bool Within(int distance)=>(((int)link.Y-(int)Position.Y+distance)&255)<distance*2+1&&
            (((int)link.X-(int)Position.X+distance)&255)<distance*2+1;
        int distanceX=((int)link.X-(int)Position.X+0x60)&255;
        // Source bug: this AND reads the X-distance result, not the saved angle.
        if(Within(0x60)&&(((distanceX&7)-3)&255)<3)Attack=0;
        else if(!Within(0x50)&&(((angle&7)-3)&255)>=3)Attack=1;
        else if(((angle-12)&255)<9)Attack=2;
        else return false;
        State=4;Substate=0;return true;
    }
    private void SpiderCollision(bool vulnerable)
    {
        CollisionMode=vulnerable?0x6b:0x6c;SetNativeCollisionRadii(16,vulnerable?8:16);RestartAnimation(vulnerable?6:5);
    }
    private void SpiderJump(RoomEntityFrame frame)
    {
        switch(Substate)
        {
            case 0:
                if(!World.SpawnPart(this,0x56,0))return;Substate=1;WebSignal=0;SpiderCollision(true);
                ZIndex=ObjectDrawPriority.FixedLowPriorityZIndex;return;
            case 1:
                if(WebSignal==0)return;_z=unchecked((short)(_z-0x300));
                if((_z>>8)>=0||((_z>>8)<-32&&(((int)Position.Y+(_z>>8)-(int)-World.Entities.ToScreen(Vector2.Zero).Y)&255)>=0xb0))
                {Substate=2;Counter=90;_collision=false;_z&=255;Visible=false;}return;
            case 2:
                if(Dec1()!=0)return;Substate=3;_collision=true;_speedZ=0x100;
                var target=(frame.EnemyTarget??frame.Player.Position).Floor();
                Position=target+new Vector2(Position.X%1,Position.Y%1);
                int cameraY=(int)-World.Entities.ToScreen(Vector2.Zero).Y;
                int offset=(-(int)Position.Y-8)&255,height=cameraY+offset;
                if(height>255)height=offset;if((height&128)==0)height=128;
                _z=unchecked((short)((height<<8)|(_z&255)));
                SpiderCollision(false);Visible=true;ZIndex=ObjectDrawPriority.FixedLowPriorityZIndex;return;
            case 3:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20))return;
                Substate=4;Counter=120;World.Entities.OnSoundRequested(SoundId.SndStrongPound);World.Entities.BeginScreenShake(90);
                ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;return;
            case 4:if(Dec1()==0)SpiderRoam(resetAttack:true);return;
            default:throw Unsupported();
        }
    }
    private void SpiderGrab(Player player)
    {
        void LinkPosition(int y)=>player.CopyVeranSpiderGrabPosition(new(Position.X,((int)Position.Y+y)&255),_z>>8);
        switch(Substate)
        {
            case 0:Substate=1;Counter=30;WebSignal=0;SpiderCollision(false);return;
            case 1:if(Dec1()!=0)return;Counter2=8;Substate=2;SpiderCollision(true);return;
            case 2:
                if(Dec2()!=0)return;if(!World.SpawnPart(this,0x56,1))return;
                Substate=3;Counter=90;SpiderCollision(false);return;
            case 3:
                if(WebSignal==0||Dec1()!=0)return;
                if(!player.EnemyGrabActive){SpiderRoam(resetAttack:true);return;}
                Substate=4;Counter=16;RestartAnimation(6);LinkPosition(-8);return;
            case 4:
                if(Dec1()!=0)return;Counter=4;Substate=5;RestartAnimation(5);World.Entities.BeginScreenShake(4);
                LinkPosition(20);player.ApplyVeranSpiderGrabDamage(-6,8);World.Entities.OnSoundRequested(SoundId.SndStrongPound);return;
            case 5:if(Dec1()!=0)return;Counter=8;Substate=6;RestartAnimation(6);LinkPosition(-10);return;
            case 6:
                if(Dec1()!=0)return;Counter=15;Substate=7;RestartAnimation(5);World.Entities.BeginScreenShake(20);
                LinkPosition(20);player.ApplyVeranSpiderGrabDamage(-10,24);World.Entities.OnSoundRequested(SoundId.SndStrongPound);return;
            case 7:
                if(Dec1()!=0)return;_collision=true;SpiderRoam(resetAttack:true);
                // The source falls through into veranFinal_grabbingLink.
                player.ReleaseVeranSpiderGrab();return;
            default:throw Unsupported();
        }
    }
}
