using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal interface IRoomEntity
{
    Node2D Node { get; }
    void SetTransitionDrawOffset(Vector2 offset);
}

internal interface IVariableRoomEntity
{
    void Update(double delta, Player player);
}

internal interface IFixedRoomEntity
{
    void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns);
}

internal interface ILinkContactEntity
{
    void HandleLinkContact(Player player);
}

internal interface ISwordHittableRoomEntity
{
    bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns);
}

internal interface IRoomBlocker
{
    bool BlocksLink(Vector2 linkCenter);
}

internal interface ITalkTarget
{
    NpcCharacter? FindTalkTarget(Player player);
}

internal interface INpcTalkLifecycle
{
    NpcCharacter TalkNpc { get; }
    void OnNpcTalkStarted();
    void OnNpcTalkEnded();
}

/// <summary>
/// Identifies ordinary placed NPCs whose imported save predicates may be
/// refreshed live. Script-created cutscene actors deliberately do not opt in.
/// </summary>
internal interface IOrdinaryNpcEntity
{
    NpcCharacter Npc { get; }
}

internal interface IRoomEntityLifetime
{
    bool Finished { get; }
    void OnFinished(ICollection<RoomEntitySpawn> spawns);
}

internal interface IPlayerRestriction
{
    bool DisablesSword { get; }
    bool DisablesMovement => false;
}

internal interface IRoomSaveStateEntity
{
    void RefreshSaveState();
}

internal readonly record struct RoomEntityFrame(
    Player Player,
    int Counter,
    bool AnyButtonJustPressed);

internal abstract record RoomEntitySpawn(bool UpdateThisFrame = false);
internal sealed record OctorokRockSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record GelSpawn(Vector2 Position, string Name = "Gel")
    : RoomEntitySpawn;
internal sealed record EnemyDeathPuffSpawn(
    Vector2 Position,
    bool HighKnockback = false,
    int EnemyId = -1) : RoomEntitySpawn;
internal sealed record KillEnemyPuffSpawn(Vector2 Position) : RoomEntitySpawn;
internal sealed record ItemDropSpawn(
    int SubId,
    Vector2 Position,
    int Angle = 0,
    bool DugUp = false) : RoomEntitySpawn;
internal sealed record ShovelDebrisSpawn(Vector2 Position, Vector2I Direction)
    : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record CutsceneNpcSpawn(
    NpcDatabase.NpcRecord Record,
    string Name,
    bool Talkable = false,
    bool Solid = false)
    : RoomEntitySpawn;
