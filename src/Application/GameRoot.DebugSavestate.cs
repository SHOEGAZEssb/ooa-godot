
namespace Oracles;

public partial class GameRoot
{
    private string _debugSavestateStatus = string.Empty;
    private double _debugSavestateStatusFrames;
    private Func<bool>? _canCaptureDebugState;
    private Func<double>? _debugAnimationTicks;
    private Action<DebugSavestateData>? _restoreDebugState;

    internal virtual SaveResult StoreDebugStateSlot(int slot, DebugSavestateData state) =>
        DebugSavestateStore.SaveSlot(slot,state);
    internal virtual DebugSavestateLoadResult LoadDebugStateSlot(int slot) =>
        DebugSavestateStore.LoadSlot(_selectedGame,slot);

    private bool CanCaptureDebugSavestate() =>
        _gameplaySession is { CanCaptureDebugState: true } && _canCaptureDebugState?.Invoke() == true;

    internal DebugSavestateData CaptureDebugSavestate() =>
        _gameplaySession is { Active: true } session && _debugAnimationTicks is not null
            ? session.CaptureDebugState(_debugAnimationTicks()) : throw new InvalidOperationException("No active gameplay session for savestate capture.");

    internal void RestoreDebugSavestate(DebugSavestateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_gameplaySession is not { Active: true } session || _restoreDebugState is null)
            throw new InvalidOperationException("No active gameplay session for savestate restore.");
        if (state.Game != _selectedGame)
            throw new ArgumentException("Cannot load another game's debug savestate.",nameof(state));
        if (!session.Rooms.HasRoom(state.Group,state.Room))
            throw new ArgumentException($"Debug state room ${state.Group:x2}:${state.Room:x2} has no imported content.",nameof(state));
        session.ValidateDebugState(state);
        _restoreDebugState(state);
        (_gameplaySession ?? throw new InvalidOperationException("Debug restore did not compose a gameplay session.")).RestoreDebugPlayerBindings();
    }

    internal void UpdateRoomDebugLabel()
    {
        if (_gameplaySession is not { Active: true } session) return;
        string roomText = $"{session.Rooms.ActiveGroup:x1}:{session.Rooms.CurrentRoom.Id:x2}";
        if (_debugCollision.CollisionsDisabled)
            roomText += " NOCLIP";
        if (_debugFastForward)
            roomText += " FF x4";
        if (_debugSavestateStatusFrames > 0.0 &&
            !string.IsNullOrEmpty(_debugSavestateStatus))
        {
            roomText += $"  {_debugSavestateStatus}";
        }
        if (_roomDebug.Text != roomText)
            _roomDebug.Text = roomText;
    }

    private void AdvanceDebugSavestateStatus(double delta)
    {
        if (_debugSavestateStatusFrames <= 0.0)
            return;

        _debugSavestateStatusFrames = Math.Max(
            0.0,
            _debugSavestateStatusFrames + delta * -60.0);
        if (_debugSavestateStatusFrames == 0.0)
            _debugSavestateStatus = string.Empty;
    }

    private void SetDebugSavestateStatus(string status)
    {
        _debugSavestateStatus = status;
        _debugSavestateStatusFrames = 120.0;
        UpdateRoomDebugLabel();
    }

    internal void ClearDebugSavestateStatus()
    {
        _debugSavestateStatus = string.Empty;
        _debugSavestateStatusFrames = 0.0;
        UpdateRoomDebugLabel();
    }

    private void HandleGameplayDebugInput(InputEvent @event)
    {
        if (_gameplaySession is not { Active: true } ||
            _frontendIntro is not null ||
            _mainMenu is not null ||
            _newGameIntro is not null)
        {
            return;
        }

        if (!DebugSavestateController.TryDecodeInput(@event, out var command))
            return;

        GetViewport().SetInputAsHandled();
        if (command.Kind == DebugSavestateCommandKind.Save)
        {
            if (!CanCaptureDebugSavestate())
            {
                SetDebugSavestateStatus($"S{command.Slot} BUSY");
                return;
            }

            SaveResult result = StoreDebugStateSlot(
                command.Slot,
                CaptureDebugSavestate());
            if (result.Success)
            {
                GD.Print($"Saved debug state slot {command.Slot}.");
                SetDebugSavestateStatus($"S{command.Slot} SAVED");
            }
            else
            {
                GD.PushWarning(
                    $"Could not save debug state slot {command.Slot}: " +
                    result.ErrorMessage);
                SetDebugSavestateStatus($"S{command.Slot} SAVE ERR");
            }
            return;
        }

        DebugSavestateLoadResult loaded =
            LoadDebugStateSlot(command.Slot);
        if (loaded.Success)
        {
            try { RestoreDebugSavestate(loaded.State!); }
            catch (ArgumentException exception)
            {
                GD.PushWarning($"Could not load debug state slot {command.Slot}: {exception.Message}");
                SetDebugSavestateStatus($"S{command.Slot} LOAD ERR");
                return;
            }
            GD.Print($"Loaded debug state slot {command.Slot}.");
            SetDebugSavestateStatus($"S{command.Slot} LOADED");
        }
        else if (!loaded.Found)
        {
            SetDebugSavestateStatus($"S{command.Slot} EMPTY");
        }
        else
        {
            GD.PushWarning(
                $"Could not load debug state slot {command.Slot}: " +
                loaded.ErrorMessage);
            SetDebugSavestateStatus($"S{command.Slot} LOAD ERR");
        }
    }




}
