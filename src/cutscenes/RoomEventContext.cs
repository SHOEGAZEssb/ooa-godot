using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared runtime services and common operations available to room-entry
/// events. Individual event implementations keep their own state while actor
/// resolution and player-relative dialogue follow one consistent path.
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
    TreasureDatabase treasures,
    OracleSoundEngine sound)
{
    private readonly DialogueBox _dialogue = dialogue;
    private readonly Func<Vector2, Vector2> _worldToScreen = worldToScreen;

    public RoomSession Rooms { get; } = rooms;
    public RoomEntityManager Entities { get; } = entities;
    public RoomTransitionController Transitions { get; } = transitions;
    public Player Player { get; } = player;
    public RoomView RoomView { get; } = roomView;
    public Func<long> AnimationTick { get; } = animationTick;
    public CanvasLayer InterfaceLayer { get; } = interfaceLayer;
    public ColorRect Fade { get; } = fade;
    public Hud Hud { get; } = hud;
    public InventoryState Inventory { get; } = inventory;
    public TreasureDatabase Treasures { get; } = treasures;
    public OracleSoundEngine Sound { get; } = sound;
    public bool DialogueOpen => _dialogue.IsOpen;
    internal ICutsceneCommandTraceSink? CommandTraceSink { get; set; }

    public NpcCharacter RequireNpc(
        int group,
        int room,
        int interactionId,
        int subId,
        string interactionName)
    {
        foreach (NpcCharacter npc in Entities.Entities<NpcCharacter>())
        {
            if (Matches(npc, interactionId, subId))
                return npc;
        }

        throw new InvalidOperationException(
            $"Room {group:x}:{room:x2} did not instantiate " +
            $"{interactionName} ${interactionId:x2}:${subId:x2}.");
    }

    public void DeactivateNpcs(int interactionId, int subId)
    {
        foreach (NpcCharacter npc in Entities.Entities<NpcCharacter>())
        {
            if (Matches(npc, interactionId, subId))
                npc.SetActive(false);
        }
    }

    public void ShowDialogue(string message, int? textboxPosition = null)
    {
        float playerScreenY = _worldToScreen(Player.Position).Y;
        if (textboxPosition.HasValue)
            _dialogue.ShowMessage(message, playerScreenY, textboxPosition.Value);
        else
            _dialogue.ShowMessage(message, playerScreenY);
    }

    private static bool Matches(NpcCharacter npc, int interactionId, int subId) =>
        npc.Record.Id == interactionId && npc.Record.SubId == subId;
}

internal interface IRoomEvent
{
    bool HasState { get; }
    bool BlocksGameplay { get; }
    void UpdateFrame();
    void Cancel();
}
