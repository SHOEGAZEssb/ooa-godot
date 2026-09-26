using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBoomerangDrops()
    {
        FailIf(!BoomerangCollisionDatabase.Shared.PartEnabled(1) || BoomerangCollisionDatabase.Shared.Effect(EnemyCollisionMode.Item) != 0x24,
            "PART$01 mode$01 column$17 must attach via source effect$24.");
        foreach (bool batched in new[] { false, true })
        foreach (int outcome in new[] { 0, 1, 2, 3, 4 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(40, 80));
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.ClearPhysicalPlayerItems();
                var drop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(ItemDropDatabase.OneRupee, new(120.25f, 80.5f)));
                Step(70);
                FailIf(!drop.CollisionEnabled || drop.State != DropState.Grounded || drop.Finished,
                    "Boomerang pickup fixture must let the native drop land away from Link.");
                int counter = drop.Counter;
                Vector2 fraction = drop.PrecisePosition - drop.PrecisePosition.Floor();
                var item = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(drop.Position, ObjectAngle.Right));
                Step();
                FailIf(drop.State != DropState.Grounded || drop.Collected || drop.CanAttachToItem || item.State != 1,
                    "effect$24 publishes a pending attachment and return without collecting or moving the drop.");
                if (outcome == 4)
                {
                    _entities.ClearPhysicalPlayerItems();
                    _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(new(160, 80), ObjectDirection.Right));
                }
                Step();
                FailIf(drop.State != DropState.Attached || drop.Finished || drop.Collected,
                    "PARTSTATUS_JUST_HIT enters state3 on the following eligible part update.");
                if (outcome != 4)
                    FailIf(drop.Position != item.Position || drop.PrecisePosition - drop.Position != fraction ||
                        drop.ZFixed >> 8 != item.ZHigh || item.State != 2,
                        "Attached drop copies carrier high XYZ after item movement and retains its own fractions.");
                else
                    FailIf(drop.Position != _entities.Entities<SwordBeamEffect>().Single().Position,
                        "First state3 update caches the current slot ID, even when replaced before initialization.");
                counter = drop.Counter;
                var text = _entities.TextActiveSource;
                Vector2 frozen = drop.Position;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(5);
                    FailIf(drop.Position != frozen || drop.Counter != counter || drop.Finished,
                        "Dialogue freezes attached drops and their carrier.");
                }
                finally { _entities.TextActiveSource = text; }
                if (outcome is 1 or 2 or 3)
                {
                    _entities.ClearPhysicalPlayerItems();
                    if (outcome == 2)
                        item = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(new(152, 80), ObjectAngle.Left));
                    if (outcome == 3)
                        _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(new(160, 80), ObjectDirection.Right));
                    Step();
                    if (outcome is 1 or 3)
                    {
                        FailIf(!drop.Finished || drop.Collected,
                            "Missing or different-ID carrier deletes an attached drop without granting it.");
                        continue;
                    }
                    FailIf(drop.Finished || drop.Position != item.Position,
                        "Same-ID native slot reuse continues following the replacement carrier.");
                }
                if (outcome == 4)
                {
                    _entities.ClearPhysicalPlayerItems();
                    Step();
                    FailIf(!drop.Finished || drop.Collected, "Replacement carrier deletion must release its attached drop.");
                    continue;
                }
                int rupees = _inventory.Rupees;
                for (int i = 0; i < 160 && !drop.Finished; i++) Step();
                FailIf(!drop.Collected || _inventory.Rupees != rupees + 1,
                    "Returning boomerang must bring its drop into Link pickup range and grant exactly once.");
                Step(8);
                FailIf(_inventory.Rupees != rupees + 1, "Finished attachment must not grant again.");
            }
        }
    }
}
