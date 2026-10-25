using System;

namespace Oracles;

internal sealed partial class GanonCharacter
{
    private int _reversePalette;
    private void ReversePalette()
    {
        if((World.Entities.RuntimeState.ReadWramByte(AgesWramAddress.wScrollMode)&1)==0||
            World.Entities.PaletteFadeActiveSource()||(World.Entities.FrameCounter&1)!=0)return;
        World.BackgroundPalette(_reversePalette++&7);
    }
    private void ReverseControls(RoomEntityFrame frame)
    {
        switch(Substate)
        {
            case 4:
                if(Dec()!=0){if(Counter<60&&(Counter&3)==0)Palette^=5;return;}
                if(Health==0)return;
                Substate=5;CollisionType&=127;Palette=1;World.BlackFade(true,2);return;
            case 5:
                if(World.Entities.PaletteFadeActiveSource())return;
                Substate=6;World.ReloadArena(4);
                // ganon_makeRoomBoundarySolid uses the $10 storage stride
                // and $0f playable width: the right edge starts at $1e.
                var room=World.Entities.ActiveRoom;
                for(int y=1;y<10;y++){room.SetPackedTileCollision((byte)(y*16),15);room.SetPackedTileCollision((byte)(y*16+14),15);}
                for(int x=0;x<15;x++){room.SetPackedTileCollision((byte)x,15);room.SetPackedTileCollision((byte)(0xa0+x),15);}
                return;
            case 6:
                Substate=7;Counter=60;CollisionType|=128;Speed=0x14;
                Counter2=World.Data.Bytes("reverse-delays")[World.Entities.Random.Next().Value&7];
                World.BlackFade(false,1);return;
            case 7:
                World.Entities.RuntimeState.SetWramByte(0xcbc3,2);
                if((World.Entities.FrameCounter&3)==0)
                {
                    Counter2=(Counter2-1)&255;
                    if(Counter2==0){Substate=9;CollisionType&=127;World.FastWhiteFade();return;}
                }
                if(Dec()==0){Substate=8;Counter=80;return;}
                Aim(frame);
                new EnemyTerrainMovement(this,World.Entities.ActiveRoom).MoveAtAngle(Angle,Speed,allowHoles:false);
                AdvanceAnimation();ReversePalette();return;
            case 8:
                World.Entities.RuntimeState.SetWramByte(0xcbc3,2);ReversePalette();
                if((World.Entities.FrameCounter&3)==0)Counter2=(Counter2-1)&255;
                if(Dec()==0){Substate=7;Counter=60;RestartAnimation(2);return;}
                if(Counter==60){RestartAnimation(5);Part(0x52,2);}return;
            case 9:
                if(World.Entities.PaletteFadeActiveSource())return;
                Substate=10;World.ReloadArena(3);World.BackgroundPalette(0);return;
            case 10:
                CollisionType|=128;World.ClearPalette();NextAttack();return;
            default:throw new NotSupportedException($"ganon_stateC: substate${Substate:x2}.");
        }
    }
}
