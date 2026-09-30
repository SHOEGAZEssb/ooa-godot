using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkCollisionRom()
    {
        LoadValidationRoom(0, 0x33);
        _entities.Clear();
        var room = _rooms.CurrentRoom;
        var rom = new LinkCollisionRom();
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
            room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, 0, 0);
        int probes = 0, movements = 0;
        for (int raised = 0; raised < 2; raised++)
        for (int collision = 0; collision < 32; collision++)
        {
            _runtimeState.SetWramByte(WramAddress.wLinkRaisedFloorOffset, (byte)raised);
            rom[0xcc69] = (byte)raised;
            room.SetPositionTileAndCollision(new(40, 40), 0x2c, (byte)collision, 0);
            rom[0xce22] = (byte)collision;
            // Sweep every pixel around all four edges of one metatile. Raw
            // collision bytes cover every quadrant mask and special pattern.
            for (int y = 24; y < 56; y++)
            for (int x = 24; x < 56; x++)
            {
                Vector2 position = new(x + 0.5f, y + 255 / 256.0f);
                rom.Word(0xd00a, (int)(position.Y * 256));
                rom.Word(0xd00c, (int)(position.X * 256));
                rom.Call(LinkCollisionRom.Probe);
                int walls = _collision.AdjacentWallsBitset(position);
                FailIf(walls != rom[0xd033],
                    $"Link wall probes collision=${collision:x2}, raised={raised}, XY={position}: ROM=${rom[0xd033]:x2}, runtime=${walls:x2}.");
                probes++;
                if ((x & 3) != 0 || (y & 3) != 0) continue;
                for (int angle = 0; angle < 32; angle++)
                {
                    int speed = ((x + y + angle) % 3) switch { 0 => 0x1e, 1 => 0x28, _ => 0x32 };
                    rom.Word(0xd00a, (int)(position.Y * 256));
                    rom.Word(0xd00c, (int)(position.X * 256));
                    rom.Call(LinkCollisionRom.Move, speed, angle);
                    Vector2 actual = position + _collision.ResolveNativeMovement(position, speed, angle, true);
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(actual != expected,
                        $"Link movement collision=${collision:x2}, raised={raised}, XY={position}, speed=${speed:x2}, angle=${angle:x2}: ROM={expected}, runtime={actual}.");
                    for (int offset = 0; offset < 4; offset++)
                        FailIf(_runtimeState.ReadWramByte(0xcec0 + offset) != rom[0xcec0 + offset],
                            $"Link velocity scratch ${0xcec0 + offset:x4} diverged, angle=${angle:x2}.");
                    movements++;
                }
            }
        }
        GD.Print($"Validated {probes} ROM Link wall probes and {movements} partial-tile/raised-floor/sliding/diagonal movements with velocity scratch.");
    }
}
