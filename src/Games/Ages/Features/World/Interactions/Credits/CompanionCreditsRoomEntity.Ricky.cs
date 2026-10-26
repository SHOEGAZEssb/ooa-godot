using System;

namespace Oracles;

internal sealed partial class CompanionCreditsRoomEntity
{
    private void RickyJump(int animation){Animation(animation);SpeedZ=-0x200;}
    private void AdvanceRicky()
    {
        switch(Substate)
        {
            // The source's LD L,substate / INC(HL) retains H in ROM after
            // rst_jumpTable. That ignored ROM write does not increment state.
            case 0:goto case 1;
            case 1:
                Move();int x=(int)Position.X;
                if((x&128)==0&&((x+24)&255)>=(int)context.Player.Position.X)
                {Substate+=2;ZFixed=0;Counter=60;_animation.Advance();return;}
                if(Gravity(0x40)){Substate++;Counter=8;_animation.Advance();}return;
            case 2:if(Decrement()){Substate=1;RickyJump(2);}return;
            case 3:if(Decrement()){Substate=4;Counter=90;Animation(0x14);}return;
            case 4:
                _animation.Advance();if(!Decrement())return;
                Substate=5;Counter=12;Animation(0x1f);
                if(context.Entities.InteractionSlotAvailable)context.Entities.Spawn(new EnemyClinkSpawn(Position+new Godot.Vector2(0x12,-8),InitializeOnUpdate:true,ZHigh:ZFixed>>8));
                return;
            case 5:if(Decrement()){Substate=6;Counter=60;Animation(0x1e);}return;
            case 6:
                if(!Decrement())return;
                Substate=7;Counter=30;context.Inventory.UnequipRing();context.Player.BeginNativeCreditsJump();context.Sound.PlayNativeSound(SoundId.SndJump);return;
            case 7:
                if(Decrement()){Substate=8;Counter=20;context.Player.Visible=false;context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects,1);}return;
            case 8:if(Decrement()){Substate=9;Angle=0x18;RickyJump(0x1c);}return;
            case 9:
                Move();if((((int)Position.X-16)&128)!=0){context.Entities.RuntimeState.SetWramByte(0xcfdf,1);return;}
                if(Gravity(0x40)){Substate=10;Counter=8;_animation.Advance();}return;
            case 10:if(Decrement()){Substate=9;RickyJump(0x1c);}return;
            default:throw new NotSupportedException($"Ricky credits substate${Substate:x2}.");
        }
    }
}
