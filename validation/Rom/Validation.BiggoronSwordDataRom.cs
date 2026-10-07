using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Data/clock boundary only: native parent and reserved weapon passes run;
    // Link/vehicle movement, damage targets and tile rewards are separate work.
    private void ValidateBiggoronSwordDataRom()
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x91); _entities.Clear();
        _inventory.GiveTreasure(TreasureId.BiggoronSword,1);
        var data=new BiggoronSwordDatabase();
        ReadOnlyMemory<byte> bytes=ValidationRom.LoadCleanUs();
        string Oam(SomariaRom rom,int slot)
        {
            int pointer=rom.Word(slot+0x1e);
            int offset=(0x13+(pointer>>14))*0x4000+(pointer&0x3fff);
            int count=bytes.Span[offset++];
            FailIf(count is <1 or >16,$"ITEM$0c native OAM pointer ${pointer:x4} is invalid.");
            return string.Join(';',Enumerable.Range(0,count).Select(part=>string.Join(',',
                Enumerable.Range(0,4).Select(cell=>bytes.Span[offset+part*4+cell]))));
        }
        int fixtures=0;
        foreach(bool mounted in new[]{false,true})
        foreach(bool raft in mounted?new[]{false,true}:new[]{false})
        foreach(int direction in Enumerable.Range(0,4))
        {
            var rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,direction,120,112);
            rom[0xc689]=0x0c; rom[0xc688]=0;
            rom[0xd01a]=0x80;
            // Declared vehicle ownership, not an executed vehicle controller.
            rom[0xcc2c]=(byte)(mounted?0xd1:0xd0); rom[0xd101]=(byte)(raft?0x13:0);
            var parent=new BiggoronSwordParentAnimation(data,mounted,raft);
            int updates=0,eligible=0,probes=0;
            rom.Update(1,1,updates++);
            if(parent.ConsumeTileProbe()) probes++;
            while(true)
            {
                string context=$"ITEM$0c mounted={mounted}, raft={raft}, dir={direction}, update={updates}";
                FailIf(parent.Active!=(rom[0xd200]!=0),context+": native parent lifetime differs.");
                if(!parent.Active) break;
                FailIf(rom[0xd200]!=0xff || rom[0xd204]!=1 || rom[0xd230]!=parent.Mode ||
                    rom[0xd220]!=parent.Counter || rom[0xd221]!=parent.Parameter ||
                    rom[0xd231]!=data.Frames(parent.Mode)[parent.Frame].Graphic,
                    context+$": parent counter/parameter runtime={parent.Counter}/${parent.Parameter:x2}, native={rom[0xd220]}/${rom[0xd221]:x2}; enabled/state/mode/graphic=${rom[0xd200]:x2}/{rom[0xd204]}/${rom[0xd230]:x2}/${rom[0xd231]:x2}, expected mode/graphic=${parent.Mode:x2}/${data.Frames(parent.Mode)[parent.Frame].Graphic:x2}.");
                int sector=data.SelectArc(parent.Parameter,direction);
                SwordArc arc=data.Arc(sector); BiggoronSwordGraphic weapon=data.Weapon(sector);
                FailIf(rom[0xd600]==0 || rom[0xd601]!=0x0c || rom[0xd604]!=1 || rom[0xd630]!=sector ||
                    rom[0xd60d]!=(byte)(120+arc.OffsetX) || rom[0xd60b]!=(byte)(112+arc.OffsetY) ||
                    rom[0xd60f]!=0xfe || rom[0xd626]!=arc.RadiusY || rom[0xd627]!=arc.RadiusX ||
                    rom[0xd624]!=weapon.Collision || rom[0xd628]!=weapon.Damage ||
                    rom[0xd63a]!=weapon.Damage || rom[0xd60b]==112 && rom[0xd60d]==120,
                    context+$": sector {sector} geometry/attributes differ; native enabled/id/state=${rom[0xd600]:x2}/${rom[0xd601]:x2}/{rom[0xd604]}, sector={rom[0xd630]}, XY={rom[0xd60d]},{rom[0xd60b]}, Z=${rom[0xd60f]:x2}, radius={rom[0xd626]},{rom[0xd627]}, collision/damage/base=${rom[0xd624]:x2}/${rom[0xd628]:x2}/${rom[0xd63a]:x2}; expected arc={arc}, collision/damage=${weapon.Collision:x2}/${weapon.Damage:x2}.");
                var frame=OracleGraphicsCache.GetAnimationDefinition(weapon.Animation).Frames.Single();
                FailIf(frame.Duration!=rom[0xd620] || frame.Parameter!=rom[0xd621] || frame.EncodedOam!=Oam(rom,0xd600) ||
                    weapon.TileBase!=rom[0xd61d] || weapon.OamFlags!=rom[0xd61c] || weapon.SourceOffset!=0xa0,
                    context+": native weapon OAM/graphics upload differs.");
                rom.PublishLinkGraphics();
                LinkGraphicRecord link=data.LinkGraphic(parent.Mode,parent.Frame,direction);
                int graphicsTable=6*0x4000+0x4451-0x4000;
                int graphicsPointer=bytes.Span[graphicsTable]|bytes.Span[graphicsTable+1]<<8;
                int record=6*0x4000+graphicsPointer-0x4000+rom[0xd032]*3;
                int source=bytes.Span[record+1]|bytes.Span[record+2]<<8;
                int sourceOffset=(source&0xffe0)-0x4000+((source&1)<<14);
                FailIf(link.GraphicsIndex!=rom[0xd032] || link.OamIndex!=bytes.Span[record] ||
                    link.ByteOffset!=sourceOffset || link.Oam!=Oam(rom,0xd000),
                    context+$": native Link graphics source/OAM differs: runtime=${link.GraphicsIndex:x2}/${link.ByteOffset:x4}/{link.Oam}, native=${rom[0xd032]:x2}/${sourceOffset:x4}/{Oam(rom,0xd000)}.");
                // Freeze two actual parent callers while the post pass keeps
                // resetting the weapon's inert pose. No long held-input loop.
                bool frozen=updates is 4 or 5;
                rom[0xcba0]=(byte)(frozen?1:0);
                if(!frozen) { parent.Update(); eligible++; }
                rom.Update(0,0,updates++);
                if(parent.ConsumeTileProbe()) probes++;
                FailIf(updates>40,context+": native parent did not complete within its source bound.");
            }
            FailIf(eligible!=33 || probes!=(parent.Mode==0x23?5:0) || rom[0xd600]!=0 || rom.RandomCalls!=0 ||
                !rom.Sounds.SequenceEqual(new[]{data.Weapon(0).Sound}),
                $"ITEM$0c fixture {fixtures}: expected 33 eligible parent advances, deleted child and one SND_BIGSWORD without RNG.");
            parent=new BiggoronSwordParentAnimation(data,mounted,raft);
            rom.Update(1,1,updates++); parent.ConsumeTileProbe();
            rom.ClearItemParents(); parent.Cancel(); rom[0xcba0]=1;
            rom.Update(0,0,updates++);
            FailIf(parent.Active || rom[0xd200]!=0 || rom[0xd600]!=0 || rom.RandomCalls!=0 ||
                !rom.Sounds.SequenceEqual(new[]{data.Weapon(0).Sound,data.Weapon(0).Sound}),
                "ITEM$0c cancelled parent must delete its reserved weapon in the frozen post pass.");
            fixtures++;
        }
        GD.Print($"Validated {fixtures} clean-US Biggoron Sword data/clock fixtures: all arcs, normal/mounted/raft parent timing, native weapon/Link OAM, text holds, completion and cue/RNG. Gameplay wiring remains separate.");
    }
}
