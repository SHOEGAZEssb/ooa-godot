namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonFirstKey()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                session.Inventory.GiveTreasure(TreasureId.Sword,1);session.Inventory.EquipA(TreasureId.Sword);
                session.DebugTravel(4,0x1a);session.Link.WarpTo(new(200,88));
                // The block room has its own physical regression; stage its
                // solved count and approach the key room through the real door.
                app.Step(90,Vector2.Zero,batched:batched);
                session.Entities.ClearRoomEnemyCount();app.Step(90,Vector2.Zero,batched:batched);
                FailIf(session.Collision.Collides(session.Link.Position),"The $4:1b approach must begin on original eastern $4:1a floor.");
                for(int update=0;update<80&&!session.Scrolling;update++)app.Step(1,Vector2.Right,batched:batched);
                FailIf(!session.Scrolling,$"The solved block room's east shutter must admit $4:1b: Link{session.Link.Position}, room${session.RoomId:x2}, tile${session.Room.Layout[0x5e]:x2}, count{session.Entities.RoomEnemyCount}, disabled${session.Memory.ReadField("wDisabledObjects"):x2}.");
                for(int update=0;update<120&&session.Scrolling;update++)app.Step(1,Vector2.Zero,batched:batched);
                app.Step(24,Vector2.Right,batched:batched);
                FailIf(session.RoomId!=0x1b || session.Entities.RoomEnemyCount!=4 ||
                    session.Entities.Entities<StalfosCharacter>().Count!=4 ||
                    session.Entities.EntityAdapters<ItemDropProducerRoomEntity>().Count()!=3 ||
                    session.Entities.Entities<DungeonRewardRoomEntity>().Count!=1,
                    "$4:1b must construct four counted Stalfos, three uncounted $59 producers and its falling-key watcher.");
                bool countedDeath=false;
                void Observe()
                {
                    if(session.Entities.Entities<EnemyDeathPuffEffect>().Count>0 && session.Entities.RoomEnemyCount>0)countedDeath=true;
                    FailIf(session.Entities.RoomEnemyCount>0 && session.Entities.Entities<GroundTreasurePickup>().Count!=0,
                        "The first small key must wait for Stalfos and their counted destruction PARTs.");
                }
                // Isolate the Sword/death/key handoff on clear original floor.
                for(int swing=0;swing<3&&session.Entities.Entities<StalfosCharacter>().Any(enemy=>!enemy.IsDead);swing++)
                {
                    session.Link.WarpTo(new(72,136));session.Link.Face(Vector2I.Up);
                    foreach(var enemy in session.Entities.Entities<StalfosCharacter>().Where(enemy=>!enemy.IsDead))enemy.Position=new(72,120);
                    FailIf(session.Collision.Collides(session.Link.Position),"The key-room Sword handoff must use original floor.");
                    app.Step(2,Vector2.Zero,batched:batched);
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,afterUpdate:Observe);
                    app.Step(32,Vector2.Zero,batched:batched,afterUpdate:Observe);
                }
                FailIf(session.Entities.Entities<StalfosCharacter>().Any(enemy=>!enemy.IsDead) || !countedDeath,
                    "Actual Sword contacts must defeat all four source Stalfos through their destruction PARTs.");
                for(int update=0;update<300&&!session.Entities.Entities<GroundTreasurePickup>().Any(key=>key.State==PickupState.Waiting);update++)
                    app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Observe);
                var key=session.Entities.Entities<GroundTreasurePickup>().Single();
                FailIf(key.Position!=new Vector2(120,88) || key.Record.TreasureObject!="TREASURE_OBJECT_SMALL_KEY_01" ||
                    key.State!=PickupState.Waiting || key.Record.SpawnMode!=2 || key.Record.GrabMode!=TreasureGrabMode.NoChange ||
                    key.Record.SpawnDelayFrames!=40 || key.Record.BounceCount!=2 || key.Record.Gravity!=0x10 || key.Record.BounceSpeed!=-0xaa ||
                    session.Save.HasRoomFlag(4,0x1b,0x20),
                    "$12:$01 must drop the original $30:$01 key at (120,88), with wait40 and two gravity$10 bounces, before setting ROOMFLAG_ITEM.");
                app.Step(48,Vector2.Right,batched:batched);app.Step(48,Vector2.Up,batched:batched);
                FailIf(session.Inventory.GetDungeonSmallKeys(1)!=1 || !session.Save.HasRoomFlag(4,0x1b,0x20) ||
                    session.Inventory.GetDungeonSmallKeys(0)!=0 || session.Inventory.GetDungeonSmallKeys(2)!=0,
                    $"Approaching the fallen key through floor must grant only dungeon$01's key and ROOMFLAG_ITEM: Link{session.Link.Position}.");

                // The dungeon item is a separate route. Stage its inventory
                // dependency here to exercise the real satchel -> torch -> door.
                session.Inventory.GiveTreasure(TreasureId.SeedSatchel,1);
                session.Inventory.GiveTreasure(TreasureId.EmberSeeds,8);session.Inventory.SelectSatchelSeeds(0);
                session.Inventory.EquipA(TreasureId.SeedSatchel);
                for(int visit=0;visit<2;visit++)
                {
                    if(visit!=0)
                    {
                        session.DebugTravel(4,0x1a);session.DebugTravel(4,0x1b);
                        // Freeze enemy updates while checking the already
                        // collected reward and repeated seed action.
                        session.Memory.SetField("wDisabledObjects",4);
                        app.Step(3,Vector2.Zero,batched:batched);
                        FailIf(session.Entities.Entities<DungeonRewardRoomEntity>().Count!=0 ||
                            session.Entities.Entities<GroundTreasurePickup>().Count!=0 || session.Inventory.GetDungeonSmallKeys(1)!=1,
                            "A collected $12:$01 watcher must retire without another key on re-entry.");
                    }
                    var torches=session.Entities.Entities<LightableTorchRoomEntity>();
                    FailIf(!torches.Select(torch=>torch.PackedPosition).Order().SequenceEqual(new[]{0x16,0x18}) ||
                        session.Room.Layout[0x16]!=8 || session.Room.Layout[0x18]!=8 || session.Room.Layout[7]!=0x78,
                        "The source scanner and torch door must restore both unlit torches and the closed north gate each visit.");
                    session.Link.WarpTo(new(120,24));session.Link.Face(Vector2I.Left);
                    FailIf(session.Collision.Collides(session.Link.Position),"Satchel use must begin on original floor between the torches.");
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);app.Step(24,Vector2.Zero,batched:batched);
                    FailIf(session.Room.Layout[0x16]!=9 || session.Room.Layout[0x18]!=8 || session.Room.Layout[7]!=0x78,
                        "The first real Ember Seed must light only $16 and leave the two-torch gate closed.");
                    session.Link.Face(Vector2I.Right);
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);app.Step(90,Vector2.Zero,batched:batched);
                    FailIf(session.Room.Layout[0x18]!=9 || session.Room.Layout[7]!=0xa0 ||
                        session.Entities.Entities<LightableTorchRoomEntity>().Count!=0 ||
                        session.Entities.Entities<DungeonDoorRoomEntity>().Any(door=>door.SubId==0x14),
                        "The second Ember Seed must release $1e:$14 through the shared torch count and retire the completed gate.");
                }
            }
            finally {root.Free();}
        }
    }

    private void ValidateSeasonsRootDungeonMapChest()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                session.Inventory.GiveTreasure(TreasureId.Sword,1);session.Inventory.EquipA(TreasureId.Sword);
                session.DebugTravel(4,0x1a);session.Link.WarpTo(new(24,88));
                // $50 is native key door$73. Stage the TREASURE_SMALL_KEY $30
                // earned in $4:1b; its producer has a separate regression.
                session.Inventory.GiveTreasure(0x30,1);
                FailIf(session.Collision.Collides(session.Link.Position),"$4:19 approach must begin on actual western $4:1a floor.");
                for(int update=0;update<60&&!session.Scrolling;update++)app.Step(1,Vector2.Left,batched:batched);
                FailIf(!session.Scrolling,$"The keyed $4:1a western passage must scroll into $4:19: Link{session.Link.Position}, keys={session.Inventory.GetDungeonSmallKeys(1)}.");
                for(int update=0;update<120&&session.Scrolling;update++)app.Step(1,Vector2.Zero,batched:batched);
                FailIf(session.RoomId!=0x19 || session.Entities.Entities<StalfosCharacter>().Count!=4 || session.Entities.RoomEnemyCount!=4 ||
                    session.Inventory.GetDungeonSmallKeys(1)!=0,
                    "The real eastern $4:19 arrival must admit its four native Stalfos before the map-chest gate runs.");
                app.Step(24,Vector2.Left,batched:batched);
                bool pendingDeath=false;
                void Observe()
                {
                    if(session.Entities.Entities<EnemyDeathPuffEffect>().Count>0 && session.Entities.RoomEnemyCount>0)pendingDeath=true;
                    FailIf(session.Entities.RoomEnemyCount>0 && session.Room.Layout[0x57]==0xf1,
                        "The map chest must wait for counted enemies and their destruction PARTs.");
                }
                // Stage combatants on clear original floor after proving room
                // construction. This isolates Sword -> death PART -> chest.
                for(int swing=0;swing<3&&session.Entities.Entities<StalfosCharacter>().Any(enemy=>!enemy.IsDead);swing++)
                {
                    session.Link.WarpTo(new(72,136));session.Link.Face(Vector2I.Up);
                    foreach(var enemy in session.Entities.Entities<StalfosCharacter>().Where(enemy=>!enemy.IsDead))enemy.Position=new(72,120);
                    FailIf(session.Collision.Collides(session.Link.Position),"The staged Sword handoff must use actual room floor.");
                    app.Step(2,Vector2.Zero,batched:batched);
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,afterUpdate:Observe);
                    FailIf(!session.Link.IsAttacking,"The map-room combat fixture must use the equipped Sword caller.");
                    app.Step(32,Vector2.Zero,batched:batched,afterUpdate:Observe);
                }
                FailIf(session.Entities.Entities<StalfosCharacter>().Any(enemy=>!enemy.IsDead) || !pendingDeath,
                    "Sword contacts must defeat all four source Stalfos through counted destruction PARTs.");
                for(int update=0;update<100&&session.Room.Layout[0x57]!=0xf1;update++)
                    app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Observe);
                FailIf(session.Room.Layout[0x57]!=0xf1 || session.Entities.RoomEnemyCount!=0,
                    "Completing $4:19 combat must reveal its $57 Dungeon Map chest.");
                app.Step(48,Vector2.Right,batched:batched);app.Step(48,Vector2.Up,batched:batched);
                app.Step(2,Vector2.Zero,batched:batched);
                FailIf(session.Collision.Collides(session.Link.Position) || session.Link.Position.X!=120,
                    $"The map chest must be approached from collision-valid southern floor: Link{session.Link.Position}.");
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                FailIf(!session.Save.HasRoomFlag(4,0x19,0x20) || session.Room.Layout[0x57]!=0xf0,
                    "The real chest input must open $57 and publish ROOMFLAG_ITEM before the held-treasure grant.");
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf(!session.Inventory.HasDungeonMap(1) || session.Inventory.HasDungeonMap(0) || session.Inventory.HasDungeonMap(2) ||
                    !session.Save.HasRoomFlag(4,0x19,0x20) || session.Room.Layout[0x57]!=0xf0,
                    $"Opening the $4:19 chest must grant only dungeon$01's map and set ROOMFLAG_ITEM: Link{session.Link.Position}, map={session.Inventory.HasDungeonMap(1)}.");
                session.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched);
                session.DebugTravel(4,0x1a);session.DebugTravel(4,0x19);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(session.Room.Layout[0x57]!=0xf0 || session.Entities.Entities<EnemyClearChestRoomEntity>().Count!=0 ||
                    !session.Inventory.HasDungeonMap(1),"Re-entry must retain the opened map chest and retire its collected watcher.");
            }
            finally {root.Free();}
        }
    }

    private void ValidateSeasonsRootDungeonBlockRoom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;
                var app=new ApplicationValidationFixture(root);
                for(int visit=0;visit<2;visit++)
                {
                    session.DebugTravel(4,0x1c);
                    session.Link.WarpTo(new(120,24));
                    FailIf(session.Collision.Collides(session.Link.Position),"$4:1a must be approached through actual $4:1c floor.");
                    for(int update=0;update<60&&!session.Scrolling;update++)app.Step(1,Vector2.Up,batched:batched);
                    FailIf(!session.Scrolling,"The northern $4:1c passage must scroll into $4:1a.");
                    for(int update=0;update<120&&session.Scrolling;update++)app.Step(1,Vector2.Zero,batched:batched);
                    app.Step(3,Vector2.Zero,batched:batched);
                    FailIf(session.RoomId!=0x1a || session.Rooms.CurrentDungeonIndex!=1 ||
                        session.Entities.EntityAdapters<ItemDropProducerRoomEntity>().Count()!=2 || session.Entities.RoomEnemyCount!=1,
                        "$4:1a must construct two uncounted ENEMY$59 producers and one counted INTERAC$13:$01 sentinel.");
                    var room=session.Room;
                    FailIf(room.Layout[0x39]!=0x1c || session.Entities.EntityAdapters<DungeonDoorRoomEntity>().Count()!=3,
                        "$4:1a must restore its source $39 block and construct all three enemy shutters.");
                    for(int update=0;update<150&&session.Link.Position.Y>56;update++)app.Step(1,Vector2.Up,batched:batched);
                    app.Step(14,Vector2.Right,batched:batched);
                    FailIf(session.Collision.Collides(session.Link.Position) || session.Link.Position.X<128 || session.Link.Position.Y!=56,
                        $"The $39 block must be reachable from the southern doorway: Link{session.Link.Position}.");
                    for(int update=0;update<100&&room.Layout[0x39]==0x1c;update++)app.Step(1,Vector2.Right,batched:batched);
                    FailIf(room.Layout[0x39]==0x1c,"Pushing east from actual floor must move the $39 block.");
                    int releaseUpdate=0;
                    // Reserved block movement precedes the interaction walk, so the
                    // trigger installs counter1 on the tile-changing update.
                    app.Step(30,Vector2.Zero,batched:batched,afterUpdate:()=>
                    {
                        releaseUpdate++;
                        FailIf(session.Entities.RoomEnemyCount!=(releaseUpdate<30?1:0),
                            $"pushblockTrigger.s must observe the changed tile, then wait exactly $1e updates: update{releaseUpdate}.");
                    });
                    app.Step(80,Vector2.Zero,batched:batched);
                    FailIf(new[]{0x07,0x5e,0xa7}.Any(position=>room.Layout[position]!=0xa0) ||
                        session.Entities.EntityAdapters<PushBlockTriggerRoomEntity>().Any(),
                        "$4:1a must retire the solved trigger and open its north/east/south shutters.");
                    for(int tick=0;tick<80&&session.Link.Position.X>120;tick++)app.Step(1,Vector2.Left,batched:batched);
                    for(int tick=0;tick<120&&!session.Scrolling;tick++)app.Step(1,Vector2.Up,batched:batched);
                    for(int tick=0;tick<120&&session.Scrolling;tick++)app.Step(1,Vector2.Zero,batched:batched);
                    FailIf(session.RoomId!=0x16,"The solved $4:1a north shutter must reach the original wise man's $4:16 room.");
                    for(int tick=0;tick<150&&session.Link.Position.Y>68;tick++)app.Step(1,Vector2.Up,batched:batched);
                    FailIf(session.Collision.Collides(session.Link.Position),"The wise man must be approached from the real southern passage and clear floor.");
                    for(int conversation=0;conversation<2;conversation++)
                    {
                        app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);app.Step(8,Vector2.Zero,batched:batched);
                        FailIf(session.Dialogue.SourceTextId!=0x3300||!session.Dialogue.IsOpen,
                            $"The actual $4:16 approach must admit repeat TX_3300: Link{session.Link.Position}, text{session.Dialogue.SourceTextId:x4}.");
                        session.Dialogue.Close();app.Step(5,Vector2.Zero,batched:batched);
                    }
                }
            }
            finally {root.Free();}
        }
    }
}
