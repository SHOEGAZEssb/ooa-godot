using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionDirectionsRom()
    {
        var rom = new LinkCollisionRom();
        foreach (int angle in new[] { 0, 1, 3, 4, 7, 8, 9, 12, 15, 16, 17, 20, 23, 24, 25, 28, 31, 0xff })
        {
            for (int direction = 0; direction < 4; direction++)
            {
                rom[0xcc2b] = (byte)angle; rom[0xd108] = (byte)direction;
                rom.Call(0x2b5d, objectPage: 0xd1);
                FailIf(CompanionMovement.DirectionForAngle(angle, direction) != rom[0xd108],
                    $"ROM companion direction differs for angle=${angle:x2}, previous={direction}.");
            }
            if (angle == 0xff) continue; // Moving-toward-wall returns Z with A=$ff when idle.
            for (int walls = 0; walls < 256; walls++)
            {
                rom[0xd109] = (byte)angle; rom[0xd133] = (byte)walls;
                rom.Call(0x451b, objectPage: 0xd1);
                FailIf(CompanionMovement.FacingWallMask(angle, walls) != rom[0xc200],
                    $"ROM companion facing-wall mask differs for angle=${angle:x2}, walls=${walls:x2}.");
            }
        }
        GD.Print("Validated native companion direction retention and facing-wall masks, including diagonal and idle input.");
    }

    private void ValidateCompanionTerrainRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var rom = new LinkCollisionRom();
        int cases = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        {
            PrepareCompanionFidelityRoom(); _entities.Clear();
            var actor = SpawnFidelityCompanion(id, new(72, 56));
            var positionField = actor.GetType().GetField("_precisePosition", flags)!;
            var probes = actor.GetType().GetMethod(id == 0x0b ? "CalculateAdjacentWallsBitset" : "AdjacentWalls", flags)!;
            rom[0xd101] = (byte)id;
            // Every collision shape at every in-tile pixel. Extra rows cover
            // native vine overrides and Dimitri's $fe/$ff pass-through.
            for (int terrain = 0; terrain < 37; terrain++)
            {
                byte tile = terrain < 32 ? (byte)0 : new byte[] { 0xd4, 0xd5, 0xd6, 0xfe, 0xff }[terrain - 32];
                byte collision = terrain < 32 ? (byte)terrain : tile == 0xff ? (byte)0xff : (byte)15;
                for (int y = 8; y < 128; y += 16)
                for (int x = 8; x < 160; x += 16)
                    _currentRoom.SetPositionTileAndCollision(new(x, y), tile, collision, 0);
                for (int packed = 0; packed < 256; packed++)
                { rom[0xcf00 + packed] = tile; rom[0xce00 + packed] = collision; }
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    Vector2 position = new(64 + x + 0.75f, 48 + y + 0.25f);
                    positionField.SetValue(actor, position);
                    rom.Word(0xd10c, (int)(position.X * 256)); rom.Word(0xd10a, (int)(position.Y * 256));
                    rom.Call(0x4486, objectPage: 0xd1);
                    int actual = (int)probes.Invoke(actor, null)!;
                    FailIf(actual != rom[0xd133], $"ROM companion ${id:x2} probes differ: tile=${tile:x2}, collision=${collision:x2}, x={x}, y={y}: ${actual:x2}/${rom[0xd133]:x2}.");
                    int angle = ((x + y) & 7) * 4;
                    int speed = id == 0x0d ? 0x28 : 0x1e;
                    rom.Call(LinkCollisionRom.Move, speed, angle, objectPage: 0xd1);
                    SpecialObjectMovement.ApplySpeed(ref position, speed, angle, actual);
                    FailIf((int)(position.X * 256) != rom.Word(0xd10c) || (int)(position.Y * 256) != rom.Word(0xd10a),
                        $"ROM companion ${id:x2} fixed-point wall movement differs.");
                    cases++;
                }
            }
        }
        GD.Print($"Validated {cases} ROM companion probe/movement cases across all three runtime owners, collision shapes and vine/waterfall restrictions.");
    }
}
