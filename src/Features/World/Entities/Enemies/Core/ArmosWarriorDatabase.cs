using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArmosWarriorDatabase
{
    private readonly Dictionary<string, byte[]> _tables = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ArmosWarriorMessage> _messages = new();

    internal ArmosWarriorDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/armos_warrior_tables.tsv",
            new GeneratedTableSchema("Armos Warrior native tables", GeneratedTableKeySemantics.Unique,
                ["profile", "values", "source"], ["profile"], headerRequired: true));
        foreach (var row in table.Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), value => Convert.ToByte(value, 16)));
        foreach (var (name, count) in new[] {
            ("shield-offsets", 4), ("sword-boxes", 32), ("sword-speeds", 4), ("parent-speeds", 3),
            ("sword-boundaries", 16), ("collision-44", 32), ("collision-60", 32),
            ("collision-61", 32), ("collision-62", 32), ("active-collisions", 32) })
            if (!_tables.TryGetValue(name, out var bytes) || bytes.Length != count)
                throw new InvalidOperationException($"ENEMY_ARMOS_WARRIOR $73: missing or incomplete {name} table.");
        if (_tables.Count != 10) throw new InvalidOperationException("Unexpected Armos Warrior table profile.");
        table = GeneratedTable.Load("res://assets/oracle/objects/armos_warrior_text.tsv",
            new GeneratedTableSchema("Armos Warrior messages", GeneratedTableKeySemantics.Unique,
                ["text-id", "position", "message-base64", "source"], ["text-id"], headerRequired: true));
        foreach (var row in table.Rows)
            _messages.Add(row.HexWord(0), new(row.Decimal(1, 0, 2), row.Base64Utf8(2), row.RequiredString(3)));
        if (_messages.Count != 2 || !_messages.ContainsKey(0x2f01) || !_messages.ContainsKey(0x2f02))
            throw new InvalidOperationException("ENEMY_ARMOS_WARRIOR $73: missing TX_2f01/TX_2f02.");
    }

    internal Vector2I ShieldOffset(int frame) => new(
        (sbyte)_tables["shield-offsets"][frame * 2 + 1], (sbyte)_tables["shield-offsets"][frame * 2]);
    internal ArmosWarriorSwordBox SwordBox(int frame)
    {
        var row = _tables["sword-boxes"].AsSpan(frame * 4, 4);
        return new(new((sbyte)row[1], (sbyte)row[0]), row[3], row[2]);
    }
    internal Vector2I SwordBoundaries(int angle)
    {
        int offset = ((angle + 2) & 0x1c) >> 1;
        return new(_tables["sword-boundaries"][offset + 1], _tables["sword-boundaries"][offset]);
    }
    internal int SwordSpeed(int counter) => _tables["sword-speeds"][(counter >> 5) & 3];
    internal int ParentSpeed(int shieldHitsRemaining) => _tables["parent-speeds"][shieldHitsRemaining];
    internal bool CollisionEnabled(int item) => _tables["active-collisions"][item] != 0;
    internal int CollisionEffect(int mode, int item) => _tables[$"collision-{mode:x2}"][item];
    internal ArmosWarriorMessage Message(int id) => _messages[id];
}

internal readonly record struct ArmosWarriorSwordBox(Vector2I Offset, int RadiusX, int RadiusY);
internal readonly record struct ArmosWarriorMessage(int Position, string Text, string Source);
