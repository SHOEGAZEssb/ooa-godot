using Godot;
using System;

namespace oracleofages;

public sealed class DebugObjectSpawnerController
{
    private const string InputAction = "debug_object_spawner";
    private readonly DebugObjectSpawnerScreen _screen;
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly Player _player;
    private readonly GameplayPauseController _pause;
    private readonly Func<bool> _canOpen;
    private PauseLease? _lease;
    public bool IsActive => _lease is not null;

    internal DebugObjectSpawnerController(DebugObjectSpawnerScreen screen,
        RoomSession rooms, RoomEntityManager entities, Player player,
        GameplayPauseController pause, Func<bool> canOpen)
    {
        _screen = screen;
        _rooms = rooms;
        _entities = entities;
        _player = player;
        _pause = pause;
        _canOpen = canOpen;
        if (!InputMap.HasAction(InputAction))
        {
            InputMap.AddAction(InputAction);
            InputMap.ActionAddEvent(InputAction, new InputEventKey { PhysicalKeycode = Key.F4 });
        }
    }

    // Returning ownership also consumes the close update, including A/B edges.
    internal bool Update()
    {
        if (Input.IsActionJustPressed(InputAction))
        {
            if (IsActive) Close();
            else return TryOpen();
            return true;
        }
        if (!IsActive) return false;
        if (Input.IsActionJustPressed("item")) Close();
        else if (Input.IsActionJustPressed("attack")) SpawnSelection();
        else if (Input.IsActionJustPressed("map")) _screen.MoveObject(10);
        else if (Input.IsActionJustPressed("move_up")) _screen.MoveVertical(-1);
        else if (Input.IsActionJustPressed("move_down")) _screen.MoveVertical(1);
        else if (Input.IsActionJustPressed("move_left")) _screen.MoveHorizontal(-1);
        else if (Input.IsActionJustPressed("move_right")) _screen.MoveHorizontal(1);
        return true;
    }

    internal bool TryOpen()
    {
        if (IsActive || !_canOpen()) return false;
        _lease = _pause.TryAcquire(this);
        if (_lease is null) return false;
        _screen.Open(_rooms.CurrentRoom, _player.Position);
        return true;
    }

    internal void Close()
    {
        _screen.Close();
        _lease?.Dispose();
        _lease = null;
    }

    internal void SpawnSelection()
    {
        if (!IsActive) return;
        DebugObjectEntry entry = _screen.Selection;
        bool success = _screen.IsDrop
            ? _entities.TrySpawnDebugItemDrop(entry.SubId, _screen.SpawnPosition, out string error)
            : _entities.TrySpawnDebugEnemy(entry.Id, entry.SubId, _screen.SpawnPosition, out error);
        if (success)
            _screen.ShowResult(true, $"Spawned ${entry.Id:x2}:${entry.SubId:x2}.\nClose to resume.");
        else
        {
            _screen.ShowResult(false, error);
            GD.Print(error);
        }
    }
}
