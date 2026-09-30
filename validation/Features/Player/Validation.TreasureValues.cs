using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTreasureRupeeValues()
    {
        int[] expected = [0, 1, 2, 5, 10, 20, 40, 30, 60, 70, 25, 50, 100, 200, 400, 150, 300, 500, 900, 80, 999];
        for (int parameter = 0; parameter <= 255; parameter++)
        {
            var save = OracleSaveData.CreateStandardGame();
            var inventory = new InventoryState(_treasures, save);
            inventory.GiveTreasure(0x28, parameter);
            int amount = expected[System.Math.Min(parameter, 0x14)];
            FailIf(inventory.Rupees != amount || inventory.TotalRupeesCollected != amount,
                $"bank0.getRupeeValue parameter ${parameter:x2} lost its native value/clamp.");
        }
        GD.Print("Validated all 21 source rupee values and parameter-$14 clamp without ROM input.");
    }
}
