using Godot;
using System;

namespace oracleofages;

internal sealed partial class GanonCharacter
{
    private int Dec()=>Counter=(Counter-1)&255;
    private void HighPosition(int x,int y)=>Position=new Vector2(x&255,y&255)+Position-Position.Floor();
    private void DecideAttack()
    {
        AttackChoice=(Health<0x41?4:0)+(World.Entities.Random.Next().Value&3);SequenceIndex=0;NextAttack();
    }
    private void NextAttack()
    {
        int state=World.Data.Bytes($"attack-{AttackChoice}")[SequenceIndex++];
        if(state==0){DecideAttack();return;}State=state;Substate=0;
    }
    private void TeleportStart()
    {World.Entities.OnSoundRequested(SoundId.SndTeleport);CollisionType&=127;Counter=60;BaseX=(int)Position.X;}
    private void TeleportFlicker(bool outgoing)
    {
        int delta=(Counter&0x3e)>>1;if(outgoing)delta=32-delta;if((Counter&2)!=0)delta=-delta;
        HighPosition(BaseX+delta,(int)Position.Y);Visible=!Visible;
    }
    private void ChooseTeleport()
    {
        var rng=World.Entities.Random.Next();int at=rng.High&14;var positions=World.Data.Bytes("teleport-positions");
        HighPosition(positions[at+1],positions[at]);Counter=World.Data.Bytes("teleport-delays")[rng.Low&15];
    }
    private void Aim(RoomEntityFrame frame)=>Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),(frame.EnemyTarget??frame.Player.Position).Floor());
    private void Part(int id,int subid=0,int angle=0,int speed=0,bool uncheckedAllocation=false,int offsetY=0,int offsetX=0)
    {
        if(!World.Entities.PartSlotAvailable)
        {
            if(uncheckedAllocation)throw new NotSupportedException($"ganon.s: unchecked PART${id:x2} allocation aliases the full native pool.");
            return;
        }
        World.Entities.AddEntity(new GanonAttackPart(World,id,subid,Slot,
            new(((int)Position.X+offsetX)&255,((int)Position.Y+offsetY)&255),ZFixed,angle,speed));
    }
    private void CombatFrame(RoomEntityFrame frame)
    {
        if(Substate==0){TeleportStart();Substate=1;return;}
        if(Substate==1)
        {
            if(Dec()!=0){TeleportFlicker(true);return;}
            Substate=2;
            if(State is 8 or 0x0d)ChooseTeleport();
            else{Counter=State==0x0b?180:State==0x0c?90:120;if(State is 9 or 0x0c)HighPosition(0x78,0x58);}
            Visible=false;return;
        }
        if(Substate==2)
        {
            if(Dec()!=0)return;Substate=3;
            if(State==0x0a)
            {
                Vector2 target=(frame.EnemyTarget??frame.Player.Position).Floor();int x=(int)target.X,y=(int)target.Y;
                SlashSide=x<120?3:0;x+=SlashSide==3?40:-40;if(y>=0x30)y-=24;
                HighPosition(x,y);RestartAnimation(7+SlashSide);
            }
            if(State==0x0b){HighPosition(0x78,0x28);RestartAnimation(4);}
            TeleportStart();return;
        }
        if(Substate==3)
        {
            if(Dec()!=0){TeleportFlicker(false);return;}
            Substate=4;
            if(State is 8 or 0x0d)HighPosition(BaseX,(int)Position.Y);
            Visible=true;if(State==0x0d)return;
            CollisionType|=128;Counter=State==0x0a?2:State==0x0b?64:State==0x0c?90:8;
            if(State==0x0c)World.Entities.OnSoundRequested(SoundId.SndFadeOut);
            if(State==0x0a){Speed=0x78;Aim(frame);Part(0x50);}
            if(State==0x0b)Part(0x51,offsetY:-8,offsetX:16);
            return;
        }
        switch(State)
        {
            case 8:
                switch(Substate)
                {
                    case 4:if(Dec()==0){Counter=2;Substate=5;}return;
                    case 5:
                        if(Dec()!=0)return;Counter=45;Substate=6;RestartAnimation(5);Aim(frame);Part(0x52,angle:Angle,speed:0x3c);return;
                    case 6:
                        if(Dec()==0){Counter=60;Substate=7;RestartAnimation(2);return;}
                        if(Counter==25){Part(0x52,angle:(Angle+2)&31,speed:0x64);Part(0x52,angle:(Angle-2)&31,speed:0x64);}return;
                    case 7:if(Dec()==0)NextAttack();return;
                }
                break;
            case 9:
                switch(Substate)
                {
                    case 4:
                        if(Dec()!=0)return;Counter=40;Substate=5;RestartAnimation(9);
                        foreach(int angle in new[]{28,20,12,4})Part(0x52,1,angle,uncheckedAllocation:true);return;
                    case 5:if(Dec()==0){Counter=40;Substate=6;RestartAnimation(7);}return;
                    case 6:if(Dec()==0){Counter=80;Substate=7;RestartAnimation(2);}return;
                    case 7:if(Dec()==0)NextAttack();return;
                }
                break;
            case 0x0a:
                switch(Substate)
                {
                    case 4:
                        if(Dec()!=0)return;Counter=4;Substate=5;RestartAnimation(8+SlashSide);
                        // Source falls into substate5 on the same visit.
                        goto case 5;
                    case 5:
                        if(Dec()==0){Counter=16;Substate=6;RestartAnimation(9+SlashSide);return;}
                        SlashMove();return;
                    case 6:
                        if(Dec()==0){Substate=7;Counter=30;return;}SlashMove();return;
                    case 7:if(Dec()==0){RestartAnimation(2);NextAttack();}return;
                }
                break;
            case 0x0d:NextAttack();return;
            case 0x0b:LargeBall(frame);return;
            case 0x0c:ReverseControls(frame);return;
        }
        throw new NotSupportedException($"ganon.s: state${State:x2} substate${Substate:x2}.");
    }
    private void LargeBall(RoomEntityFrame frame)
    {
        switch(Substate)
        {
            case 4:if(Dec()==0){Substate=5;SpeedZ=-0x1c0;RestartAnimation(5);}return;
            case 5:
                int z=ZFixed,v=SpeedZ;bool landed=OracleObjectMath.UpdateSpeedZ(ref z,ref v,0x20);ZFixed=z;SpeedZ=v;
                if(!landed){if(SpeedZ==0x00c0)RestartAnimation(6);return;}
                Counter=120;Substate=6;World.Entities.SetScreenShake(120,World.Entities.HorizontalScreenShakeCounter,0);
                World.Entities.OnSoundRequested(SoundId.SndExplosion);return;
            case 6:
                if(Dec()!=0){if(Counter>=105&&frame.Player.EnemyContactZ>=0)frame.Player.RequestCollapse(0);return;}
                Counter=4;Substate=7;RestartAnimation(4);return;
            case 7:
                if(Dec()!=0)return;Counter=24;Substate=8;Part(0x51,1,offsetY:-8,offsetX:16);return;
            case 8:
                if(Dec()!=0)return;Counter=60;Substate=9;World.Entities.TryCreateVirePuff(Position,ZFixed>>8,0);RestartAnimation(5);return;
            case 9:if(Dec()==0){Counter=60;Substate=10;RestartAnimation(2);}return;
            case 10:if(Dec()==0)NextAttack();return;
        }
        throw new NotSupportedException($"ganon_stateB: substate${Substate:x2}.");
    }
    private void SlashMove()
    {
        if(((((int)Position.Y-24)&255)>=128)||((((int)Position.X-24)&255)>=192))return;
        Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    }
}
