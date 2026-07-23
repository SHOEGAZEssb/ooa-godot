using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal static class EnemyHazardSounds
{
    internal static void PlayHoleSound(
        HazardType hazard,
        Action<int> soundRequested)
    {
        if (hazard == HazardType.Hole)
            soundRequested(OracleSoundEngine.SndFallInHole);
    }
}
