using System;

namespace oracleofages;

public sealed class GlobalFlagDatabase
{
    private const string Prefix = "GLOBALFLAG_";
    private readonly string[] _names = new string[OracleSaveData.GlobalFlagCount];

    public GlobalFlagDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/global_flags.tsv",
            new GeneratedTableSchema(
                "global flags",
                GeneratedTableKeySemantics.Unique,
                ["flag", "name"],
                ["flag"]));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int flag = row.HexByte(0);
            if (flag >= _names.Length || _names[flag] is not null)
                throw new InvalidOperationException($"Invalid or duplicate global flag ${flag:x2}.");
            _names[flag] = row.RequiredString(1);
            count++;
        }

        if (count != OracleSaveData.GlobalFlagCount || Array.Exists(_names, name => name is null))
            throw new InvalidOperationException(
                $"Expected {OracleSaveData.GlobalFlagCount} imported global flags, loaded {count}.");
    }

    public string GetName(int flag)
    {
        if (flag < 0 || flag >= _names.Length)
            throw new ArgumentOutOfRangeException(nameof(flag));
        return _names[flag];
    }

    public string GetShortName(int flag)
    {
        string name = GetName(flag);
        return name.StartsWith(Prefix, StringComparison.Ordinal) ? name[Prefix.Length..] : name;
    }
}
