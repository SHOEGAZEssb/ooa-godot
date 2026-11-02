using System.Collections.Concurrent;

namespace Oracles;

/// <summary>Native staticDungeonObjects lists and the selected build's buffer layout.</summary>
internal sealed class StaticDungeonObjectDatabase
{
    public string AssetRoot { get; }

    private static readonly ConcurrentDictionary<string, Lazy<StaticDungeonObjectDatabase>> Packs = new(StringComparer.Ordinal);
    internal static StaticDungeonObjectDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<StaticDungeonObjectDatabase>(() => new(key))).Value;
    internal int Start { get; }
    internal int Slots { get; }
    internal int Dungeons => _records.Length;
    private readonly StaticObjectRecord[][] _records;

    internal StaticDungeonObjectDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        var layout = GeneratedTable.Load(AssetRoot + "objects/static_object_layout.tsv",
            new("native static object buffer", GeneratedTableKeySemantics.Ordered,
                ["start", "slots", "dungeons", "source"], headerRequired: true)).SingleRow();
        Start = layout.HexWord(0); Slots = layout.Decimal(1, 1, 16);
        _records = new StaticObjectRecord[layout.Decimal(2, 1, 16)][];
        _ = layout.RequiredString(3);
        if (Start != 0xcd80 || Start + Slots * 8 > 0xce00)
            throw layout.Invalid(0, "the native wStaticObjects buffer within $cd80-$cdff");
        var table = GeneratedTable.Load(AssetRoot + "objects/dungeon_static_objects.tsv",
            new GeneratedTableSchema("native dungeon static objects", GeneratedTableKeySemantics.Unique,
                ["dungeon", "records-base64", "source"], ["dungeon"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int dungeon = row.HexByte(0);
            if (dungeon >= _records.Length) throw row.Invalid(0, "a native static dungeon index");
            string encoded = row.RequiredString(1), source = row.RequiredString(2);
            byte[] bytes;
            try { bytes = encoded == "-" ? [] : Convert.FromBase64String(encoded); }
            catch (FormatException) { throw row.Invalid(1, "base64 six-byte static object records or '-' for an empty list"); }
            if (bytes.Length % 6 != 0 || bytes.Length > Slots * 6)
                throw row.Invalid(1, "complete six-byte records within the native static buffer");
            var records = new StaticObjectRecord[bytes.Length / 6];
            for (int slot = 0; slot < records.Length; slot++)
            {
                int at = slot * 6;
                if (bytes[at] is < 1 or > 5) throw row.Invalid(1, "native static types $01-$05");
                records[slot] = new(slot, bytes[at], bytes[at+1], bytes[at+2], bytes[at+3], bytes[at+4], bytes[at+5], source);
            }
            _records[dungeon] = records;
        }
        if (_records.Any(records => records is null))
            throw new InvalidOperationException($"{assetRoot}: incomplete staticDungeonObjects, including empty lists.");
    }

    internal IReadOnlyList<MinecartStaticRecord> Minecarts(int dungeon) =>
        Records(dungeon).Where(record => record.Type == 3 && (record.Id & 0x7f) == 0x16)
            .Select(record => new MinecartStaticRecord(record.Slot, record.Room, record.Y, record.X, record.Source)).ToArray();

    internal IReadOnlyList<StaticObjectRecord> Records(int dungeon) =>
        dungeon >= 0 && dungeon < _records.Length ? _records[dungeon] :
            throw new InvalidOperationException($"{AssetRoot}: no staticDungeonObjects table for dungeon ${dungeon:x2}.");
}

internal readonly record struct StaticObjectRecord(int Slot, int Type, int Room, int Id, int SubId, int Y, int X, string Source);
