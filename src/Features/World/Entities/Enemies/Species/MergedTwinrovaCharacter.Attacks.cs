using Godot;
using System;

namespace Oracles;

internal sealed partial class MergedTwinrovaCharacter
{
    private bool WaitMovement()
    {if(MovementWait!=0)MovementWait--;return MovementWait!=0;}
    private void ChooseTarget()
    {
        State++;
        int index;
        do{index=World.Entities.Random.Next().Value&14;}while(index==TargetIndex);
        TargetIndex=index;var positions=World.Data.Bytes("merged-targets");Target=new(positions[index+1],positions[index]);
        World.Entities.OnSoundRequested(SoundId.SndCircling);
    }
    private bool MoveToTarget()
    {
        if((((int)Position.X-Target.X+2)&255)<5&&(((int)Position.Y-Target.Y+2)&255)<5)return true;
        Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),Target);
        Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;return false;
    }
    private Vector2 Offset(int y,int x)=>new(((int)Position.X+unchecked((sbyte)x))&255,((int)Position.Y+unchecked((sbyte)y))&255);
    private void Part(int id,int offsetY,RoomEntityFrame frame)
    {
        if(!World.Entities.PartSlotAvailable)return;
        int angle=id==0x4c?OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),(frame.EnemyTarget??frame.Player.Position).Floor()):0;
        World.Entities.AddEntity(new TwinrovaAttackPart(World,id,0,Offset(offsetY,0),ZFixed,angle));
    }
    private void Minion(int id,int y,int x,int angle=0)
    {
        World.Entities.TryAllocateEnemy(_=>new TwinrovaMinionRoomEntity(new(World,id,Slot,Offset(y,x),ZFixed,angle)));
    }
    private void AttackFrame(RoomEntityFrame frame)
    {
        if(Subid==0){LavaAttack(frame);return;}
        if(Subid!=1)throw new NotSupportedException($"mergedTwinrova.s: subid${Subid:x2}.");
        switch(State)
        {
            case 0x0c:
                State=0x0d;Substate=0;Counter=10;SwapDisabled=1;
                IcePattern=World.Entities.Random.Next().Value&1;Counter2=BossHealth<2?4:3;RestartAnimation(5);return;
            case 0x0d:
                if(Substate==0)
                {
                    if(Dec()!=0){AdvanceAnimation();return;}
                    Counter=30;Counter2=(Counter2-1)&255;
                    if(Counter2==0){Substate=1;Counter=120;SwapDisabled=0;RestartAnimation(6);return;}
                    var positions=World.Data.Bytes(BossHealth<2?"merged-ice-low":"merged-ice-high");int at=(Counter2-1)*4;
                    Minion(0x5d,positions[at],positions[at+1],positions[at+2+IcePattern]);return;
                }
                if(Substate!=1)throw new NotSupportedException($"mergedTwinrova_iceRoom_stateD: substate${Substate:x2}.");
                if(Dec()!=0)return;
                Counter=90;State=0x0e;RestartAnimation(2);return;
            case 0x0e:
                if(WaitMovement())return;
                Speed=0x3c;ChooseTarget();return;
            case 0x0f:
                if(!MoveToTarget())return;
                State=0x10;Substate=0;Counter=30;SwapDisabled=1;RestartAnimation(8);Part(0x4e,0xe8,frame);return;
            case 0x10:
                if(Substate==0)
                {
                    if(Dec()!=0){AdvanceAnimation();return;}
                    Counter=60;Substate=1;RestartAnimation(6);return;
                }
                if(Substate!=1)throw new NotSupportedException($"mergedTwinrova_iceRoom_state10: substate${Substate:x2}.");
                if(Dec()!=0)return;
                State=0x0e;Counter=90;MovementWait=30;SwapDisabled=0;RestartAnimation(2);return;
            default:throw new NotSupportedException($"mergedTwinrova_iceRoom: state${State:x2}.");
        }
    }
    private void DoneLavaAttack()
    {State=0x0c;MovementWait=30;MinimumMovement=150;SwapDisabled=0;RestartAnimation(1);}
    private void LavaAttack(RoomEntityFrame frame)
    {
        switch(State)
        {
            case 0x0c:
                if(WaitMovement())return;
                Speed=0x32;ChooseTarget();return;
            case 0x0d:
                if(MinimumMovement!=0)MinimumMovement--;
                if(!MoveToTarget())return;
                if(MinimumMovement!=0){State=0x0c;MovementWait=30;return;}
                State=0x0e;Substate=0;SwapDisabled=1;AttackIndex=(AttackIndex+1)&7;
                Attack=World.Data.Bytes("merged-attacks")[AttackIndex];return;
            case 0x0e:
                if(Attack==0)
                {
                    switch(Substate)
                    {
                        case 0:Substate=1;Counter=30;RestartAnimation(3);Part(0x4c,16,frame);return;
                        case 1:
                            if(Dec()!=0){AdvanceAnimation();return;}
                            Counter=16;Substate=2;RestartAnimation(4);return;
                        case 2:if(Dec()==0)DoneLavaAttack();return;
                    }
                }
                else
                {
                    switch(Substate)
                    {
                        case 0:Substate=1;Counter=10;Counter2=5;RestartAnimation(7);return;
                        case 1:
                            if(Dec()!=0){AdvanceAnimation();return;}
                            Counter=20;Counter2=(Counter2-1)&255;
                            if(Counter2==0){Counter=180;Substate=2;RestartAnimation(3);return;}
                            var positions=World.Data.Bytes("merged-bats");int at=(Counter2-1)*2;
                            Minion(0x5e,positions[at],positions[at+1]);return;
                        case 2:if(Dec()==0)DoneLavaAttack();return;
                    }
                }
                throw new NotSupportedException($"mergedTwinrova_lavaRoom_stateE: attack${Attack:x2}, substate${Substate:x2}.");
            default:throw new NotSupportedException($"mergedTwinrova_lavaRoom: state${State:x2}.");
        }
    }
}
