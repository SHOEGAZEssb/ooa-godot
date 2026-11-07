namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonTrapRoom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);var entities=s.Entities;
                s.Inventory.GiveTreasure(TreasureId.Sword,1);s.Inventory.EquipA(TreasureId.Sword);s.Inventory.EquipB(0);
                s.DebugTravel(4,0x0f);s.Memory.SetField("wDisabledObjects",4);
                // Boarding has separate native coverage. Start mounted on the
                // original western exit rail to isolate this room handoff.
                Vector2 start=new(24,136);
                MinecartValidationView.PrepareMountedBody(s.Memory,start,3);
                s.Memory.SetWramByte(0xd109,0x18);
                entities.BindNativeSpecialObjectPresentation(SpecialObjectId.Minecart,new MinecartBodySpawn());
                s.Link.WarpTo(start);s.Link.FinishMinecartMount(start,3,0);
                for(int tick=0;tick<100&&!s.Scrolling;tick++)app.Step(1,Vector2.Zero,batched:batched);
                FailIf(!s.Scrolling || s.RoomId!=0x0e || entities.Entities<BoomerangMoblinCharacter>().Count!=2 ||
                    entities.Entities<BladeTrapCharacter>().Count!=2 || entities.RoomEnemyCount!=2 ||
                    entities.EntityAdapters<PushBlockTriggerRoomEntity>().Count()!=1,
                    $"$4:0e arrival must construct two counted Moblins, two uncounted blue traps and the pending push-block sentinel: room${s.RoomId:x2}, scroll{s.Scrolling}, count{entities.RoomEnemyCount}, Link{s.Link.Position}.");
                var incoming=entities.Entities<EnemyCharacter>().ToArray();var positions=incoming.Select(enemy=>enemy.Position).ToArray();
                app.Step(8,Vector2.Zero,batched:batched);
                FailIf(!s.Scrolling || !incoming.Select(enemy=>enemy.Position).SequenceEqual(positions),
                    "$4:0e hostiles must remain frozen during the minecart scroll.");
                for(int tick=0;tick<240&&(s.Scrolling || s.Link.MinecartRideActive || s.Link.MinecartJumpActive);tick++)
                    app.Step(1,Vector2.Zero,batched:batched);
                app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.RoomId!=0x0e || s.Scrolling || s.Link.MinecartRideActive || s.Link.MinecartJumpActive ||
                    entities.Entities<MinecartInteractionRoomEntity>().Single().Position!=new Vector2(168,136) ||
                    s.Room.GetPackedPosition(s.Link.Position)!=0x89 || s.Collision.Collides(s.Link.Position) || s.Room.Layout[0x50]==0xa0 || entities.RoomEnemyCount!=3,
                    $"The original $89 platform must land Link on floor and restore a stationary cart at $8a: Link{s.Link.Position}, ride{s.Link.MinecartRideActive}, jump{s.Link.MinecartJumpActive}.");
                s.Memory.SetField("wDisabledObjects",0);
                bool countedDeath=false;
                void Observe()
                {
                    countedDeath|=entities.Entities<EnemyDeathPuffEffect>().Count>0;
                    FailIf(entities.RoomEnemyCount>1 && s.Room.Layout[0x54]==0x1c,
                        "The source push-block sentinel must keep $54 locked until both Moblins and their destruction PARTs retire.");
                    FailIf(entities.RoomEnemyCount>0 && s.Room.Layout[0x50]==0xa0,
                        "The west shutter must wait for both Moblins and the block puzzle.");
                }
                // Isolate Sword/death/count ownership on original floor;
                // each enemy's motion and projectile have native coverage.
                for(int swing=0;swing<4&&entities.Entities<BoomerangMoblinCharacter>().Any(enemy=>!enemy.IsDead);swing++)
                {
                    s.Link.WarpTo(new(88,136));s.Link.Face(Vector2I.Up);
                    foreach(var enemy in entities.Entities<BoomerangMoblinCharacter>().Where(enemy=>!enemy.IsDead))enemy.Position=new(88,120);
                    FailIf(s.Collision.Collides(s.Link.Position),"The $4:0e Sword fixture must use original floor.");
                    app.Step(2,Vector2.Zero,batched:batched);
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,afterUpdate:Observe);
                    app.Step(32,Vector2.Zero,batched:batched,afterUpdate:Observe);
                }
                for(int tick=0;tick<100&&entities.RoomEnemyCount>1;tick++)app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Observe);
                FailIf(!countedDeath || entities.RoomEnemyCount!=1 || entities.Entities<BladeTrapCharacter>().Count!=2 ||
                    s.Room.Layout[0x54]!=0x1c || s.Room.Layout[0x50]==0xa0,
                    "Defeating the two Moblins must leave the uncounted traps, restore block$54 and retain the closed west shutter.");
                // Trap motion is compared separately; freeze that category
                // while proving the block -> sentinel -> shutter handoff.
                s.Memory.SetField("wDisabledObjects",4);
                s.Link.WarpTo(new(56,104));
                for(int tick=0;tick<24&&s.Link.Position.Y>88;tick++)app.Step(1,Vector2.Up,batched:batched);
                FailIf(s.Link.Position.Y!=88 || s.Collision.Collides(s.Link.Position),
                    $"The original block must be approached from western floor: Link{s.Link.Position}.");
                for(int tick=0;tick<100&&s.Room.Layout[0x54]==0x1c;tick++)app.Step(1,Vector2.Right,batched:batched);
                FailIf(s.Room.Layout[0x54]==0x1c,"A real eastward push must move the unlocked $54 block.");
                int update=0;
                app.Step(30,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    update++;FailIf(entities.RoomEnemyCount!=(update<30?1:0),"The trap-room block must release its sentinel after exactly $1e updates.");
                });
                app.Step(80,Vector2.Zero,batched:batched);
                FailIf(s.Room.Layout[0x50]!=0xa0,"The completed $4:0e block puzzle must open its west shutter.");
                for(int tick=0;tick<100&&!s.Scrolling;tick++)app.Step(1,Vector2.Left,batched:batched);
                FailIf(!s.Scrolling || s.RoomId!=0x0d,"The solved west shutter must admit the optional original $4:0d Gasha chest room.");
                for(int tick=0;tick<120&&s.Scrolling;tick++)app.Step(1,Vector2.Zero,batched:batched);
                for(int tick=0;tick<60&&!s.Scrolling;tick++)app.Step(1,Vector2.Right,batched:batched);
                FailIf(!s.Scrolling || s.RoomId!=0x0e,"The optional chest room must return through its real east passage.");
                for(int tick=0;tick<120&&s.Scrolling;tick++)app.Step(1,Vector2.Zero,batched:batched);
                s.Memory.SetField("wDisabledObjects",4);
                for(int tick=0;tick<64&&s.Link.Position.X<56;tick++)app.Step(1,Vector2.Right,batched:batched);
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf(entities.RoomEnemyCount!=1 || entities.Entities<BoomerangMoblinCharacter>().Count!=0 ||
                    entities.Entities<BladeTrapCharacter>().Count!=2 || s.Room.Layout[0x54]!=0x1c || s.Room.Layout[0x50]==0xa0,
                    "Re-entry must retain the recent Moblin defeats, respawn uncounted traps and reset the block/shutter puzzle.");
                for(int tick=0;tick<100&&s.Room.Layout[0x54]==0x1c;tick++)app.Step(1,Vector2.Right,batched:batched);
                FailIf(s.Room.Layout[0x54]==0x1c,$"The source block must admit another real push after re-entry: Link{s.Link.Position}, count{entities.RoomEnemyCount}, collision{s.Collision.Collides(s.Link.Position)}.");
                app.Step(110,Vector2.Zero,batched:batched);
                FailIf(entities.RoomEnemyCount!=0 || s.Room.Layout[0x50]!=0xa0,
                    "Repeating the source block action must release the sentinel and open the shutter again.");
            }
            finally {root.Free();}
        }
    }
}
