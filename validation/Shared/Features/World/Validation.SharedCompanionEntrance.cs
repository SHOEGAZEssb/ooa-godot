
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedCompanionEntrance(FrontendValidationRoot root,bool batched,int companionId)
    {
        var rooms=root._gameplaySession!.Rooms;var save=rooms.SaveData;var memory=rooms.RuntimeState;
        var entities=root._seasonsOpening?.Entities??root._entities;var player=root._scene.Player;var inventory=player.Inventory;
        var app=new ApplicationValidationFixture(root);var game=save.Game;
        int oldGroup=rooms.ActiveGroup,oldRoom=rooms.CurrentRoom.Id,oldAnimal=inventory.AnimalCompanion;
        string stateField=companionId==0x0b?"wRickyState":companionId==0x0c?"wDimitriState":"wMooshState";
        int oldA=inventory.EquippedA,oldB=inventory.EquippedB;byte oldFlags=save.ReadField(stateField);
        bool forest=game==OracleGame.Ages&&save.HasGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest);
        void Load(int group,int room){if(game==OracleGame.Ages)root.LoadDebugRoom(group,room);else root._seasonsOpening!.DebugTravel(group,room);}
        void Clear()
        {
            entities.Clear();player.ClearNativeItemParents();
            CompanionRuntimeState.Clear(memory,companionId);CompanionRuntimeState.ForgetRemembered(memory);
            memory.SetField("wLinkObjectIndex",0xd0);memory.SetField("wDisabledObjects",0);memory.SetField("wMenuDisabled",0);
        }
        try
        {
            Clear();Load(0,game==OracleGame.Ages?0x2a:0x97);Clear();
            var room=rooms.CurrentRoom;
            FailIf(!new FluteDatabase(rooms.AssetRoot).Callable(room.Id),"Companion entrance fixture requires an original callable overworld room.");
            foreach(var (flags,blocked,cancel,direction) in new (int,bool,bool,int)[]{(0x80,false,false,2),(0,false,false,2),(0x20,false,false,2),(0x40,false,false,2),(0x80,true,false,2),(0,true,false,2),(0x80,false,true,2),
                (0x80,false,false,0),(0x80,false,false,3),(0x80,false,false,1)})
            {
                Clear();
                for(int at=0xd100;at<0xd140;at++)memory.SetWramByte(at,0);
                for(int y=0;y<8;y++)for(int x=0;x<10;x++)
                    room.SetPositionTileAndCollision(new(x*16+8,y*16+8),game==OracleGame.Ages?(byte)0x3a:(byte)4,0,0);
                if(direction!=2)room.SetPositionTileAndCollision(new(88,8),game==OracleGame.Ages?(byte)0x3a:(byte)4,15,0);
                if(direction is 1 or 3)room.SetPositionTileAndCollision(new(88,104),game==OracleGame.Ages?(byte)0x3a:(byte)4,15,0);
                if(direction==1)room.SetPositionTileAndCollision(new(136,72),game==OracleGame.Ages?(byte)0x3a:(byte)4,15,0);
                save.WriteField(stateField,0x80);
                if(game==OracleGame.Ages)save.SetGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest,false);
                inventory.GiveTreasure(TreasureId.Flute,companionId);inventory.EquipA(TreasureId.Flute);inventory.EquipB(0);
                save.WriteField("wFluteIcon",(byte)(companionId-0x0a));inventory.AssignAnimalCompanion(companionId);
                inventory.SetScriptedHealth(inventory.MaxHealthQuarters);root._gameplaySession!.StatusBar.SynchronizeHealth();
                player.WarpTo(new(88.25f,72.5f));player.Face(Vector2I.Down);
                app.Step(1,Vector2.Zero,batched:batched);
                memory.SetWramByte(0xd10a,0x80);memory.SetWramByte(0xd10c,0x40);
                var native=new BraceletRom(room,player.PrecisePosition,2,0xff,game:game);
                native.LoadSave(save);native.InitializeWalkingAnimation();native[0xcc2d]=0;
                for(int at=0xd100;at<0xd140;at++)native[at]=memory.ReadWramByte(at);
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                var sounds=root._sound.AttachPlayRequestAudit();int update=0,previous=0;
                void Step(int count=1,int keys=0)
                {
                    int edge=keys&~previous;previous=keys;
                    app.Step(count,Vector2.Zero,MenuRomActions(keys),MenuRomActions(edge),batched:batched,afterUpdate:()=>
                    {
                        native.UpdateLinkItems(entities.FrameCounter,keys,interactions:true);
                        string context=$"{game}/companion${companionId:x2} entrance flags${flags:x2}/dir${direction:x2}/blocked{blocked}/cancel{cancel}/batch{batched}/update{++update}";
                        foreach(int offset in companionId==0x0b?Enumerable.Range(0,64):new[]{0,1,3,4,5,6,7,8,9,10,11,12,13,14,15,0x10,0x14,0x15,0x1a,0x1e,0x1f,0x20,0x21,0x22,0x23,0x24,0x30,0x31,0x32,0x35,0x38,0x3b,0x3c,0x3d,0x3f})
                            FailIf(memory.ReadWramByte(0xd100+offset)!=native[0xd100+offset],context+$": D1 byte${offset:x2} runtime${memory.ReadWramByte(0xd100+offset):x2}/native${native[0xd100+offset]:x2}.");
                        var actors=entities.EntityAdapters<IRoomEntity>().Where(actor=>actor is IRoomInitializedCompanion).ToArray();
                        FailIf(actors.Length!=(native[0xd100]==0?0:1),context+": native slot birth/transfer/cancellation differs.");
                        if(actors.Length!=0)FailIf(actors[0].Node.Visible!=((native[0xd11a]&0x80)!=0),context+": native visibility differs.");
                        if(actors.Length!=0)
                        {
                            Vector2 world=new(native[0xd10d],native[0xd10b]);
                            if(direction==1&&world.X>=248)world.X-=256;
                            if(direction==2&&world.Y>=248)world.Y-=256;
                            FailIf(actors[0].Node.Position!=world,context+$": native edge projection differs, runtime{actors[0].Node.Position}/expected{world}.");
                        }
                        FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                            player.IsUsingHarp!=(native[0xd500]!=0),context+": real Flute parent or Link movement differs.");
                        var rng=entities.Random.CaptureState();
                        FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                            !sounds.Requests.SequenceEqual(native.Sounds),context+$": native RNG/cues runtime${rng.Rng1:x2}/${rng.Rng2:x2}/{rng.Calls-seed.Calls} [{string.Join(',',sounds.Requests.Select(id=>$"${id:x2}"))}], native${native[0xff94]:x2}/${native[0xff95]:x2}/{native.RandomCalls} [{string.Join(',',native.Sounds.Select(id=>$"${id:x2}"))}].");
                    });
                }
                Step(1,1);Step(254,1);
                FailIf(native[0xd104]!=0x0c || native[0xd103]!=0 || player.IsUsingHarp,
                    "The actual $ff-update Flute completion must leave a newly born stateC pending its first D1 visit.");
                Step();
                if(companionId==0x0b)
                {
                    FailIf(native[0xd107]!=2 || native[0xd103]!=1 || native[0xd108]!=direction,
                        "Ricky's first stateC visit must install two hops and the native entry direction.");
                    if(cancel)
                    {
                        Step(3);CompanionRuntimeState.Clear(memory,companionId);
                        for(int at=0xd100;at<0xd140;at++)native[at]=0;
                        Step(4);continue;
                    }
                    if(blocked)
                    {
                        room.SetPositionTileAndCollision(new(88,8),game==OracleGame.Ages?(byte)0x3a:(byte)4,15,0);
                        native.CopyRoom(room);
                    }
                    Step(3);save.WriteField(stateField,(byte)flags);native.LoadSave(save);
                    bool restart=flags==0x80 && !blocked && direction==2;
                    if(restart)
                    {
                        // A native writer can re-enter parameter0 without replacing the body.
                        memory.SetWramByte(0xd103,0);native[0xd103]=0;
                    }
                    memory.SetField("wDisabledObjects",0x20);native[0xcc8a]=0x20;Step(3);
                    memory.SetField("wDisabledObjects",0);native[0xcc8a]=0;
                    if(restart)
                    {
                        Step();
                        FailIf(memory.ReadWramByte(0xd103)!=1 || memory.ReadWramByte(0xd107)!=2,
                            "Ricky stateC parameter0 must restart on its next eligible visit after a native admission hold.");
                    }
                    for(int entering=0;native[0xd104]==0x0c && entering<160;entering++)Step();
                    bool waiting=flags==0x80 || game==OracleGame.Ages&&flags==0x20;
                    FailIf(native[0xd104]!=(waiting?1:0x0a),"Ricky must return through native state0 after its cutscene hops.");
                    Step(3);continue;
                }
                FailIf(native[0xd107]!=0x3c || native[0xd103]!=1 || native[0xd108]!=direction,"Flute stateC must initialize its own $3c counter in the first eligible visit with the ordered entrance direction.");
                if(cancel)
                {
                    Step(10);CompanionRuntimeState.Clear(memory,companionId);
                    for(int at=0xd100;at<0xd140;at++)native[at]=0;
                    Step(4);continue;
                }
                if(blocked)
                {
                    room.SetPositionTileAndCollision(new(88,8),game==OracleGame.Ages?(byte)0x3a:(byte)4,15,0);
                    native.CopyRoom(room);
                }
                else Step(59);
                save.WriteField(stateField,(byte)flags);
                if(game==OracleGame.Ages)save.SetGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest,flags==0x40);
                native.LoadSave(save);
                memory.SetField("wDisabledObjects",0x20);native[0xcc8a]=0x20;Step(3);
                FailIf(native[0xd104]!=0x0c || native[0xd107]!=(blocked?60:1),"Frozen arrival must preserve its source boundary counter.");
                memory.SetField("wDisabledObjects",0);native[0xcc8a]=0;Step();
                bool ordinary=flags==0x80 || game==OracleGame.Ages&&flags==0x20 || game==OracleGame.Seasons&&companionId==0x0d;
                int parameter=!ordinary&&companionId==0x0d?1:0;
                FailIf(native[0xd104]!=(ordinary?1:0x0a) || native[0xd108]!=2 || native[0xd109]!=2 || native[0xd103]!=parameter ||
                    native[0xd107]!=(blocked?60:0),"Arrival must initialize in the same nonzero-state visit, using the newly sampled game flags.");
                (int yWord,int xWord)=direction switch{2=>(0x2580,0x5840),0=>(0x5b80,0x5840),1=>(0x4880,0x2540),_=>(0x4880,0x7b40)};
                if(!blocked)FailIf(native.Word(0xd10a)!=yWord || native.Word(0xd10c)!=xWord,
                    $"Sixty native SPEED_c0 updates in direction${direction:x2} must retain coordinate lows and end at ${yWord:x4}/${xWord:x4}.");
                Step(3);
            }
        }
        finally
        {
            Clear();save.WriteField(stateField,oldFlags);save.WriteField("wAnimalCompanion",(byte)oldAnimal);
            typeof(InventoryState).GetProperty(nameof(InventoryState.AnimalCompanion))!.SetValue(inventory,oldAnimal);
            if(game==OracleGame.Ages)save.SetGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest,forest);
            inventory.EquipA(oldA);inventory.EquipB(oldB);Load(oldGroup,oldRoom);
        }
    }
}
