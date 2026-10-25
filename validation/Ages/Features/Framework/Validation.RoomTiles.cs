using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    // Arrangement only: no original-engine update observes the intermediate
    // writes. The final room image is published before returning to the test.
    private void FillValidationRoom(byte tile, byte collision = 0, int? width = null, int? height = null) =>
        ConfigureValidationRoomTiles((_, _) => (tile, collision), width, height);

    private void ConfigureValidationRoomTiles(Func<int, int, (byte Tile, byte Collision)> tileAt,
        int? width = null, int? height = null)
    {
        int columns = width ?? _currentRoom.WidthInTiles, rows = height ?? _currentRoom.HeightInTiles;
        if (columns < 1 || rows < 1 || columns > _currentRoom.WidthInTiles || rows > _currentRoom.HeightInTiles)
            throw new ArgumentOutOfRangeException(nameof(width), "Fixture tile bounds must stay inside the playable room.");
        var writes = new RoomTileWrite[columns * rows];
        int index = 0;
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            var value = tileAt(x, y);
            writes[index++] = new(new(x * 16 + 8, y * 16 + 8), value.Tile, value.Collision);
        }
        _currentRoom.SetTilesAndCollisions(writes, 0);
    }
}
