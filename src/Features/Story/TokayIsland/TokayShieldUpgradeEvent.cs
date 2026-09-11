using System;

namespace oracleofages;

/// <summary>
/// tokayWithShieldUpgradeScript for INTERAC_TOKAY $48:$1d.
/// </summary>
internal sealed class TokayShieldUpgradeEvent : TokayScriptEvent, IRoomEvent
{
    private TokayShieldUpgradeStage _stage;
    private NpcCharacter? _actor;
    private GroundTreasurePickup? _reward;
    private int _counter;

    internal TokayShieldUpgradeEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
        : base(context, database)
    {
    }

    public bool MenusDisabled => HasState;
    public bool HasState => _stage != TokayShieldUpgradeStage.Inactive;
    internal TokayShieldUpgradeStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record is not { Id: 0x48, SubId: 0x1d })
            return false;

        _actor = npc;
        ((TokayCharacter)npc).ScriptOwnsNativeUpdate = true;
        EventResources.LockInput();
        if (CurrentRoomFlag(OracleSaveData.RoomFlag40))
        {
            Show(0x0a69);
            _stage = TokayShieldUpgradeStage.DialogueOnly;
            return true;
        }
        Show(0x0a68);
        _stage = TokayShieldUpgradeStage.Intro;
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
        if (_stage == TokayShieldUpgradeStage.Inactive)
            return;
        if (_stage is TokayShieldUpgradeStage.GiveWait or
            TokayShieldUpgradeStage.FinalTextWait)
        {
            if (--_counter == 0)
            {
                if (_stage == TokayShieldUpgradeStage.GiveWait)
                    GiveShieldUpgrade();
                else
                {
                    Show(0x0a69);
                    _stage = TokayShieldUpgradeStage.FinalText;
                }
            }
            return;
        }
        if (_stage == TokayShieldUpgradeStage.Reward)
        {
            if (_reward is { Finished: true })
            {
                _reward = null;
                _counter = 30;
                _stage = TokayShieldUpgradeStage.FinalTextWait;
            }
            return;
        }
        if (Context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TokayShieldUpgradeStage.DialogueOnly:
            case TokayShieldUpgradeStage.FinalText:
                FinishInteraction();
                break;
            case TokayShieldUpgradeStage.Intro:
                _counter = 30;
                _stage = TokayShieldUpgradeStage.GiveWait;
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay shield-upgrade stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void Cancel()
    {
        if (_actor is TokayCharacter actor) actor.ScriptOwnsNativeUpdate = false;
        _reward?.Finish(Context.Player);
        _reward = null;
        EventResources.UnlockInput();
        _actor = null;
        _counter = 0;
        _stage = TokayShieldUpgradeStage.Inactive;
    }

    private void GiveShieldUpgrade()
    {
        int parameter = Context.Inventory.ShieldLevel < 2 ? 1 : 2;
        _actor?.SetScriptAnimation(Interactions.Animation(0x02));
        if (_actor is TokayCharacter tokay)
        {
            if (tokay.Accessory is { } accessory) accessory.Retired = true;
            tokay.SetFacingDirection(Godot.Vector2I.Down);
            tokay.NativeAnimation = TokayAnimationMode.FaceLink;
        }
        _reward = Context.GrantScriptTreasure(
            Context.Rooms.ActiveGroup,
            Context.Rooms.CurrentRoom.Id,
            TreasureDatabase.TreasureShield,
            parameter,
            $"TREASURE_OBJECT_SHIELD_{parameter:x2}",
            "scripts/ages:tokayGiveShieldUpgradeToLink",
            objectParameter: parameter + 1);
        SetCurrentRoomFlag(OracleSaveData.RoomFlag40);
        _stage = TokayShieldUpgradeStage.Reward;
    }

    private void FinishInteraction()
    {
        if (_actor is TokayCharacter actor) actor.ScriptOwnsNativeUpdate = false;
        EventResources.UnlockInput();
        _actor = null;
        _stage = TokayShieldUpgradeStage.Inactive;
    }
}

internal enum TokayShieldUpgradeStage
{
    Inactive,
    DialogueOnly,
    Intro,
    GiveWait,
    Reward,
    FinalTextWait,
    FinalText
}
