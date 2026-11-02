
namespace Oracles;

/// <summary>
/// applyStandardTileSubstitutions: room-flag bits 0-3 and 7 select an imported
/// replacement list by Ages' collision mode or Seasons' room group at load.
/// </summary>
public sealed class StandardTileSubstitutionDatabase
{
    public string AssetRoot { get; }
    private readonly bool _groupProfiles;

    private readonly Dictionary<(int Flag, int Profile), Dictionary<byte, byte>>
        _substitutions = new();

    internal int RecordCount { get; }

    public StandardTileSubstitutionDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        _groupProfiles = OracleAssetPack.GameFor(assetRoot) == OracleGame.Seasons;

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "metadata/standard_tile_substitutions.tsv",
            new GeneratedTableSchema(
                "standard tile substitutions",
                GeneratedTableKeySemantics.Grouped,
                ["room-flag", "profile", "replacement", "original", "source"],
                ["room-flag", "profile"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int flag = row.HexByte(0);
            int profile = row.Decimal(1, 0, _groupProfiles ? 7 : 5);
            byte replacement = (byte)row.HexByte(2);
            byte original = (byte)row.HexByte(3);
            row.RequiredString(4);
            var key = (flag, profile);
            if (!_substitutions.TryGetValue(key, out Dictionary<byte, byte>? records))
            {
                records = new Dictionary<byte, byte>();
                _substitutions.Add(key, records);
            }
            if (!records.TryAdd(original, replacement))
            {
                throw new InvalidOperationException(
                    $"Duplicate standard tile substitution ${flag:x2}/" +
                    $"${profile:x2}:${original:x2}.");
            }
            count++;
        }
        RecordCount = count;
        if (count != (_groupProfiles ? 67 : 50))
            throw new InvalidOperationException($"standardTileSubstitutions: incomplete selected profile table ({count} rows).");
    }

    internal void Apply(OracleRoomData room, byte roomFlags, long animationTick)
    {
        foreach (int flag in new[] { 0x01, 0x02, 0x04, 0x08, 0x80 })
        {
            if ((roomFlags & flag) == 0 ||
                !_substitutions.TryGetValue(
                    (flag, _groupProfiles ? room.Group : room.ActiveCollisions),
                    out Dictionary<byte, byte>? records))
            {
                continue;
            }
            room.ApplyMetatileSubstitutions(records, animationTick);
        }
    }
}
