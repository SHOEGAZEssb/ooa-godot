using Godot;

namespace oracleofages;

internal sealed partial class VeranFinalCharacter
{
    private void Bee(RoomEntityFrame frame)
    {
        switch(State)
        {
            case 0:return;
            case 1:
                AdvanceAnimation();if(AnimationParameter!=255)return;
                RestartAnimation(7);State=2;Speed=0x50;InitializeForm(BeeHealth,0x6b,16,12);return;
            case 2:
                if((((int)Position.X-120+2)&255)>=5||(((int)Position.Y-88+2)&255)>=5)
                {Toward(new(120,88));Move();AdvanceAnimation();return;}
                State=3;var random=World.Entities.Random.Next();Counter=random.High&8;Counter2=Data.Bytes("bee-counters")[random.Low&1];NextBeeTarget();return;
            case 3:
                if(!MoveTowardTarget()){AdvanceAnimation();return;}
                SnapTarget();NextBeeTarget();
                if(Dec2()==0){State=4;Counter=1;Target=new((Position.X>=128)?0:240,224);}AdvanceAnimation();return;
            case 4:
                if(Dec1()==0){Counter=6;int toward=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),Target);int difference=(Angle-toward)&31;if(difference!=0)Angle=(Angle+(difference<16?-1:1))&31;}
                Move();if(Position.Y<192){AdvanceAnimation();return;}State=5;Counter=30;Visible=false;return;
            case 5:
                if(Dec1()!=0)return;Counter=15;State=6;
                int roll=World.Entities.Random.Next().Value&16;
                Position=new((roll==0?8:232)+Position.X%1,32+Position.Y%1);Target=new(roll==0?232:8,Target.Y);Angle=roll+8;
                Visible=true;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;return;
            case 6:
                if(Dec1()==0){Counter=15;World.SpawnPart(this,0x58,0);}Move();
                if((((int)Position.X-Target.X+1)&255)>=3){AdvanceAnimation();return;}
                State=7;Counter=60;_collision=false;Visible=false;return;
            case 7:
                if(Dec1()!=0)return;
                int excluded=Quadrant(frame.EnemyTarget??frame.Player.Position),cornerRoll;
                do{cornerRoll=World.Entities.Random.Next().Value;}while((cornerRoll&3)==excluded);
                Corner=cornerRoll&3;var positions=Data.Bytes("bee-corners");int at=Corner*4;
                Target=new(positions[at+1],positions[at]);Position=new(positions[at+3]+Position.X%1,positions[at+2]+Position.Y%1);
                Counter2=((cornerRoll&0x30)>>4)+2;State=8;_collision=true;Visible=true;ZIndex=ObjectDrawPriority.InFrontOfLinkZIndex;return;
            case 8:
                if(MoveTowardTarget()){SnapTarget();State=9;Counter=30;}AdvanceAnimation();return;
            case 9:if(Dec1()==0){Counter=25;State=10;}AdvanceAnimation();return;
            case 10:
                if(Dec1()!=0)
                {
                    if((Counter&7)==0)World.SpawnBee(this,((Counter&0x18)>>3)-1);
                    AdvanceAnimation();return;
                }
                Counter=20;State=Dec2()==0?2:11;AdvanceAnimation();return;
            case 11:
                if(Dec1()!=0){AdvanceAnimation();return;}State=8;
                int quadrant=Quadrant(frame.EnemyTarget??frame.Player.Position),choice;
                do{choice=World.Entities.Random.Next().Value&3;}while(choice==quadrant||choice==Corner);
                Corner=choice;var corners=Data.Bytes("bee-corners");Target=new(corners[choice*4+1],corners[choice*4]);AdvanceAnimation();return;
            default:throw Unsupported();
        }
    }
    private void NextBeeTarget()
    {int at=Counter*2;Counter=(Counter+1)&15;var positions=Data.Bytes("bee-targets");Target=new(positions[at+1],positions[at]);}
    private static int Quadrant(Vector2 target)=>(target.Y>=88?2:0)+(target.X>=120?1:0);
}
