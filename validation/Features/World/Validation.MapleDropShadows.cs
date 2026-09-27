using Godot;
using System.Linq;

namespace oracleofages;

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
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.Clear();
                // Occupy native part slot 0: encounter item indices 0/1 must
                // use actual pages $d1/$d2, not their encounter-local indices.
                _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x02, new(24, 40)));
                var encounter = new MapleEncounterState { ObjectsDisabled = true };
                var drops = new[]
                {
                    _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(
                        data.Item(12), encounter, encounter.AllocateSlot(), new(80, 64), -8 * 256)),
                    _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(
                        data.Item(13), encounter, encounter.AllocateSlot(), new(112, 64), 0))
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
                    drop.ReleaseFromMaple();
                    FailIf(drop.TerrainShadowDrawn, "Cancelled Maple collection must return its shadow to ground height.");
                    drop.CompleteMapleCollection();
                    FailIf(drop.TerrainShadowDrawn, "Deleted Maple items must stop casting shadows immediately.");
                }
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.Entities<MapleDroppedItem>().Count != 0,
                    "Maple collection must release shadow nodes and native slots before another scatter.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
