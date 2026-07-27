using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IRoomEnemyOutcomeSource
{
    bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome);
}

internal readonly record struct RoomEnemyOutcome(
    RoomEnemyOutcomeKind Kind,
    bool DecrementsRoomCount,
    bool MarksRecentDefeat,
    bool AdvancesKillCounters,
    int KillableEnemyIndex)
{
    internal static RoomEnemyOutcome EnemyDie(int killableEnemyIndex) =>
        new(
            RoomEnemyOutcomeKind.EnemyDie,
            DecrementsRoomCount: false,
            MarksRecentDefeat: true,
            AdvancesKillCounters: true,
            killableEnemyIndex);

    internal static RoomEnemyOutcome EnemyDieUncounted() =>
        new(
            RoomEnemyOutcomeKind.EnemyDieUncounted,
            DecrementsRoomCount: false,
            MarksRecentDefeat: false,
            AdvancesKillCounters: true,
            KillableEnemyIndex: 0);

    internal static RoomEnemyOutcome RoomCountDecrement() =>
        new(
            RoomEnemyOutcomeKind.RoomCountDecrement,
            DecrementsRoomCount: true,
            MarksRecentDefeat: false,
            AdvancesKillCounters: false,
            KillableEnemyIndex: 0);

    internal static RoomEnemyOutcome HazardDeletion(bool decrementsRoomCount) =>
        new(
            RoomEnemyOutcomeKind.HazardDeletion,
            decrementsRoomCount,
            MarksRecentDefeat: false,
            AdvancesKillCounters: false,
            KillableEnemyIndex: 0);

    internal static RoomEnemyOutcome ReplacementDeletion(
        bool decrementsRoomCount) =>
        new(
            RoomEnemyOutcomeKind.ReplacementDeletion,
            decrementsRoomCount,
            MarksRecentDefeat: false,
            AdvancesKillCounters: false,
            KillableEnemyIndex: 0);

    internal static RoomEnemyOutcome SilentDeletion(bool decrementsRoomCount) =>
        new(
            RoomEnemyOutcomeKind.SilentDeletion,
            decrementsRoomCount,
            MarksRecentDefeat: false,
            AdvancesKillCounters: false,
            KillableEnemyIndex: 0);

    internal static RoomEnemyOutcome WallmasterSpawnerCompletion(
        int killableEnemyIndex,
        bool decrementsRoomCount) =>
        new(
            RoomEnemyOutcomeKind.WallmasterSpawnerCompletion,
            decrementsRoomCount,
            MarksRecentDefeat: true,
            AdvancesKillCounters: false,
            killableEnemyIndex);

    internal static RoomEnemyOutcome BossTeardown(int killableEnemyIndex) =>
        new(
            RoomEnemyOutcomeKind.BossTeardown,
            DecrementsRoomCount: false,
            MarksRecentDefeat: true,
            AdvancesKillCounters: false,
            killableEnemyIndex);

    internal static RoomEnemyOutcome PlacementConsumed(
        int killableEnemyIndex) =>
        new(
            RoomEnemyOutcomeKind.PlacementConsumed,
            DecrementsRoomCount: false,
            MarksRecentDefeat: true,
            AdvancesKillCounters: false,
            killableEnemyIndex);
}

internal enum RoomEnemyOutcomeKind
{
    EnemyDie,
    EnemyDieUncounted,
    RoomCountDecrement,
    HazardDeletion,
    ReplacementDeletion,
    SilentDeletion,
    WallmasterSpawnerCompletion,
    BossTeardown,
    PlacementConsumed
}
