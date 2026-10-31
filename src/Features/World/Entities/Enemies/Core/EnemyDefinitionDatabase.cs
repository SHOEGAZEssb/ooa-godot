using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Oracles;

// Native properties and animations, independent of species, rooms and story.
internal sealed class EnemyDefinitionDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<EnemyDefinitionDatabase>> Packs = new(StringComparer.Ordinal);
    internal static EnemyDefinitionDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<EnemyDefinitionDatabase>(() => new(key))).Value;
    private readonly Dictionary<(int Id, int SubId), ImportedEnemyDefinition> _definitions = new();
    internal string AssetRoot { get; }
    internal IReadOnlyDictionary<(int Id, int SubId), ImportedEnemyDefinition> Definitions => _definitions;
    internal ImportedEnemyDefinition Get(int id, int subId = 0) => _definitions.TryGetValue((id, subId), out var record)
        ? record : throw new KeyNotFoundException($"{AssetRoot}objects/common_enemies.tsv: native enemy ${id:x2}:${subId:x2} has no imported definition.");

    private EnemyDefinitionDatabase(string root)
    {
        AssetRoot = root;
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "objects/common_enemies.tsv",
            new GeneratedTableSchema(
                "common enemy definitions",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "sprites", "tile-base", "palette",
                    "source-grayscale-inverted", "radius-y", "radius-x",
                    "damage-quarters", "health", "animations-base64", "raw-damage"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            ImportedEnemyDefinition record = new ImportedEnemyDefinition(AssetRoot,
                row.HexByte(0),
                row.HexByte(1),
                row.SplitRequired(2, ','),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.Boolean01(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                row.EncodedAnimations(10), row.HexByte(11));
            if (!_definitions.TryAdd((record.Id, record.SubId), record))
            {
                throw new InvalidOperationException(
                    $"Duplicate common enemy ${record.Id:x2}:${record.SubId:x2}.");
            }
        }
        if (_definitions.Count == 0) throw new InvalidOperationException($"{root}: empty native enemy definition catalog.");
    }
}

internal readonly record struct ImportedEnemyDefinition(string AssetRoot, int Id, int SubId, string[] Sprites, int TileBase, int Palette, bool SourceGrayscaleInverted, int RadiusY, int RadiusX, int DamageQuarters, int Health, string[] Animations, int RawDamage = 0);
