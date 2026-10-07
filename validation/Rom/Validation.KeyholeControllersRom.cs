using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private KeyholeControllerRoomEntity RetainKeyholeControllerRom(int expectedSlot = 2, bool retainCompanionPlacements = false)
    {
        // Keep the actual production allocation; remove unrelated actors from
        // this focused fixture through the manager's normal release operation.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var entities = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!;
        var release = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
        var controller = entities.OfType<KeyholeControllerRoomEntity>().Single();
        foreach (var entity in entities.Where(entity => !ReferenceEquals(entity,controller) &&
            (!retainCompanionPlacements || entity is not CompanionBarrierRoomEntity and not CompanionTutorialRoomEntity)).ToArray())
        {
            entities.Remove(entity);
            release.Invoke(_entities,[entity]);
        }
        FailIf(_entities.InteractionSlot(controller.Node) != expectedSlot,
            $"mainData.s keyhole controller must retain original INTERACTION${0xd0+expectedSlot:x2}.");
        return controller;
    }
}
