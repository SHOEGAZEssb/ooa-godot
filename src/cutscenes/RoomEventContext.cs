using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared runtime services available to room-entry events. Keeping these
/// dependencies in one immutable context lets individual event implementations
/// own their state without turning the coordinator into a service locator.
/// </summary>
internal sealed class RoomEventContext(
    RoomSession rooms,
    RoomEntityManager entities,
    RoomTransitionController transitions,
    DialogueBox dialogue,
    Player player,
    RoomView roomView,
    Func<Vector2, Vector2> worldToScreen,
    Func<long> animationTick,
    CanvasLayer interfaceLayer,
    ColorRect fade,
    Hud hud,
    InventoryState inventory,
    TreasureDatabase treasures)
{
    public RoomSession Rooms { get; } = rooms;
    public RoomEntityManager Entities { get; } = entities;
    public RoomTransitionController Transitions { get; } = transitions;
    public DialogueBox Dialogue { get; } = dialogue;
    public Player Player { get; } = player;
    public RoomView RoomView { get; } = roomView;
    public Func<Vector2, Vector2> WorldToScreen { get; } = worldToScreen;
    public Func<long> AnimationTick { get; } = animationTick;
    public CanvasLayer InterfaceLayer { get; } = interfaceLayer;
    public ColorRect Fade { get; } = fade;
    public Hud Hud { get; } = hud;
    public InventoryState Inventory { get; } = inventory;
    public TreasureDatabase Treasures { get; } = treasures;
}

internal interface IRoomEvent
{
    bool HasState { get; }
    bool BlocksGameplay { get; }
    void UpdateFrame();
    void Cancel();
}
