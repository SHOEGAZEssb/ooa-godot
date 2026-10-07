using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidDungeonAdmission()
    {
        ReinitializeGameplayForValidation();
        var native = new MermaidDungeonDatabase();
        var enemies = new EnemyDatabase();
        int nativeCount=0;
        // data/ages/dungeonLayouts.s: the two Mermaid maps contain the
        // contiguous 25/30 source rooms$510-$528 and$529-$546.
        for (int room=0x10; room<=0x46; room++)
        {
            LoadValidationRoom(5,room);
            FailIf(_rooms.CurrentDungeonIndex!=(room<=0x28 ? 6 : 0x0c),
                $"Mermaid room$5:${room:x2} must retain its source era's dungeon index.");
            nativeCount+=native.GetRoomRecords(5,room).Count;
            foreach (var source in enemies.GetRoomObjects(5,room))
            {
                var resolution=enemies.EnemyHandlers.Resolve(source);
                FailIf(resolution.Handler is {SupportsOrderedConstruction:false},
                    $"{source.Source}: Mermaid placement ENEMY${source.Id:x2}:${source.SubId:x2} has no implemented handler.");
            }
            if (room is 0x10 or 0x11 or 0x29 or 0x2a)
                FailIf(_rooms.ActiveGroup!=7,
                    $"Mermaid side-view source room$5:${room:x2} must admit active group$7.");
        }
        FailIf(nativeCount!=28,"Mermaid mainData placements require28 native records beyond the shared dungeon owners.");
        LoadValidationRoom(5,0x26);
        var entrance=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
        FailIf(entrance.Count(actor=>actor is DungeonEntranceRoomEntity)!=1 ||
            entrance.Count(actor=>actor is StatueEyeballSpawnerRoomEntity)!=1 ||
            entrance.Count(actor=>actor is MinibossPortalRoomEntity)!=1,
            "The requested start$5:$26 must admit its source entrance, eye spawner and Vire return portal.");
        GD.Print("Validated admission of all55 source Mermaid rooms, both era indices, four side-view aliases, implemented enemy handlers and entrance$5:$26.");
    }
}
