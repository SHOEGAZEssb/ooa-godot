using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMapleDropShadows()
    {
        var data = new MapleEventDatabase();
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(216, 152));
            FillValidationRoom(0xa0, width: 15, height: 11);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.Clear();
                // Occupy native part slot 0: encounter item indices 0/1 must
                // use actual pages $d1/$d2, not their encounter-local indices.
                _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x02, new(24, 40)));
                var encounter = new MapleEncounterState(_runtimeState) { ObjectsDisabled = true };
                var drops = new[]
                {
                    _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(
                        data.Item(12), encounter, 0, new(80, 64), -8 * 256)),
                    _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(
                        data.Item(13), encounter, 0, new(112, 64), 0))
                };
                FailIf(drops.Any(drop => drop.TerrainShadowDrawn),
                    "PART$14/$15 must not cast shadows before state0 enables visiblec3.");
                bool sawAir = false;
                void CheckShadows()
                {
                    for (int index = 0; index < drops.Length; index++)
                    {
                        MapleDroppedItem drop = drops[index];
                        bool air = drop.ZFixed < 0;
                        sawAir |= air;
                        bool expected = air && ((_entities.FrameCounter ^ (index + 1)) & 1) != 0;
                        FailIf(drop.TerrainShadowDrawn != expected,
                            $"Maple item ${drop.ItemIndex:x2} lost native page ${0xd1 + index:x2} shadow phase in {drop.State}.");
                        FailIf(drop.GetChildren().OfType<ObjectTerrainShadow>().Count() != 1,
                            "Maple drops must use the shared terrain-shadow renderer.");
                    }
                }
                for (int update = 0; update < 180; update += batched ? 2 : 1)
                {
                    StepGameplayUpdates(batched ? 2 : 1, Vector2.Zero, batched: batched);
                    CheckShadows();
                }
                FailIf(!sawAir || drops.Any(drop => drop.State != MapleDroppedItemState.Grounded || drop.TerrainShadowDrawn),
                    "Maple drops must bounce and stop casting shadows when grounded.");
                foreach (MapleDroppedItem drop in drops)
                    drop.BeginMapleCollection(1, () => drop.Position);
                for (int update = 0; update < 44; update += batched ? 2 : 1)
                {
                    StepGameplayUpdates(batched ? 2 : 1, Vector2.Zero, batched: batched);
                    CheckShadows();
                }
                FailIf(drops.Any(drop => !drop.MapleCollectionReady),
                    "Maple state4 must retain terrain effects while raising items for collection.");
                foreach (MapleDroppedItem drop in drops)
                {
                    int z = drop.ZFixed;
                    drop.ReleaseFromMaple();
                    FailIf(drop.ZFixed != z || drop.State != MapleDroppedItemState.Bouncing,
                        "Bomb interruption retains item height and resumes source state1 bouncing.");
                }
                CheckShadows();
                for (int update = 0; update < 120; update += batched ? 2 : 1)
                {
                    StepGameplayUpdates(batched ? 2 : 1, Vector2.Zero, batched: batched);
                    CheckShadows();
                }
                FailIf(drops.Any(drop => drop.State != MapleDroppedItemState.Grounded || drop.TerrainShadowDrawn),
                    "Released items stop casting shadows only after the retained vertical motion lands.");
                foreach (MapleDroppedItem drop in drops)
                    drop.BeginMapleCollection(1, () => drop.Position);
                StepGameplayUpdates(44, Vector2.Zero, batched: batched);
                FailIf(drops.Any(drop => !drop.MapleCollectionReady), "Released drops must remain collectible on a later Maple approach.");
                foreach (MapleDroppedItem drop in drops)
                {
                    drop.CompleteMapleCollection();
                    FailIf(drop.Finished, "A companion's retirement request must retain the drop until its later PART visit.");
                }
                CheckShadows();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.Entities<MapleDroppedItem>().Count != 0,
                    "Maple collection must release shadow nodes and native slots before another scatter.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
