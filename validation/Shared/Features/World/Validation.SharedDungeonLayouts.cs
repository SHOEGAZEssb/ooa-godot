
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateSharedDungeonLayouts()
    {
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool ages = game == OracleGame.Ages;
            var rom = ValidationRom.LoadCleanUs(game);
            int Byte(int address) => rom.Span[address];
            int Word(int address) => Byte(address) | Byte(address + 1) << 8;
            var maps = new DungeonMapDatabase(OracleAssetPack.ForGame(game));
            int table = ages ? 0x4d2a : 0x4cc5, layouts = ages ? 0x4fce : 0x4f41;
            for (int dungeon = 0; dungeon < (ages ? 16 : 12); dungeon++)
            {
                int header = Word(table + dungeon * 2);
                int group = Byte(header) - 0xc5;
                int start = layouts + Byte(header + 2) * 64, floors = Byte(header + 3);
                int properties = (ages ? 0x4dce : 0x4d41) + (group - 4) * 256;
                var info = maps.GetDungeon(dungeon);
                string context = $"{game}: dungeon ${dungeon:x2}";
                FailIf(info.Group != group || info.WallmasterDestinationRoom != Byte(header + 1) ||
                    info.FloorCount != floors || info.BaseFloor != Byte(header + 4) ||
                    info.CompassFloors != Byte(header + 5), context + ": native eight-byte metadata differs.");
                int count = 0;
                var first = new Dictionary<int, DungeonCell>();
                for (int at = 0; at < floors * 64; at++)
                {
                    int room = Byte(start + at), floor = at / 64, x = at % 8, y = at % 64 / 8;
                    bool present = info.TryGetCell(floor, x, y, out var cell);
                    FailIf(present != (room != 0), context + $": native floor {floor} cell ({x},{y}) presence differs.");
                    if (room == 0) continue;
                    count++;
                    var expected = new DungeonCell(floor, x, y, room, (byte)Byte(properties + room));
                    FailIf(cell != expected, context + $": native room ${room:x2} properties or position differs.");
                    first.TryAdd(room, expected);
                    foreach (var direction in new[] { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left })
                    {
                        int nx = x + direction.X, ny = y + direction.Y;
                        int neighbor = nx is >= 0 and < 8 && ny is >= 0 and < 8
                            ? Byte(start + floor * 64 + ny * 8 + nx) : 0;
                        bool found = maps.TryGetNeighbor(dungeon, room, direction, out int actual);
                        FailIf(found != (neighbor != 0) || found && actual != neighbor,
                            context + $": native room ${room:x2} adjacency {direction} differs.");
                    }
                }
                FailIf(info.Cells.Count() != count || first.Any(pair =>
                    !info.TryGetRoom(pair.Key, out var actual) || actual != pair.Value),
                    context + ": room lookup must retain the native first occurrence and complete floor extent.");
            }
        }
    }
}
