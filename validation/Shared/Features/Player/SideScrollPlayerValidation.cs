using Godot;
using System;
using System.Reflection;

namespace Oracles;

internal static class SideScrollPlayerValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo BeginAir = typeof(Player).GetMethod("BeginSideScrollAirborne", Private)!;
    private static readonly MethodInfo Advance = typeof(Player).GetMethod("AdvanceSideScrollUpdate", Private)!;
    private static readonly FieldInfo Collision = typeof(PlayerWorld).GetField("_collision", Private)!;

    internal static void AdvanceSideScrollUpdateForValidation(this Player player, Vector2 input,
        bool startJump = false, bool attackJustPressed = false, bool directionJustPressed = false)
    {
        var world = player.World;
        if (!world.SideScrolling)
            throw new InvalidOperationException("A side-scrolling Link update requires a side-scrolling tileset.");
        // These geometry-only legacy probes omit the real Link dispatcher.
        // Stage its publication through the same production collision owner.
        if (world is PlayerWorld native)
            ((RoomCollision)Collision.GetValue(native)!).UpdateSideScrollActiveTile(player.PrecisePosition);
        if (startJump)
        {
            if (player.SideScrollAirborne)
                throw new InvalidOperationException("The validation side-scrolling jump began while Link was already airborne.");
            BeginAir.Invoke(player, [true, world.SideScrollParameters, null]);
        }
        Advance.Invoke(player, [input, true, attackJustPressed, directionJustPressed]);
        player.Position = OracleObjectMath.ToPixelPosition(player.PrecisePosition);
        player.QueueRedraw();
    }
}
