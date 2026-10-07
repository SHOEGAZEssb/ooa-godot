using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBiggoronSwordGameplayRom()
    {
        int fixture=0;
        foreach(bool primary in new[]{false,true})
        foreach(int direction in Enumerable.Range(0,4))
        foreach(bool batched in RomHostSchedules(fixture++))
        {
            SomariaRom rom=PrepareSomariaMotionRom(direction,primary:primary);
            _inventory.GiveTreasure(TreasureId.BiggoronSword,1);
            EquipSomariaMotionItem(rom,TreasureId.BiggoronSword,primary);
            rom.InitializeLinkWalkingAnimation();
            if (primary && direction == 0)
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
            var seed=_random.CaptureState(); var sounds=_sound.AttachPlayRequestAudit();
            int button=primary?1:2,previous=0,update=0;
            void Check()
            {
                string context=$"Biggoron A={primary}, dir={direction}, batch={batched}, update={++update}";
                var sword=_entities.Biggoron!;
                FailIf(sword.Active!=(rom[0xd200]!=0 && rom[0xd201]==0x0c) ||
                    (sword.Weapon!=null)!=(rom[0xd600]!=0 && rom[0xd601]==0x0c),context+": parent/weapon lifetime differs.");
                if(sword.Active)
                    FailIf(sword.Parent!.Counter!=rom[0xd220] || sword.Parent.Parameter!=rom[0xd221] ||
                        sword.Parent.Mode!=rom[0xd230],context+": parent counter/parameter/mode differs.");
                if(sword.Weapon is {} weapon)
                    FailIf(weapon.State!=rom[0xd604] || weapon.Sector!=rom[0xd630] ||
                        weapon.Position!=new Vector2(rom[0xd60d],rom[0xd60b]) ||
                        weapon.ZHigh!=unchecked((sbyte)rom[0xd60f]) || weapon.Collision!=rom[0xd624] ||
                        weapon.Radius!=new Vector2I(rom[0xd627],rom[0xd626]) || weapon.Damage!=-(sbyte)rom[0xd628],
                        context+$": weapon runtime={weapon.State}/{weapon.Sector}/{weapon.Position}/{weapon.ZHigh}/{weapon.Radius}/{weapon.Damage}, native={rom[0xd604]}/{rom[0xd630]}/{rom[0xd60d]},{rom[0xd60b]}/{unchecked((sbyte)rom[0xd60f])}/{rom[0xd627]},{rom[0xd626]}/{-(sbyte)rom[0xd628]}.");
                var rng=_random.CaptureState();
                FailIf(rng.Rng1!=rom[0xff94] || rng.Rng2!=rom[0xff95] || rng.Calls-seed.Calls!=rom.RandomCalls ||
                    // The declared text owner renders the long ring pause;
                    // the native fixture executes item dispatch, not that text.
                    !sounds.Requests.Where(id => !(primary && direction == 0) || id != SoundId.SndText)
                        .SequenceEqual(rom.Sounds),context+$": RNG={rng.Rng1:x2},{rng.Rng2:x2},{rng.Calls-seed.Calls}/{rom[0xff94]:x2},{rom[0xff95]:x2},{rom.RandomCalls}; cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
            }
            void Step(int count=1,int held=0,int angle=0xff)
            {
                int edge=held&~previous; previous=held;
                StepSomariaMotionRom(rom,count,batched,angle,held,edge,Check);
            }
            Step(1,button); Step(4,angle:(direction*8+8)&31);
            Step(1,button); // Fresh press cannot replace the active $ff parent.
            _dialogue.ShowMessage("Biggoron clock pause.",_player.Position.Y); rom[0xcba0]=1;
            Step(3);
            if (primary && direction == 0)
            {
                for (int ring = 0; ring < 64; ring++)
                {
                    _inventory.GrantAppraisedRingForDebug(ring);
                    FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                        $"Cannot equip Biggoron ring ${ring:x2}.");
                    rom[WramAddress.wActiveRing] = (byte)ring;
                    // updateItemsPost reads the live ring even during text;
                    // Whimsical/Double-Edged never run ordinary sword rolls.
                    Step();
                }
                _inventory.EquipRingAt(0);
                rom[WramAddress.wActiveRing] = 0xff;
                Step();
            }
            _dialogue.Close(); rom[0xcba0]=0;
            Step(27);
            FailIf(!_entities.Biggoron!.Active || rom[0xd221]!=0x88,"Biggoron must retain its terminal pose for one update.");
            Step(angle: (direction * 8 + 8) & 31);
            FailIf(_entities.Biggoron.Active || rom[0xd200]!=0,"Biggoron must clear after exactly 33 eligible parent advances.");
            Step(1,button); Step(5);
            _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); Check();
            Step(3, angle: (direction * 8 + 24) & 31);
            Step(1,button); Step(34);
            FailIf(rom[0xd600]!=0 || _entities.Biggoron.Weapon!=null,"Repeated Biggoron swing did not retire its reserved weapon.");
        }
        var collision=new ObjectCollisionRom(); var data=BiggoronSwordCollisionDatabase.Shared;
        for(int mode=0;mode<0x7d;mode++)
            FailIf(data.Effect(mode)!=collision.Table(0x6d0a+mode*32+7),$"Biggoron native collision mode ${mode:x2} differs.");
        foreach(bool part in new[]{false,true})
        for(int type=0;type<(part?0x5a:128);type++)
        {
            collision.ClearObjects(); int target=part?0xd1c0:0xd080;
            collision[target]=1; collision[target+0xb]=64; collision[target+0xd]=80;
            collision[target+0x24]=(byte)(0x80|type); collision[target+0x26]=collision[target+0x27]=5;
            collision[target+0x29]=0x40; collision[target+0x3e]=1;
            collision[0xd600]=1; collision[0xd601]=0x0c;
            collision[0xd60b]=64; collision[0xd60d]=80; collision[0xd60f]=0xfe;
            collision[0xd624]=0x87; collision[0xd626]=collision[0xd627]=11; collision[0xd628]=0xfb;
            collision.Call(ObjectCollisionRom.Scan);
            bool dispatched=collision.Dispatches.Any(entry=>entry.Type==7);
            FailIf(dispatched!=(part?data.PartEnabled(type):data.EnemyEnabled(type)),
                $"Biggoron native {(part?"part":"enemy")} active mask ${type:x2} differs.");
        }
        CompareBiggoronSwordContactsRom();
        CompareBiggoronSwordTilesRom();
        GD.Print("Validated clean-US Biggoron A/B swings: exact parent/post arc, completion movement, text/live rings, cancellation/recast, cues/RNG, $7d collision rows, enemy/PART masks, ordered Keese/Armos contacts and actual L2 sign breaking with an L1 Sword.");
    }
}
