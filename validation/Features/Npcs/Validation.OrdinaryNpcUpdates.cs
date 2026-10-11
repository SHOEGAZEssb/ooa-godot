using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // These owner-level fixtures select through the real router and then run
    // the actor's update slot, where genericNpcScript consumes pressed A.
    // Gameplay/ROM scenarios supply host input through Application instead.
    private static bool SelectOrdinaryNpcAndRunObjectPass(
        InteractionController interactions, RoomEntityManager entities, Player player)
    {
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(
            pressed: [], justPressed: [], movement: Vector2.Zero));
        try
        {
            if (!interactions.TryInteract(player)) return false;
            entities.Update(1.0 / 60.0, player);
            return true;
        }
        finally
        {
            Input.EndOriginalUpdate();
        }
    }
}
