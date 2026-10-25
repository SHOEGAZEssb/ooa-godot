using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionNativeAdmissionRom()
    {
        foreach(int id in new[]{0x0b,0x0c,0x0d})
        foreach(int gate in new[]{0,1,2,3,4,5})
        foreach(bool batched in new[]{false,true})
        {
            var (actor,rom)=PrepareMountedCompanionRom(id,1);
            StepCompanionRom(actor,rom,3,Vector2.Right,batched);
            int mask=gate switch {0=>0x20,1=>0x80,2=>1,_=>0};
            int scroll=gate switch {3=>2,4=>8,5=>14,_=>1};
            _runtimeState.SetWramByte(0xcc8a,(byte)mask);rom[0xcc8a]=(byte)mask;
            _runtimeState.SetWramByte(0xcd00,(byte)scroll);rom[0xcd00]=(byte)scroll;
            Vector2 before=rom.Position;
            StepCompanionRom(actor,rom,8,Vector2.Right,batched);
            FailIf((gate!=2&&rom.Position!=before)||(gate==2&&rom.Position==before),
                "companionRetIfInactive must freeze mask$a0/scroll$0e while allowing a Link-only mask$01.");
            _runtimeState.SetWramByte(0xcc8a,0);rom[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,1);rom[0xcd00]=1;
            StepCompanionRom(actor,rom,5,Vector2.Right,batched);
            _entities.Clear();
        }
    }
}
