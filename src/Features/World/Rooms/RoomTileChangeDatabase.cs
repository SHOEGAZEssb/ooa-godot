using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Declarative room-initialization rules imported from
/// applyRoomSpecificTileChanges. Positions retain wRoomLayout's packed YX
/// format while the room object handles its compact/large storage stride.
/// </summary>
public sealed class RoomTileChangeDatabase
{

    private readonly Dictionary<(int Group, int Room), List<Rule>> _rules = new();

    internal int RuleCount { get; }
    internal int RoomCount => _rules.Count;

    public RoomTileChangeDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/room_tile_changes.tsv",
            new GeneratedTableSchema(
                "room-specific tile changes",
                GeneratedTableKeySemantics.Grouped,
                ["group", "room", "conditions", "operations", "source"],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int group = row.Decimal(0, 0, 7);
            int room = row.HexByte(1);
            Condition[] conditions = ParseConditions(row.RequiredString(2));
            Operation[] operations = ParseOperations(row.RequiredString(3));
            row.RequiredString(4);

            var key = (group, room);
            if (!_rules.TryGetValue(key, out List<Rule>? roomRules))
            {
                roomRules = new List<Rule>();
                _rules.Add(key, roomRules);
            }
            roomRules.Add(new Rule(conditions, operations));
            count++;
        }
        RuleCount = count;
    }

    internal void Apply(
        int group,
        OracleRoomData room,
        OracleSaveData save,
        OracleWorldData world,
        long animationTick)
    {
        var writes = new Dictionary<int, byte>();
        if (_rules.TryGetValue((group, room.Id), out List<Rule>? rules))
        {
            foreach (Rule rule in rules)
            {
                if (!Matches(rule.Conditions, group, room.Id, save))
                    continue;
                foreach (Operation operation in rule.Operations)
                    ApplyOperation(operation, group, room, world, writes);
            }
        }

        room.ApplyRoomInitializationChanges(writes, animationTick);
    }

    private static bool Matches(
        Condition[] conditions,
        int group,
        int room,
        OracleSaveData save)
    {
        foreach (Condition condition in conditions)
        {
            bool matches = condition.Kind switch
            {
                ConditionKind.GlobalSet => save.HasGlobalFlag(condition.A),
                ConditionKind.CurrentRoomSet =>
                    (save.GetRoomFlags(group, room) & condition.A) != 0,
                ConditionKind.CurrentRoomClear =>
                    (save.GetRoomFlags(group, room) & condition.A) == 0,
                ConditionKind.RoomSet =>
                    (save.GetRoomFlags(condition.A, condition.B) & condition.C) != 0,
                ConditionKind.EssenceSet =>
                    (save.ReadWramByte(0xc6bf) & (1 << condition.A)) != 0,
                ConditionKind.WramMaskEquals =>
                    (save.ReadWramByte(condition.A) & condition.B) == condition.C,
                _ => false
            };
            if (!matches)
                return false;
        }
        return true;
    }

    private static void ApplyOperation(
        Operation operation,
        int group,
        OracleRoomData room,
        OracleWorldData world,
        Dictionary<int, byte> writes)
    {
        int[] values = operation.Values;
        switch (operation.Kind)
        {
            case OperationKind.Set:
                for (int index = 0; index < values.Length; index += 2)
                    Set(values[index], (byte)values[index + 1], room, writes);
                break;

            case OperationKind.Fill:
                Fill(values[0], values[1], values[2], (byte)values[3], room, writes);
                break;

            case OperationKind.Draw:
            {
                int position = values[0];
                int height = values[1];
                int width = values[2];
                int tileIndex = 3;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Set(OffsetPosition(position, x, y), (byte)values[tileIndex++], room, writes);
                }
                break;
            }

            case OperationKind.Replace:
                for (int y = 0; y < room.HeightInTiles; y++)
                for (int x = 0; x < room.WidthInTiles; x++)
                {
                    int position = (y << 4) | x;
                    byte current = writes.TryGetValue(position, out byte written)
                        ? written
                        : room.GetMetatile(PointFor(position));
                    if (current == values[0])
                        writes[position] = (byte)values[1];
                }
                break;

            case OperationKind.CopyOriginal:
            {
                OracleRoomData source = world.LoadRoom(group, values[0]);
                if (source.WidthInTiles != room.WidthInTiles ||
                    source.HeightInTiles != room.HeightInTiles)
                {
                    throw new InvalidOperationException(
                        $"Room {group:x1}:{room.Id:x2} cannot copy differently sized " +
                        $"room {group:x1}:{values[0]:x2}.");
                }
                for (int y = 0; y < room.HeightInTiles; y++)
                for (int x = 0; x < room.WidthInTiles; x++)
                {
                    int position = (y << 4) | x;
                    writes[position] = source.GetOriginalMetatile(PointFor(position));
                }
                break;
            }
        }
    }

    private static void Fill(
        int position,
        int height,
        int width,
        byte tile,
        OracleRoomData room,
        Dictionary<int, byte> writes)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            Set(OffsetPosition(position, x, y), tile, room, writes);
    }

    private static void Set(
        int position,
        byte tile,
        OracleRoomData room,
        Dictionary<int, byte> writes)
    {
        int x = position & 0x0f;
        int y = position >> 4;
        if (x < 0 || x >= room.WidthInTiles || y < 0 || y >= room.HeightInTiles)
        {
            throw new InvalidOperationException(
                $"Room {room.Group:x1}:{room.Id:x2} tile change uses invalid position ${position:x2}.");
        }
        writes[position] = tile;
    }

    private static int OffsetPosition(int position, int x, int y) =>
        (((position >> 4) + y) << 4) | ((position & 0x0f) + x);

    private static Vector2 PointFor(int position) => new(
        (position & 0x0f) * OracleRoomData.MetatileSize + 8,
        (position >> 4) * OracleRoomData.MetatileSize + 8);

    private static Condition[] ParseConditions(string source)
    {
        if (source == "always")
            return Array.Empty<Condition>();

        string[] tokens = source.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = new Condition[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            string[] fields = tokens[index].Split(':');
            result[index] = fields[0] switch
            {
                "global_set" when fields.Length == 2 =>
                    new Condition(ConditionKind.GlobalSet, Hex(fields[1]), 0, 0),
                "current_room_set" when fields.Length == 2 =>
                    new Condition(ConditionKind.CurrentRoomSet, Hex(fields[1]), 0, 0),
                "current_room_clear" when fields.Length == 2 =>
                    new Condition(ConditionKind.CurrentRoomClear, Hex(fields[1]), 0, 0),
                "room_set" when fields.Length == 4 =>
                    new Condition(
                        ConditionKind.RoomSet, int.Parse(fields[1]), Hex(fields[2]), Hex(fields[3])),
                "essence_set" when fields.Length == 2 =>
                    new Condition(ConditionKind.EssenceSet, int.Parse(fields[1]), 0, 0),
                "wram_mask_eq" when fields.Length == 4 =>
                    new Condition(
                        ConditionKind.WramMaskEquals, Hex(fields[1]), Hex(fields[2]), Hex(fields[3])),
                _ => throw new InvalidOperationException(
                    $"Malformed room tile-change condition '{tokens[index]}'.")
            };
        }
        return result;
    }

    private static Operation[] ParseOperations(string source)
    {
        string[] tokens = source.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var result = new Operation[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (token.StartsWith("set:", StringComparison.Ordinal))
            {
                result[index] = new Operation(
                    OperationKind.Set, ParsePairs(token[4..]));
                continue;
            }

            string[] fields = token.Split(':');
            result[index] = fields[0] switch
            {
                "fill" when fields.Length == 5 =>
                    new Operation(OperationKind.Fill, ParseHexValues(fields[1..])),
                "draw" when fields.Length == 5 =>
                    ParseDraw(fields),
                "replace" when fields.Length == 3 =>
                    new Operation(OperationKind.Replace, ParseHexValues(fields[1..])),
                "copy_original" when fields.Length == 2 =>
                    new Operation(OperationKind.CopyOriginal, new[] { Hex(fields[1]) }),
                _ => throw new InvalidOperationException(
                    $"Malformed room tile-change operation '{token}'.")
            };
        }
        return result;
    }

    private static Operation ParseDraw(string[] fields)
    {
        int position = Hex(fields[1]);
        int height = Hex(fields[2]);
        int width = Hex(fields[3]);
        int[] tiles = ParseHexValues(fields[4].Split(','));
        if (tiles.Length != height * width)
        {
            throw new InvalidOperationException(
                $"Room tile-change draw ${position:x2} needs {height * width} tiles, got {tiles.Length}.");
        }
        var values = new int[tiles.Length + 3];
        values[0] = position;
        values[1] = height;
        values[2] = width;
        tiles.CopyTo(values, 3);
        return new Operation(OperationKind.Draw, values);
    }

    private static int[] ParsePairs(string source)
    {
        string[] pairs = source.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var values = new int[pairs.Length * 2];
        for (int index = 0; index < pairs.Length; index++)
        {
            string[] fields = pairs[index].Split(':');
            if (fields.Length != 2)
                throw new InvalidOperationException($"Malformed room tile-change pair '{pairs[index]}'.");
            values[index * 2] = Hex(fields[0]);
            values[index * 2 + 1] = Hex(fields[1]);
        }
        return values;
    }

    private static int[] ParseHexValues(string[] fields)
    {
        var values = new int[fields.Length];
        for (int index = 0; index < fields.Length; index++)
            values[index] = Hex(fields[index]);
        return values;
    }

    private static int Hex(string value) => Convert.ToInt32(value, 16);
}
