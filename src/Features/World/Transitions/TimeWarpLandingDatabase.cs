using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TimeWarpLandingDatabase
{
    private readonly Dictionary<byte, bool> _invalidTiles = new();
    private readonly HashSet<int> _strangeForceRooms = new();
    private readonly HashSet<int> _solidNpcIds = new();
    internal string StrangeForceText { get; }

    internal TimeWarpLandingDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/timewarp_landing.tsv",
            new GeneratedTableSchema("timewarp landing", GeneratedTableKeySemantics.Ordered,
                ["kind", "key", "value", "source"], headerRequired: true));
        string? text = null;
        foreach (GeneratedTableRow row in table.Rows)
        {
            string source = row.RequiredString(3);
            switch (row.RequiredString(0))
            {
                case "tile":
                    if (!_invalidTiles.TryAdd((byte)row.HexByte(1), row.HexByte(2) switch
                        { 0 => false, 1 => true, _ => throw new InvalidOperationException(source) }))
                        throw new InvalidOperationException($"{source}: duplicate timewarp tile.");
                    break;
                case "room":
                    if (row.HexByte(2) != 1 || !_strangeForceRooms.Add(row.HexByte(1)))
                        throw new InvalidOperationException($"{source}: invalid strange-force room.");
                    break;
                case "text":
                    if (row.RequiredString(1) != "5112" || text is not null)
                        throw new InvalidOperationException($"{source}: expected unique TX_5112.");
                    text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row.RequiredString(2)));
                    break;
                case "solid-npc":
                    if (row.HexByte(2) != 1 || !_solidNpcIds.Add(row.HexByte(1)))
                        throw new InvalidOperationException($"{source}: invalid solid NPC handler.");
                    break;
                default:
                    throw new InvalidOperationException($"{source}: unsupported timewarp landing record.");
            }
        }
        if (_invalidTiles.Count != 10 || _strangeForceRooms.Count != 14 || text is null)
            throw new InvalidOperationException("Timewarp landing tables lost their 10 tiles, 14 rooms, or TX_5112.");
        StrangeForceText = text;
    }

    internal bool SentBackByStrangeForce(int room) => _strangeForceRooms.Contains(room);
    internal bool MarksSolidPosition(NpcRecord record) => _solidNpcIds.Contains(record.Id) && record.Id switch
    {
        // forestFairy_initNpcFromData belongs to the post-minigame NPCs;
        // the hiding-game fairies never reserve a timewarp landing tile.
        0x49 => record.SubId >= 0x05,
        // carpenter.s calls the marker only from @initSubid00/@initSubid09.
        0x9a => (record.SubId & 0x0f) is 0x00 or 0x09,
        _ => true
    };

    internal bool CanStandOnTile(OracleRoomData room, Vector2 position, bool hasMermaidSuit)
    {
        // checkPositionSurroundedByWalls rotates two bits at a time. It accepts
        // any completely open side; no $db/$ee movement-mask normalization.
        ReadOnlySpan<Vector2> probes = [new(-3, -3), new(2, -3), new(-3, 7), new(2, 7),
            new(-5, 0), new(-5, 5), new(4, 0), new(4, 5)];
        bool openSide = false;
        for (int index = 0; index < probes.Length; index += 2)
            openSide |= !room.IsSolid(position + probes[index]) && !room.IsSolid(position + probes[index + 1]);
        if (!openSide) return false;
        return !_invalidTiles.TryGetValue(room.GetMetatile(position), out bool requiresSuit) ||
            requiresSuit && hasMermaidSuit;
    }
}
