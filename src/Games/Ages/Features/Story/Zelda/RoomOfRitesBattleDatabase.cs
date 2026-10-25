using System;
using System.Collections.Generic;
using Godot;

namespace Oracles;

internal sealed class RoomOfRitesBattleDatabase
{
    private readonly Lookup<string, Actor> _actors = new();
    internal RoomOfRitesRitualDatabase Ritual { get; } = new();
    internal RoomOfRitesBattleDatabase()
    {
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/twinrova_flame_actors.tsv", new("Twinrova flame cutscene actors",
            GeneratedTableKeySemantics.Unique, ["scene", "order", "id", "subid", "y", "x", "source"], ["scene", "order"], headerRequired: true)).Rows)
        {
            string scene = row.RequiredString(0);
            var actors = _actors.GetOrAdd(scene);
            if (row.UnsignedDecimal(1) != actors.Count) throw row.Invalid(1, "original object stream order");
            actors.Add(new(row.HexByte(2), row.HexByte(3), new(row.HexByte(5), row.HexByte(4)), row.RequiredString(6)));
        }
        if (_actors.ValuesOrEmpty("calm").Count != 4 || _actors.ValuesOrEmpty("angry").Count != 3)
            throw new InvalidOperationException("extraData1.s: objectData4022/402f actor counts differ.");
    }
    internal IReadOnlyList<Actor> Actors(string scene) => _actors.ValuesOrEmpty(scene);
    internal readonly record struct Actor(int Id, int Subid, Vector2 Position, string Source);
}
