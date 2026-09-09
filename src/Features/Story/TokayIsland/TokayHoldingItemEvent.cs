using System;

namespace oracleofages;

/// <summary>
/// tokayHoldingItemScript for INTERAC_TOKAY $48:$06-$0a.
/// </summary>
internal sealed class TokayHoldingItemEvent : TokayScriptEvent, IRoomEvent
{
    private TokayHoldingItemStage _stage;
    private NpcCharacter? _actor;
    private GroundTreasurePickup? _reward;
    private int _counter;

    internal TokayHoldingItemEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
        : base(context, database)
    {
    }

    public bool MenusDisabled => HasState;
    public bool HasState => _stage != TokayHoldingItemStage.Inactive;
    internal TokayHoldingItemStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active ||
            npc.Record.Id != 0x48 || npc.Record.SubId is < 0x06 or > 0x0a)
        {
            return false;
        }

        _actor = npc;
        ((TokayCharacter)npc).ScriptOwnsNativeUpdate = true;
        bool returned = Context.Rooms.SaveData.HasRoomFlag(
            npc.Record.Group, npc.Record.Room, OracleSaveData.RoomFlag40);
        if (returned)
        {
            Show(((TokayCharacter)npc).ReturnedItemDialogue);
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
        var actor = _actor as TokayCharacter;
        UpdateScript();
        actor?.RunNativeUpdate(Context.Player);
    }

    private void UpdateScript()
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
        if (Context.DialogueOpen)
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
        if (_actor is TokayCharacter actor) actor.ScriptOwnsNativeUpdate = false;
        _reward?.Finish(Context.Player);
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
        TokayHeldItemRecord item = Interactions.HeldItem(actor.Record.SubId);
        actor.SetScriptAnimation(Interactions.Animation(0x02));
        // writeobjectbyte Interaction.var3b,$01 deletes the related accessory
        // before tokayGiveItemToLink creates the treasure interaction.
        actor.RemoveHeldItem();
        actor.SetFacingDirection(Godot.Vector2I.Down);
        actor.NativeAnimation = TokayAnimationMode.FaceLink;
        if (item.Treasure == TreasureDatabase.TreasureSeedSatchel)
            Context.Inventory.PrepareReturnedTokaySeedSatchel();
        _reward = Context.GrantScriptTreasure(
            Context.Rooms.ActiveGroup,
            Context.Rooms.CurrentRoom.Id,
            item.Treasure,
            item.GrantSubId,
            item.GrantObject,
            "scripts/ages:tokayGiveItemToLink",
            objectParameter: item.GrantParameter);
        _stage = TokayHoldingItemStage.Reward;
    }

    private void FinishInteraction()
    {
        if (_actor is TokayCharacter actor) actor.ScriptOwnsNativeUpdate = false;
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
