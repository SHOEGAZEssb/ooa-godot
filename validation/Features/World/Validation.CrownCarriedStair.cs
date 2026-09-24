using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownCarriedStair()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool throwItem in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            _entities.Clear();
            _inventory.GiveTreasure(InventoryState.ItemSomaria, 1);
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
            _inventory.EquipA(InventoryState.ItemSomaria);
            _player.WarpTo(new(120, 56));
            _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Crown carry approach must start on actual floor.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            StepGameplayUpdates(24, Vector2.Zero, batched: batched);
            var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            _inventory.EquipA(InventoryState.ItemBracelet);
            StepGameplayUpdates(8, Vector2.Up, batched: batched);
            FailIf(_collision.Collides(_player.Position), "Bracelet approach must remain outside the block's solid tile.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            StepGameplayUpdates(30, Vector2.Zero, ["attack"], batched: batched);
            FailIf(!block.IsHeld || !_player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled,
                "Normal Cane and Bracelet input must produce a fully lifted block.");
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave);
            for (int i = 0; _player.Position.Y > 25 && !IsTransitioning && i < 80; i++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(IsTransitioning || _player.Position != new Vector2(120, 25) || !block.IsHeld,
                "checkTileWarps must reject the actual Crown stair while wLinkGrabState is carrying $83.");
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(IsTransitioning || !block.IsHeld ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds,
                "The carried block must remain held without a stair sound or transition.");
            if (throwItem)
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            else
            {
                _entities.ClearPhysicalPlayerItems();
                FailIf(_player.IsCarryingObject || block.IsHeld,
                    "Physical item clearing must release the holder before the next warp check.");
                StepGameplayUpdates(1, Vector2.Zero);
            }
            FailIf(!IsTransitioning || _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds + 1,
                $"The unchanged stair must activate on the {(throwItem ? "throw" : "post-cancellation")} update.");
        }
        ReinitializeGameplayForValidation();
    }
}
