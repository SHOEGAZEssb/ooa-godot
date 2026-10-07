using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonPlatforms()
    {
        var data = new MovingPlatformDatabase();
        string[] sourcePrograms = [
            "00:08,08:60,00:08,0a:a0,00:08,08:a0,04:02",
            "00:08,08:20,00:08,0a:c0,00:08,08:c0,04:02",
            "00:08,08:40,00:08,0a:a0,00:08,08:a0,04:02",
            "00:08,09:20,00:08,0b:c0,00:08,09:c0,04:02",
            "00:08,0a:60,00:08,08:60,04:00",
            "00:08,0b:20,00:08,09:a0,00:08,0b:a0,04:02" ];
        for (int dungeon = 2; dungeon <= 4; dungeon++)
        for (int index = 0; index < sourcePrograms.Length; index++)
            FailIf(string.Join(',', data.Script(dungeon, index).Commands.Select(c => $"{c.Opcode:x2}:{c.Operand:x2}")) != sourcePrograms[index],
                $"Dungeon ${dungeon:x2} platform script ${index:x2} lost source aliases, first leg, loop target, or waits.");
        FailIf(string.Join(',', data.Script(1, 0).Commands.Select(c => $"{c.Opcode:x2}:{c.Operand:x2}")) != "00:08,08:80,00:08,0a:80,04:00" ||
            string.Join(',', data.Script(1, 1).Commands.Select(c => $"{c.Opcode:x2}:{c.Operand:x2}")) != "00:08,0b:40,00:08,09:80,00:08,0b:80,04:02",
            "Dungeon 1 platforms inherited Skull Dungeon's different route table.");
        var cases = new[] {
            (Room: 0x15, Subid: 0x05, X: 0x30, Y: 0x90, RX: 16, RY: 16),
            (Room: 0x6c, Subid: 0x03, X: 0xa0, Y: 0x58, RX: 16, RY: 8),
            (Room: 0x6c, Subid: 0x08, X: 0x68, Y: 0x28, RX: 8, RY: 8),
            (Room: 0x74, Subid: 0x11, X: 0x70, Y: 0x40, RX: 8, RY: 16),
            (Room: 0x75, Subid: 0x1a, X: 0xa8, Y: 0x28, RX: 8, RY: 24),
            (Room: 0x75, Subid: 0x23, X: 0x68, Y: 0x48, RX: 16, RY: 8),
            (Room: 0x75, Subid: 0x29, X: 0x68, Y: 0x90, RX: 8, RY: 16) };
        var skull = new SkullDungeonDatabase();
        foreach (var c in cases)
        {
            var placed = (c.Room == 0x15 ? new SpiritsGraveDatabase().GetRoomRecords(4,c.Room) : skull.GetRoomRecords(4,c.Room))
                .Single(r=>r.SubId == c.Subid && r.Id == 0x79);
            FailIf(placed.X != c.X || placed.Y != c.Y,"INTERAC$79 lost an independently traced mainData.s placement.");
            var platform = new MovingPlatformRoomEntity(
                new DungeonInteractionVisualDatabase().Visual($"platform-{c.Subid&7:x2}"),new(c.X,c.Y),c.Subid,
                new DungeonInteractionDatabase().MovingPlatformCollisionRadii(c.Subid),new DungeonInteractionDatabase(),
                data.Script(4,c.Subid>>3),_entities.PlatformRiding,_entities.ReadPlayingInstrument);
            FailIf(platform.CollisionRadii != new Vector2(c.RX,c.RY) ||
                platform.CurrentTexture.GetWidth() < c.RX*2 || platform.CurrentTexture.GetHeight() < c.RY*2,
                "INTERAC$79 lost its source geometry or clipped its long OAM axis.");
            platform.Free();
        }
        CompareMovingPlatformMotionRom();

        CompareMovingPlatformRiderRom();
        CompareMovingPlatformSpawnerRom();
        CompareMovingPlatformAllocationRom();
        CompareMovingPlatformHoleRom();
        CompareMovingPlatformScrollRom();
        ReinitializeGameplayForValidation();
    }
}
