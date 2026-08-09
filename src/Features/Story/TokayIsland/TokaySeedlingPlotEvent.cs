using System;

namespace oracleofages;

/// <summary>
/// tokayAtSeedlingPlotScript for INTERAC_TOKAY $48:$11.
/// </summary>
internal sealed class TokaySeedlingPlotEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayInteractionDatabase _database;
    private TokaySeedlingPlotStage _stage;
    private NpcCharacter? _actor;
    private int _counter;
    private bool _inputLocked;

    internal TokaySeedlingPlotEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _stage != TokaySeedlingPlotStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal TokaySeedlingPlotStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record is not { Id: 0x48, SubId: 0x11 })
            return false;

        _actor = npc;
        if (CurrentRoomFlag(OracleSaveData.RoomFlag80))
        {
            Show(_context.Inventory.HasTreasure(0x21) ? 0x0a44 : 0x0a43);
            _stage = TokaySeedlingPlotStage.DialogueOnly;
            return true;
        }
        if (!_context.Inventory.HasTreasure(0x4d))
        {
            Show(0x0a40);
            _stage = TokaySeedlingPlotStage.DialogueOnly;
            return true;
        }

        LockInput();
        Show(0x0a40);
        _stage = TokaySeedlingPlotStage.Intro;
        return true;
    }

    public void UpdateFrame()
    {
        if (_stage == TokaySeedlingPlotStage.Inactive)
            return;
        if (_stage is TokaySeedlingPlotStage.PlantTextWait or
            TokaySeedlingPlotStage.DoneTextWait)
        {
            if (--_counter == 0)
            {
                if (_stage == TokaySeedlingPlotStage.PlantTextWait)
                {
                    Show(0x0a41);
                    _stage = TokaySeedlingPlotStage.PlantText;
                }
                else
                {
                    Show(0x0a42);
                    _stage = TokaySeedlingPlotStage.DoneText;
                }
            }
            return;
        }
        if (_context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TokaySeedlingPlotStage.DialogueOnly:
            case TokaySeedlingPlotStage.DoneText:
                FinishInteraction();
                break;
            case TokaySeedlingPlotStage.Intro:
                _counter = 30;
                _stage = TokaySeedlingPlotStage.PlantTextWait;
                break;
            case TokaySeedlingPlotStage.PlantText:
                PlantSeedling();
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay seedling-plot stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void Cancel()
    {
        UnlockInput();
        _actor = null;
        _counter = 0;
        _stage = TokaySeedlingPlotStage.Inactive;
    }

    private void PlantSeedling()
    {
        _context.Inventory.LoseTreasure(0x4d);
        OracleSaveData save = _context.Rooms.SaveData;
        int room = _context.Rooms.CurrentRoom.Id;
        save.SetRoomFlag(1, room, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(0, room, OracleSaveData.RoomFlag80);
        if (_actor is null)
            throw new InvalidOperationException("tokayAtSeedlingPlotScript lost its actor.");
        _actor.SetStatePosition(_actor.Position + new Godot.Vector2(16, 0));
        _context.Sound.PlaySound(_database.SoundGetSeed);
        _counter = 120;
        _stage = TokaySeedlingPlotStage.DoneTextWait;
    }

    private bool CurrentRoomFlag(byte flag) =>
        _context.Rooms.SaveData.HasRoomFlag(
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
        _stage = TokaySeedlingPlotStage.Inactive;
    }
}

internal enum TokaySeedlingPlotStage
{
    Inactive,
    DialogueOnly,
    Intro,
    PlantTextWait,
    PlantText,
    DoneTextWait,
    DoneText
}
