using Godot;
using System;

namespace oracleofages;

internal sealed partial class GanonAttackPart
{
    private int _componentY,_componentX;
    private void LargeBallPart(RoomEntityFrame frame)
    {
        if(Subid==0)
        {
            if(State==0){State=1;Counter=64;Visible=false;World.Entities.OnSoundRequested(SoundId.SndEnergyThing);}
            if(Dec()==0){Finish();return;}
            Visible=!Visible;AdvanceAnimation();return;
        }
        if(Subid==2)
        {
            if(State==0)
            {
                State=1;int animation=Angle<12?1:Angle<25?2:3;
                SetNativeCollisionRadii(10,animation==2?10:8);RestartAnimation(animation);
                Visible=true;ZIndex=ObjectDrawPriority.FromVisible(0x83);return;
            }
            if((AnimationParameter&128)!=0){Finish();return;}
            Visible=!Visible;AdvanceAnimation();return;
        }
        if(Subid!=1)throw new NotSupportedException($"PART$51:${Subid:x2}.");
        switch(State)
        {
            case 0:State=1;Counter=24;RestartAnimation(4);Visible=true;return;
            case 1:
                if(Dec()!=0){AdvanceAnimation();return;}
                Counter=255;State=2;_palette=5;
                HighPosition(new(((int)Position.X-16)&255,((int)Position.Y+8)&255));
                Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),frame.Player.Position.Floor());
                var velocity=NativeObjectMovement.ScaledVelocity(World.Entities.RuntimeState,0x50,Angle,2);
                _componentY=velocity.YFixed;_componentX=velocity.XFixed;Speed=_componentY&255;return;
            case 2:
                if(NativePartBoundary.HasTileCollisionOrBoundary(World.Entities.ActiveRoom,Position))
                {
                    if(World.Entities.TryCreateInteractionExplosion(Position,ZFixed>>8,var03:0)>=0)World.Entities.BeginScreenShake(60);
                    Finish();return;
                }
                Dec();
                if((Counter&7)==0&&World.Entities.PartSlotAvailable)
                    World.Entities.AddEntity(new GanonAttackPart(World,0x51,2,-1,Position,ZFixed,Angle));
                Position=OracleObjectPosition.FromPixels(Position).Add(_componentY,_componentX).PrecisePosition;
                AdvanceAnimation();return;
        }
        throw new NotSupportedException($"PART$51:01 state${State:x2}.");
    }
}
