namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonHandQuota()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x17);s.Link.WarpTo(new(120,88));
                s.Inventory.GiveTreasure(TreasureId.Sword,1);s.Inventory.EquipA(TreasureId.Sword);s.Inventory.EquipB(0);
                // Chase/grab has its own native regression. Keep contact off
                // while isolating actual Sword -> child death -> parent quota.
                entities.DisableLinkCollisionsAndMenu();
                var parent=entities.Entities<FloormasterCharacter>().Single();
                bool sawPuff=false;int swings=0;
                void Observe()
                {
                    sawPuff|=entities.Entities<EnemyDeathPuffEffect>().Count!=0;
                    FailIf(parent.RemainingChildren>0&&entities.RoomEnemyCount!=1,
                        "Uncounted hand destruction must retain its counted controller until the four-kill quota is exhausted.");
                }
                for(int tick=0;tick<2000&&entities.Entities<FloormasterCharacter>().Contains(parent);tick++)
                {
                    var hand=entities.Entities<FloormasterCharacter>().FirstOrDefault(actor=>actor.Record.SubId!=0&&
                        actor.State==11&&actor.InvincibilityCounter==0&&actor.Health>0);
                    if(hand is null){app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Observe);continue;}
                    s.Link.WarpTo(new(120,88));s.Link.Face(Vector2I.Up);
                    foreach(var other in entities.Entities<FloormasterCharacter>().Where(actor=>actor.Record.SubId!=0&&actor!=hand))
                        other.Position=new(200,136);
                    hand.Position=new(120,72);
                    int health=hand.Health;
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,afterUpdate:Observe);
                    app.Step(32,Vector2.Zero,batched:batched,afterUpdate:Observe);swings++;
                    FailIf(!hand.IsDead&&hand.Health>=health,
                        $"The actual level1 Sword must damage the active hand on original floor: HP{health}->{hand.Health}, state{hand.State}, Z{hand.ZFixed}.");
                }
                app.Step(90,Vector2.Zero,batched:batched,afterUpdate:Observe);
                FailIf(!sawPuff||swings!=12||parent.RemainingChildren!=0||entities.Entities<FloormasterCharacter>().Count!=0||
                    entities.RoomEnemyCount!=0||entities.ActiveRoomDefeatBitset!=2,
                    $"Four five-HP hands must take twelve level1 Sword swings, exhaust the quota and silently defeat only the source controller: swings{swings}, quota{parent.RemainingChildren}, count{entities.RoomEnemyCount}, defeats${entities.ActiveRoomDefeatBitset:x2}.");
                s.DebugTravel(4,0x1b);s.DebugTravel(4,0x17);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(entities.Entities<FloormasterCharacter>().Count!=0||entities.RoomEnemyCount!=0,
                    "The defeated counted controller must remain suppressed on immediate room re-entry.");
            }
            finally {root.Free();}
        }
    }
}
