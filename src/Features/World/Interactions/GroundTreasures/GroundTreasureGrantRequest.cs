using System;

namespace oracleofages;

/// <summary>
/// Source-addressed construction and grant policy for a dynamically created
/// INTERAC_TREASURE $60. Positioned $dc:$07 records continue to come from the
/// generated ground-treasure table.
/// </summary>
internal readonly record struct GroundTreasureGrantRequest(
    int Group,
    int Room,
    int Order,
    int Y,
    int X,
    string TreasureObject,
    string Source)
{
    internal int SpawnMode { get; init; }
    internal int GrabMode { get; init; } = 2;
    internal int SpawnDelayFrames { get; init; }
    internal int InitialZPixels { get; init; }
    internal int BounceCount { get; init; }
    internal int Gravity { get; init; }
    internal int BounceSpeed { get; init; }
    internal int SpawnSound { get; init; }
    internal int LandingSound { get; init; }
    internal bool InitialZAboveScreen { get; init; }
    internal int AboveScreenMargin { get; init; } = 8;
    internal int AboveScreenFallback { get; init; } = -128;
    internal int TextboxFlags { get; init; }
    internal int? TextboxPosition { get; init; }
    internal int CompletionTextId { get; init; }
    internal string CompletionMessage { get; init; } = string.Empty;
    internal GroundTreasureVisualOverride? VisualOverride { get; init; }
    internal GroundTreasureInventoryWrite InventoryWrite { get; init; } =
        GroundTreasureInventoryWrite.TreasureObject;
    internal int InventoryParameter { get; init; } = -1;
    internal GroundTreasureRoomFlagTiming RoomFlagTiming { get; init; } =
        GroundTreasureRoomFlagTiming.OnActivation;
    internal GroundTreasureSoundOrder SoundOrder { get; init; } =
        GroundTreasureSoundOrder.BehaviourThenGrab;
    internal GroundTreasureDialogueTiming DialogueTiming { get; init; } =
        GroundTreasureDialogueTiming.BeforeGrab;
    internal GroundTreasureCompletionOwner CompletionOwner { get; init; } =
        GroundTreasureCompletionOwner.SharedInteraction;
    internal int ExpectedTreasureId { get; init; } = -1;
    internal int ExpectedSubId { get; init; } = -1;
    internal int ExpectedObjectParameter { get; init; } = -1;

    internal GroundTreasureDatabaseRecord Resolve(TreasureDatabase treasures)
    {
        if (Group is < 0 or > 7 || Room is < 0 or > 0xff ||
            Order < 0 || Y is < 0 or > 0xff || X is < 0 or > 0xff ||
            string.IsNullOrWhiteSpace(TreasureObject) ||
            string.IsNullOrWhiteSpace(Source))
        {
            throw new InvalidOperationException(
                $"Invalid ground-treasure grant request from {Source}.");
        }

        TreasureObjectRecord treasure = treasures.GetObject(TreasureObject);
        if (ExpectedTreasureId >= 0 &&
            (treasure.TreasureId != ExpectedTreasureId ||
             ExpectedSubId >= 0 && treasure.SubId != ExpectedSubId ||
             ExpectedObjectParameter >= 0 &&
                 treasure.Parameter != ExpectedObjectParameter))
        {
            throw new InvalidOperationException(
                $"{TreasureObject} no longer matches {Source}'s expected " +
                $"treasure ${ExpectedTreasureId:x2}:" +
                $"{Math.Max(ExpectedSubId, 0):x2} " +
                $"(loaded ${treasure.TreasureId:x2}:${treasure.SubId:x2}, " +
                $"parameter ${treasure.Parameter:x2}).");
        }

        if (!Enum.IsDefined(InventoryWrite) ||
            !Enum.IsDefined(RoomFlagTiming) ||
            !Enum.IsDefined(SoundOrder) ||
            !Enum.IsDefined(DialogueTiming) ||
            !Enum.IsDefined(CompletionOwner))
        {
            throw new InvalidOperationException(
                $"Ground treasure from {Source} has an unknown grant policy.");
        }
        if (InventoryWrite == GroundTreasureInventoryWrite.UnappraisedRing)
        {
            if (treasure.TreasureId != TreasureDatabase.TreasureRing ||
                InventoryParameter is < 0 or > 0xff)
            {
                throw new InvalidOperationException(
                    $"Ground treasure from {Source} has an invalid " +
                    "unappraised-ring inventory write.");
            }
        }
        else if (InventoryParameter != -1)
        {
            throw new InvalidOperationException(
                $"Ground treasure from {Source} supplies an inventory " +
                "parameter for an ordinary treasure write.");
        }

        GroundTreasureVisualOverride visual = VisualOverride ??
            GroundTreasureVisualOverride.From(
                treasures.GetObjectVisual(treasure.Graphic));
        return new GroundTreasureDatabaseRecord(
            Group,
            Room,
            Order,
            Y,
            X,
            treasure.Name,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            CompletionTextId,
            CompletionMessage,
            Source,
            SpawnMode,
            GrabMode,
            SpawnDelayFrames,
            InitialZPixels,
            BounceCount,
            Gravity,
            BounceSpeed,
            SpawnSound,
            LandingSound,
            InitialZAboveScreen,
            AboveScreenMargin,
            AboveScreenFallback,
            TextboxFlags,
            InventoryWrite,
            InventoryParameter,
            RoomFlagTiming,
            SoundOrder,
            DialogueTiming,
            CompletionOwner,
            TextboxPosition);
    }
}

internal readonly record struct GroundTreasureVisualOverride(
    string Sprite,
    int TileBase,
    int Palette,
    string Animation)
{
    internal static GroundTreasureVisualOverride From(
        TreasureObjectVisualRecord visual) =>
        new(visual.Sprite, visual.TileBase, visual.Palette, visual.Animation);
}

internal enum GroundTreasureInventoryWrite
{
    TreasureObject,
    UnappraisedRing
}

internal enum GroundTreasureRoomFlagTiming
{
    OnActivation,
    Never
}

internal enum GroundTreasureSoundOrder
{
    BehaviourThenGrab,
    GrabThenBehaviour
}

internal enum GroundTreasureDialogueTiming
{
    BeforeGrab,
    AfterGrab,
    None
}

internal enum GroundTreasureCompletionOwner
{
    SharedInteraction,
    Caller
}
