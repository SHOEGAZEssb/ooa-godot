using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DungeonToggleTileDatabase
{
    private readonly Dictionary<(int Dungeon, int State), Dictionary<byte, byte>> _tiles = new();
    private readonly Dictionary<int, Dictionary<int, DynamicBackgroundTile>> _graphics = new();
    private readonly Dictionary<byte, (byte Tile, byte Collision)> _liveTiles = new();
    internal int Delay { get; private set; }
    internal bool Supports(int dungeon) => _tiles.ContainsKey((dungeon, 0));

    internal DungeonToggleTileDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/dungeon_toggle_tiles.tsv",
            new GeneratedTableSchema("dungeon toggle tiles", GeneratedTableKeySemantics.Unique,
                ["dungeon", "state", "original", "replacement", "destination-tile", "tile-count", "source-tile", "source"],
                ["dungeon", "state", "original"], headerRequired: true));
        Image image = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/gfx_animations_2.png");
        foreach (var row in table.Rows)
        {
            int dungeon = row.Decimal(0, 0, 15), state = row.Decimal(1, 0, 1);
            var key = (dungeon, state);
            if (!_tiles.TryGetValue(key, out var tiles))
                _tiles.Add(key, tiles = new());
            tiles.Add((byte)row.HexByte(2), (byte)row.HexByte(3));
            int destination = row.Decimal(4, 0, 255), count = row.Decimal(5, 1, 256), source = row.Decimal(6, 0, 4095);
            _ = row.RequiredString(7);
            if (!_graphics.ContainsKey(state))
            {
                var graphics = new Dictionary<int, DynamicBackgroundTile>();
                for (int i = 0; i < count; i++) graphics.Add(destination + i, new(image, source + i));
                _graphics.Add(state, graphics);
            }
        }
        if (table.Rows.Count != 12) throw new InvalidOperationException("Expected twelve source dungeon toggle substitutions.");
        var live = GeneratedTable.Load("res://assets/oracle/metadata/dungeon_toggle_cutscene.tsv",
            new GeneratedTableSchema("dungeon toggle cutscene", GeneratedTableKeySemantics.Unique,
                ["name", "values", "source"], ["name"], headerRequired: true));
        foreach (var row in live.Rows)
        {
            string name = row.RequiredString(0);
            int[] values = Array.ConvertAll(row.RequiredString(1).Split(','), int.Parse);
            _ = row.RequiredString(2);
            if (name == "delay") Delay = values[0];
            else if (name == "intermediate-graphics")
            {
                var graphics = new Dictionary<int, DynamicBackgroundTile>();
                for (int i = 0; i < values[1]; i++) graphics.Add(values[0] + i, new(image, values[2] + i));
                _graphics.Add(2, graphics);
            }
            else if (name.StartsWith("tile-", StringComparison.Ordinal))
                _liveTiles.Add(Convert.ToByte(name[5..], 16), ((byte)values[0], (byte)values[1]));
            else throw new InvalidOperationException($"Unsupported dungeon toggle cutscene row {name}.");
        }
        if (Delay <= 0 || _liveTiles.Count != 4 || !_graphics.ContainsKey(2))
            throw new InvalidOperationException("Incomplete dungeon toggle cutscene data.");
    }

    internal void Upload(OracleRoomData room, int phase, long tick) => room.SetDynamicBackgroundTiles(_graphics[phase], tick);

    internal void Complete(RoomSession rooms, Func<Vector2, bool> debris, long tick)
    {
        OracleRoomData room = rooms.CurrentRoom;
        // The first pass destroys movable $10 blocks over the lowered pair.
        // setTile may fail when its graphics queue is full; debris still tries.
        for (int position = 0xaf; position >= 0; position--)
        {
            if (room.Layout[position] != 0x10) continue;
            byte underlying = room.GetUnderlyingStorageMetatile(position);
            if (underlying is not (0x28 or 0x29)) continue;
            rooms.TrySetTile((byte)position, (byte)(underlying - 0x28 + 0x0e));
            _ = debris(new((position & 15) * 16 + 8, (position >> 4) * 16 + 8));
            room.SetStorageLayoutWithoutGraphics(position, underlying);
        }
        // Native loop excludes $00 and includes the padding column.
        for (int position = 0xaf; position > 0; position--)
            if (_liveTiles.TryGetValue(room.GetUnderlyingStorageMetatile(position), out var replacement))
            {
                room.SetStorageTileAndCollision(position, replacement.Tile, replacement.Collision, tick);
                room.SetUnderlyingStorageMetatile(position, replacement.Tile);
            }
    }

    internal void Apply(int group, int dungeon, byte toggleState, OracleRoomData room, long tick)
    {
        // applyAllTileSubstitutions excludes both small rooms and side-view
        // groups. replaceToggleBlocks tests the whole state byte, not bit0.
        int state = toggleState == 0 ? 0 : 1;
        if (!_tiles.TryGetValue((dungeon, state), out var tiles)) return;
        // loadTilesetGraphics also uploads this header for side-view rooms;
        // only the layout substitution has the group restriction.
        room.SetDynamicBackgroundTiles(_graphics[state], tick);
        if ((group & 6) == 4)
        {
            room.ApplyMetatileSubstitutions(tiles, tick);
            // The cached underlying buffer also retains explicit writes from
            // other systems (colored jump floors, bridges, buttons). Refresh
            // only this substitution's floor cells; do not erase those owners.
            for (int position = 0; position < room.Layout.Length; position++)
                if (_liveTiles.ContainsKey(room.Layout[position]))
                    room.SetUnderlyingStorageMetatile(position, room.Layout[position]);
        }
    }
}
