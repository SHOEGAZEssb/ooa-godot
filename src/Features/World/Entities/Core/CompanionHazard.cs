using Godot;
using System;

namespace Oracles;

/// <summary>
/// Shared companionCheckHazards, companionDragToCenterOfHole, and
/// companionRespawn behavior from the special-object common code.
/// </summary>
internal struct CompanionHazard
{
    internal HazardType Type { get; private set; }
    private Vector2 Center { get; set; }

    internal static bool TryCreate(
        OracleRoomData room,
        Vector2 position,
        out CompanionHazard hazard)
    {
        Vector2 probe = position + new Vector2(0, 5);
        HazardType type = room.GetTerrainInfo(probe).Hazard;
        if (type == HazardType.None)
        {
            hazard = default;
            return false;
        }

        int packed = room.GetPackedPosition(probe);
        hazard = new CompanionHazard
        {
            Type = type,
            Center = new Vector2(
                (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                (packed >> 4) * OracleRoomData.MetatileSize + 8)
        };
        return true;
    }

    internal bool Advance(
        ref Vector2 position,
        EnemyAnimationPlayer animation,
        OracleRuntimeState memory,
        byte startedCounter,
        bool water,
        int waterAnimation,
        int holeAnimation,
        Action<int> setAnimation,
        Action<int> playSound)
    {
        if (!water && !DragToCenter(ref position))
            return false;

        if (memory.ReadWramByte(0xd106)==0)
        {
            // State4 latches counter1 to $01 (Ricky/Dimitri) or $ff (Moosh).
            memory.SetWramByte(0xd106,startedCounter);
            setAnimation(water ? waterAnimation : holeAnimation);
            if (!water)
            {
                playSound(SoundId.SndLinkFall);
                return false;
            }
        }

        animation.Advance();
        return (animation.CurrentParameter & 0x80) != 0;
    }

    internal static Vector2 ResolveRespawn(
        Player player,
        OracleSaveData saveData,
        OracleRuntimeState memory,
        ref int angle,
        Func<Vector2, bool> canRespawnAt,OracleRoomData room,Vector2 previousPosition)
    {
        memory.SetField("wDisableScreenTransitions",0);memory.SetField("wLinkForceState",0);memory.SetField("wcc50",0);
        // specialObjectSetCoordinatesToRespawnYX resets angle before validation.
        angle = 0xff;
        memory.SetWramByte(0xd109, 0xff);
        // Native copies replace high bytes; the companion's position lows survive.
        Vector2 fraction=previousPosition-OracleObjectMath.ToPixelPosition(previousPosition);
        Vector2 localRespawn = OracleObjectMath.ToPixelPosition(player.LocalRespawnPosition)+fraction;
        if(memory.Game==OracleGame.Seasons && room.GetMetatile(OracleObjectMath.ToPixelPosition(localRespawn)+new Vector2(0,5))==0x20)
        {
            RememberedCompanion remembered=CompanionRuntimeState.ReadRemembered(memory);
            localRespawn=new Vector2(remembered.X,remembered.Y)+fraction;
        }
        if (canRespawnAt(localRespawn))
            return localRespawn;

        // companionRespawn does not validate this second position.
        Vector2 lastMount =
            CompanionRuntimeState.ReadLastAnimalMountPosition(saveData);
        player.SetLocalRespawnCoordinates(lastMount);
        return lastMount+fraction;
    }

    private bool DragToCenter(ref Vector2 position)
    {
        bool centered = true;
        if (Mathf.FloorToInt(position.X) != Mathf.FloorToInt(Center.X))
        {
            position.X += position.X < Center.X ? 0.25f : -0.25f;
            centered = false;
        }
        if (Mathf.FloorToInt(position.Y) != Mathf.FloorToInt(Center.Y))
        {
            position.Y += position.Y < Center.Y ? 0.25f : -0.25f;
            centered = false;
        }
        return centered;
    }
}
