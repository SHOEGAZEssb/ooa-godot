using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedRespawnInitialization(FrontendValidationRoot root,bool batched)
    {
        var session=root._gameplaySession!;var rooms=session.Rooms;var memory=rooms.RuntimeState;
        var entities=root._seasonsOpening?.Entities??root._entities;var player=root._scene.Player;
        var inventory=player.Inventory;var game=memory.Game;var app=new ApplicationValidationFixture(root);
        int originalGroup=rooms.ActiveGroup,originalRoom=rooms.CurrentRoom.Id;
        int ring=inventory.ActiveRing,buttonA=inventory.EquippedA,buttonB=inventory.EquippedB,health=inventory.HealthQuarters;
        void Load(int group,int room)
        {if(game==OracleGame.Ages)root.LoadDebugRoom(group,room);else root._seasonsOpening!.DebugTravel(group,room);entities.Clear();}
        try
        {
            Load(0,game==OracleGame.Ages?0x33:0x97);
            var room=rooms.CurrentRoom;
            for(int y=0;y<8;y++)for(int x=0;x<10;x++)
                room.SetPositionTileAndCollision(new(x*16+8,y*16+8),game==OracleGame.Ages?(byte)0x3a:(byte)4,0,0);
            foreach(var (requested,initialized,activeRing,directHole) in new[]{(0,2,0xff,false),(2,0,0xff,false),(2,4,0xff,false),(4,2,0xff,false),(0,0,0xff,false),(4,4,0xff,false),(2,4,0x1c,false),(0,2,0xff,true),(0,1,0xff,false),(1,0,0xff,false),(1,1,0xff,false)})
            {
                entities.Clear();player.ClearNativeItemParents();
                memory.SetField("wDisabledObjects",0);memory.SetField("wMenuDisabled",0);
                memory.SetField("wPaletteThread_mode",0);memory.SetField("wLinkObjectIndex",0xd0);
                inventory.EquipA(0);inventory.EquipB(0);inventory.SetScriptedActiveRing(activeRing);
                inventory.SetScriptedHealth(inventory.MaxHealthQuarters);session.StatusBar.SynchronizeHealth();
                Vector2 start=new(88.25f,directHole?72.5f:64.5f),respawn=new(40,40);
                player.WarpTo(start);player.Face(Vector2I.Down);player.SetLocalRespawnPosition(respawn,Vector2I.Down);
                var native=new BraceletRom(room,start,2,activeRing,game:game);native.LoadSave(rooms.SaveData);native.InitializeWalkingAnimation();
                native[0xd01a]=0xc1; // Declared initialized ordinary Link presentation.
                native[0xcc21]=40;native[0xcc22]=40;native[0xcc23]=2;
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                var sounds=root._sound.AttachPlayRequestAudit();int updates=0;bool checkVar35=false;
                void Step(int count=1)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,0);
                    string context=$"{game}/state02 {requested:x2}->{initialized:x2}/ring{activeRing:x2}/direct{directHole}/batch{batched}/update{++updates}";
                    Vector2 expected=new(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f);
                    FailIf(player.PrecisePosition!=expected || player.Visible!=((native[0xd01a]&0x80)!=0) ||
                        player.NativeNormalStateForInteraction!=(native[0xd004]==1),
                        context+$": XY/visibility/state runtime{player.PrecisePosition}/{player.Visible}/{player.NativeNormalStateForInteraction}, native{expected}/{native[0xd01a]:x2}/{native[0xd004]:x2}:{native[0xd005]:x2}.");
                    FailIf(memory.ReadField("wLinkForceState")!=native[0xcc4f] || memory.ReadField("wLinkStateParameter")!=native[0xcc51] ||
                        player.HealthQuarters!=native[game==OracleGame.Ages?0xc6aa:0xc6a2],context+": native request, parameter or health differs.");
                    if(native[0xd004]==2)
                        FailIf(memory.ReadWramByte(0xd005)!=native[0xd005] || memory.ReadWramByte(0xd006)!=native[0xd006],
                            context+$": native respawn substate/counter runtime{memory.ReadWramByte(0xd005):x2}/{memory.ReadWramByte(0xd006):x2}, native{native[0xd005]:x2}/{native[0xd006]:x2}.");
                    if(native[0xd004]==2 && native[0xd005] is 1 or 5)
                        foreach(int address in new[]{0xd020,0xd021,0xd022,0xd023,0xd030,0xd031})
                            FailIf(memory.ReadWramByte(address)!=native[address],
                                context+$": fall animation${address:x4} runtime${memory.ReadWramByte(address):x2}/native${native[address]:x2}.");
                    if(checkVar35)FailIf(memory.ReadWramByte(0xd035)!=native[0xd035],context+": native initLinkState var35 cleanup differs.");
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                        !sounds.Requests.SequenceEqual(native.Sounds),context+": native RNG or ordered cues differ.");
                });
                if(directHole)
                {
                    byte hole=(byte)Enumerable.Range(0,256).First(tile=>TilePropertyDatabase.For(rooms.AssetRoot).Type(room.ActiveCollisions,(byte)tile)==TerrainType.Hole);
                    room.SetPositionTileAndCollision(start,hole,0x10,0);native.CopyRoom(room);
                    for(int i=0;i<8 && native[0xd004]!=2;i++)Step();
                    room.SetPositionTileAndCollision(start,game==OracleGame.Ages?(byte)0x3a:(byte)4,0,0);native.CopyRoom(room);
                }
                else
                {
                    player.RequestForcedRespawn();native[0xcc4f]=2;
                    memory.SetField("wLinkStateParameter",(byte)requested);native[0xcc51]=(byte)requested;
                    Step();
                }
                FailIf(native[0xd004]!=2 || native[0xd005]!=0 || !directHole && player.PrecisePosition!=start || !player.Visible,
                    "Request consumption must select uninitialized state02 without reading its parameter or changing presentation.");
                memory.SetField("wLinkStateParameter",(byte)initialized);native[0xcc51]=(byte)initialized;
                Step();
                FailIf(native[0xd005]!=(initialized<=1?1:initialized==2?2:5),"The source initializer must select the newly written parameter.");
                if(initialized==1)FailIf(player.PrecisePosition!=start,
                    "State02 parameter$01 must retain both coordinate bytes rather than centering Link on the tile.");
                // Substates retain their selected behavior after initialization, even if the union is reused.
                byte later=(byte)(initialized==0?2:initialized==2?4:0);
                memory.SetField("wLinkStateParameter",later);native[0xcc51]=later;
                Step(64);
                FailIf(native[0xd004]!=1 || !player.NativeNormalStateForInteraction || !player.Visible,
                    "Each retargeted state02 must complete recovery without adopting later parameter-only writes.");
                player.RequestForcedRespawn();native[0xcc4f]=2;native[0xcc51]=2;
                Step();
                memory.SetField("wLinkStateParameter",(byte)initialized);native[0xcc51]=(byte)initialized;
                Step(65);
                FailIf(native[0xd004]!=1 || !player.NativeNormalStateForInteraction || !player.Visible,
                    "Repeating after completion must use the new initializer without retaining the previous mode or recovery phase.");
                player.RequestForcedRespawn();native[0xcc4f]=2;native[0xcc51]=2;Step();
                memory.SetField("wLinkStateParameter",(byte)initialized);native[0xcc51]=(byte)initialized;Step();
                player.RequestForcedRespawn();native[0xcc4f]=2;native[0xcc51]=2;
                bool cancelQueued=requested==4 && initialized==2;
                if(initialized==0)
                {memory.SetField("wLinkStateParameter",4);native[0xcc51]=4;}
                if(cancelQueued)
                {memory.SetField("wLinkForceState",0);native[0xcc4f]=0;}
                memory.SetWramByte(0xd035,0xa5);native[0xd035]=0xa5;checkVar35=true;
                bool scrollHeld=false,returned=false,consumed=false;
                void HoldInvisibleCounter()
                {
                    memory.SetField("wScrollMode",0x80);native[0xcd00]=0x80;Step(3);
                    FailIf(native[0xd006]!=4 || player.FloorDoorRespawnCounter!=4,
                        "State02 substate2 must reload its shared counter to$04 while scroll bit7 is held.");
                    memory.SetField("wScrollMode",1);native[0xcd00]=1;scrollHeld=true;
                }
                if(initialized==2)HoldInvisibleCounter();
                memory.SetField("wDisabledObjects",0x81);native[0xcc8a]=0x81;Step(3);
                memory.SetField("wDisabledObjects",0);native[0xcc8a]=0;
                for(int i=0;i<160;i++)
                {
                    bool wasNormal=native[0xd004]==1;Step();
                    if(!scrollHeld && native[0xd004]==2 && native[0xd005]==2)HoldInvisibleCounter();
                    if(!returned && native[0xd004]==1)
                    {
                        returned=true;
                        FailIf(native[0xcc4f]!=(cancelQueued?0:2) || memory.ReadWramByte(0xd035)!=0,
                            "Recovery must return to state01 and clear var35 without consuming or reviving the queued request.");
                        if(cancelQueued)break;
                    }
                    if(returned && wasNormal && native[0xd004]==2)
                    {
                        consumed=true;
                        FailIf(native[0xcc4f]!=0 || native[0xd005]!=0,
                            "The queued request must be consumed only by the following eligible normal update.");
                    }
                    if(consumed && native[0xd004]==1)break;
                }
                FailIf(!returned || !scrollHeld || !cancelQueued && !consumed || native[0xd004]!=1,
                    "Queued, replaced or cleared requests must retain their native recovery and later-consumption boundaries.");
                if(requested==1 && initialized==1)
                {
                    inventory.SetScriptedHealth(inventory.MaxHealthQuarters);session.StatusBar.SynchronizeHealth();
                    native[game==OracleGame.Ages?0xc6aa:0xc6a2]=(byte)inventory.MaxHealthQuarters;
                    player.RequestForcedRespawn();native[0xcc4f]=2;native[0xcc51]=2;Step(2);
                    memory.SetWramByte(0xd006,0);native[0xd006]=0;Step();
                    FailIf(native[0xd006]!=0xff || player.Visible,
                        "A raw counter1 zero must wrap to$ff and retain invisible substate2.");
                    memory.SetWramByte(0xd006,1);native[0xd006]=1;Step();
                    FailIf(native[0xd005]!=3 || native[0xd006]!=0x10 || !player.Visible,
                        "The same live counter1 must enter visible recovery at its zero boundary.");
                    memory.SetWramByte(0xd006,0);native[0xd006]=0;Step();
                    FailIf(native[0xd006]!=0xff || player.NativeNormalStateForInteraction,
                        "A raw recovery counter zero must wrap to$ff without completing state02.");
                    memory.SetWramByte(0xd006,1);native[0xd006]=1;Step();
                    FailIf(native[0xd004]!=1 || !player.NativeNormalStateForInteraction || memory.ReadWramByte(0xd005)!=0,
                        "Recovery must consume the live counter and clear substate at initLinkState.");
                    player.RequestForcedRespawn();native[0xcc4f]=2;native[0xcc51]=2;Step(2);
                    memory.SetWramByte(0xd005,0);native[0xd005]=0;
                    memory.SetField("wLinkStateParameter",1);native[0xcc51]=1;Step();
                    FailIf(player.Visible || native[0xd005]!=1,
                        "A raw substate0 write must rerun the live initializer without revealing hidden Link.");
                    int retainedHealth=player.HealthQuarters;
                    memory.SetWramByte(0xd005,3);native[0xd005]=3;
                    memory.SetWramByte(0xd006,2);native[0xd006]=2;Step(2);
                    FailIf(native[0xd004]!=1 || !player.NativeNormalStateForInteraction || !player.Visible || player.HealthQuarters!=retainedHealth,
                        "A raw substate3 write must select recovery directly, reveal at completion and skip respawn damage.");
                }
            }
            CheckSharedHiddenFall(root, batched);
        }
        finally
        {
            player.WarpTo(new(40,40));entities.Clear();inventory.SetScriptedActiveRing(ring);
            inventory.SetScriptedHealth(health);session.StatusBar.SynchronizeHealth();
            inventory.EquipA(buttonA);inventory.EquipB(buttonB);Load(originalGroup,originalRoom);
        }
    }
}
