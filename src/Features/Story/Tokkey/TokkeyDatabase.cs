using System.Collections.Generic;

namespace oracleofages;

internal sealed class TokkeyDatabase
{
    private readonly GeneratedTableRow _row = GeneratedTable.Load(
        "res://assets/oracle/cutscenes/tokkey_event.tsv",
        new GeneratedTableSchema("INTERAC_TOKKEY $9d:$00", GeneratedTableKeySemantics.Ordered,
            ["group", "room", "id", "subid", "harp-tile", "exclamation-frames",
             "jump-speed", "gravity", "bounce-gravity", "bounce-speed", "note-mask",
             "heard-entry", "wrong-position-text-base64", "exclamation-sprite",
             "exclamation-tile", "exclamation-palette", "exclamation-animation",
             "wrong-position-textbox-position"],
            headerRequired: true)).SingleRow();

    internal IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/tokkey_commands.tsv");
    internal int Group => _row.Decimal(0, 0, 7);
    internal int Room => _row.HexByte(1);
    internal int Id => _row.HexByte(2);
    internal int SubId => _row.HexByte(3);
    internal int HarpTile => _row.HexByte(4);
    internal int ExclamationFrames => _row.UnsignedDecimal(5);
    internal int JumpSpeed => _row.Decimal(6);
    internal int Gravity => _row.UnsignedDecimal(7);
    internal int BounceGravity => _row.UnsignedDecimal(8);
    internal int BounceSpeed => _row.Decimal(9);
    internal int NoteMask => _row.HexByte(10);
    internal int HeardEntry => _row.UnsignedDecimal(11);
    internal string WrongPositionText => _row.Base64Utf8(12);
    internal int WrongPositionTextboxPosition => _row.UnsignedDecimal(17);
    internal NpcRecord Exclamation(int x, int y) => new(Group, Room, 0x9f, 0,
        y, x, 0, 0, _row.RequiredString(13), _row.UnsignedDecimal(14),
        _row.UnsignedDecimal(15), 0, false, _row.RequiredString(16),
        _row.RequiredString(16), _row.RequiredString(16), _row.RequiredString(16),
        string.Empty, NpcImplementationClassification.EventOwned);
}
