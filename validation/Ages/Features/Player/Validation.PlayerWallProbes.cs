using Godot;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    // Component fixtures explicitly supply the probe phase that their direct
    // handler calls omit; application-loop fixtures use the actual producer.
    private static void PublishWallProbesForValidation(Player player) =>
        player.Inventory.RuntimeState.SetWramByte(0xd033,
            checked((byte)player.World.GetAdjacentWallsBitset(player.PrecisePosition)));

    private static void UpdatePushingStateForValidation(Player player, Vector2 input)
    {
        PublishWallProbesForValidation(player);
        typeof(Player).GetField("_lastMovementInput",BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player,input);
        int keys = (input.X > 0 ? 0x10 : input.X < 0 ? 0x20 : 0) | (input.Y > 0 ? 0x80 : input.Y < 0 ? 0x40 : 0);
        player.Inventory.RuntimeState.SetField("wGameKeysPressed",(byte)keys);
        player.PublishNativeWalkAnimation();
    }
}
