using Godot;
using System;

namespace oracleofages;

public sealed class DebugMapleController
{
    private const string InputAction = "debug_maple";
    private readonly RoomEntityManager _entities;
    private readonly RoomSession _rooms;
    private readonly OracleSaveData _saveData;
    private readonly InventoryState _inventory;
    private readonly Player _player;
    private readonly Action<int, int> _loadRoom;
    private readonly Func<Vector2> _findSpawn;
    private readonly Func<bool> _canActivate;
    private readonly MapleEventDatabase _maple = new();

    public DebugMapleController(
        RoomEntityManager entities,
        RoomSession rooms,
        OracleSaveData saveData,
        InventoryState inventory,
        Player player,
        Action<int, int> loadRoom,
        Func<Vector2> findSpawn,
        Func<bool> canActivate)
    {
        _entities = entities;
        _rooms = rooms;
        _saveData = saveData;
        _inventory = inventory;
        _player = player;
        _loadRoom = loadRoom;
        _findSpawn = findSpawn;
        _canActivate = canActivate;
        EnsureInputAction();
    }

    public void Update()
    {
        if (Input.IsActionJustPressed(InputAction))
            TryActivate();
    }

    internal bool ActivateForValidation() => TryActivate();

    private bool TryActivate()
    {
        if (!_canActivate() ||
            _entities.Entities<MapleEncounter>().Count != 0)
        {
            return false;
        }

        int group = _rooms.ActiveGroup;
        int room = _rooms.CurrentRoom.Id;
        bool relocated = !_maple.IsEligibleLocation(
            group, room, _inventory.AnimalCompanion);
        if (relocated)
        {
            (group, room) = _maple.DebugLocation(
                group, _inventory.AnimalCompanion);
        }

        _saveData.SetMapleKillCounter(
            RingEffects.MapleKillThreshold(_inventory));
        _loadRoom(group, room);
        if (relocated)
        {
            _player.WarpTo(_findSpawn());
            _player.Face(Vector2I.Down);
        }

        return _entities.Entities<MapleEncounter>().Count == 1;
    }

    private static void EnsureInputAction()
    {
        if (InputMap.HasAction(InputAction))
            return;
        InputMap.AddAction(InputAction);
        InputMap.ActionAddEvent(
            InputAction,
            new InputEventKey { PhysicalKeycode = Key.F3 });
    }
}
