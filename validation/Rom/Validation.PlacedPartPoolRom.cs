using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePlacedPartPoolRom()
    {
        foreach (var test in new[] { (Group: 3, Room: 0x4f, Id: 0x28),
            (Group: 5, Room: 0x01, Id: 0x23), (Group: 5, Room: 0xc9, Id: 0x28),
            (Group: 7, Room: 0x01, Id: 0x23) })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(test.Group, test.Room);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 72, 72);
            var frontend = SomariaPrivate<FrontendRom>(native, "_rom");
            // Admit this room's real enemy pointer through the skip mask;
            // execute objectDataOp8 and its original allocator unchanged.
            native[0xcc05] = 2;
            native[0xcd00] = 1;
            frontend.Call(0x55b7, 0x12);
            int NativeCount() => Enumerable.Range(0, 16).Count(slot => native[0xd0c0 + slot * 0x100] != 0);
            int RuntimeCount() => Enumerable.Range(0, 16).Count(slot => _entities.ResolvePartSlot(slot) is not null);
            FailIf(NativeCount() != 2 || Enumerable.Range(0, 2).Any(slot => native[0xd0c1 + slot * 0x100] != test.Id),
                $"Clean-US room ${test.Group:x1}:${test.Room:x2} must parse two PART${test.Id:x2} placements.");
            FailIf(RuntimeCount() != NativeCount(),
                $"Room ${test.Group:x1}:${test.Room:x2}: placed PART${test.Id:x2} must occupy its native physical pool slots.");
            int nativeAllocations = 0;
            for (int attempt = 0; attempt < 17; attempt++)
            {
                int before = NativeCount();
                frontend.Call(0x3e8e, 0); // getFreePartSlot reserves enabled=$01.
                if (NativeCount() == before) break;
                nativeAllocations++;
            }
            int runtimeAllocations = 0;
            while (runtimeAllocations < 17 && _entities.TryCreatePart(new ZoraFireSpawn(new(72, 72))))
                runtimeAllocations++;
            FailIf(nativeAllocations != 14 || runtimeAllocations != nativeAllocations || _entities.PartSlotAvailable,
                $"Room ${test.Group:x1}:${test.Room:x2}: placed PARTs must leave exactly $0e dynamic allocations.");
            LoadValidationRoom(test.Group, test.Room);
            FailIf(RuntimeCount() != 2,
                "Room re-entry must discard previous dynamic PARTs and restore exactly the two source placements.");
        }
        ComparePlacedPartBehaviorRom();
        CompareSmallFairyCollectionRom();
        CompareFallingFireContactRom();
        CompareFallingFireKeeseAliasRom();
        GD.Print("Validated clean-US placed PART$23/$28 source allocation, full shared-pool admission and repeated room entry.");
    }
}
