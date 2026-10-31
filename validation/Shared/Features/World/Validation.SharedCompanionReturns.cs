using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    // Direct native common-helper contracts; body phase handoffs have separate gameplay probes.
    private static void CheckSharedCompanionReturns(FrontendValidationRoot root)
    {
        var rooms=root._gameplaySession!.Rooms;
        var game=rooms.RuntimeState.Game;
        var player=root._seasonsOpening?.Player??root._player;
        string pack=rooms.AssetRoot;
        var room=new OracleRoomData(0,0x97,0,0,0,0,new byte[80],new byte[256],
            OracleGraphicsCache.LoadImage(pack+"gfx/spr_link.png"),OracleGraphicsCache.LoadImage(pack+"gfx/gfx_hud.png"),
            new byte[2048],new Color[6,4],new BackgroundPaletteState(new Color[4],new Color[3,4]),new OracleAnimationData(pack)){AssetRoot=pack};
        foreach(int id in new[]{0x0b,0x0c,0x0d})
        foreach(bool assigned in new[]{false,true})
        foreach(int progress in new[]{0,1,2,3,4})
        {
            var save=OracleSaveData.CreateStandardGame(game);
            save.WriteField("wAnimalCompanion",(byte)(assigned?id:id==0x0b?0x0c:0x0b));
            save.WriteField("wFluteIcon",progress is 1 or 4?(byte)1:(byte)0);
            save.WriteField("wEssencesObtained",progress is 3 or 4?(byte)0x10:(byte)0);
            var memory=OracleRuntimeState.CreateInitialized(game);
            var inventory=new InventoryState(new TreasureDatabase(pack),save,runtimeState:memory);
            if(progress is 2 or 4)inventory.GiveTreasure(0x2e,0);else inventory.LoseTreasure(0x2e);
            var native=new BraceletRom(room,new(8,8),1,-1,game:game);
            native.LoadSave(save);native[0xcc2d]=2;native[0xcc30]=0x97;
            native[0xd100]=3;native[0xd101]=(byte)id;native[0xd108]=1;
            native.Word(0xd10a,0x3840);native.Word(0xd10c,0x58c0);
            native.DismountCompanion();
            CompanionRuntimeState.RememberAfterDismount(memory,inventory,id,2,0x97,new(88.75f,56.25f));
            var remembered=CompanionRuntimeState.ReadRemembered(memory);
            FailIf(remembered.Id!=native[0xcc24] || remembered.Group!=native[0xcc25] || remembered.Room!=native[0xcc26] ||
                remembered.Y!=native[0xcc27] || remembered.X!=native[0xcc28],
                $"{game}: dismounted animal${id:x2}/assigned{assigned}/progress{progress} remembered bytes differ from native.");
            bool forgotten=game==OracleGame.Seasons && !assigned && (progress==4 || id switch{0x0b=>progress==1,0x0c=>progress==2,_=>progress==3});
            FailIf(remembered.Id!=(forgotten?0:id) || remembered.Group!=2 || remembered.Room!=0x97 || remembered.Y!=56 || remembered.X!=88,
                $"{game}: dismount must save all anchors before conditionally clearing only the native ID.");
        }
        {
            var memory=OracleRuntimeState.CreateInitialized(game);
            var wrongOwner=new InventoryState(new TreasureDatabase(pack));
            var inventory=new InventoryState(new TreasureDatabase(pack),runtimeState:memory);
            foreach(bool foreignOwner in new[]{false,true})
            {
                byte[] before=memory.CaptureState().Wram;bool rejected=false;
                try{CompanionRuntimeState.RememberAfterDismount(memory,foreignOwner?wrongOwner:inventory,foreignOwner?0x0c:0x0a,0,0x97,new(72,64));}
                catch(ArgumentException){rejected=true;}
                FailIf(!rejected || !before.AsSpan().SequenceEqual(memory.CaptureState().Wram),
                    $"{game}: invalid dismount identity/inventory owner must fail before any WRAM mutation.");
            }
        }
        Vector2 oldRespawn=player.LocalRespawnPosition;Vector2I oldFacing=player.LocalRespawnFacingVector;
        try
        {
            foreach(bool fractional in new[]{false,true})
            foreach(bool tile20 in new[]{false,true})
            foreach(bool localBlocked in new[]{false,true})
            foreach(bool rememberedBlocked in new[]{false,true})
            {
                Vector2 fraction=fractional?new(.75f,.25f):Vector2.Zero;
                Vector2 local=new(40,40),remembered=new(72,64),fallback=new(104,88);
                room.SetPositionTileAndCollision(local,tile20?(byte)0x20:(byte)0,localBlocked?(byte)0x0f:(byte)0,0);
                room.SetPositionTileAndCollision(remembered,0,rememberedBlocked?(byte)0x0f:(byte)0,0);
                var save=OracleSaveData.CreateStandardGame(game);save.SetLastAnimalMountPoint(88,104);
                var memory=OracleRuntimeState.CreateInitialized(game);
                // The Seasons $20 path reads retained XY even with an empty remembered ID.
                CompanionRuntimeState.Remember(memory,0x0c,3,0xfe,remembered);CompanionRuntimeState.ForgetRemembered(memory);
                player.SetLocalRespawnPosition(local,Vector2I.Left);
                var native=new BraceletRom(room,new(8,8),1,-1,game:game);native.LoadSave(save);
                native[0xd100]=1;native[0xd101]=0x0c;native[0xd109]=8;native[0xd124]=0x80;
                native.Word(0xd10a,0x1840);native.Word(0xd10c,0x18c0);
                if(!fractional){native[0xd10a]=0;native[0xd10c]=0;}
                native[0xcc21]=40;native[0xcc22]=40;native[0xcc23]=3;
                native[0xcc24]=0;native[0xcc27]=64;native[0xcc28]=72;
                native.RespawnCompanion();
                int angle=8;
                Vector2 actual=CompanionHazard.ResolveRespawn(player,save,memory,ref angle,
                    p=>room.GetTerrainInfo(p).Collision==0 && room.GetTerrainInfo(p).Hazard==HazardType.None,room,new Vector2(24,24)+fraction);
                bool alternate=game==OracleGame.Seasons&&tile20;
                Vector2 expected=(alternate?rememberedBlocked:localBlocked)?fallback:alternate?remembered:local;
                FailIf(actual!=expected+fraction || actual!=new Vector2(native.Word(0xd10c)/256f,native.Word(0xd10a)/256f) ||
                    angle!=native[0xd109] || memory.ReadWramByte(0xd109)!=native[0xd109] ||
                    player.LocalRespawnPosition!=new Vector2(native[0xcc22],native[0xcc21]) || player.LocalRespawnFacingVector!=Vector2I.Left,
                    $"{game}: respawn tile20{tile20}/localBlocked{localBlocked}/rememberedBlocked{rememberedBlocked}/fraction{fractional}: runtime={actual}, native={native.Word(0xd10c)}/{native.Word(0xd10a)}.");
            }
        }
        finally{player.SetLocalRespawnPosition(oldRespawn,oldFacing);}
    }
}
