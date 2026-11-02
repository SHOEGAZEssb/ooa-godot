
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedDimitriBody(FrontendValidationRoot root,bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;var game=rooms.SaveData.Game;
        var entities=root._seasonsOpening?.Entities??root._entities;var player=root._scene.Player;
        var memory=rooms.RuntimeState;var inventory=player.Inventory;var room=rooms.CurrentRoom;
        var app=new ApplicationValidationFixture(root);
        Vector2 position=(from y in Enumerable.Range(1,room.HeightInTiles-3)
            from x in Enumerable.Range(1,room.WidthInTiles-2)
            let p=new Vector2(x*16+8,y*16+8)
            where Enumerable.Range(-12,49).All(d=>Enumerable.Range(-6,13).All(dx=>
                !room.IsSolid(p+new Vector2(dx,d)) && room.GetTerrainInfo(p+new Vector2(dx,d)).Hazard==HazardType.None &&
                room.GetTerrainInfo(p+new Vector2(dx,d)).Type==TerrainType.Normal))
            select p).First();
        byte oldFlags=rooms.SaveData.ReadField("wDimitriState");int oldAnimal=inventory.AnimalCompanion;
        bool forest=game==OracleGame.Ages&&rooms.SaveData.HasGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest);
        try
        {
            foreach(bool scripted in new[]{false,true})
            foreach(bool assigned in new[]{false,true})
            {
                entities.Clear();player.ClearNativeItemParents();
                for(int i=0;i<64;i++)memory.SetWramByte(0xd100+i,0);
                memory.SetField("wLinkObjectIndex",0xd0);memory.SetField("wDisabledObjects",0);memory.SetField("wMenuDisabled",0);
                memory.SetField("wDisableScreenTransitions",0);memory.SetField("wWarpsDisabled",0);memory.SetField("wDisableWarpTiles",0);
                CompanionRuntimeState.ForgetRemembered(memory);
                rooms.SaveData.WriteField("wDimitriState",scripted?(byte)0:(byte)0x80);
                if(game==OracleGame.Ages)rooms.SaveData.SetGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest,false);
                inventory.AssignAnimalCompanion(assigned?0x0c:0x0b);inventory.GiveTreasure(TreasureId.Flippers,0);
                inventory.EquipA(0);inventory.EquipB(0);
                player.WarpTo(position+new Vector2(0,24));player.Face(Vector2I.Up);player.ResetEnemyInvincibility();
                entities.Spawn(new DimitriCompanionSpawn(position,2,rooms.ActiveGroup,room.Id));
                var native=new BraceletRom(room,player.Position,0,-1,game:game);native.LoadSave(rooms.SaveData);
                native[0xcc4f]=memory.ReadField("wLinkForceState");native[0xcc51]=memory.ReadField("wLinkStateParameter");
                native[0xcc2d]=(byte)rooms.ActiveGroup;native.InitializeWalkingAnimation();
                for(int i=0;i<64;i++)native[0xd100+i]=memory.ReadWramByte(0xd100+i);
                var sounds=root._sound.AttachPlayRequestAudit();int updates=0,previousKeys=0;
                void Step(int count=1,int keys=0)=>app.Step(count,keys==0x40?Vector2.Up:keys==0x80?Vector2.Down:Vector2.Zero,
                    MenuRomActions(keys),MenuRomActions(keys&~previousKeys),batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,keys);updates++;previousKeys=keys;
                    int state=memory.ReadWramByte(0xd104);
                    string context=$"{game}/scripted{scripted}/assigned{assigned}/batch{batched}/update{updates}";
                    FailIf(state!=native[0xd104] || memory.ReadWramByte(0xd100)!=native[0xd100] ||
                        CompanionRuntimeState.Read(memory).Position!=new Vector2(native.Word(0xd10c)/256f,native.Word(0xd10a)/256f),
                        $"{context}: native D1 state/position differs ({state:x2}/{native[0xd104]:x2}).");
                    FailIf(player.CompanionRideActive!=(native[0xcc2c]==0xd1) ||
                        player.CompanionJumpActive!=(native[0xcc2c]==0xd0 && native[0xcc5c]!=0) || player.Position!=new Vector2(native[0xd00d],native[0xd00b]),
                        $"{context}: Link ride/air/position runtime={player.CompanionRideActive}/{player.CompanionJumpActive}/{player.Position}, native={native[0xcc2c]:x2}/{native[0xcc5c]:x2}/{native[0xd00d]},{native[0xd00b]}.");
                    if(player.CompanionRideActive)FailIf(player.EnemyContactPosition!=new Vector2(native[0xd10d],native[0xd10b]),
                        context+": mounted collision targeting must read the D1 owner, independently of the riding-Link sprite offset.");
                    foreach(int at in new[]{0xd103,0xd105,0xd106,0xd107,0xd108,0xd109,0xd110,0xd11a,0xd120,0xd121,0xd130,0xd135,0xd138})
                        FailIf(memory.ReadWramByte(at)!=native[at],$"{context}: native control/animation byte${at:x4} runtime=${memory.ReadWramByte(at):x2}/native=${native[at]:x2}.");
                    FailIf(memory.ReadField("wLinkForceState")!=native[0xcc4f] || memory.ReadField("wLinkStateParameter")!=native[0xcc51],
                        context+$": authoritative force-state request/parameter runtime${memory.ReadField("wLinkForceState"):x2}/${memory.ReadField("wLinkStateParameter"):x2}, native${native[0xcc4f]:x2}/${native[0xcc51]:x2}.");
                    FailIf(player.HealthQuarters!=native[game==OracleGame.Ages?0xc6aa:0xc6a2],context+": native Link health differs.");
                    if(entities.Entities<DimitriCompanionRoomEntity>().SingleOrDefault() is {} visual)
                        FailIf(visual.ZIndex!=(native[0xd11a]&3) switch{0=>12,1=>11,2=>9,_=>8},context+": native visible-priority queue differs.");
                    int weapons=Enumerable.Range(0xd6,10).Count(page=>native[page<<8]!=0&&native[(page<<8)+1]==0x2b);
                    FailIf(entities.Entities<DimitriMouthRoomEntity>().Count!=weapons,context+": native mouth allocation/lifetime differs.");
                    FailIf(!sounds.Requests.SequenceEqual(native.Sounds),context+$": cue order runtime=[{string.Join(',',sounds.Requests)}]/native=[{string.Join(',',native.Sounds)}].");
                });
                Step(5);
                if(scripted)
                {
                    FailIf(native[0xd104]!=0x0a,"Unassigned initial flags must retain the game's scripted stateA.");
                    memory.SetWramByte(0xd108,1);native[0xd108]=1;
                    rooms.SaveData.WriteField("wDimitriState",0x80);native.LoadSave(rooms.SaveData);
                }
                for(int i=0;i<80 && (!player.CompanionRideActive || player.CompanionJumpActive);i++)Step(1,scripted?0:0x40);
                FailIf(!player.CompanionRideActive || player.CompanionJumpActive,"The real normal/forced mounting path did not complete.");
                Step(5,0x40);Step(5,0x80);Step();
                Vector2 feet=CompanionRuntimeState.Read(memory).Position+new Vector2(0,5);
                byte previousTile=room.GetMetatile(feet),previousCollision=room.GetTerrainInfo(feet).Collision;
                foreach(var hazard in new[]{HazardType.Water,HazardType.Hole})
                {
                    byte tile=(byte)Enumerable.Range(0,256).First(t=>TilePropertyDatabase.For(rooms.AssetRoot).Hazard(room.ActiveCollisions,(byte)t)==hazard);
                    room.SetPositionTileAndCollision(feet,tile,0,0);native.CopyRoom(room);
                    memory.SetWramByte(0xd105,3);native[0xd105]=3;
                    memory.SetWramByte(0xd106,0x55);native[0xd106]=0x55;
                    Step();
                    FailIf(memory.ReadWramByte(0xd105)!=0 || memory.ReadWramByte(0xd106)!=0 || memory.ReadWramByte(0xd138)!=4,
                        "Shared Dimitri movement must retain native hazard resets, including the water status surviving a following hole.");
                }
                room.SetPositionTileAndCollision(feet,previousTile,previousCollision,0);native.CopyRoom(room);
                // Declared external state write cancels the pending hole handler before its next visit.
                memory.SetWramByte(0xd104,5);native[0xd104]=5;
                Step();
                for(int repeat=0;repeat<2;repeat++)
                {
                    Step(1,1);
                    if(repeat==0)
                    {
                        memory.SetField("wDisabledObjects",0x20);native[0xcc8a]=0x20;Step(3);
                        memory.SetField("wDisabledObjects",0);native[0xcc8a]=0;
                        // Declared native consumer write: state8/substate1 uses the live counter, not a cached bite phase.
                        memory.SetWramByte(0xd105,1);native[0xd105]=1;
                        memory.SetWramByte(0xd106,2);native[0xd106]=2;
                        memory.SetWramByte(0xd109,0);native[0xd109]=0;
                    }
                    Step(70);
                }
                Step(1,1);
                var mounted=entities.Entities<DimitriCompanionRoomEntity>().Single();
                if(game==OracleGame.Ages)mounted.StopAtCarpenterSearchBoundary();
                else memory.SetWramByte(0xd104,0x0d);
                native[0xd104]=0x0d;
                Step(3);
                Step(70);
                Step(1,2);for(int i=0;i<80 && (player.CompanionRideActive||player.CompanionJumpActive);i++)Step();
                Step(5);
                var remembered=CompanionRuntimeState.ReadRemembered(memory);
                FailIf(remembered.Id!=native[0xcc24] || remembered.Y!=native[0xcc27] || remembered.X!=native[0xcc28],
                    $"{game}: live body dismount did not execute the native temporary/assigned companion rule.");
                if(!scripted && assigned)
                for(int repetition=0;repetition<2;repetition++)
                {
                    Step(12,0x80);
                    for(int i=0;i<80 && (!player.CompanionRideActive || player.CompanionJumpActive);i++)Step(1,0x40);
                    FailIf(!player.CompanionRideActive || player.CompanionJumpActive,"Forced-water fixture must naturally remount through its clear collision corridor.");
                    Vector2 safe=position+new Vector2(0,24);
                    player.SetLocalRespawnCoordinates(safe);native[0xcc21]=(byte)safe.Y;native[0xcc22]=(byte)safe.X;
                    Vector2 waterFeet=CompanionRuntimeState.Read(memory).Position+new Vector2(0,5);
                    byte floor=room.GetMetatile(waterFeet),floorCollision=room.GetTerrainInfo(waterFeet).Collision;
                    byte water=(byte)Enumerable.Range(0,256).First(t=>TilePropertyDatabase.For(rooms.AssetRoot).Hazard(room.ActiveCollisions,(byte)t)==HazardType.Water);
                    room.SetPositionTileAndCollision(waterFeet,water,0,0);native.CopyRoom(room);
                    player.RequestForcedRespawn();native[0xcc4f]=2;native[0xcc51]=2;
                    memory.SetField("wDisabledObjects",0x20);native[0xcc8a]=0x20;Step(3);
                    memory.SetField("wDisabledObjects",0);native[0xcc8a]=0;Step();
                    FailIf(native[0xd104]!=(game==OracleGame.Ages?4:5) || native[0xcc4f]!=(game==OracleGame.Ages?0:2),
                        "Ages Dimitri must consume the native water-respawn request; Seasons' ridden Link retains it.");
                    room.SetPositionTileAndCollision(waterFeet,floor,floorCollision,0);native.CopyRoom(room);
                    if(game==OracleGame.Ages)
                    {
                        for(int i=0;i<160 && native[0xd104]==4;i++)Step();
                        FailIf(native[0xd104]!=5 || !player.CompanionRideActive,
                            "Ages forced-water fall must complete native companion respawn while retaining the rider.");
                    }
                    else
                    {
                        Step(3);Step(1,2);Step(35);
                        FailIf(memory.ReadField("wLinkForceState")!=0,"Native Seasons dismount must clear the retained request.");
                    }
                }
            }
        }
        finally
        {
            entities.Clear();CompanionRuntimeState.Clear(memory,SpecialObjectId.Dimitri);CompanionRuntimeState.ForgetRemembered(memory);
            memory.SetField("wLinkObjectIndex",0xd0);memory.SetField("wDisabledObjects",0);memory.SetField("wMenuDisabled",0);
            memory.SetField("wDisableScreenTransitions",0);memory.SetField("wWarpsDisabled",0);memory.SetField("wDisableWarpTiles",0);
            rooms.SaveData.WriteField("wDimitriState",oldFlags);rooms.SaveData.WriteField("wAnimalCompanion",(byte)oldAnimal);
            // Restore the unassigned fixture value too; the gameplay assignment API accepts only $0b-$0d.
            typeof(InventoryState).GetProperty(nameof(InventoryState.AnimalCompanion))!.SetValue(inventory,oldAnimal);
            if(game==OracleGame.Ages)rooms.SaveData.SetGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest,forest);
            player.WarpTo(position+new Vector2(0,24));
        }
    }
}
