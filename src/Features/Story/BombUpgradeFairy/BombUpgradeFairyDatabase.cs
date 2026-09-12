using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class BombUpgradeFairyDatabase
{
    private readonly GeneratedTableRow _row = GeneratedTable.Load(
        "res://assets/oracle/cutscenes/bomb_upgrade_fairy.tsv",
        new GeneratedTableSchema("INTERAC_BOMB_UPGRADE_FAIRY $83:$00", GeneratedTableKeySemantics.Ordered,
            ["group", "room", "global-flag", "trigger-y", "trigger-height", "trigger-x", "trigger-width",
             "base-capacity", "first-upgrade", "second-upgrade", "poof-sound"], headerRequired: true)).SingleRow();
    private readonly Dictionary<string, GeneratedTableRow> _visuals = GeneratedTable.Load(
        "res://assets/oracle/cutscenes/bomb_upgrade_fairy_visuals.tsv",
        new GeneratedTableSchema("bomb fairy visuals", GeneratedTableKeySemantics.Unique,
            ["name", "id", "subid", "sprite", "tile-base", "palette", "animation", "source-offset", "source-grayscale-inverted"], ["name"], headerRequired: true))
        .Rows.ToDictionary(r => r.RequiredString(0));
    internal IReadOnlyList<CutsceneCommand> Commands { get; } = CutsceneCommandCatalog.Load(
        "res://assets/oracle/cutscenes/bomb_upgrade_fairy_commands.tsv");
    internal (Vector2 Offset, int Delay)[] Bombs { get; } = GeneratedTable.Load(
        "res://assets/oracle/cutscenes/bomb_upgrade_fairy_bombs.tsv",
        new GeneratedTableSchema("$83:$01 bombPositions", GeneratedTableKeySemantics.Ordered,
            ["index", "y-offset", "x-offset", "delay"], headerRequired: true)).Rows.Select(r =>
                (new Vector2(unchecked((sbyte)r.HexByte(2)), unchecked((sbyte)r.HexByte(1))), r.HexByte(3))).ToArray();
    internal Color[] GoldPalette { get; } = OracleGraphicsData.LoadPaletteColors(
        "res://assets/oracle/cutscenes/bomb_upgrade_fairy_gold_palette.bin", transparentZero: true);
    internal int Group => _row.Decimal(0, 0, 7);
    internal int Room => _row.HexByte(1);
    internal int GlobalFlag => _row.UnsignedDecimal(2);
    internal int PoofSound => _row.UnsignedDecimal(10);
    internal int SourceOffset(string name) => _visuals[name].UnsignedDecimal(7);
    internal bool SourceGrayscaleInverted(string name) => _visuals[name].Decimal(8, 0, 1) != 0;
    internal int Capacity(int maximum) => _row.HexByte(maximum == _row.HexByte(7) ? 8 : 9);
    internal bool Contains(Vector2 point) =>
        unchecked((byte)((int)point.Y - _row.HexByte(3))) < _row.HexByte(4) &&
        unchecked((byte)((int)point.X - _row.HexByte(5))) < _row.HexByte(6);
    internal NpcRecord Visual(string name, Vector2 position)
    {
        GeneratedTableRow row = _visuals[name];
        string animation = row.RequiredString(6);
        return new(Group, Room, row.HexByte(1), row.HexByte(2), (int)position.Y, (int)position.X,
            0, 0, row.RequiredString(3), row.UnsignedDecimal(4), row.UnsignedDecimal(5), 0, false,
            animation, animation, animation, animation, string.Empty, NpcImplementationClassification.EventOwned);
    }
}
