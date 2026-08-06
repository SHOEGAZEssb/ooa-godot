using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>Imported INTERAC_RAFT $e6 / SPECIALOBJECT_RAFT $13 contract.</summary>
internal sealed class RaftDatabase
{
    private readonly Lookup<int, RaftPlacement> _placements = new();

    internal RaftBehavior Behavior { get; }

    internal RaftDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/raft.tsv",
            new GeneratedTableSchema(
                "Ages raft",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "subid", "y", "x", "interaction-id",
                    "special-id", "changed-rooms-flag",
                    "dimitri-state-address", "dimitri-mask", "past-mask",
                    "radius", "speed", "knockback-speed", "dismount-collision",
                    "dismount-delay", "dismount-walk", "sprite", "waiting-tile-base",
                    "waiting-palette", "waiting-vertical",
                    "waiting-horizontal", "mounted-palette",
                    "mounted-vertical", "mounted-horizontal",
                    "mounted-vertical-offsets", "mounted-horizontal-offsets",
                    "valid-tiles", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        RaftBehavior? behavior = null;
        foreach (GeneratedTableRow row in table.Rows)
        {
            var placement = new RaftPlacement(
                row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
                row.HexByte(3), row.HexByte(4), row.RequiredString(28));
            _placements.Add((placement.Group << 8) | placement.Room, placement);
            var parsed = new RaftBehavior(
                row.HexByte(5), row.HexByte(6), row.HexByte(7), row.HexWord(8),
                row.HexByte(9), row.HexByte(10), row.HexByte(11),
                row.HexByte(12), row.HexByte(13), row.HexByte(14),
                row.HexByte(15), row.HexByte(16), row.RequiredString(17),
                row.UnsignedDecimal(18), row.UnsignedDecimal(19),
                [row.RequiredString(20), row.RequiredString(21)],
                row.UnsignedDecimal(22),
                [row.RequiredString(23), row.RequiredString(24)],
                [
                    row.RequiredString(25).Split(',').Select(value =>
                        Convert.ToInt32(value, 16)).ToArray(),
                    row.RequiredString(26).Split(',').Select(value =>
                        Convert.ToInt32(value, 16)).ToArray()
                ],
                row.RequiredString(27).Split(',').Select(value =>
                    Convert.ToInt32(value, 16)).ToArray());
            if (behavior.HasValue &&
                (behavior.Value.InteractionId != parsed.InteractionId ||
                 behavior.Value.SpecialObjectId != parsed.SpecialObjectId ||
                 behavior.Value.Sprite != parsed.Sprite ||
                 !behavior.Value.ValidTiles.SequenceEqual(parsed.ValidTiles)))
            {
                throw new InvalidOperationException("Raft behavior differs by placement.");
            }
            behavior = parsed;
        }
        Behavior = behavior ?? throw new InvalidOperationException(
            "The imported raft table is empty.");
        Validate();
    }

    internal IReadOnlyList<RaftPlacement> GetPlacements(int group, int room) =>
        _placements.ValuesOrEmpty((group << 8) | room);

    private void Validate()
    {
        IReadOnlyList<RaftPlacement> a7 = GetPlacements(1, 0xa7);
        IReadOnlyList<RaftPlacement> a9 = GetPlacements(1, 0xa9);
        if (a7.Count != 1 || a7[0] is not { SubId: 1, Y: 0x58, X: 0x78 } ||
            a9.Count != 1 || a9[0] is not { SubId: 0, Y: 0x38, X: 0x78 } ||
            Behavior is not
            {
                InteractionId: 0xe6, SpecialObjectId: 0x13,
                ChangedRoomsFlag: 0x26, DimitriStateAddress: 0xc647,
                DimitriMask: 0x40, PastTilesetMask: 0x80,
                MountRadius: 9, Speed: 0x23, KnockbackSpeed: 0x28,
                DismountCollision: 0x18, DismountDelay: 4,
                DismountWalkFrames: 14,
                Sprite: "spr_raft", WaitingTileBase: 0,
                WaitingPalette: 3, MountedPalette: 3
            } || Behavior.WaitingAnimations.Length != 2 ||
            Behavior.MountedAnimations.Length != 2 ||
            Behavior.ValidTiles is not { Length: 7 })
        {
            throw new InvalidOperationException(
                "Imported raft data diverges from the source contract.");
        }
    }
}

internal readonly record struct RaftPlacement(
    int Group, int Room, int SubId, int Y, int X, string Source);

internal readonly record struct RaftBehavior(
    int InteractionId,
    int SpecialObjectId,
    int ChangedRoomsFlag,
    int DimitriStateAddress,
    int DimitriMask,
    int PastTilesetMask,
    int MountRadius,
    int Speed,
    int KnockbackSpeed,
    int DismountCollision,
    int DismountDelay,
    int DismountWalkFrames,
    string Sprite,
    int WaitingTileBase,
    int WaitingPalette,
    string[] WaitingAnimations,
    int MountedPalette,
    string[] MountedAnimations,
    int[][] MountedSourceOffsets,
    int[] ValidTiles);
