using System.Reflection;

namespace Oracles;

internal static class AgesInteractionValidation
{
    private static readonly FieldInfo Scripts = typeof(InteractionController)
        .GetField("_npcScripts", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static AgesNpcInteractionScripts AgesScripts(this InteractionController controller) =>
        (AgesNpcInteractionScripts)Scripts.GetValue(controller)!;

    internal static InteractionController Create(
        RoomSession rooms,
        RoomEntityManager entities,
        SignDatabase signs,
        ChestDatabase chests,
        TreasureDatabase treasures,
        DialogueBox dialogue,
        Node worldRoot,
        RoomView roomView,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick,
        InventoryState inventory,
        Node interfaceLayer,
        Action<int>? playSound = null,
        Func<bool>? gashaCountersCaughtUp = null,
        IReadOnlyList<NpcInteractionHandler>? roomInteractionHandlers = null,
        Func<bool>? rupeeDisplayUpdated = null)
    {
        var scripts = new AgesNpcInteractionScripts(
            rooms, entities, dialogue, treasures, entities.FamilyStateResolver,
            inventory, rupeeDisplayUpdated ?? (static () => throw new InvalidOperationException(
                "oldManScript_takesRupees requires the status bar rupee counter.")),
            () => roomView.QueueRedraw());
        var groundTreasures = new GroundTreasureController(rooms,entities,inventory,dialogue,worldToScreen,
            playSound ?? (static _ => { }));
        worldRoot.TreeExiting += groundTreasures.Dispose;
        return new InteractionController(
            rooms, entities, signs, chests, treasures, dialogue, worldRoot,
            roomView, worldToScreen, animationTick, inventory,
            scripts, groundTreasures, playSound, gashaCountersCaughtUp, roomInteractionHandlers);
    }
}
