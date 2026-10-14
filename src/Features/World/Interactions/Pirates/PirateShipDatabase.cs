using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PirateShipDatabase
{
    internal static PirateShipDatabase Shared { get; } = new();
    internal IReadOnlyList<CutsceneCommand> CaptainCommands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/pirate_captain_commands.tsv");
    private readonly NpcRecord _ship;
    private readonly Dictionary<string, (int Group, int Room, int Parameter, int Position, int Transition)> _warps = new();
    internal List<PirateShipPlacement> Placements { get; } = [];
    internal string CrewJump { get; }
    internal string DepartureText { get; }
    internal int DepartureTextId { get; }

    private PirateShipDatabase()
    {
        var visuals = GeneratedTable.Load("res://assets/oracle/world/pirate_ship_visuals.tsv",
            new GeneratedTableSchema("pirate ship visuals", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "up", "right", "down", "left", "crew-jump", "source"], ["sprite"], headerRequired: true));
        if (visuals.Rows.Count != 1) throw new InvalidOperationException("pirateShip.s: expected one ship visual.");
        var row = visuals.Rows[0];
        _ship = new(0, 0, InteractionId.PirateShip, 0, 0, 0, 0, 0,
            row.RequiredString(0), row.UnsignedDecimal(1), row.UnsignedDecimal(2), 0, false,
            row.RequiredString(3), row.RequiredString(4), row.RequiredString(5), row.RequiredString(6), "",
            NpcImplementationClassification.SpecializedNative);
        CrewJump = row.RequiredString(7);
        var placements = GeneratedTable.Load("res://assets/oracle/world/pirate_ship_placements.tsv",
            new GeneratedTableSchema("pirate departure placements", GeneratedTableKeySemantics.Unique,
                ["group", "room", "order", "subid", "y", "x", "source"], ["group", "room"], headerRequired: true));
        foreach (var p in placements.Rows)
        {
            int subid = p.HexByte(3);
            if (subid is not (1 or 2)) throw p.Invalid(3, "$c2 departure subid $01/$02");
            Placements.Add(new(p.Decimal(0), p.HexByte(1), p.UnsignedDecimal(2), subid, p.HexByte(4), p.HexByte(5)));
        }
        var warps = GeneratedTable.Load("res://assets/oracle/world/pirate_ship_warps.tsv",
            new GeneratedTableSchema("pirate warps", GeneratedTableKeySemantics.Unique,
                ["role", "group", "room", "parameter", "position", "transition", "source"], ["role"], headerRequired: true));
        foreach (var w in warps.Rows)
        {
            if (w.HexByte(3) != 1) throw w.Invalid(3, "immediate transition2 $01");
            _warps.Add(w.RequiredString(0), (w.Decimal(1), w.HexByte(2), w.HexByte(3), w.HexByte(4), w.HexByte(5)));
        }
        if (Placements.Count != 2 || _warps.Count != 3 || !_warps.ContainsKey("board") || !_warps.ContainsKey("linked") || !_warps.ContainsKey("unlinked"))
            throw new InvalidOperationException("pirateShip.s/pirateCaptain_warpOut: incomplete placements or warps.");
        var texts = GeneratedTable.Load("res://assets/oracle/world/pirate_ship_departure_text.tsv",
            new GeneratedTableSchema("pirate departure text", GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64", "source"], ["text-id"], headerRequired: true));
        if (texts.Rows.Count != 1) throw new InvalidOperationException("pirateShip.s: missing departure text.");
        DepartureTextId = texts.Rows[0].HexWord(0);
        DepartureText = texts.Rows[0].Base64Utf8(1);
    }

    internal NpcRecord Ship(int group, int room, int subid, int y, int x) =>
        _ship with { Group = group, Room = room, SubId = subid, Y = y, X = x };

    internal Warp Warp(string role, int sourceGroup, int sourceRoom)
    {
        var w = _warps[role];
        // m_HardcodedWarpA's second operand is transition2 (immediate load),
        // not the destination entrance parameter packed into transition.
        return new(sourceGroup, sourceRoom, -1, 0, 0, w.Group, w.Room, w.Position,
            (w.Transition >> 4) & 7, w.Transition & 15);
    }
}

internal sealed record PirateShipPlacement(int Group, int Room, int Order, int SubId, int Y, int X);
