using System;

namespace oracleofages;

/// <summary>
/// tokayWithShieldUpgradeScript for INTERAC_TOKAY $48:$1d.
/// </summary>
internal sealed class TokayShieldUpgradeEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayIslandDatabase _database;
    private TokayShieldUpgradeStage _stage;
    private NpcCharacter? _actor;
    private GroundTreasurePickup? _reward;
    private int _counter;
    private bool _inputLocked;

    internal TokayShieldUpgradeEvent(
        RoomEventContext context,
        TokayIslandDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _stage != TokayShieldUpgradeStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal TokayShieldUpgradeStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record is not { Id: 0x48, SubId: 0x1d })
            return false;

        _actor = npc;
        if (CurrentRoomFlag(OracleSaveData.RoomFlag40))
        {
            Show(0x0a69);
            _stage = TokayShieldUpgradeStage.DialogueOnly;
            return true;
        }
        LockInput();
        Show(0x0a68);
        _stage = TokayShieldUpgradeStage.Intro;
        return true;
    }

    public void UpdateFrame()
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
                SetCurrentRoomFlag(OracleSaveData.RoomFlag40);
                _counter = 30;
                _stage = TokayShieldUpgradeStage.FinalTextWait;
            }
            return;
        }
        if (_context.DialogueOpen)
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
        _reward?.Finish(_context.Player);
        _reward = null;
        UnlockInput();
        _actor = null;
        _counter = 0;
        _stage = TokayShieldUpgradeStage.Inactive;
    }

    private void GiveShieldUpgrade()
    {
        int parameter = _context.Inventory.ShieldLevel < 2 ? 1 : 2;
        _actor?.SetScriptAnimation(_database.Animation(0x02));
        _reward = _context.GrantScriptTreasure(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom.Id,
            TreasureDatabase.TreasureShield,
            parameter,
            $"TREASURE_OBJECT_SHIELD_{parameter:x2}",
            "scripts/ages:tokayGiveShieldUpgradeToLink",
            objectParameter: parameter + 1);
        _stage = TokayShieldUpgradeStage.Reward;
    }

    private bool CurrentRoomFlag(byte flag) =>
        _context.Rooms.SaveData.HasRoomFlag(
            _context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id, flag);

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
