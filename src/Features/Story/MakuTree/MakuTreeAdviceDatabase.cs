using System.Collections.Generic;

namespace oracleofages;

internal sealed class MakuTreeAdviceDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<(int, bool), MakuTreeAdviceRecord> _records = [];
    private readonly Dictionary<int, IReadOnlyList<CutsceneCommand>> _commands = [];
    internal SavedEventRecord Graphics { get; } = new MakuTreeSavedDatabase().Record;
    internal NpcRecord Flower { get; }
    internal string FrowningFlower { get; }

    internal MakuTreeAdviceDatabase()
    {
        foreach (GeneratedTableRow row in GeneratedTable.Load(Root + "maku_tree_advice.tsv",
            new GeneratedTableSchema("Maku Tree ordinary advice", GeneratedTableKeySemantics.Unique,
                ["state", "linked", "mode", "text-id", "position", "text-base64",
                 "second-text-id", "second-position", "second-text-base64"],
                keyColumns: ["state", "linked"], headerRequired: true)).Rows)
        {
            int mode = row.UnsignedDecimal(2);
            _records.Add((row.HexByte(0), row.Boolean01(1)), new(
                mode, row.HexWord(3), row.UnsignedDecimal(4), row.Base64Utf8(5),
                row.HexWord(6), row.UnsignedDecimal(7), row.Base64Utf8(8)));
            if (!_commands.ContainsKey(mode))
                _commands.Add(mode, CutsceneCommandCatalog.Load(Root + $"maku_tree_advice_mode{mode}.tsv"));
        }
        GeneratedTableRow flower = GeneratedTable.Load(Root + "maku_tree_flower.tsv",
            new GeneratedTableSchema("Maku Tree related flower", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "animation0", "animation1"], headerRequired: true)).SingleRow();
        string animation = flower.RequiredString(3);
        Flower = new NpcRecord(Graphics.Group, Graphics.Room, 0x86, 0,
            0x40, 0x50, 0, 0, flower.RequiredString(0), flower.UnsignedDecimal(1),
            flower.UnsignedDecimal(2), 0, false, animation, animation, animation, animation,
            string.Empty, NpcImplementationClassification.EventOwned);
        FrowningFlower = flower.RequiredString(4);
    }

    internal bool TryGet(int state, bool linked, out MakuTreeAdviceRecord record) =>
        _records.TryGetValue((state, linked), out record);
    internal IReadOnlyList<CutsceneCommand> Commands(int mode) => _commands[mode];
}

internal readonly record struct MakuTreeAdviceRecord(
    int Mode, int TextId, int Position, string Text,
    int SecondTextId, int SecondPosition, string SecondText);
