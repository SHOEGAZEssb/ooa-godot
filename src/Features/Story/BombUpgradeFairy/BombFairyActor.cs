using Godot;

namespace oracleofages;

internal sealed class BombFairyActor(NpcCharacter actor, string kind, bool alwaysUpdate, int counter)
{
    internal NpcCharacter Actor { get; } = actor;
    internal string Kind { get; } = kind;
    internal bool AlwaysUpdate { get; } = alwaysUpdate;
    internal int State { get; set; }
    internal int Parameter { get; set; }
    internal int Counter { get; set; } = counter;
    internal bool Finished { get; private set; }
    internal void Finish() { Finished = true; if (GodotObject.IsInstanceValid(Actor)) Actor.SetActive(false); }
}
