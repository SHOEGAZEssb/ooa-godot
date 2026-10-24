using Godot;
using System;

namespace oracleofages;

internal sealed record TwinrovaEnvironment(TwinrovaDatabase Data, RoomEntityManager Entities,
    Func<Vector2, int> CameraY, Action<int> Cutscene, Action<bool, int,int> PaletteFade,
    Action<TwinrovaCharacter> SpawnMerged, Action<int> ReloadArena)
{
    internal void Fade(bool fadeOut,int delay) => PaletteFade(fadeOut,delay,1);
}
