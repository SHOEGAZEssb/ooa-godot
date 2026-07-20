using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class BreakableTileDatabase
{
    public const int SourceBracelet = 0x00;
    public const int SourceSwordLevel1 = 0x01;
    public const int SourceSwordLevel2 = 0x02;
    public const int SourceShovel = 0x06;
    public const int SourceEmberSeed = 0x0c;

    private readonly Dictionary<int, BreakableTileRecord> _records = new();

    public BreakableTileDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/metadata/breakable_tiles.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 9)
                throw new InvalidOperationException($"Malformed breakable tile row: {line}");

            int activeCollisions = int.Parse(columns[0]);
            int tile = Convert.ToInt32(columns[1], 16);
            var record = new BreakableTileRecord(
                activeCollisions,
                tile,
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16),
                Convert.ToInt32(columns[4], 16),
                Convert.ToInt32(columns[5], 16),
                Convert.ToInt32(columns[6], 16),
                Convert.ToInt32(columns[7], 16),
                int.Parse(columns[8]));
            _records[(activeCollisions << 8) | tile] = record;
        }
    }

    public bool TryGet(int activeCollisions, int tile, out BreakableTileRecord record) =>
        _records.TryGetValue((activeCollisions << 8) | tile, out record);

    public readonly record struct BreakableTileRecord(
        int ActiveCollisions,
        int Tile,
        int Mode,
        int SourceMask,
        int Drop,
        int Effect,
        int Replacement,
        int RoomFlagAction,
        int GashaMaturity)
    {
        public bool AllowsSource(int source) => (SourceMask & (1 << source)) != 0;

        public void ApplyPersistentEffects(
            OracleSaveData? saveData,
            int group,
            int room)
        {
            if ((Effect & 0x80) == 0)
                return;
            if (saveData is null)
            {
                throw new InvalidOperationException(
                    $"Breakable tile ${Tile:x2} requires live room-flag state.");
            }

            if (GashaMaturity != 0)
                saveData.AddGashaMaturity(GashaMaturity);
            if (RoomFlagAction == 0xff)
                return;
            if ((RoomFlagAction & 0xc0) != 0)
            {
                throw new InvalidOperationException(
                    $"Breakable tile ${Tile:x2} uses linked room-flag action " +
                    $"${RoomFlagAction:x2}, which is not supported by this break source.");
            }
            saveData.SetRoomFlag(
                group, room, (byte)(1 << (RoomFlagAction & 0x0f)));
        }
    }
}
