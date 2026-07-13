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

internal interface IRoomEntityLifetime
{
    bool Finished { get; }
    void OnFinished(ICollection<RoomEntitySpawn> spawns);
}

internal interface IPlayerRestriction
{
    bool DisablesSword { get; }
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
internal sealed record ItemDropSpawn(int SubId, Vector2 Position) : RoomEntitySpawn;
