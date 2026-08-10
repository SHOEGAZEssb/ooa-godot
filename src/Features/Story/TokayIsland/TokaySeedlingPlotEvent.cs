using Godot;
using System;

namespace oracleofages;

/// <summary>
/// tokayAtSeedlingPlotScript for INTERAC_TOKAY $48:$11.
/// </summary>
internal sealed class TokaySeedlingPlotEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayInteractionDatabase _database;
    private readonly TokaySeedlingPlotDatabase _plotDatabase;
    private readonly TokaySeedlingPlotRecord _record;
    private TokaySeedlingPlotStage _stage;
    private NpcCharacter? _actor;
    private int _counter;
    private int _moveAngle;
    private Vector2 _precisePosition;
    private Vector2I _towardDirection;
    private bool _inputLocked;

    internal TokaySeedlingPlotEvent(
        RoomEventContext context,
        TokayInteractionDatabase database,
        TokaySeedlingPlotDatabase plotDatabase)
    {
        _context = context;
        _database = database;
        _plotDatabase = plotDatabase;
        _record = plotDatabase.Record;
    }

    public bool HasState => _stage != TokaySeedlingPlotStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal TokaySeedlingPlotStage Stage => _stage;
    internal int Counter => _counter;
    internal int MoveAngle => _moveAngle;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || !_plotDatabase.MatchesNpc(npc.Record))
            return false;

        _actor = npc;
        FaceActorTowardLink();
        if (CurrentRoomFlag(checked((byte)_record.RoomFlag)))
        {
            Show(_context.Inventory.HasTreasure(_database.TreasureScentSeeds)
                ? 0x0a44
                : 0x0a43);
            _stage = TokaySeedlingPlotStage.DialogueOnly;
            return true;
        }
        if (!_context.Inventory.HasTreasure(_database.TreasureScentSeedling))
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
                    FaceActorTowardLink();
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
                _counter = _record.IntroWait;
                _stage = TokaySeedlingPlotStage.PlantTextWait;
                break;
            case TokaySeedlingPlotStage.PlantText:
                BeginMovement();
                break;
            case TokaySeedlingPlotStage.Moving:
                UpdateMovement();
                break;
            case TokaySeedlingPlotStage.Plant:
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
        _moveAngle = 0;
        _precisePosition = Vector2.Zero;
        _towardDirection = Vector2I.Zero;
        _stage = TokaySeedlingPlotStage.Inactive;
    }

    private void BeginMovement()
    {
        NpcCharacter actor = RequireActor();
        int towardAngle = CardinalAngleTowardLink(actor);
        _towardDirection = Direction(towardAngle);
        _moveAngle = towardAngle ^ 0x10;
        actor.SetFacingDirection(Direction(_moveAngle));
        _precisePosition = actor.Position;
        _counter = _record.MoveCounter;
        _stage = TokaySeedlingPlotStage.Moving;
    }

    private void UpdateMovement()
    {
        NpcCharacter actor = RequireActor();
        actor.SetFacingDirection(Direction(_moveAngle));
        _counter--;
        if (_counter != 0)
        {
            actor.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, _record.Speed, _moveAngle));
            return;
        }

        // interactionRunScript returns on the counter2 zero update. The
        // following object update resumes at the second tokayFlipDirection.
        _stage = TokaySeedlingPlotStage.Plant;
    }

    private void PlantSeedling()
    {
        NpcCharacter actor = RequireActor();
        actor.SetFacingDirection(_towardDirection);
        _context.Inventory.LoseTreasure(_database.TreasureScentSeedling);
        OracleSaveData save = _context.Rooms.SaveData;
        int room = _context.Rooms.CurrentRoom.Id;
        save.SetRoomFlag(_record.Group, room, checked((byte)_record.RoomFlag));
        save.SetRoomFlag(
            _record.Group - 1, room, checked((byte)_record.RoomFlag));
        _context.Entities.Spawn<TokaySeedlingDecorationRoomEntity>(
            new TokaySeedlingDecorationSpawn(_record));
        _context.Sound.PlaySound(_database.SoundGetSeed);
        _counter = _record.DoneWait;
        _stage = TokaySeedlingPlotStage.DoneTextWait;
    }

    private NpcCharacter RequireActor() =>
        _actor ?? throw new InvalidOperationException(
            "tokayAtSeedlingPlotScript lost its actor.");

    private int CardinalAngleTowardLink(NpcCharacter actor) =>
        (OracleObjectMovement.Shared.RelativeAngle(
            actor.Position, _context.Player.Position) + 0x04) & 0x18;

    private static Vector2I Direction(int angle) =>
        new(
            Mathf.RoundToInt(OracleObjectMath.StrictCardinalVector(angle).X),
            Mathf.RoundToInt(OracleObjectMath.StrictCardinalVector(angle).Y));

    private void FaceActorTowardLink()
    {
        NpcCharacter actor = RequireActor();
        actor.SetFacingDirection(Direction(CardinalAngleTowardLink(actor)));
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
    Moving,
    Plant,
    DoneTextWait,
    DoneText
}
