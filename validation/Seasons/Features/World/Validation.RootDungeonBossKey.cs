namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonBossKey()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                // Satchel collection/portal return have separate physical-route coverage.
                s.Inventory.GiveTreasure(TreasureId.SeedSatchel,1);s.Inventory.GiveTreasure(TreasureId.Sword,1);
                s.Inventory.GiveTreasure(TreasureId.EmberSeeds,8);s.Inventory.SelectSatchelSeeds(0);
                s.Inventory.EquipA(TreasureId.SeedSatchel);s.Inventory.EquipB(0);
                s.DebugTravel(4,0x15);s.Memory.SetField("wDisabledObjects",4);s.Link.WarpTo(new(88,88));
                entities.DisableLinkCollisionsAndMenu();
                app.Step(3,Vector2.Zero,batched:batched);
                FailIf(!entities.Entities<LightableTorchRoomEntity>().Select(torch=>torch.PackedPosition).Order().SequenceEqual(new[]{0x41,0x61})||
                    s.Room.Layout[0x50]!=0x7b||entities.RoomEnemyCount!=4,
                    "The source $4:15 west gate must start closed with two unlit torches and four counted Zols.");
                for(int tick=0;tick<100&&s.Link.Position.X>24;tick++)app.Step(1,Vector2.Left,batched:batched);
                FailIf(s.Link.Position.X!=24||s.Collision.Collides(s.Link.Position),
                    $"The torch pair must be approached through the original west corridor: Link{s.Link.Position}.");
                foreach(var direction in new[]{Vector2I.Up,Vector2I.Down})
                {
                    s.Link.Face(direction);app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                    app.Step(24,Vector2.Zero,batched:batched);
                }
                app.Step(80,Vector2.Zero,batched:batched);
                FailIf(s.Room.Layout[0x41]!=9||s.Room.Layout[0x61]!=9||s.Room.Layout[0x50]!=0xa0||entities.RoomEnemyCount!=4,
                    $"Two actual Satchel uses must light both source torches and open the west gate while counted Zols remain alive: Link{s.Link.Position}, torches${s.Room.Layout[0x41]:x2}/${s.Room.Layout[0x61]:x2}, gate${s.Room.Layout[0x50]:x2}, count{entities.RoomEnemyCount}, seeds${s.Inventory.EmberSeeds:x2}.");
                for(int tick=0;tick<100&&!s.Scrolling;tick++)app.Step(1,Vector2.Left,batched:batched);
                FailIf(!s.Scrolling||s.RoomId!=0x14||entities.Entities<BoomerangMoblinCharacter>().Count!=3||entities.RoomEnemyCount!=3,
                    "The torch gate must enter $4:14's original ordered random stream of three counted Moblins.");
                for(int tick=0;tick<240&&s.Scrolling;tick++)app.Step(1,Vector2.Zero,batched:batched);
                entities.EnableLinkCollisionsAndMenu();s.Memory.SetField("wDisabledObjects",0);s.Inventory.EquipA(TreasureId.Sword);
                bool sawPuff=false;
                void Observe()
                {
                    sawPuff|=entities.Entities<EnemyDeathPuffEffect>().Count!=0;
                    FailIf(entities.RoomEnemyCount!=0&&s.Room.Layout[0x57]==0xf1,
                        "The boss-key chest must wait for the counted Moblins and their destruction PARTs.");
                }
                // Moblin AI has native coverage; isolate the Sword/count/chest
                // handoff on the unchanged southern room floor.
                for(int swing=0;swing<3&&entities.Entities<BoomerangMoblinCharacter>().Any(actor=>!actor.IsDead);swing++)
                {
                    s.Link.WarpTo(new(120,136));s.Link.Face(Vector2I.Up);
                    foreach(var actor in entities.Entities<BoomerangMoblinCharacter>().Where(actor=>!actor.IsDead))actor.Position=new(120,120);
                    app.Step(2,Vector2.Zero,batched:batched);
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,afterUpdate:Observe);
                    app.Step(32,Vector2.Zero,batched:batched,afterUpdate:Observe);
                }
                for(int tick=0;tick<120&&s.Room.Layout[0x57]!=0xf1;tick++)app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Observe);
                FailIf(!sawPuff||entities.RoomEnemyCount!=0||s.Room.Layout[0x57]!=0xf1,
                    "Defeating all three source Moblins must reveal the boss-key chest at $57.");
                app.Step(40,Vector2.Up,batched:batched);
                FailIf(s.Collision.Collides(s.Link.Position),"The boss-key chest must be approached through original southern floor.");
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf(!s.Inventory.HasDungeonBossKey(1)||s.Inventory.HasDungeonBossKey(0)||s.Inventory.HasDungeonBossKey(2)||
                    !s.Save.HasRoomFlag(4,0x14,0x20)||s.Room.Layout[0x57]!=0xf0,
                    "The actual BOSS_KEY_03 chest must grant only dungeon$01's boss key and ROOMFLAG_ITEM.");
                s.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched);
                s.DebugTravel(4,0x15);s.DebugTravel(4,0x14);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.Room.Layout[0x57]!=0xf0||entities.Entities<EnemyClearChestRoomEntity>().Count!=0||!s.Inventory.HasDungeonBossKey(1),
                    "Collected-room re-entry must retain the opened boss-key chest and retire its watcher.");
            }
            finally {root.Free();}
        }
    }
}
