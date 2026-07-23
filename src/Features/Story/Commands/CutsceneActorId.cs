using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Stable importer-owned actor identifier. Keeping this distinct from arbitrary
/// strings lets a host validate every binding before a command stream starts.
/// </summary>
internal readonly record struct CutsceneActorId
{
    public string Value { get; }

    public CutsceneActorId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A cutscene actor identifier cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;

    public static implicit operator CutsceneActorId(string value) => new(value);
    public static implicit operator string(CutsceneActorId actor) => actor.Value;
}
