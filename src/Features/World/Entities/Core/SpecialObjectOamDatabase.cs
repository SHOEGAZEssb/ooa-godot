using System;
using System.Collections.Concurrent;

namespace Oracles;

internal sealed class SpecialObjectOamDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<SpecialObjectOamDatabase>> Packs = new(StringComparer.Ordinal);
    internal static SpecialObjectOamDatabase For(string root) => Packs.GetOrAdd(root,
        static pack => new Lazy<SpecialObjectOamDatabase>(() => new(pack))).Value;
    private readonly OracleGame _game;
    private readonly (byte TileBase, byte Flags)[] _attributes = new (byte, byte)[0x14];

    private SpecialObjectOamDatabase(string root)
    {
        _game = OracleAssetPack.GameFor(root);
        var rows = GeneratedTable.Load(root + "metadata/special_object_oam.tsv",
            new("specialObjectSetOamVariables", GeneratedTableKeySemantics.Ordered,
                ["id", "tile-base", "flags", "source"], headerRequired: true)).Rows;
        if (rows.Count != _attributes.Length)
            throw new InvalidOperationException("commonCode.s:specialObjectSetOamVariables requires native IDs$00-$13.");
        for (int id = 0; id < rows.Count; id++)
        {
            var row = rows[id];
            if (row.HexByte(0) != id) throw row.Invalid(0, "native special-object ID order");
            _attributes[id] = (checked((byte)row.HexByte(1)), checked((byte)row.HexByte(2))); _ = row.RequiredString(3);
        }
    }

    internal void InitializeCompanion(OracleRuntimeState memory)
    {
        if (memory.Game != _game) throw new ArgumentException("Native OAM initialization requires the selected game's pack.", nameof(memory));
        int id = memory.ReadWramByte(0xd101);
        if ((uint)id >= _attributes.Length)
            throw new NotSupportedException($"commonCode.s:specialObjectSetOamVariables: SPECIALOBJECT ${id:x2} has no native attribute row.");
        var attributes = _attributes[id];
        memory.SetWramByte(0xd132, 0xff);
        memory.SetWramByte(0xd11d, attributes.TileBase);
        memory.SetWramByte(0xd11c, attributes.Flags);
        memory.SetWramByte(0xd11b, attributes.Flags);
    }
}
