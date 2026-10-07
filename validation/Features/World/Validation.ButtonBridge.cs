using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateButtonBridge()
    {
        var data = new CrownDungeonDatabase();
        var rule = data.ButtonBridge;
        var placement = data.GetRoomRecords(4,0xad).Single(row => row.Kind == DungeonObjectKind.ButtonBridge);
        FailIf(placement.Order != 1 || placement.Id != 0x90 || placement.SubId != 0x19 ||
            rule.First != 0x55 || rule.Last != 0x59 || rule.Interval != 8 ||
            rule.Hole != 0xf4 || rule.Bridge != 0x6d || rule.Diamond != 0xdb,
            "INTERAC$90:$19 lost source placement, scan limits, interval or tile IDs.");
        CompareButtonBridgeRom();
        ReinitializeGameplayForValidation();
    }
}
