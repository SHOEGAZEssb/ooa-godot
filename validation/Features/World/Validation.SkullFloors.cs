using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonFloors()
    {
        CompareColoredFloorChildrenRom();
        CompareColoredFloorFeatherRom();
        CompareFloorColorWorkersRom();
        CompareColoredFloorScrollRom();
        var data = new SkullDungeonDatabase();
        var records = data.GetRoomRecords(4,0x71);
        FailIf(records.Count != 2 || records[0] is not { Order:2,Id:InteractionId.FloorColorChanger,SubId:0,X:0x78,Y:0x58 } ||
            records[1] is not { Order:3,Id:InteractionId.ToggleFloor,SubId:0 },"Room$04:$71 lost its source floor-controller order/coordinates.");
        foreach (var (room,order) in new[] { (0x72,0),(0x79,1),(0x7b,1) })
        {
            FailIf(data.GetRoomRecords(4,room).Single(row => row.Id == 0x15) is not { SubId:0 } record || record.Order != order,
                $"Room$04:${room:x2} lost its source toggle-floor controller.");
            LoadValidationRoom(4,room);
            FailIf(_entities.Entities<ToggleFloorRoomEntity>().Count != 1,
                $"Room$04:${room:x2} did not instantiate its physical floor controller.");
        }
    }
}
