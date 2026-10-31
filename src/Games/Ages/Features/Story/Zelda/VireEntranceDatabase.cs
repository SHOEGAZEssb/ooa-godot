using System.Collections.Generic;

namespace Oracles;

internal sealed class VireEntranceDatabase
{
    private const string Root = AgesGameContent.AssetRoot + "cutscenes/";
    private readonly GeneratedTableRow _row = GeneratedTable.Load(Root + "vire_entrance.tsv",
        new("Vire entrance", GeneratedTableKeySemantics.Ordered,
            ["group", "room", "order", "y", "x", "sprite", "tile-base", "palette", "animation", "init-speed", "init-sound", "source"],
            headerRequired: true)).SingleRow();
    internal IReadOnlyList<CutsceneCommand> Commands { get; } = CutsceneCommandCatalog.Load(Root + "vire_entrance_commands.tsv");
    internal bool Matches(int group, int room) => group == _row.UnsignedDecimal(0) && room == _row.HexByte(1);
    internal int Speed => _row.UnsignedDecimal(9);
    internal int Sound => _row.UnsignedDecimal(10);
    internal string Source => _row.RequiredString(11);
    internal NpcRecord Record => new(_row.UnsignedDecimal(0), _row.HexByte(1), 0xb8, 0,
        _row.HexByte(3), _row.HexByte(4), 0, 0, _row.RequiredString(5), _row.UnsignedDecimal(6),
        _row.UnsignedDecimal(7), 0, false, _row.RequiredString(8), _row.RequiredString(8),
        _row.RequiredString(8), _row.RequiredString(8), string.Empty, NpcImplementationClassification.EventOwned);
}
