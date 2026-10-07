using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonRails()
    {
        var data = new SkullDungeonDatabase();
        var railData = new DungeonInteractionDatabase();
        // Independent switchTileToggler.s / mainData.s / replaceSwitchTiles
        // goldens; the native comparisons use the actual unchanged geometry.
        FailIf(railData.SwitchTiles(0x0b) != (0x5c, 0x5d) || railData.SwitchTiles(0x0c) != (0x59, 0x5e) ||
            data.GetRoomRecords(4, 0x89) is not [{ Id: InteractionId.SwitchTileToggler, SubId: 4, Y: 0x67, X: 0x0b, Order: 0 }] ||
            data.GetRoomRecords(4, 0x8f) is not [{ Id: InteractionId.SwitchTileToggler, SubId: 8, Y: 0x52, X: 0x0c, Order: 0 }],
            "Skull rail junctions lost source placement, switch masks, or replacement table rows.");
        CompareSkullRailJunctionRom();
        CompareMinecartScrollRom();
        ReinitializeGameplayForValidation();
        GD.Print("Validated Skull rail import goldens, both original junction states and four native cart/shutter/scroll handoffs; shared track and repeated mounting/dismounting have separate ROM coverage.");
    }
}
