using Godot;
using System;

namespace Oracles;

// The INTERAC_TREASURE slot owns grants, held updates and deletion. This
// session service connects its text/heart-container handoff in either game.
internal sealed class GroundTreasureController : IDisposable
{
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly InventoryState _inventory;
    private readonly TreasureDatabase _treasures;
    private readonly DialogueBox _dialogue;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Action<int> _playSound;
    private GroundTreasurePickup? _active;
    private Player? _player;
    private bool _completesHeartContainer, _showingHeartContainer, _disposed;

    internal GroundTreasurePickup? Active
    {
        get
        {
            // Deletion happens in the interaction walk, independently of the
            // game's later NPC host. Only the managed dialogue lease retires here.
            if (_active is { Finished: true }) Clear();
            return _active;
        }
    }

    internal GroundTreasureController(RoomSession rooms, RoomEntityManager entities,
        InventoryState inventory, DialogueBox dialogue, Func<Vector2, Vector2> worldToScreen,
        Action<int> playSound)
    {
        _rooms = rooms; _entities = entities; _inventory = inventory; _dialogue = dialogue;
        _treasures = TreasureDatabase.For(rooms.AssetRoot);
        _worldToScreen = worldToScreen; _playSound = playSound;
        dialogue.SetHeartPieceCountProvider(() => inventory.HeartPieces);
        dialogue.HeartPieceSetFilled += HeartPieceSetFilled;
        dialogue.HeartPieceSetAccepted += HeartPieceSetAccepted;
        rooms.RoomChanged += RoomChanged;
        entities.GroundTreasureCollected += Collected;
        entities.GroundTreasureDialogueRequested += ShowDialogue;
        entities.GroundTreasureCollectionAllowed = () => !dialogue.BlocksPlayerInput && Active is null;
    }

    private void Collected(GroundTreasurePickup treasure, Player player)
    {
        if (treasure.Record.GrabMode == TreasureGrabMode.NoChange ||
            treasure.Record.CompletionOwner == GroundTreasureCompletionOwner.Caller) return;
        if (Active is not null)
            throw new InvalidOperationException($"INTERAC_TREASURE $60 at {treasure.Record.Source}: another reward is active.");
        _completesHeartContainer = _treasures.GetObject(treasure.Record.TreasureObject).TreasureId == TreasureId.HeartPiece &&
            _inventory.HeartPieces == 4;
        _showingHeartContainer = false; _active = treasure; _player = player;
        treasure.BindCompletion(() => !_dialogue.IsOpen && (!_completesHeartContainer || _showingHeartContainer));
    }

    private void ShowDialogue(GroundTreasurePickup treasure, TreasureObjectRecord record, Player player)
    {
        if (treasure.Record.TextboxFlags != 0)
            _dialogue.ShowGameplayMessageWithFlags(record.Message, _worldToScreen(player.Position).Y,
                treasure.Record.TextboxFlags, treasure.Record.TextboxPosition);
        else if (treasure.Record.TextboxPosition is int position)
            _dialogue.ShowGameplayMessage(record.Message, _worldToScreen(player.Position).Y, position);
        else
            _dialogue.ShowGameplayMessage(record.Message, _worldToScreen(player.Position).Y);
    }

    private void HeartPieceSetFilled()
    {
        if (Active is not null && _completesHeartContainer && !_showingHeartContainer)
            _inventory.ResetCompletedHeartPieceSet();
    }

    private void HeartPieceSetAccepted()
    {
        if (Active is not { } treasure || !_completesHeartContainer || _showingHeartContainer) return;
        _inventory.GiveCompletedHeartContainer(_treasures.GetObject("TREASURE_OBJECT_HEART_CONTAINER_00"));
        _playSound(SoundId.SndFilledHeartContainer);
        _dialogue.ShowGameplayMessage(treasure.Record.CompletionMessage, _worldToScreen(_player!.Position).Y);
        _showingHeartContainer = true;
    }

    private void RoomChanged(int group, OracleRoomData room) => Cancel();

    private void Cancel()
    {
        if (_active is { Finished: false } treasure && _player is { } player) treasure.Finish(player);
        Clear();
    }

    private void Clear()
    {
        _active = null; _player = null; _completesHeartContainer = _showingHeartContainer = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dialogue.HeartPieceSetFilled -= HeartPieceSetFilled;
        _dialogue.HeartPieceSetAccepted -= HeartPieceSetAccepted;
        _rooms.RoomChanged -= RoomChanged;
        _entities.GroundTreasureCollected -= Collected;
        _entities.GroundTreasureDialogueRequested -= ShowDialogue;
        Cancel();
    }
}
