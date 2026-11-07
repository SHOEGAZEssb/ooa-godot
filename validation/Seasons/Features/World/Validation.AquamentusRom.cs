namespace Oracles;

public sealed partial class ValidationRoot
{
    private async System.Threading.Tasks.Task ValidateSeasonsAquamentusRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var viewport=new SubViewport {Size=new(160,144),Disable3D=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};
            AddChild(viewport);
            var root=StartSeasonsAfterImpa(batched,viewport);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x12);s.Link.WarpTo(new(120,136));s.Link.BeginCutsceneControl();
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);entities.DisableLinkCollisionsAndMenu();
                FailIf(entities.Entities<AquamentusCharacter>().Count!=1||entities.RoomEnemyCount!=1,
                    "The original $4:12 before-event stream must place one counted ENEMY_AQUAMENTUS $78:$00.");
                var definition=EnemyDefinitionDatabase.For(SeasonsGameContent.AssetRoot).Get(0x78);
                FailIf(definition.Sprites.Length!=5||definition.Health!=20||definition.Animations.Length!=9||
                    !definition.Animations[0].Split('|').Any(frame=>frame.Split('@')[1].Split('~')[0].Split(';').Length==18),
                    "Aquamentus must import all five graphics sheets, HP20, nine poses and the eighteen-sprite enemyOamData4ba07 hovering frame.");
                var seed=entities.Random.CaptureState();var native=SharedNativeEnemyReference(s.Rooms,s.Link,0x78);native.LoadDungeon(1);
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                native[0xcc0c]=0xa7;native[0xcc0e]=0x8f;
                native[0xd080]=0x11;native[0xd081]=0x78;native[0xd08b]=80;native[0xd08d]=176;
                native.SetNativeByte(s.Memory.FieldAddress("wNumEnemies"),1);
                native.SetNativeByte(s.Memory.FieldAddress("wScrollMode"),1);
                native.SetNativeByte(s.Memory.FieldAddress("wActiveMusic2"),(byte)s.Sound.NativeRoomMusic);
                byte signal=s.Memory.ReadField("wcc93"),text=0;int update=0;bool shots=false,declaredCharge=false;
                byte cameraY=unchecked((byte)-(int)entities.ToScreen(Vector2.Zero).Y),cameraX=unchecked((byte)-(int)entities.ToScreen(Vector2.Zero).X);
                var states=new HashSet<int>();
                var graphics=new LinkCollisionRom(game:OracleGame.Seasons);
                graphics.Call(0x04e7,bank:0,accumulator:0x0f);
                graphics.Call(0x04e7,bank:0,accumulator:0x80);
                // Normalize the five native extra-GFX slots to tile base $00.
                foreach(var (pointer,bank,index) in new[]{(0x44b2,0xab,0),(0x4673,0xab,1),(0x4851,0xab,2),(0x49f7,0xab,3),(0x4b98,0x6b,4)})
                    graphics.Call(0x064e,speed:31,angle:bank,bank:0,objectPage:0x80+index*2,objectOffset:0,dataAddress:pointer,restoresBank:false);
                var poses=new HashSet<(int SubId,int Animation,int Frame)>();
                void Compare()
                {
                    update++;
                    // Door timing has physical-route coverage. Feed its prior
                    // interaction-pass signal into this isolated ENEMY walk.
                    native.SetNativeByte(s.Memory.FieldAddress("wcc93"),signal);
                    native.SetNativeByte(s.Memory.FieldAddress("wTextIsActive"),text);
                    native.Word(0xd00a,(int)(s.Link.PrecisePosition.Y*256));native.Word(0xd00c,(int)(s.Link.PrecisePosition.X*256));
                    native[0xffae]=(byte)s.Link.Position.Y;native[0xffaf]=(byte)s.Link.Position.X;
                    native[0xffa8]=cameraY;native[0xffa9]=cameraX;
                    native.AdvanceEnemies(entities.FrameCounter);native.AdvanceParts();native.AdvanceScreenShake();
                    var actors=entities.Entities<AquamentusCharacter>();
                    FailIf(actors.Count!=Enumerable.Range(0,16).Count(slot=>native[0xd080+slot*256]!=0),
                        $"Aquamentus update{update}: physical ENEMY allocation differs.");
                    foreach(var actor in actors)
                    {
                        int at=0xd080+actor.Slot*256;if(actor.SubId==1)states.Add(actor.State);
                        if(actor.SubId is 2 or 3&&actor.State>=9&&actor.Health>0&&
                            poses.Add((actor.SubId,actor.AnimationIndex,actor.AnimationFrame)))
                            CompareSeasonsInteractionPixels(native,graphics,actor.CurrentAnimationTexture,actor.Animation.CurrentOffset,at,
                                $"Aquamentus ENEMY$78:${actor.SubId:x2} animation${actor.AnimationIndex:x2}/frame{actor.AnimationFrame}",tileBase:0);
                        FailIf(actor.SubId!=native[at+2]||actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Counter2!=native[at+7]||
                            actor.Angle!=native[at+9]||actor.Speed!=native[at+16]||actor.Health!=native[at+0x29]||
                            actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            actor.ZFixed!=unchecked((short)native.Word(at+14))||actor.Visible!=((native[at+0x1a]&128)!=0)||
                            actor.CollisionEnabled!=((native[at+0x24]&128)!=0)||
                            actor.InvincibilityCounter!=unchecked((sbyte)native[at+0x2b])||actor.KnockbackCounter!=native[at+0x2d]||actor.KnockbackAngle!=native[at+0x2c]||
                            actor.Animation.RemainingUpdates!=native[at+0x20]||actor.AnimationParameter!=native[at+0x21],
                            $"Aquamentus update{update}/slot${actor.Slot:x2}/sub{actor.SubId}: state{actor.State}/{native[at+4]}, counters{actor.Counter}:{actor.Counter2}/{native[at+6]}:{native[at+7]}, speed{actor.Speed}/{native[at+16]}, angle{actor.Angle}/{native[at+9]}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{actor.ZFixed}/{unchecked((short)native.Word(at+14))}, animation{actor.Animation.RemainingUpdates}:{actor.AnimationParameter}/{native[at+0x20]}:{native[at+0x21]}.");
                    }
                    var projectiles=entities.EntityAdapters<AquamentusProjectileRoomEntity>().ToArray();
                    FailIf(projectiles.Length!=Enumerable.Range(0,16).Count(slot=>native[0xd0c0+slot*256]!=0&&native[0xd0c1+slot*256]==0x40),
                        $"Aquamentus update{update}: ordered PART$40 spread allocation differs.");
                    foreach(var shot in projectiles)
                    {
                        shots=true;int slot=Enumerable.Range(0,16).Single(slot=>ReferenceEquals(entities.ResolvePartSlot(slot),shot));int at=0xd0c0+slot*256;
                        FailIf(shot.State!=native[at+4]||shot.Counter!=native[at+6]||shot.Angle!=native[at+9]||
                            shot.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            shot.ZHigh!=unchecked((sbyte)native[at+15])||shot.Palette!=(native[at+0x1c]&7)||shot.Visible!=((native[at+0x1a]&128)!=0),
                            $"Aquamentus PART$40 update{update}/slot${slot:x2}: state{shot.State}/{native[at+4]}, counter{shot.Counter}/{native[at+6]}, angle{shot.Angle}/{native[at+9]}, XY{shot.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{shot.ZHigh}/{unchecked((sbyte)native[at+15])}, palette{shot.Palette}/{native[at+0x1c]&7}.");
                    }
                    foreach(var adapter in entities.EntityAdapters<BossShadowRoomEntity>())
                    {
                        var shadow=(BossShadowEffect)adapter.Node;
                        int slot=Enumerable.Range(0,16).Single(slot=>ReferenceEquals(entities.ResolvePartSlot(slot),adapter));int at=0xd0c0+slot*256;
                        FailIf(native[at+1]!=7||shadow.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            shadow.Visible!=((native[at+0x1a]&128)!=0),$"Aquamentus shadow update{update}: native high-position copy or visibility differs.");
                    }
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||
                        entities.RoomEnemyCount!=native.ReadNativeByte(s.Memory.FieldAddress("wNumEnemies"))||
                        entities.ScreenShakeCounter!=native[0xcd18]||entities.HorizontalScreenShakeCounter!=native[0xcd19],
                        $"Aquamentus update{update}: shared RNG/count differs: draws{rng.Calls-seed.Calls}/{native.RandomCalls}, RNG${rng.Rng2:x2}{rng.Rng1:x2}/${native[0xff95]:x2}{native[0xff94]:x2}, count{entities.RoomEnemyCount}/{native.ReadNativeByte(s.Memory.FieldAddress("wNumEnemies"))}.");
                    signal=s.Memory.ReadField("wcc93");
                    // Cutscene-controlled Link deliberately cannot complete
                    // the entry movement; acknowledge the external door gate.
                    if(update==1){s.Memory.SetField("wcc93",0);signal=0;}
                    cameraY=unchecked((byte)-(int)entities.ToScreen(Vector2.Zero).Y);cameraX=unchecked((byte)-(int)entities.ToScreen(Vector2.Zero).X);
                    if(update==5)s.Dialogue.ShowGameplayMessage("Boss pause.",100);
                    if(update==9)s.Dialogue.Close();
                    if(update==300)s.Link.WarpTo(new(216,88));
                    if(update==1100)s.Link.WarpTo(new(40,88));
                    if(update>=1100&&!declaredCharge&&actors.Any(actor=>actor.SubId==1&&actor.State==11&&actor.Counter==1))
                    {
                        // One declared global input exercises the charge gate:
                        // native next draw $d4 selects bit4 of quarter$01's $31.
                        entities.Random.RestoreState(entities.Random.CaptureState() with{Rng1=0x5e,Rng2=0x27});
                        native[0xff94]=0x5e;native[0xff95]=0x27;s.Link.WarpTo(new(104,88));declaredCharge=true;
                    }
                    text=(byte)(entities.TextActiveSource()?1:0);
                    entities.DisableLinkCollisionsAndMenu();
                }
                for(int tick=0;tick<300&&!entities.Entities<AquamentusCharacter>().Any(actor=>actor.SubId==1&&actor.State==10&&actor.Counter==2);tick++)
                    app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Compare);
                var landingBody=entities.Entities<AquamentusCharacter>().Single(actor=>actor.SubId==1);
                var landingHorn=entities.Entities<AquamentusCharacter>().Single(actor=>actor.SubId==3);
                FailIf(landingBody.State!=10||landingBody.Counter!=2,"ENEMY$78 must reach the two updates before landing.");
                int draws=0,drawnZ=0;
                landingHorn.Draw+=()=>{draws++;drawnZ=landingHorn.ZFixed>>8;};
                landingHorn.QueueRedraw();
                await FlushDraws();
                FailIf(draws==0||drawnZ!=(landingHorn.ZFixed>>8),"ENEMY$78:$03 must publish its airborne draw before landing.");
                int beforeDraws=draws,pose=landingHorn.AnimationIndex;
                app.Step(2,Vector2.Zero,batched:batched,afterUpdate:Compare);
                await FlushDraws();
                FailIf(landingBody.State!=11||landingHorn.AnimationIndex!=pose||draws<=beforeDraws||drawnZ!=-7,
                    $"ENEMY$78:$03 must redraw at native zh=$f9 when the body lands without changing its horn pose: draws{beforeDraws}->{draws}, cachedZ{drawnZ}, currentZ{landingHorn.ZFixed>>8}.");
                app.Step(2400-update,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(!shots||!new[]{8,9,10,11,12,13,14,15}.All(states.Contains),
                    $"The native boss comparison must cover landing, both attacks, recovery and projectiles: states{string.Join(',',states)}, shots{shots}.");
                var body=entities.Entities<AquamentusCharacter>().Single(actor=>actor.SubId==1);
                var horn=entities.Entities<AquamentusCharacter>().Single(actor=>actor.SubId==3);
                for(int tick=0;tick<900&&(body.ZFixed>>8!=0||Math.Abs(body.Position.Y-horn.Position.Y)<20);tick++)
                    app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Compare);
                var hornAdapter=entities.EntityAdapters<AquamentusRoomEntity>().Single(adapter=>adapter.Node==horn);
                Vector2 origin=horn.Position.Floor()-Vector2.Right;
                FailIf(!hornAdapter.ApplySwordHit(horn.CollisionBounds,origin,2,EnemyKnockbackStrength.Low,[]),"Declared horn contact must accept native effect$21.");
                native.CheckDeclaredItemCollision(4,2,origin,horn.ZFixed>>8);
                FailIf(horn.Health!=18||native[0xd0a9+horn.Slot*256]!=18||horn.InvincibilityCounter!=32,
                    "The native horn row must deal two HP, publish JUST_HIT and grant counter$20 without recoil.");
                app.Step(33,Vector2.Zero,batched:batched,afterUpdate:Compare);
                var armor=entities.EntityAdapters<AquamentusRoomEntity>().Single(adapter=>adapter.Node==body);
                var clinks=new List<RoomEntitySpawn>();origin=body.Position.Floor()-Vector2.Right;
                FailIf(!armor.ApplySwordHit(body.CollisionBounds,origin,2,EnemyKnockbackStrength.Low,clinks),"The native body must accept an armored Sword contact.");
                bool recoils=armor.TryGetSwordAttackerKnockback(EnemyKnockbackStrength.Low,out var recoil);
                native.CheckDeclaredItemCollision(4,2,origin,body.ZFixed>>8);
                FailIf(body.Health!=20||body.InvincibilityCounter!=-28||native[0xd0ab+body.Slot*256]!=0xe4||
                    !recoils||recoil.Frames!=11||native[0xd72d]!=11||clinks is not [EnemyClinkSpawn],
                    "The native body row must retain HP20, grant negative$1c invincibility and produce a clink plus eleven attacker-recoil updates.");
                app.Step(33,Vector2.Zero,batched:batched,afterUpdate:Compare);
                // Sword collision has physical coverage. Declare the native
                // lethal input here to compare linked deletion/count tails.
                horn.Health=0;native[0xd0a9+horn.Slot*256]=0;s.Link.WarpTo(new(40,136));
                app.Step(230,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(entities.Entities<AquamentusCharacter>().Count!=0||entities.RoomEnemyCount!=0||!s.Save.HasRoomFlag(4,0x12,0x80)||
                    s.Sound.NativeActiveMusic!=native.ReadNativeByte(s.Memory.FieldAddress("wActiveMusic"))||
                    s.Sound.ActiveMusic!=s.Sound.NativeRoomMusic,
                    "Native horn deletion must kill both linked objects, retain the renderer's 120-update tail and pass its count through PART$04 to the reward script.");
                s.DebugTravel(4,0x1c);s.Link.EndCutsceneControl();s.Link.WarpTo(new(72,72));s.Link.Face(Vector2I.Right);
                foreach(int level in new[]{1,2,3})
                {
                    s.Inventory.GiveTreasure(TreasureDatabase.For(SeasonsGameContent.AssetRoot).GetObject($"TREASURE_OBJECT_SHIELD_0{level-1}"));
                    s.Inventory.EquipA(TreasureId.Shield);s.Link.UpdateShieldForValidation(attackHeld:true,itemHeld:false);
                    Vector2 edge=new(s.Link.ShieldCollisionBounds.End.X+2,s.Link.ShieldCollisionBounds.GetCenter().Y);
                    var shot=new AquamentusProjectileRoomEntity(new(0,edge,0,1),
                        AquamentusDatabase.For(SeasonsGameContent.AssetRoot).Projectile,entities);
                    shot.UpdateFrame(new(s.Link,entities.FrameCounter,false),[]);
                    FailIf(s.Link.OverlapsEnemyCollision(shot.Bounds),"The Aquamentus shield fixture must exclude Link's body.");
                    shot.HandleLinkContact(s.Link);
                    FailIf(shot.Finished,"PART$40 must defer shield deletion until its next update.");
                    shot.UpdateFrame(new(s.Link,entities.FrameCounter+1,false),[]);
                    FailIf(shot.Finished!=(level>=2),$"PART$40 must ignore the wooden shield and accept shield levels 2/3; level{level}, nativeLevel{s.Inventory.ShieldLevel}, Link{s.Link.Position}, shield{s.Link.ShieldCollisionBounds}, shot{shot.Position}, HP{s.Inventory.HealthQuarters}.");
                    FailIf((object)shot is ISeedCollisionTarget or ISwordHittableRoomEntity or IPostObjectItemCollisionRoomEntity,
                        "PART$40's native $0d000000 mask must ignore seeds, Swords and other item attacks.");
                    shot.Free();
                }
            }
            finally {root.Free();viewport.Free();}
        }

        async System.Threading.Tasks.Task FlushDraws()
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
    }
}
