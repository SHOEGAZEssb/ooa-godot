using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared companionCheckHazards, companionDragToCenterOfHole, and
/// companionRespawn behavior from the special-object common code.
/// </summary>
internal struct CompanionHazard
{
    internal HazardType Type { get; private set; }
    private Vector2 Center { get; set; }
    private bool AnimationStarted { get; set; }

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
        int waterAnimation,
        int holeAnimation,
        Action<int> setAnimation,
        Action<int> playSound)
    {
        bool water = Type == HazardType.Water;
        if (!water && !DragToCenter(ref position))
            return false;

        if (!AnimationStarted)
        {
            AnimationStarted = true;
            setAnimation(water ? waterAnimation : holeAnimation);
            if (!water)
            {
                playSound(OracleSoundEngine.SndLinkFall);
                return false;
            }
        }

        animation.Advance();
        return (animation.CurrentParameter & 0x80) != 0;
    }

    internal static Vector2 ResolveRespawn(
        Player player,
        OracleRuntimeState runtime,
        Func<Vector2, bool> canRespawnAt)
    {
        Vector2 localRespawn = player.LocalRespawnPosition;
        if (canRespawnAt(localRespawn))
            return localRespawn;

        // companionRespawn does not validate this second position.
        Vector2 lastMount =
            CompanionRuntimeState.ReadLastAnimalMountPosition(runtime);
        player.SetLocalRespawnCoordinates(lastMount);
        return lastMount;
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
