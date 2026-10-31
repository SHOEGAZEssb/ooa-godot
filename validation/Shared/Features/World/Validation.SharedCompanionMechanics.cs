using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedCompanionMechanics(FrontendValidationRoot root)
    {
        var rooms=root._gameplaySession!.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var game=rooms.RuntimeState.Game;
        var before=entities.Random.CaptureState();
        var data=new CompanionTerrainDatabase(rooms.AssetRoot);
        var weapons=CompanionWeaponDatabase.For(rooms.AssetRoot);
        var rom=ValidationRom.LoadCleanUs(game);
        var oamMemory=new OracleRuntimeState(game);
        var oamNative=new LinkCollisionRom(game:game,bankedStack:true);
        var oam=SpecialObjectOamDatabase.For(rooms.AssetRoot);
        for(int id=0;id<0x14;id++)
        {
            oamMemory.SetWramByte(0xd101,(byte)id);oamNative[0xd101]=(byte)id;
            oam.InitializeCompanion(oamMemory);
            oamNative.Call(game==OracleGame.Ages?0x41f7:0x41b5,bank:5,objectPage:0xd1);
            foreach(int at in new[]{0xd11b,0xd11c,0xd11d,0xd132})
                FailIf(oamMemory.ReadWramByte(at)!=oamNative[at],$"{game}: SPECIALOBJECT${id:x2} OAM initialization byte${at:x4} differs.");
        }
        foreach(bool foreign in new[]{false,true})
        {
            var rejectedMemory=new OracleRuntimeState(foreign?(game==OracleGame.Ages?OracleGame.Seasons:OracleGame.Ages):game);
            rejectedMemory.SetWramByte(0xd101,0xff);
            var image=rejectedMemory.CaptureState();bool rejected=false;
            try{oam.InitializeCompanion(rejectedMemory);}
            catch(ArgumentException) when(foreign){rejected=true;}
            catch(NotSupportedException error) when(!foreign&&error.Message.Contains("$ff")){rejected=true;}
            FailIf(!rejected || !image.Wram.AsSpan().SequenceEqual(rejectedMemory.CaptureState().Wram),
                $"{game}: wrong-pack/unknown native OAM identity must reject before writing state.");
        }
        int vines=game==OracleGame.Ages?0x144be:0x14471;
        FailIf(data.VineTop!=(game==OracleGame.Ages?0xd4:0xdd) || data.VineTop!=rom.Span[vines+13] ||
            data.VineMiddle!=rom.Span[vines+9] || data.VineBottom!=rom.Span[vines+5],
            $"{game}: native companion vine IDs must follow the selected ROM, not Ages tile numbers.");
        // Literal clean-US addresses are independent of the importer symbol resolver.
        int carry=game==OracleGame.Ages?0x1785d:0x17723;
        int cliff=game==OracleGame.Ages?0x14796:0x14771;
        int collision=game==OracleGame.Ages?0x144ae:0x14461;
        foreach(var spec in new[]{(Kind:"carry",At:carry,Count:4),(Kind:"cliff",At:cliff,Count:4),(Kind:"collision",At:collision,Count:8)})
        {
            int x=0,y=0;
            for(int index=0;index<spec.Count;index++)
            {
                int dy=unchecked((sbyte)rom.Span[spec.At+index*2]);
                int dx=unchecked((sbyte)rom.Span[spec.At+index*2+1]);
                if(spec.Kind=="collision") { x+=dx; y+=dy; } else { x=dx; y=dy; }
                FailIf(data.Probes(spec.Kind)[index]!=new Vector2(x,y),
                    $"{game}: {spec.Kind} companion probe${index:x2} differs from native Y/X order.");
            }
        }
        FailIf(!data.Probes("collision").SequenceEqual(new Vector2[]{new(-3,-5),new(4,-5),new(-3,8),new(4,8),
            new(-5,-3),new(-5,6),new(6,-3),new(6,6)}),$"{game}: cumulative wall probes lost source order.");
        int masks=game==OracleGame.Ages?0x1535:0x1523;
        for(int index=0;index<16;index++)
            FailIf(data.CollisionMasks[index]!=rom.Span[masks+index],$"{game}: companion collision mask${index:x2} differs from native bank0.");
        FailIf(!data.CollisionMasks.SequenceEqual(new[]{0,255,3,192,195,195,195,0,0,255,3,192,193,193,255,0}),
            $"{game}: companion bridge/hole masks lost their source shape.");
        int attributes=game==OracleGame.Ages?0x1e5b3:0x1e351;
        foreach(int item in new[]{0x28,0x2a,0x2b})
        {
            var weapon=weapons.Weapon(item);
            FailIf(weapon.CollisionType!=(rom.Span[attributes+item*4]&0x7f) || weapon.Damage!=256-rom.Span[attributes+item*4+2] ||
                weapon.CollisionType!=(item==0x2b?0x0f:0x19) || weapon.Damage!=4,
                $"{game}: companion ITEM${item:x2} collision identity or signed damage differs.");
        }
        var collisions=ObjectCollisionDatabase.For(rooms.AssetRoot);
        var dimitri=new DimitriDatabase(rooms.AssetRoot);
        int enemyMasks=game==OracleGame.Ages?0x1e9a2:0x1e740;
        for(int type=0;type<128;type++)
            FailIf(collisions.EnemyEnabled(type,0x0f)!=((rom.Span[enemyMasks+type*4+1]&0x80)!=0) ||
                dimitri.AcceptsMouthCollision(type)!=((rom.Span[enemyMasks+type*4+1]&0x80)!=0),
                $"{game}: Dimitri mouth enemy type${type:x2} mask differs from the native bit15.");
        int effects=game==OracleGame.Ages?0x1ed0a:0x1ea90;
        int modes=game==OracleGame.Ages?0x7d:0x73;
        FailIf(collisions.ModeCount!=modes,$"{game}: native collision mode count differs.");
        for(int mode=0;mode<modes;mode++)
        {
            FailIf(collisions.Effect(mode,0x0f)!=rom.Span[effects+mode*32+0x0f] ||
                dimitri.CanSwallow(mode)!=(rom.Span[effects+mode*32+0x0f]==0x25),
                $"{game}: Dimitri mouth mode${mode:x2} differs from the native collision table.");
            FailIf(collisions.Effect(mode,0x0f) is not (0 or 0x1c or 0x25 or 0x26),
                $"{game}: native Dimitri mouth effect requires its explicit runtime binding.");
        }
        var after=entities.Random.CaptureState();
        FailIf(before.Rng1!=after.Rng1 || before.Rng2!=after.Rng2 || before.Calls!=after.Calls,
            $"{game}: loading companion mechanics consumed gameplay RNG.");
    }
}
