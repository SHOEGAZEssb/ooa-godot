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

internal interface IPlayerInteractable
{
    bool TryInteract(Player player);
}

internal interface ISwordHittableRoomEntity
{
    bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns);
}

internal interface ISeedHittableRoomEntity
{
    SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns);
}

internal enum SeedHitResult
{
    None,
    Ignite,
    Consume
}

internal interface ISeedProjectileRoomEntity
{
    bool CollisionEnabled { get; }
    Rect2 CollisionBounds { get; }
    void OnCollision(SeedHitResult result, ISeedBurnTarget? burnTarget);
}

internal interface ISeedBurnTarget
{
    bool IsSeedBurning { get; }
    Vector2 SeedBurnPosition { get; }
    void CompleteSeedBurn(ICollection<RoomEntitySpawn> spawns);
}

internal interface IPlayerProjectileRoomEntity
{
    bool CollisionEnabled { get; }
    Rect2 CollisionBounds { get; }
    int Damage { get; }
    void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns);
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

/// <summary>
/// Contributes to the room's live wNumEnemies equivalent. Native puzzle
/// sentinels opt in alongside combat enemies.
/// </summary>
internal interface IRoomEnemyCounterEntity
{
    bool CountsAsEnemy { get; }
}

/// <summary>
/// Retains the source object's one-based enemy index from Enemy.enabled. The
/// original uses indices $01-$07 to suppress recently defeated placements.
/// </summary>
internal interface IRoomKillTrackedEnemy
{
    int KillableEnemyIndex { get; }
    bool MarksEnemyKilled { get; }
    bool CountsAsDefeat => true;
}

internal interface IPlayerRestriction
{
    bool DisablesSword { get; }
    bool DisablesItems => false;
    bool DisablesMovement => false;
    bool DisablesMenus => false;
    bool DisablesRingTransformations => false;
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
internal sealed record MaskedMoblinSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record EnemyArrowSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record GelSpawn(
    Vector2 Position,
    string Name = "Gel",
    int KillableEnemyIndex = 0)
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
    bool DugUp = false,
    bool UpdateThisFrame = false) : RoomEntitySpawn(UpdateThisFrame);
internal sealed record ShovelDebrisSpawn(Vector2 Position, Vector2I Direction)
    : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record EmberSeedSpawn(
    Vector2 LinkPosition,
    Vector2I Direction,
    SeedSatchelDatabase.SeedRecord Record,
    int Group)
    : RoomEntitySpawn;
internal sealed record PuzzlePuffSpawn(Vector2 Position, int Sound)
    : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record FallingDownHoleSpawn(Vector2 Position) : RoomEntitySpawn;
internal sealed record DungeonKeyUseSpawn(
    Vector2 Position,
    TreasureDatabase.TreasureObjectVisualRecord Visual) : RoomEntitySpawn;
internal sealed record OverworldKeyUseSpawn(
    Vector2 Position,
    OverworldKeyholeDatabase.Record Visual,
    OverworldKeyholeDatabase.ConstantsRecord Constants) : RoomEntitySpawn;
internal sealed record CutsceneNpcSpawn(
    NpcDatabase.NpcRecord Record,
    string Name,
    bool Talkable = false,
    bool Solid = false)
    : RoomEntitySpawn;
internal sealed record GroundTreasureSpawn(GroundTreasureDatabase.Record Record)
    : RoomEntitySpawn;
internal sealed record LightableTorchSpawn(
    DarkRoomState State,
    int PackedPosition)
    : RoomEntitySpawn(UpdateThisFrame: true);
internal sealed record SwordBeamSpawn(Vector2 LinkPosition, int Direction)
    : RoomEntitySpawn;
internal sealed record SwordBeamClinkSpawn(Vector2 Position)
    : RoomEntitySpawn;
