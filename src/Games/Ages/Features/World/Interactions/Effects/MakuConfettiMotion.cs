using System;

namespace Oracles;

// makuConfetti.s:@state2. Component additions wrap at 16 bits; the speed
// comparison reverses acceleration on equality as well as overshoot.
internal static class MakuConfettiMotion
{
    internal static MakuConfettiStep Advance(int y,int x,int speedY,int speedX,int accelerationY,int accelerationX,
        int yOffset,int yLimit,int xLimit,int deleteY)
    {
        speedY=Add(speedY,accelerationY);speedX=Add(speedX,accelerationX);
        y=Add(Add(y,yOffset),speedY);x=Add(x,speedX);
        int high=(byte)(y>>8);bool alive=high<deleteY||high>=0xd8;
        if(alive)
        {
            if(Absolute(speedY)>=yLimit)accelerationY=unchecked((short)-accelerationY);
            if(Absolute(speedX)>=xLimit)accelerationX=unchecked((short)-accelerationX);
        }
        return new(y,x,speedY,speedX,accelerationY,accelerationX,alive);
    }
    private static int Add(int a,int b)=>unchecked((short)(a+b));
    private static int Absolute(int value)=>value==short.MinValue?0x8000:Math.Abs(value);
}

internal readonly record struct MakuConfettiStep(int Y,int X,int SpeedY,int SpeedX,int AccelerationY,int AccelerationX,bool Alive);
