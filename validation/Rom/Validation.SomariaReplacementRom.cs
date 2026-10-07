using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaReplacementRom()
    {
        foreach(bool primary in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            SomariaRom rom=PrepareSomariaMotionRom(2,primary:primary);
            rom.InitializeLinkWalkingAnimation();
            _inventory.GiveTreasure(TreasureId.Sword,1);
            rom[0xc6b2]=(byte)_inventory.SwordLevel;
            var seed=_random.CaptureState(); var sounds=_sound.AttachPlayRequestAudit();
            int button=primary?1:2,other=primary?2:1;
            void Check()
            {
                var cane=_entities.Somaria!;
                bool active=rom[0xd200]!=0 && rom[0xd201]==0x04;
                FailIf(cane.Active!=active || (cane.Weapon!=null)!=(rom[0xd600]!=0 && rom[0xd601]==0x04),
                    $"Somaria replacement A={primary}, batch={batched}: native parent/weapon ownership differs.");
                if(active)
                    FailIf(cane.Parent!.Parameter!=rom[0xd221] || SomariaPrivate<int>(cane.Parent,"_counter")!=rom[0xd220],
                        "Somaria replacement parent parameter differs.");
                var rng=_random.CaptureState();
                FailIf(rng.Rng1!=rom[0xff94] || rng.Rng2!=rom[0xff95] || rng.Calls-seed.Calls!=rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),"Somaria replacement cue/global RNG order differs.");
            }
            void Step(int count,int held=0,int edge=0,int angle=0xff) =>
                StepSomariaMotionRom(rom,count,batched,angle,held,edge,Check);
            Step(1,button,button); Step(12,angle:8); Step(1);
            FailIf(_player.Position!=new Vector2(120,112) || _player.FacingVector!=Vector2I.Down || rom.Blocks.Length!=0,
                "Cane must retain native movement/turning locks before creation.");
            Step(1,button,button); Step(12,angle:8);
            FailIf(rom.Blocks.Length!=0,"An equal-priority Cane restart must restart its full creation delay.");
            _inventory.EquipA(primary?TreasureId.CaneOfSomaria:TreasureId.Sword);
            _inventory.EquipB(primary?TreasureId.Sword:TreasureId.CaneOfSomaria);
            rom[0xc689]=(byte)_inventory.EquippedA; rom[0xc688]=(byte)_inventory.EquippedB;
            Step(1,other,other);
            FailIf(!_player.IsAttacking || rom[0xd201]!=0x05 || rom.Blocks.Length!=0,
                "Higher-priority Sword must replace Cane before its pending block allocation.");
            Step(40);
            Step(1,button,button); Step(24);
            FailIf(rom.Blocks.Length!=1 || rom[rom.Blocks[0]+4]!=3,
                "Cane must permit a new complete cast after Sword completion.");
        }
        GD.Print("Validated clean-US Cane movement/turning locks, equal-priority restart, pending-creation Sword replacement and subsequent recast through split/batched A/B gameplay.");
    }
}
