using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Owns the dynamic named actors used by the Nayru intro, including spawning,
/// required and reverse lookup, animation-template aliases, visibility, and
/// lifecycle cleanup across its temporary room loads.
/// </summary>
internal sealed class NayruActorRegistry : IReadOnlyDictionary<string, NpcCharacter>
{
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly NayruIntroEventDatabase _database;
    private readonly Dictionary<string, NpcCharacter> _actors = new();
    private readonly Dictionary<NpcCharacter, string> _names = new();

    public NayruActorRegistry(
        RoomSession rooms,
        RoomEntityManager entities,
        NayruIntroEventDatabase database)
    {
        _rooms = rooms;
        _entities = entities;
        _database = database;
    }

    public NpcCharacter this[string name] => _actors.TryGetValue(name, out NpcCharacter? actor)
        ? actor
        : throw new InvalidOperationException(
            $"Nayru intro actor '{name}' is not registered in the current scene.");

    public IEnumerable<string> Keys => _actors.Keys;
    public IEnumerable<NpcCharacter> Values => _actors.Values;
    public int Count => _actors.Count;

    public bool ContainsKey(string name) => _actors.ContainsKey(name);

    public bool TryGetValue(string name, out NpcCharacter actor) =>
        _actors.TryGetValue(name, out actor!);

    public bool TryGetActive(string name, out NpcCharacter actor)
    {
        if (TryGetValue(name, out actor) && actor.Active)
            return true;
        actor = null!;
        return false;
    }

    public string? NameOf(NpcCharacter actor) =>
        _names.TryGetValue(actor, out string? name) ? name : null;

    public NpcCharacter Spawn(
        string template,
        string name,
        Vector2? position = null,
        bool solid = false)
    {
        NayruIntroEventDatabase.ActorRecord actor = _database.Actor(template);
        NpcCharacter npc = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            actor.ToNpcRecord(_rooms.ActiveGroup, _rooms.CurrentRoom.Id),
            $"NayruIntro_{name}", Solid: solid));
        ApplyExtraGraphics(npc, actor);
        if (position.HasValue)
            npc.Position = position.Value;
        Register(name, npc);
        return npc;
    }

    public NpcCharacter SpawnAudience(string name, int textId, int group, int room)
    {
        NayruIntroEventDatabase.ActorRecord actor = _database.Actor(name);
        NayruIntroEventDatabase.TextRecord text = _database.Text(textId);
        NpcDatabase.NpcRecord record = actor.ToNpcRecord(group, room) with
        {
            TextId = textId,
            Message = text.Message
        };
        NpcCharacter npc = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            record, $"NayruIntro_{name}", Talkable: true, Solid: true));
        ApplyExtraGraphics(npc, actor);
        npc.SetScriptAnimation(actor.Animation(actor.InitialAnimation));
        Register(name, npc);
        return npc;
    }

    public void Register(string name, NpcCharacter actor)
    {
        if (_actors.TryGetValue(name, out NpcCharacter? previous))
            _names.Remove(previous);
        if (_names.TryGetValue(actor, out string? previousName) && previousName != name)
            _actors.Remove(previousName);
        _actors[name] = actor;
        _names[actor] = name;
    }

    public string AnimationSource(string name, int animation)
    {
        string template = name switch
        {
            "Impa" or "AftermathImpa" => "AftermathImpa",
            _ when name.StartsWith("VignetteMonkey", StringComparison.Ordinal) => "Monkey",
            _ => name
        };
        return _database.Actor(template).Animation(animation);
    }

    public void SetAnimation(string name, int animation)
    {
        if (!TryGetValue(name, out NpcCharacter actor) || !actor.Active)
            return;
        actor.SetScriptAnimation(AnimationSource(name, animation));
    }

    public void SetAnimationIfChanged(string name, int animation)
    {
        if (!TryGetValue(name, out NpcCharacter actor) || !actor.Active)
            return;
        string source = AnimationSource(name, animation);
        if (actor.CurrentScriptAnimationSource != source)
            actor.SetScriptAnimation(source);
    }

    public bool IsUsingAnimation(string name, int animation) =>
        TryGetValue(name, out NpcCharacter actor) &&
        actor.CurrentScriptAnimationSource == AnimationSource(name, animation);

    public void Hide(string name)
    {
        if (TryGetValue(name, out NpcCharacter actor))
            actor.SetActive(false);
    }

    public void Clear(bool deactivateActors)
    {
        if (deactivateActors)
        {
            foreach (NpcCharacter actor in _actors.Values)
            {
                actor.SetScriptDrawOffset(Vector2.Zero);
                actor.SetActive(false);
            }
        }
        _actors.Clear();
        _names.Clear();
    }

    public IEnumerator<KeyValuePair<string, NpcCharacter>> GetEnumerator() =>
        _actors.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static void ApplyExtraGraphics(
        NpcCharacter npc,
        NayruIntroEventDatabase.ActorRecord actor)
    {
        if (!string.IsNullOrEmpty(actor.ExtraSprite))
            npc.AppendScriptGraphics(actor.ExtraSprite);
    }
}
