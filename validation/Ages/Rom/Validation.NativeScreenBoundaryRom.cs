using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateNativeScreenBoundaryRom()
    {
        var rom=new ObjectMotionRom();
        foreach(int camera in new[]{0,8,80,248})
        foreach(bool horizontal in new[]{false,true})
        for(int coordinate=0;coordinate<256;coordinate++)
        {
            int x=horizontal?coordinate:(camera+32)&255;
            int y=horizontal?(camera+32)&255:coordinate;
            rom[0xffaa]=rom[0xffac]=(byte)camera;
            rom.Word(ObjectMotionRom.Object+10,y*256+0x80);
            rom.Word(ObjectMotionRom.Object+12,x*256+0x40);
            rom.Call(ObjectMotionRom.CheckScreenBoundary);
            Vector2 screen=new(x-camera+0.25f,y-camera+0.5f);
            FailIf(OracleObjectMath.IsInsideOriginalScreenBoundary(screen)!=rom.Carry,
                $"bank0.objectCheckWithinScreenBoundary camera${camera:x2}/YX=${y:x2}${x:x2}: shared predicate differs from the original unsigned high-byte boundary.");
        }
    }
}
