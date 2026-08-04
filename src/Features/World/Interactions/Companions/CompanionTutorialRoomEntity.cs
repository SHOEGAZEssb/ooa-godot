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
        if (record.Id != 0xd0 || record.SubId is < 0 or > 5)
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
                InitializeTutorial(frame.Player);
                return;
            case 2:
                UpdateCompletion(frame.Player);
                return;
            default:
                throw new InvalidOperationException(
                    $"INTERAC_COMPANION_TUTORIAL `$d0:${_record.SubId:x2} " +
                    $"entered unsupported state ${_state:x2} from {_record.Source}.");
        }
    }

    private void InitializeTutorial(Player player)
    {
        // interactionCoded0 state 1 advances before checking w1Companion.
        // Only the one-shot dismount variants $02/$05 delete without one.
        if (!CompanionRuntimeState.AnyActive(_runtime))
        {
            if (_record.SubId is 0x02 or 0x05)
                Finished = true;
            return;
        }

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

        // wLinkObjectIndex bit 0 is independent of w1Companion.enabled: the
        // remembered on-screen animal can remain active after Link dismounts.
        if (player.CompanionRideActive)
        {
            TextShown = true;
            _showText(_record.TextId, _record.Message, Position);
        }

        if (_record.SubId is 0x02 or 0x05)
            Finished = true;
    }

    private void UpdateCompletion(Player player)
    {
        if (!CompanionRuntimeState.AnyActive(_runtime))
            return;
        ActiveCompanion companion = CompanionRuntimeState.Read(_runtime);
        int companionX = Mathf.FloorToInt(companion.X);
        int companionY = Mathf.FloorToInt(companion.Y);
        int linkX = Mathf.FloorToInt(player.Position.X);
        bool complete = _record.Completion switch
        {
            CompanionTutorialCompletion.CompanionRight =>
                companionX > _record.X,
            CompanionTutorialCompletion.CompanionAbove =>
                companionY <= _record.Y,
            CompanionTutorialCompletion.CompanionBelowOrLeft =>
                companionY > _record.Y || companionX <= _record.X,
            CompanionTutorialCompletion.CompanionAboveWithLinkXRange =>
                linkX >= _record.LinkXMin && linkX < _record.LinkXMax &&
                companionY <= _record.Y,
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
