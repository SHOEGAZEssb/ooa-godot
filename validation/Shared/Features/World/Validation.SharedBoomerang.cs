
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedBoomerang(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var game = memory.Game;
        var app = new ApplicationValidationFixture(root);
        var room = rooms.CurrentRoom;
        var tiles = (from y in Enumerable.Range(1,6) from x in Enumerable.Range(1,8)
            let point = new Vector2(x*16+8,y*16+8)
            select (Point:point,Tile:room.GetMetatile(point),Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        try
        {
            foreach (int subid in game == OracleGame.Seasons ? new[] {0,1} : new[] {0})
            {
                byte[] bytes = OracleAssetCache.ReadBytes(rooms.AssetRoot+$"metadata/boomerang_gfx_{subid}.bin");
                string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
                FailIf(hash!=(subid==0 ? "4917528fb711ca6727ad026e1bef265e58b7878501c9940bbaf74b194e2e2115" :
                    "f3bf8f1a8f68f7346de35ef35c77665a0d6aefdf5c12cabb26fe538a1f0b8d95"),
                    $"{game}: Boomerang variant${subid:x2} must match its independent clean-US weapon upload bytes.");
            }
            foreach (int level in game == OracleGame.Seasons ? new[] {1,2} : new[] {1})
            foreach (bool primary in new[] {false,true})
            foreach (int scenario in level == 2 ? new[] {0,1,2} : new[] {0,1})
            {
                bool cancel=scenario==1, tileBreak=scenario==2;
                player.ClearNativeItemParents(); entities.Clear();
                foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point, (byte)(game == OracleGame.Ages ? 0x3a : 4), 0, 0);
                player.WarpTo(new(88,72)); player.Face(Vector2I.Up);
                Vector2 bush=new(88,40);
                if (tileBreak)
                {
                    room.SetPositionTileAndCollision(bush,0xc4,0x0f,0);
                    room.SetUnderlyingMetatile(bush,4);
                }
                inventory.GiveTreasure(TreasureId.Boomerang, level);
                FailIf(inventory.BoomerangLevel != level, $"{game}: Boomerang fixture did not obtain level{level}.");
                inventory.EquipA(primary ? TreasureId.Boomerang : 0); inventory.EquipB(primary ? 0 : TreasureId.Boomerang);
                var native = new BraceletRom(room, player.PrecisePosition, 0, 0xff, game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
                native.InitializeHud(root._gameplaySession.StatusBar.DisplayedHealth,root._gameplaySession.StatusBar.DisplayedRupees);
                native[0xcc2d] = (byte)rooms.ActiveGroup;
                var rng = entities.Random.CaptureState(); native[0xff94]=rng.Rng1; native[0xff95]=rng.Rng2;
                var sounds = root._sound.AttachPlayRequestAudit();
                int update=0, previous=0;
                void Step(int count=1, int keys=0)
                {
                    int edge=keys & ~previous; previous=keys;
                    Vector2 direction = (keys & 0x10)!=0 ? Vector2.Right : (keys & 0x80)!=0 ? Vector2.Down : (keys & 0x40)!=0 ? Vector2.Up : Vector2.Zero;
                    app.Step(count,direction,held:MenuRomActions(keys),pressed:MenuRomActions(edge),batched:batched,afterUpdate:()=>
                    {
                        native.UpdateLinkItems(entities.FrameCounter,keys,interactions:true,parts:tileBreak,statusBar:true);
                        string context=$"{game}: shared Boomerang L{level} A={primary} cancel={cancel} update{++update}";
                        var children=entities.Entities<BoomerangItem>();
                        int child=Enumerable.Range(0xd7,5).Select(page=>page<<8).FirstOrDefault(at=>native[at]!=0 && native[at+1]==6);
                        int parent=Enumerable.Range(0xd3,2).Select(page=>page<<8).FirstOrDefault(at=>native[at]!=0 && native[at+1]==6);
                        FailIf(children.Count!=(child==0 ? 0 : 1) || entities.BoomerangParent.Active!=(parent!=0),context+": native parent/child lifetime differs.");
                        CompareParentAnimationPublicationRom(player,at=>native[at],context);
                        if (parent!=0)
                            FailIf(entities.BoomerangParent.Counter!=native[parent+0x20] || entities.BoomerangParent.Parameter!=native[parent+0x21] ||
                                entities.BoomerangParent.Controlling!=(native[parent+4]==2),context+": native parent control/animation differs.");
                        if (child!=0)
                        {
                            var item=children.Single();
                            FailIf(item.State!=native[child+4] || item.SubId!=native[child+2] || item.Counter!=native[child+6] || item.Angle!=native[child+9] ||
                                item.TargetAngle!=native[child+0x34] || item.PrecisePosition!=new Vector2(native.Word(child+0x0c)/256f,native.Word(child+0x0a)/256f) ||
                                item.CollisionEnabled!=((native[child+0x24]&0x80)!=0),
                                context+$": native flight/target/collision differs: state{item.State}/${native[child+4]:x2}, target${item.TargetAngle:x2}/${native[child+0x34]:x2}, tileBreak={tileBreak}, hit${native[child+0x2a]:x2}. "+
                                string.Join("; ",entities.Entities<ItemDropEffect>().Select(drop=>$"drop${drop.SubId:x2}/{drop.State}/{drop.PrecisePosition}/collision{drop.CollisionEnabled}")));
                            FailIf(item.CollisionEnabled && item.CollisionType!=(native[child+0x24]&0x7f) ||
                                item.Damage!=-unchecked((sbyte)native[child+0x28]),context+": native collision type/damage differs.");
                        }
                        int packed=room.GetPackedPosition(bush);
                        FailIf(room.GetMetatile(bush)!=native[0xcf00+packed] || room.GetTerrainInfo(bush).Collision!=native[0xce00+packed] ||
                            room.GetUnderlyingMetatile(bush)!=native.Underlying(packed),context+": magic tile/underlying/collision side effects differ.");
                        if(tileBreak)
                        {
                            var drops=entities.Entities<ItemDropEffect>();
                            int at=Enumerable.Range(0,16).Select(slot=>0xd0c0+slot*256).FirstOrDefault(address=>native[address]!=0&&native[address+1]==1);
                            FailIf(drops.Count!=(at==0?0:1),context+$": native drop lifetime differs, runtime{drops.Count}/native${at:x4}.");
                            if(at!=0)
                            {
                                var drop=drops.Single();
                                FailIf((int)drop.State!=native[at+4] || drop.Counter!=native[at+6] ||
                                    drop.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f) ||
                                    (drop.ZFixed&0xffff)!=native.Word(at+14) || drop.CollisionEnabled!=((native[at+0x24]&0x80)!=0),
                                    context+$": drop runtime{drop.State}/{drop.Counter}/{drop.PrecisePosition}/${drop.ZFixed&0xffff:x4}/{drop.CollisionEnabled}, native{native[at+4]}/{native[at+6]}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}/${native.Word(at+14):x4}/${native[at+0x24]:x2}.");
                            }
                        }
                        FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f),context+": redirected Link movement differs.");
                        FailIf(inventory.HealthQuarters!=native[game==OracleGame.Ages?0xc6aa:0xc6a2] ||
                            root._gameplaySession.StatusBar.DisplayedHealth!=native[0xcbe4],context+": native live/displayed health differs.");
                        FailIf(!sounds.Requests.SequenceEqual(native.Sounds),context+$": cues runtime[{string.Join(',',sounds.Requests.Select(id=>$"${id:x2}"))}], native[{string.Join(',',native.Sounds.Select(id=>$"${id:x2}"))}]; tileBreak={tileBreak}, rupees runtime{inventory.Rupees}/hud{root._gameplaySession.StatusBar.DisplayedRupees}, nativeBCD${native.Word(game==OracleGame.Ages?0xc6ad:0xc6a5):x4}/hud${native.Word(game==OracleGame.Ages?0xcbe5:0xcbe6):x4}.");
                        var now=entities.Random.CaptureState();
                        FailIf(now.Rng1!=native[0xff94] || now.Rng2!=native[0xff95] || now.Calls-rng.Calls!=native.RandomCalls,
                            context+$": RNG differs: ${now.Rng1:x2}/${now.Rng2:x2} calls{now.Calls-rng.Calls}, native${native[0xff94]:x2}/${native[0xff95]:x2} calls{native.RandomCalls}, tileBreak={tileBreak}.");
                    });
                }
                int button=primary ? 1 : 2;
                Step(1,button);
                Step(tileBreak ? 12 : 3,button|(tileBreak ? 0x40 : 0x10));
                if (tileBreak) FailIf(room.GetMetatile(bush)!=4,$"{game}: the controlled magic Boomerang must cut native bush$c4 with source$07.");
                root._scene.Dialogue.ShowMessage("Boomerang control pause.",player.Position.Y); native[0xcba0]=1;
                Step(3,button|0x80);
                root._scene.Dialogue.Close(); native[0xcba0]=0;
                Step(4,button|0x80);
                if (cancel) { player.ClearNativeItemParents(); native.ClearItemParents(); }
                Step(1);
                for (int guard=0; (entities.Entities<BoomerangItem>().Count!=0 || entities.BoomerangParent.Active) && guard<220; guard++) Step();
                FailIf(entities.Entities<BoomerangItem>().Count!=0 || entities.BoomerangParent.Active,$"{game}: Boomerang did not return and release both allocations.");
                Step(1,button); Step(20);
                for (int guard=0; entities.Entities<BoomerangItem>().Count!=0 && guard<220; guard++) Step();
                FailIf(entities.Entities<BoomerangItem>().Count!=0,$"{game}: repeated Boomerang use did not complete.");
            }
        }
        finally
        {
            player.ClearNativeItemParents(); entities.ClearPhysicalPlayerItems();
            foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,tile.Tile,tile.Collision,0);
        }
    }
}
