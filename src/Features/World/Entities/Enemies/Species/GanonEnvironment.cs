using System;

namespace oracleofages;

internal sealed record GanonEnvironment(GanonDatabase Data,RoomEntityManager Entities,
    Action<GanonCharacter> Initialize,Action<bool,int,bool> Fade,Action ClearPalette,Action<bool> StatusBar,
    Action<int> ReloadArena,Action<bool,int> BlackFade,Action FastWhiteFade,Action<int> BackgroundPalette,Action Defeated);
