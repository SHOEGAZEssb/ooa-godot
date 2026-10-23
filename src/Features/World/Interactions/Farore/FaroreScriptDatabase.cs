using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Source command streams for INTERAC_FARORE $10 in room 5:cf.</summary>
internal sealed class FaroreScriptDatabase
{
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _scripts = new();
    private readonly Dictionary<int, string> _texts = new();
    private readonly FaroreRewardRecord[] _rewards = new FaroreRewardRecord[10];

    internal FaroreScriptDatabase()
    {
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/cutscenes/farore_texts.tsv",
            new GeneratedTableSchema("Farore text", GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"], ["text-id"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        if (_texts.Count != 28) throw new InvalidOperationException("Farore TX_5501-$551c import is incomplete.");

        _scripts = CutsceneCommandCatalog.LoadPrograms(
            "res://assets/oracle/cutscenes/farore_scripts.tsv");
        if (_scripts.Count != 2 || !_scripts.ContainsKey("faroreUnlinked") ||
            !_scripts.ContainsKey("faroreLinked"))
            throw new InvalidOperationException("Farore linked/unlinked source streams are missing.");

        int rewardIndex = 0;
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/cutscenes/farore_rewards.tsv",
            new GeneratedTableSchema("Farore rewards", GeneratedTableKeySemantics.Unique,
                ["index", "treasure-symbol", "subid", "kind"], ["index"], headerRequired: true)).Rows)
        {
            if (row.HexByte(0) != rewardIndex || rewardIndex >= _rewards.Length)
                throw row.Invalid(0, "ten ordered Farore rewards $00-$09");
            string kind = row.RequiredString(3);
            if (kind is not ("chest" or "upgrade")) throw row.Invalid(3, "chest or upgrade");
            _rewards[rewardIndex++] = new(row.RequiredString(1), row.HexByte(2), kind == "upgrade");
        }
        if (rewardIndex != 10) throw new InvalidOperationException("Farore reward table is incomplete.");
    }

    internal IReadOnlyList<CutsceneCommand> Script(bool linked) =>
        _scripts[linked ? "faroreLinked" : "faroreUnlinked"];
    internal string Text(int id) => _texts.TryGetValue(id, out string? value) ? value :
        throw new InvalidOperationException($"Missing Farore TX_{id:x4}.");
    internal FaroreRewardRecord Reward(int index) => (uint)index < 10 ? _rewards[index] :
        throw new InvalidOperationException($"Invalid Farore secret index ${index:x2}.");
}

internal readonly record struct FaroreRewardRecord(string Symbol, int SubId, bool Upgrade);
