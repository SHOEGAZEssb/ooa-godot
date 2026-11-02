
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedForcedStates(FrontendValidationRoot root,bool batched)
    {
        var session=root._gameplaySession!;var rooms=session.Rooms;var memory=rooms.RuntimeState;
        var entities=root._seasonsOpening?.Entities??root._entities;var player=root._scene.Player;
        var inventory=player.Inventory;var game=memory.Game;var app=new ApplicationValidationFixture(root);
        int originalGroup=rooms.ActiveGroup,originalRoom=rooms.CurrentRoom.Id;
        int health=inventory.HealthQuarters,ring=inventory.ActiveRing,a=inventory.EquippedA,b=inventory.EquippedB;
        void Load(int group,int room)
        {if(game==OracleGame.Ages)root.LoadDebugRoom(group,room);else root._seasonsOpening!.DebugTravel(group,room);entities.Clear();}
        try
        {
            Load(0,game==OracleGame.Ages?0x33:0x97);
            var room=rooms.CurrentRoom;
            for(int y=0;y<8;y++)for(int x=0;x<10;x++)
                room.SetPositionTileAndCollision(new(x*16+8,y*16+8),game==OracleGame.Ages?(byte)0x3a:(byte)4,0,0);
            byte[] itemMasks=[0,1,0x80,0x81,2,0x20,0xff];
            for(int scenario=0;scenario<14+4*itemMasks.Length;scenario++)
            {
                entities.Clear();player.ClearNativeItemParents();
                memory.SetField("wLinkForceState",0);memory.SetField("wDisabledObjects",0);
                memory.SetField("wPaletteThread_mode",0);memory.SetField("wScrollMode",1);
                memory.SetField("wLinkObjectIndex",0xd0);memory.SetField("wcc50",0);
                memory.SetField("wLinkStateParameter",0);
                inventory.EquipA(0);inventory.EquipB(0);inventory.SetScriptedActiveRing(0xff);
                inventory.SetScriptedHealth(inventory.MaxHealthQuarters);session.StatusBar.SynchronizeHealth();
                Vector2 start=new(88.25f,64.5f);
                player.WarpTo(start);player.Face(Vector2I.Down);player.SetLocalRespawnPosition(new(40,40),Vector2I.Down);
                var native=new BraceletRom(room,start,2,0xff,game:game);native.LoadSave(rooms.SaveData);native.InitializeWalkingAnimation();
                native[0xcc52]=memory.ReadField("wcc52");
                native[0xd01a]=0xc1;native[0xcc21]=40;native[0xcc22]=40;native[0xcc23]=2;
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                var sounds=root._sound.AttachPlayRequestAudit();int updates=0;var owner=new object();
                void Set(string field,int address,byte value){memory.SetField(field,value);native[address]=value;}
                void Request(byte state)=>Set("wLinkForceState",0xcc4f,state);
                void Move(int parameter)
                {
                    player.RequestNativeForcedMovement(parameter,8,1,owner);
                    native[0xcc4f]=0x0b;native[0xcc51]=(byte)parameter;native[0xd008]=1;native[0xd009]=8;
                }
                void State08(byte parameter=0xff)
                {player.RequestNativeState08Control(owner,parameter);native.RequestState08();native[0xcc50]=parameter;}
                void Respawn(){player.RequestForcedRespawn();native[0xcc4f]=2;native[0xcc51]=2;}
                void Step(int count=1)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,0);
                    string context=$"{game}/forced-state case{scenario}/batch{batched}/update{++updates}";
                    Vector2 expected=new(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f);
                    FailIf(player.PrecisePosition!=expected || player.Visible!=((native[0xd01a]&0x80)!=0) ||
                        player.NativeNormalStateForInteraction!=(native[0xd004]==1) ||
                        player.NativeForcedMovementActive!=(native[0xd004]==0x0b),
                        context+$": XY/visibility/selected state runtime{player.PrecisePosition}/{player.Visible}/{player.NativeNormalStateForInteraction}/{player.NativeForcedMovementActive}, native{expected}/{native[0xd01a]:x2}/{native[0xd004]:x2}:{native[0xd005]:x2}.");
                    FailIf(memory.ReadWramByte(0xd005)!=native[0xd005] || memory.ReadWramByte(0xd006)!=native[0xd006],
                        context+": shared native substate/counter differs.");
                    if(native[0xd004]==4 && native[0xd005]==1)
                        FailIf(player.IsHoldingItemOneHand!=(native[0xd030]==0x0e) || player.IsHoldingItemTwoHands!=(native[0xd030]==0x0f),
                            context+": initialized native item-get pose differs.");
                    if (native[0xd004] is 4 or 8 or 0x0b || scenario >= 8 + 4 * itemMasks.Length)
                    {
                        foreach (int address in new[] { 0xd020, 0xd021, 0xd022, 0xd023, 0xd030, 0xd031 })
                            FailIf(memory.ReadWramByte(address) != native[address],
                                context+$": native animation ${address:x4} differs: runtime${memory.ReadWramByte(address):x2}/ROM${native[address]:x2}.");
                        FailIf(memory.ReadField("wcc52") != native[0xcc52],
                            context+": item-get saved mode must use live wcc52.");
                    }
                    if(native[0xd004]==0x0b && native[0xd005]==1)
                    {
                        FailIf(memory.ReadWramByte(0xd020)!=native[0xd020] ||
                            memory.ReadWramByte(0xd031)!=native[0xd031],
                            context+": forced movement native animation counter/frame differs.");
                        FailIf(player.NativePlanarSpeed!=native[0xd010] ||
                            SomariaPrivate<int>(player,"_topDownMovementTargetSpeedRaw")!=native[0xd011] ||
                            SomariaPrivate<int>(player,"_topDownMovementTerrainMode")!=native[0xd036],
                            context+": forced movement must initialize the shared source speed/target/terrain owner.");
                    }
                    FailIf(memory.ReadField("wLinkForceState")!=native[0xcc4f] || memory.ReadField("wLinkStateParameter")!=native[0xcc51] ||
                        memory.ReadField("wcc50")!=native[0xcc50] || player.HealthQuarters!=native[game==OracleGame.Ages?0xc6aa:0xc6a2],
                        context+$": shared request {memory.ReadField("wLinkForceState"):x2}/{native[0xcc4f]:x2}, parameter {memory.ReadField("wLinkStateParameter"):x2}/{native[0xcc51]:x2}, command {memory.ReadField("wcc50"):x2}/{native[0xcc50]:x2}, health {player.HealthQuarters}/{native[game==OracleGame.Ages?0xc6aa:0xc6a2]} differs.");
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                        !(scenario<8?sounds.Requests:sounds.Requests.Where(cue=>cue!=SoundId.SndText)).SequenceEqual(native.Sounds),
                        context+": shared RNG or ordered cues differ.");
                });
                if(scenario==0)
                {
                    Set("wDisabledObjects",0xcc8a,0x80);
                    Move(6);State08();Step(2);
                    Respawn();Move(4);Step();
                    Set("wLinkStateParameter",0xcc51,2);Step();
                    State08();Set("wLinkStateParameter",0xcc51,6);Step();
                    FailIf(native[0xd004]!=1 || native[0xcc4f]!=8,
                        "State$0b must finish its initialized counter before the queued state$08 is consumed.");
                    Step(2);Set("wDisabledObjects",0xcc8a,0);Step();
                    Move(6);Request(0);Step();
                    State08();Request(0);Step();
                    FailIf(player.CutsceneControlled || player.NativeForcedMovementActive,
                        "Clearing pending native requests must retire their control metadata without resurrecting private requests.");
                }
                else if(scenario==1)
                {
                    Move(6);Step();Set("wLinkStateParameter",0xcc51,0);Step();
                    FailIf(native[0xd006]!=0xff || !player.NativeForcedMovementActive,
                        "State$0b initialization must read the live zero parameter and decrement it to$ff on that update.");
                    Set("wDisabledObjects",0xcc8a,0x81);Set("wPaletteThread_mode",0xc4ab,1);State08();Step(3);
                    memory.SetWramByte(0xd006,1);native[0xd006]=1;Step();
                    Set("wPaletteThread_mode",0xc4ab,0);Step(2);
                    player.RequestGetItemState(0x80);native.RequestGetItemState(0x80);
                    Set("wPaletteThread_mode",0xc4ab,1);Step(3);
                    FailIf(native[0xd004]!=1 || player.CutsceneControlled,
                        "State$08 must consume state$04 before its palette/object gates, retaining deferred initialization and completion.");
                }
                else if(scenario==2)
                {
                    Respawn();Step(2);Move(5);Step(18);
                    FailIf(native[0xd004]!=1 || native[0xcc4f]!=0x0b,
                        "State$02 recovery must retain a queued movement request through its own completion update.");
                    Step(6);
                    FailIf(native[0xd004]!=1 || player.NativeForcedMovementActive,
                        "The later normal visit must consume and complete the retained movement request.");
                }
                else if(scenario==3)
                {
                    Move(6);Step();Set("wLinkStateParameter",0xcc51,1);Step();
                    FailIf(native[0xd004]!=1 || player.PrecisePosition!=start,
                        "A late parameter1 must finish state$0b on initialization without a movement update.");
                    Move(6);Step();Set("wLinkStateParameter",0xcc51,3);Step();
                    Move(5);Step(2);
                    FailIf(native[0xd004]!=1 || native[0xcc4f]!=0x0b,
                        "An active movement must retain a replacement request rather than restart its live counter.");
                    Step(2);memory.SetWramByte(0xd006,1);native[0xd006]=1;Step();
                }
                else if(scenario<8)
                {
                    Move(8);Step();
                    var terrain=new[]{TerrainType.Grass,TerrainType.Stairs,TerrainType.Hole,TerrainType.Vines}[scenario-4];
                    Set("wActiveTileType",0xcc9c,(byte)terrain);Step();
                    // State0b advances its retained cursor directly, without selecting WALK again.
                    int address = scenario switch { 4 => 0xd020, 5 => 0xd022, 6 => 0xd030, _ => 0xd021 };
                    byte value = scenario switch { 4 => 0, 5 => (byte)(native[0xd022] - 3), 6 => 0x06, _ => 0x20 };
                    memory.SetWramByte(address,value);native[address]=value;
                    // state0b initializes speed once; later terrain publication does not reselect it.
                    Set("wActiveTileType",0xcc9c,0);Step(7);
                    FailIf(native[0xd004]!=1 || player.NativeForcedMovementActive,
                        "Terrain-selected state$0b must retain its initialized speed and return on the source counter boundary.");
                }
                else if(scenario<8+4*itemMasks.Length)
                {
                    int index=scenario-8;
                    byte parameter=new byte[]{0,1,0x80,0x81}[index/itemMasks.Length],mask=itemMasks[index%itemMasks.Length];
                    Set("wDisabledObjects",0xcc8a,mask);
                    root._scene.Dialogue.ShowMessage("Item-get mask boundary.",64);native[0xcba0]=1;
                    player.RequestGetItemState(parameter);native.RequestGetItemState(parameter);Step(3);
                    Set("wDisabledObjects",0xcc8a,0);Step();
                    FailIf(native[0xd004]!=4,"Every state04 parameter must retain its text gate after a raw mask clear.");
                    Set("wDisabledObjects",0xcc8a,mask);
                    root._scene.Dialogue.Close();native[0xcba0]=0;Step();
                    bool released=(parameter&0x80)!=0 || (mask&0x81)==0;
                    FailIf((native[0xd004]==1)!=released,
                        "State04 must read only live mask bits$81 after text; bit7 of its parameter skips that mask.");
                    if(!released){Set("wcc50",0xcc50,(byte)(parameter|0x80));Step();}
                    Set("wDisabledObjects",0xcc8a,0x81);
                    player.RequestGetItemState(parameter);native.RequestGetItemState(parameter);Step(2);
                    Set("wcc50",0xcc50,(byte)(parameter&1));Step(2);
                    FailIf(native[0xd004]!=4,"Clearing the live skip-mask bit must restore the mask gate without changing the initialized pose.");
                    Set("wDisabledObjects",0xcc8a,0);Step();
                    FailIf(native[0xd004]!=1,"Clearing the native mask must release repeated state04 on the next eligible update.");
                }
                else
                {
                    Set("wDisabledObjects",0xcc8a,0x80);
                    State08(); Step(2);
                    Set("wcc50",0xcc50,0x06); Step();
                    memory.SetWramByte(0xd020,2); native[0xd020]=2;
                    Set("wcc50",0xcc50,0x06); Step();
                    player.RequestGetItemState(0); native.RequestGetItemState(0); Step(2);
                    FailIf(memory.ReadField("wcc52") != 0x06, "State04 must save the actual preceding native mode.");
                    byte restore = new byte[] { 0x06, 0x09, 0x10, 0x18, 0x0a, 0x0d }[scenario - 8 - 4 * itemMasks.Length];
                    Set("wcc52",0xcc52,restore);
                    Step(3);
                    Set("wDisabledObjects",0xcc8a,0); Step();
                    FailIf(native[0xd004] != 1 || memory.ReadWramByte(0xd030) != restore,
                        "State04 completion must reload the live saved mode, including a same-mode reset.");
                }
            }
        }
        finally
        {
            memory.SetField("wDisabledObjects",0);memory.SetField("wPaletteThread_mode",0);
            root._scene.Dialogue.Close();
            player.WarpTo(new(40,40));entities.Clear();inventory.SetScriptedActiveRing(ring);
            inventory.SetScriptedHealth(health);session.StatusBar.SynchronizeHealth();
            inventory.EquipA(a);inventory.EquipB(b);Load(originalGroup,originalRoom);
        }
    }
}
