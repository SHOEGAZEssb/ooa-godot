namespace Oracles;

internal sealed class RodOfSeasonsDatabase
{
    private const string Root = SeasonsGameContent.AssetRoot;
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _programs = CutsceneCommandCatalog.LoadPrograms(Root + "rod/commands.tsv");
    private readonly Dictionary<string, byte[]> _tables = [];
    private readonly Dictionary<int, string> _texts = [];
    internal string[] WeaponPoses { get; } = new string[8];
    internal int TileBase { get; }
    internal int Flags { get; }
    internal int Collision { get; }
    internal int Damage { get; }
    internal int Health { get; }
    internal RodOfSeasonsDatabase()
    {
        foreach (var row in GeneratedTable.Load(Root + "rod/weapon_poses.tsv", new("ITEM$07 OAM", GeneratedTableKeySemantics.Unique,
            ["index", "animation", "source"], ["index"], headerRequired: true)).Rows)
        {
            int index = row.Decimal(0, 0, 7);
            WeaponPoses[index] = row.RequiredString(1); _ = row.RequiredString(2);
        }
        if (WeaponPoses.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("ITEM$07 requires eight native weapon poses.");
        var weapon = GeneratedTable.Load(Root + "rod/weapon.tsv", new("ITEM$07 attributes", GeneratedTableKeySemantics.Ordered,
            ["tile-base", "flags", "collision", "damage", "health", "source"], headerRequired: true)).SingleRow();
        TileBase = weapon.HexByte(0); Flags = weapon.HexByte(1); Collision = weapon.HexByte(2);
        Damage = -unchecked((sbyte)weapon.HexByte(3)); Health = weapon.HexByte(4); _ = weapon.RequiredString(5);
        foreach (var row in GeneratedTable.Load(Root + "rod/native_tables.tsv", new("Rod native tables", GeneratedTableKeySemantics.Unique,
            ["name", "bytes", "source"], ["name"], headerRequired: true)).Rows)
        { _tables.Add(row.RequiredString(0), row.RequiredString(1).Split(',').Select(value => Convert.ToByte(value, 16)).ToArray()); _ = row.RequiredString(2); }
        foreach (var row in GeneratedTable.Load(Root + "rod/texts.tsv", new("Rod text", GeneratedTableKeySemantics.Unique,
            ["id", "message-base64", "source"], ["id"], headerRequired: true)).Rows)
        { _texts.Add(row.HexWord(0), row.Base64Utf8(1)); _ = row.RequiredString(2); }
    }
    internal IReadOnlyList<CutsceneCommand> Program(string name) => _programs[name];
    internal string Text(int id) => _texts[id];
    internal byte[] Table(string name) => _tables[name];
}
