using System;

namespace oracleofages;

// Immutable imported definitions only. Event instances, command runners,
// save bindings and counters remain owned by each RoomEventController.
internal sealed class RoomEventSourceData
{
    private static readonly Lazy<RoomEventSourceData> Source = new(() => new());
    internal static RoomEventSourceData Shared => Source.Value;
    internal ImpaIntroEventDatabase Impa { get; } = new();
    internal TokayInteractionDatabase Tokay { get; } = new();
    internal OceanInteractionDatabase Ocean { get; } = new();
    internal TokayShopDatabase TokayShop { get; } = new();
    internal WildTokayGameDatabase WildTokay { get; } = new();
    internal RemoteMakuFirstEssenceDatabase MakuFirst { get; } = new();
    internal RemoteMakuSecondEssenceDatabase MakuSecond { get; } = new();
    internal RemoteMakuThirdEssenceDatabase MakuThird { get; } = new();
    internal RemoteMakuFourthEssenceDatabase MakuFourth { get; } = new();
    internal RemoteMakuFifthEssenceDatabase MakuFifth { get; } = new();
    internal RemoteMakuSixthEssenceDatabase MakuSixth { get; } = new();
    internal RemoteMakuSeventhEssenceDatabase MakuSeventh { get; } = new();
    internal RemoteMakuEighthEssenceDatabase MakuEighth { get; } = new();
    internal RemoteMakuHarpDatabase MakuHarp { get; } = new();
    internal RemoteMakuWingDungeonDatabase MakuWing { get; } = new();
}
