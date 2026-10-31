using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedParentItemUsage()
    {
        // Independent data/{game}/itemUsageTables.s values. Only NONE, SHIELD
        // and BRACELET read held input; every other native row reads its edge.
        byte[] seasons = [0x00,0x05,0x03,0x23,0x00,0x63,0x02,0x33,
            0x53,0x00,0x00,0x00,0x73,0x02,0x05,0x00,0x00,0x00,
            0x00,0x43,0x00,0x13,0x13,0x01,0x00,0x02,0x00,0x00,
            0x00,0x00,0x03,0x00];
        Span<ParentItemSlotState> slots = stackalloc ParentItemSlotState[4];
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            byte[] expected = (byte[])seasons.Clone();
            if (game == OracleGame.Ages)
            {
                expected[4] = 0x03; expected[7] = 0; expected[8] = 0; expected[0x0a] = 0x73;
                expected[0x0f] = 0x43; expected[0x11] = 5; expected[0x13] = 0; expected[0x1e] = 0;
            }
            string root = OracleAssetPack.ForGame(game);
            var data = new ParentItemUsageDatabase(root);
            for (int item = 0; item < 32; item++)
                FailIf(data.Item(item).Usage != expected[item] || data.Item(item).JustPressed != (item is not (0 or 1 or 0x16)),
                    $"{game} ITEM${item:x2}: native allocation priority/selector/input binding differs from source.");
            slots.Clear();
            slots[0] = new(0x61, 5);
            FailIf(data.ChooseSlot(5, slots) != 2 || data.ChooseSlot(0x0c, slots) != 2,
                "Native parent allocation must permit equal-priority replacement and a stronger Biggoron Sword.");
            if (game == OracleGame.Seasons)
                FailIf(data.ChooseSlot(7, slots) != -1 || data.ChooseSlot(8, slots) != -1 || data.ChooseSlot(0x13, slots) != -1,
                    "Seasons Rod, Magnet Gloves and Slingshot must lose arbitration to the occupied Sword parent slot.");
            slots.Clear(); slots[1] = new(0, 6);
            FailIf(data.ChooseSlot(6, slots) != -1,
                "The shared chooseParentItemSlot selector $02 compares retained IDs even in a disabled parent.");
            slots[1] = default;
            FailIf(data.ChooseSlot(6, slots) != 3 || data.ChooseSlot(1, slots) != 5,
                "Native Boomerang and Shield allocation must use the original physical parent-slot order.");
        }
    }
}
