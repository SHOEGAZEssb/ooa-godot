using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_COMPANION_TUTORIAL $d0. The first two updates preserve the
/// source state-0/state-1 split; state 2 watches the live companion position.
/// </summary>
internal sealed partial class CompanionTutorialRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity
{
    private readonly CompanionTutorialRecord _record;
    private readonly OracleRuntimeState _runtime;
    private readonly OracleSaveData _save;
    private readonly Action<int, string, Vector2> _showText;
    private int _state;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int State => _state;
    internal CompanionTutorialRecord Record => _record;
    internal bool TextShown { get; private set; }

    internal CompanionTutorialRoomEntity(
        CompanionTutorialRecord record,
        OracleRuntimeState runtime,
        OracleSaveData save,
        Action<int, string, Vector2> showText)
    {
        if (record is not
            {
                Id: 0xd0,
                SubId: 0x04,
                Completion: CompanionTutorialCompletion.CompanionRight
            })
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _runtime = runtime;
        _save = save;
        _showText = showText;
        Name = $"CompanionTutorial_{record.Order}";
        Position = new Vector2(record.X, record.Y);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (_state == 0)
            _state = 1;
        return ScreenTransitionPresentation.Visible;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _ = spawns;
        if (Finished)
            return;

        switch (_state)
        {
            case 0:
                _state = 1;
                return;
            case 1:
                _state = 2;
                InitializeTutorial();
                return;
            case 2:
                UpdateCompletion();
                return;
            default:
                throw new InvalidOperationException(
                    $"INTERAC_COMPANION_TUTORIAL `$d0:${_record.SubId:x2} " +
                    $"entered unsupported state ${_state:x2} from {_record.Source}.");
        }
    }

    private void InitializeTutorial()
    {
        // interactionCoded0 state 1 advances before checking w1Companion.
        // Subid $04 remains in state 2 when no companion is enabled.
        if (!CompanionRuntimeState.AnyActive(_runtime))
            return;

        ActiveCompanion companion = CompanionRuntimeState.Read(_runtime);
        if (companion.Id != _record.RequiredCompanion)
        {
            Finished = true;
            return;
        }
        if (FlagSet())
        {
            Finished = true;
            return;
        }

        // In the current live-companion owner, an active animal is precisely
        // the source wLinkObjectIndex bit-0 mounted state.
        TextShown = true;
        _showText(_record.TextId, _record.Message, Position);
    }

    private void UpdateCompletion()
    {
        if (!CompanionRuntimeState.AnyActive(_runtime))
            return;
        ActiveCompanion companion = CompanionRuntimeState.Read(_runtime);
        bool complete = _record.Completion switch
        {
            CompanionTutorialCompletion.CompanionRight =>
                companion.X > _record.X,
            _ => throw new InvalidOperationException(
                $"Unsupported companion tutorial completion in {_record.Source}.")
        };
        if (!complete)
            return;

        using (_save.BeginMutation())
        {
            _save.WriteWramByte(
                _record.FlagAddress,
                (byte)(_save.ReadWramByte(_record.FlagAddress) |
                    (1 << _record.FlagBit)));
        }
        Finished = true;
    }

    private bool FlagSet() =>
        (_save.ReadWramByte(_record.FlagAddress) &
            (1 << _record.FlagBit)) != 0;
}
