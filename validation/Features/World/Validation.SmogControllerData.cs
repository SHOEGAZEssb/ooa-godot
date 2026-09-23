using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogControllerData()
    {
        var data = new SmogControllerDatabase();
        FailIf(data.Intro != new SmogEnemySpawn(new(0x78,0x58),0),
            "INTERAC$33 initial index$ff must advance to intro spawn$00 at $58,$78.");
        // Independently transcribed from smogBoss.s's relative phase lists;
        // source order matters because one tile is written every five updates.
        int[][] expectedTiles = [
            [0x110c,0x371d,0x461d,0x471d,0x481d,0x761d,0x771d,0x781d,0x871d],
            [0x110c,0x3a1c,0x441d,0x471d,0x4a1d,0x541c,0x571d,0x641d,0x671d],
            [0x110c,0x571c,0x621d,0x631d,0x641d,0x6a1d,0x6b1d,0x6c1d,0x771c],
            [0x110c,0x251d,0x261d,0x271d,0x321d,0x371d,0x3a1c,0x3c1d,0x421d,0x461d,0x4c1d,
                0x521d,0x591d,0x5c1d,0x621c,0x681d,0x6c1d,0x721d,0x741d,0x771c,0x7c1d]
        ];
        int[][] expectedSpawns = [
            [0x02386801,0x02888803],
            [unchecked((int)0x8258a801),0x02384801,0x02787803],
            [0x02583801,unchecked((int)0x8278b801)],
            [0x02282801,unchecked((int)0x82588803),unchecked((int)0x8288c801)]
        ];
        Vector2I[] positions = [new(0x78,0x58),new(0x78,0x28),new(0x78,0x38),new(0x68,0x38)];
        int[] first = [1,3,6,8];
        FailIf(data.Phases.Length != 4, "INTERAC$33 requires all four phases.");
        for (int phase = 0; phase < 4; phase++)
        {
            var record = data.Phases[phase];
            FailIf(record.LinkPosition != positions[phase] || record.FirstSpawnIndex != first[phase] ||
                !record.Tiles.Select(tile => tile.Position * 256 + tile.Tile).SequenceEqual(expectedTiles[phase]),
                $"Smog phase${phase:x2} lost its Link destination, shared stream index or ordered tile pairs/terminating boundary.");
            FailIf(record.Enemies.Any(enemy => enemy.Phase != phase) ||
                !record.Enemies.Select(enemy => (enemy.SubId << 24) | ((int)enemy.Position.Y << 16) |
                    ((int)enemy.Position.X << 8) | enemy.Direction).SequenceEqual(expectedSpawns[phase]),
                $"Smog phase${phase:x2} lost native subids, byte positions, directions or spawn ordering.");
        }
        GD.Print("Validated Smog controller relative tile lists, four Link destinations and the complete ordered intro/phase spawn stream.");
    }
}
