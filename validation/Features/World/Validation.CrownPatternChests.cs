using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownPatternChests()
    {
        var data = new DungeonChestPatternDatabase();
        // Independent original dungeonEvents.s tables; native gameplay below
        // checks every accepted range, rejected boundary and reward side effect.
        foreach (var test in new[] {
            (Sub:0x13, Positions:new[] { 0x47,0x48,0x49,0x57,0x59,0x67,0x68,0x69 },
                Tiles:new[] { 0x2c,0x2d,0x2e,0x2c,0x2d,0x2e,0x2c,0x2e }),
            (Sub:0x14, Positions:new[] { 0x45,0x49 }, Tiles:new[] { 0x2a,0x2a }),
            (Sub:0x15, Positions:new[] { 0x54,0x62,0x33,0x52,0x44,0x73 },
                Tiles:new[] { 0x2c,0x2c,0x2d,0x2d,0x2e,0x2e }) })
        {
            var cells = data.Cells(test.Sub);
            FailIf(cells.Count != test.Positions.Length, "Crown pattern lost its source cell count.");
            for (int index = 0; index < cells.Count; index++)
            {
                int minimum = test.Sub == 0x13 ? 0x2c : test.Tiles[index];
                int maximum = test.Sub == 0x13 ? 0x2e : test.Tiles[index];
                FailIf(cells[index].Position != test.Positions[index] ||
                    cells[index].MinimumTile != minimum || cells[index].MaximumTile != maximum,
                    $"INTERAC$21:${test.Sub:x2} cell{index} must retain original table order and tile condition.");
            }
        }
        CompareCrownPatternChestRom();
        LoadValidationRoom(0, 0x60);
    }
}
