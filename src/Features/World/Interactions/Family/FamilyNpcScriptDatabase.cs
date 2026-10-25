using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>The imported Ages household talk programs and their text chains.</summary>
internal sealed class FamilyNpcScriptDatabase
{
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _scripts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<int, string> _animations = new();
    private readonly Dictionary<(int Id, int Index), string> _actorScripts = new();

    internal FamilyNpcScriptDatabase()
    {
        GeneratedTable texts = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "cutscenes/family_npc_texts.tsv",
            new GeneratedTableSchema("family script texts",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"], ["text-id"], headerRequired: true));
        foreach (GeneratedTableRow row in texts.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));

        GeneratedTable animations = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "cutscenes/family_npc_animations.tsv",
            new GeneratedTableSchema("family Bipin animations",
                GeneratedTableKeySemantics.Unique,
                ["animation", "encoded-data"], ["animation"], headerRequired: true));
        foreach (GeneratedTableRow row in animations.Rows)
            _animations.Add(row.HexByte(0), row.Base64Utf8(1));

        _scripts = CutsceneCommandCatalog.LoadPrograms(
            AgesGameContent.AssetRoot + "cutscenes/family_npc_scripts.tsv");
        if (_scripts.Count != 33)
            throw new InvalidOperationException($"Expected 33 household scripts, got {_scripts.Count}.");

        GeneratedTable actorScripts = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "cutscenes/family_actor_scripts.tsv",
            new GeneratedTableSchema("native family actor script tables",
                GeneratedTableKeySemantics.Unique,
                ["id", "index", "script"], ["id", "index"], headerRequired: true));
        foreach (GeneratedTableRow row in actorScripts.Rows)
            _actorScripts.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        if (_actorScripts.Count != 50)
            throw new InvalidOperationException(
                $"Expected 50 native family script slots, got {_actorScripts.Count}.");
    }

    internal IReadOnlyList<CutsceneCommand> Script(NpcRecord record)
    {
        string name = ScriptName(record);
        return _scripts.TryGetValue(name, out IReadOnlyList<CutsceneCommand>? commands)
            ? commands
            : throw new InvalidOperationException($"Missing imported family script {name}.");
    }

    internal bool HasScript(NpcRecord record) => ScriptName(record) switch
    {
        "blossomScript0" or "childScript00" => false,
        _ => true
    };

    internal string Text(int textId) => _texts.TryGetValue(textId, out string? text)
        ? text
        : throw new InvalidOperationException($"Family script requested missing TX_{textId:x4}.");

    internal string Animation(int animation) => _animations.TryGetValue(animation,
        out string? encoded) ? encoded : throw new InvalidOperationException(
            $"Family script requested missing Bipin animation ${animation:x2}.");

    private string ScriptName(NpcRecord record)
    {
        int index = record.Id == InteractionId.Child ? record.Var03 : record.SubId;
        return _actorScripts.TryGetValue((record.Id, index), out string? script)
            ? script : throw new InvalidOperationException(
                $"No native family script slot for {record.Group}:{record.Room:x2} " +
                $"${record.Id:x2} index ${index:x2}.");
    }
}
