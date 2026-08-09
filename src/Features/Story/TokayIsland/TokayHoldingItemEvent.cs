using System;

namespace oracleofages;

/// <summary>
/// tokayHoldingItemScript for INTERAC_TOKAY $48:$06-$0a.
/// </summary>
internal sealed class TokayHoldingItemEvent : IRoomEvent
{
    private static readonly int[] RecoveredItems =
    [
        TreasureDatabase.TreasureSword,
        TreasureDatabase.TreasureShovel,
        TreasureDatabase.TreasureHarp,
        0x2e,
        TreasureDatabase.TreasureSeedSatchel,
        TreasureDatabase.TreasureBombs,
        TreasureDatabase.TreasureBracelet,
        TreasureDatabase.TreasureFeather
    ];

    private readonly RoomEventContext _context;
    private readonly TokayInteractionDatabase _database;
    private TokayHoldingItemStage _stage;
    private NpcCharacter? _actor;
    private GroundTreasurePickup? _reward;
    private int _counter;
    private bool _inputLocked;

    internal TokayHoldingItemEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _stage != TokayHoldingItemStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal TokayHoldingItemStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active ||
            npc.Record.Id != 0x48 || npc.Record.SubId is < 0x06 or > 0x0a)
        {
            return false;
        }

        _actor = npc;
        bool returned = _context.Rooms.SaveData.HasRoomFlag(
            npc.Record.Group, npc.Record.Room, OracleSaveData.RoomFlag40);
        if (returned)
        {
            Show(AllRecovered() ? 0x0a0d : 0x0a0c);
            _stage = TokayHoldingItemStage.DialogueOnly;
            return true;
        }

        LockInput();
        Show(npc.Record.SubId == 0x07 ? 0x0a0a : 0x0a0b);
        _stage = TokayHoldingItemStage.Intro;
        return true;
    }

    public void UpdateFrame()
    {
        if (_stage == TokayHoldingItemStage.Inactive)
            return;
        if (_stage is TokayHoldingItemStage.GiveWait or TokayHoldingItemStage.FinalWait)
        {
            if (--_counter == 0)
            {
                if (_stage == TokayHoldingItemStage.GiveWait)
                    GiveHeldItem();
                else
                {
                    Show(0x0a0c);
                    _stage = TokayHoldingItemStage.FinalText;
                }
            }
            return;
        }
        if (_stage == TokayHoldingItemStage.Reward)
        {
            if (_reward is { Finished: true })
            {
                _reward = null;
                _counter = 30;
                _stage = TokayHoldingItemStage.FinalWait;
            }
            return;
        }
        if (_context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TokayHoldingItemStage.DialogueOnly:
                FinishInteraction();
                break;
            case TokayHoldingItemStage.Intro:
                _counter = 30;
                _stage = TokayHoldingItemStage.GiveWait;
                break;
            case TokayHoldingItemStage.FinalText:
                SetCurrentRoomFlag(OracleSaveData.RoomFlag40);
                FinishInteraction();
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay holding-item stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void Cancel()
    {
        _reward?.Finish(_context.Player);
        _reward = null;
        UnlockInput();
        _actor = null;
        _counter = 0;
        _stage = TokayHoldingItemStage.Inactive;
    }

    private void GiveHeldItem()
    {
        TokayHoldingItemCharacter actor = _actor as TokayHoldingItemCharacter ??
            throw new InvalidOperationException(
                "tokayHoldingItemScript lost its actor.");
        TokayHeldItemRecord item = _database.HeldItem(actor.Record.SubId);
        actor.SetScriptAnimation(_database.Animation(0x02));
        // writeobjectbyte Interaction.var3b,$01 deletes the related accessory
        // before tokayGiveItemToLink creates the treasure interaction.
        actor.RemoveHeldItem();
        if (item.Treasure == TreasureDatabase.TreasureSeedSatchel)
            _context.Inventory.PrepareReturnedTokaySeedSatchel();
        _reward = _context.GrantScriptTreasure(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom.Id,
            item.Treasure,
            item.GrantSubId,
            item.GrantObject,
            "scripts/ages:tokayGiveItemToLink",
            objectParameter: item.GrantParameter);
        _stage = TokayHoldingItemStage.Reward;
    }

    private bool AllRecovered()
    {
        foreach (int treasure in RecoveredItems)
            if (!_context.Inventory.HasTreasure(treasure))
                return false;
        return true;
    }

    private void SetCurrentRoomFlag(byte flag) =>
        _context.Rooms.SaveData.SetRoomFlag(
            _context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id, flag);

    private void Show(int textId) =>
        _context.ShowDialogue(_database.Text(textId));

    private void LockInput()
    {
        _context.Player.BeginCutsceneControl();
        _inputLocked = true;
    }

    private void UnlockInput()
    {
        if (!_inputLocked)
            return;
        _context.Player.EndCutsceneControl();
        _inputLocked = false;
    }

    private void FinishInteraction()
    {
        UnlockInput();
        _actor = null;
        _stage = TokayHoldingItemStage.Inactive;
    }
}

internal enum TokayHoldingItemStage
{
    Inactive,
    DialogueOnly,
    Intro,
    GiveWait,
    Reward,
    FinalWait,
    FinalText
}
