using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

/// <summary>
/// Imported MENU_MAP lookup data: 14x14 area text bytes, cursor popup types,
/// tree icons, dungeon entrance fallbacks, and resolved TX strings.
/// </summary>
public sealed class MapDataDatabase
{
    private readonly Dictionary<(int Group, int Room), MapCell> _cells = new();
    private readonly Dictionary<int, MapText> _texts = new();
    private readonly Dictionary<(int Group, int Room), int> _treePopups = new();
    private readonly Dictionary<int, DungeonEntrance> _dungeonEntrances = new();

    public MapDataDatabase()
    {
        LoadCells();
        LoadTexts();
        LoadTreeWarps();
        LoadDungeonEntrances();
    }

    public int GetPopupByte(int group, int room) =>
        _cells.TryGetValue((group, room), out MapCell cell) ? cell.Popup : 0;

    public int GetTreePopup(int group, int room) =>
        _treePopups.TryGetValue((group, room), out int popup) ? popup : 0;

    public bool TryResolveAreaText(RoomSession rooms, int group, int room,
        out MapText text)
    {
        text = default;
        if (!_cells.TryGetValue((group, room), out MapCell cell))
            return false;

        int index = cell.Text;
        int textId;
        if ((index & 0x80) == 0)
        {
            textId = 0x0300 | index;
        }
        else
        {
            textId = ResolveSpecialTextId(rooms, group, index);
        }
        return _texts.TryGetValue(textId, out text);
    }

    private int ResolveSpecialTextId(RoomSession rooms, int group, int index)
    {
        OracleSaveData save = rooms.SaveData;
        switch (index & 0x07)
        {
            case 0: // Maku Tree advice.
                bool past = group == 1;
                int metFlag = past ? 0x3f : 0x3e;
                if (!save.HasGlobalFlag(metFlag))
                    return past ? 0x0324 : 0x0323;
                return 0x0500 | save.ReadWramByte(past ? 0xc6e7 : 0xc6e6);

            case 1: // Dungeon entrance name after entering it once.
                int dungeon = (index >> 3) & 0x0f;
                if (!_dungeonEntrances.TryGetValue(dungeon, out DungeonEntrance entrance))
                    return 0x0332;
                return rooms.HasVisited(entrance.Group, entrance.Room)
                    ? 0x0200 | dungeon
                    : 0x0300 | entrance.FallbackText;

            case 2: // Moblin's Keep / ruins.
                return save.HasGlobalFlag(0x1a) ? 0x0317 : 0x0318;

            case 3: // Animal companion region.
                return 0x032d + Math.Clamp(save.ReadWramByte(0xc610) - 0x0b, 0, 2);

            case 4: // Advance Shop text changes after it is visited.
                return rooms.HasVisited(1, 0xfe) ? 0x0325 : 0x0326;

            default:
                return 0x0332;
        }
    }

    private void LoadCells()
    {
        foreach (string line in DataLines("res://assets/oracle/map/overworld.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 5)
                throw new InvalidOperationException($"Malformed overworld map row: {line}");
            int room = Convert.ToInt32(columns[0], 16);
            _cells[(0, room)] = new MapCell(
                Convert.ToInt32(columns[1], 16), Convert.ToInt32(columns[3], 16));
            _cells[(1, room)] = new MapCell(
                Convert.ToInt32(columns[2], 16), Convert.ToInt32(columns[4], 16));
        }
        if (_cells.Count != 392)
            throw new InvalidOperationException($"Expected 392 era/map cells, got {_cells.Count}.");
    }

    private void LoadTexts()
    {
        foreach (string line in DataLines("res://assets/oracle/map/texts.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 3)
                throw new InvalidOperationException($"Malformed map text row: {line}");
            int textId = Convert.ToInt32(columns[0], 16);
            _texts[textId] = new MapText(
                textId,
                Encoding.UTF8.GetString(Convert.FromBase64String(columns[2])),
                int.Parse(columns[1]));
        }
    }

    private void LoadTreeWarps()
    {
        foreach (string line in DataLines("res://assets/oracle/map/tree_warps.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 3)
                throw new InvalidOperationException($"Malformed map tree row: {line}");
            _treePopups[(int.Parse(columns[0]), Convert.ToInt32(columns[1], 16))] =
                Convert.ToInt32(columns[2], 16);
        }
    }

    private void LoadDungeonEntrances()
    {
        foreach (string line in DataLines("res://assets/oracle/map/dungeon_entrances.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 4)
                throw new InvalidOperationException($"Malformed dungeon entrance row: {line}");
            _dungeonEntrances[int.Parse(columns[0])] = new DungeonEntrance(
                int.Parse(columns[1]), Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16));
        }
        if (_dungeonEntrances.Count != 16)
            throw new InvalidOperationException(
                $"Expected 16 dungeon entrance text records, got {_dungeonEntrances.Count}.");
    }

    private static IEnumerable<string> DataLines(string path)
    {
        foreach (string rawLine in FileAccess.GetFileAsString(path)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
                yield return line;
        }
    }

    private readonly record struct MapCell(int Text, int Popup);
    private readonly record struct DungeonEntrance(int Group, int Room, int FallbackText);
    public readonly record struct MapText(int TextId, string Message, int Position);
}
