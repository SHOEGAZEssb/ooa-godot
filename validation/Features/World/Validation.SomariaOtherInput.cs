using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaOtherInput()
    {
        // Usage selectors: Shield$01=5, Boomerang$06=2, Bracelet$16=3
        // (priority$10), Cane$04=3 (priority$00). Shield retains its slot but
        // checkNoOtherParentItemsInUse prevents raising it during Cane's swing.
        foreach (int other in new[] { InventoryState.ItemShield, InventoryState.ItemBoomerang,
            InventoryState.ItemBracelet })
        foreach (bool canePrimary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(InventoryState.ItemSomaria, 1);
            _inventory.GiveTreasure(other, 1);
            _inventory.EquipA(canePrimary ? InventoryState.ItemSomaria : other);
            _inventory.EquipB(canePrimary ? other : InventoryState.ItemSomaria);
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            string caneButton = canePrimary ? "attack" : "item";
            string otherButton = canePrimary ? "item" : "attack";
            void HoldOther(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, [otherButton], [], batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.ClearPhysicalPlayerItems();
                _player.WarpTo(new(72, 72));
                _player.Face(Vector2I.Down);
                StepGameplayUpdates(1, Vector2.Zero);
                StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"]);
                if (other == InventoryState.ItemBracelet)
                {
                    FailIf(_player.IsUsingSomaria || _bracelet.State != BraceletState.SeekingWall,
                        "Bracelet priority$10 must win over Cane$00 even without a wall to grab.");
                    HoldOther(3);
                    StepGameplayUpdates(1, Vector2.Zero, [otherButton, caneButton], [caneButton]);
                    FailIf(_player.IsUsingSomaria || _bracelet.State != BraceletState.SeekingWall,
                        "A held, empty-handed Bracelet parent must reject later Cane input.");
                    HoldOther();
                    StepGameplayUpdates(1, Vector2.Zero, [caneButton], [caneButton]);
                    FailIf(_player.IsUsingSomaria || _bracelet.State != BraceletState.Idle,
                        "Releasing Bracelet clears it after input priority; Cane cannot claim its slot until a later press.");
                    StepGameplayUpdates(1, Vector2.Zero);
                    StepGameplayUpdates(1, Vector2.Zero, [caneButton], [caneButton]);
                    FailIf(!_player.IsUsingSomaria, "Fresh Cane input must work after Bracelet releases its slot.");
                    StepGameplayUpdates(23, Vector2.Zero, batched: batched);
                }
                else
                {
                    FailIf(!_player.IsUsingSomaria || _player.IsUsingShield ||
                        _player.IsUsingBoomerang != (other == InventoryState.ItemBoomerang),
                        "Cane must coexist with Boomerang and suppress the held Shield in either button assignment.");
                    HoldOther(9);
                    FailIf(!_player.IsUsingSomaria || _player.IsUsingBoomerang || _player.IsUsingShield,
                        "Boomerang parent completion must not release Cane's remaining swing lock.");
                    HoldOther(9);
                    FailIf(_player.IsUsingSomaria || _player.IsUsingShield != (other == InventoryState.ItemShield),
                        "Cane parent completion must allow an already-held Shield to rise without a fresh press.");
                    HoldOther(5);
                }
                FailIf(_entities.Entities<SomariaBlock>().Count() != 1,
                    "A completed Cane use must leave exactly one block after the other input resolves.");
            }
        }
    }
}
