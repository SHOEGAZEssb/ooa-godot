using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorEdges()
    {
        Vector2 center = new(232, 136);
        void Allocate()
        {
            _saveData.SetRoomFlag(4, 0xb3, 2, false);
            LoadValidationRoom(4, 0xb3);
            _entities.Clear();
            _player.WarpTo(new(220, 136));
            while (_inventory.TryUseDungeonSmallKey(5)) { }
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            for (int i = 0; i < 10; i++)
                _keyDoors.UpdatePushAttempt(_player.Position, Vector2I.Right, Vector2.Right);
            _sound.ClearPlayRequestAudit();
        }
        foreach (bool batch in new[] { false, true })
        {
            void Step(int n)
            {
                if (batch) _entities.Update(n / 60.0, _player);
                else for (int i = 0; i < n; i++) _entities.Update(1.0 / 60, _player);
            }
            // bank0 objectCheckWithinScreenBoundary: byte Y+7 < $8f,
            // X+7 < $af. Test both sides of every signed viewport edge.
            foreach (var edge in new[]
            {
                (Point: new Vector2(-8, 64), Visible: false),
                (Point: new Vector2(-7, 64), Visible: true),
                (Point: new Vector2(167, 64), Visible: true),
                (Point: new Vector2(168, 64), Visible: false),
                (Point: new Vector2(80, -8), Visible: false),
                (Point: new Vector2(80, -7), Visible: true),
                (Point: new Vector2(80, 135), Visible: true),
                (Point: new Vector2(80, 136), Visible: false)
            })
            {
                Allocate();
                var oldScreen = _entities.WorldToScreen;
                try
                {
                    _entities.WorldToScreen = position => position - center + edge.Point;
                    Step(2);
                    int first = edge.Visible ? 1 : 0;
                    FailIf(_sound.PlayRequestsFor(SoundId.SndDoorClose) != first ||
                        !_currentRoom.IsSolid(center) || _keyDoors.OpeningCounter != 6,
                        $"Key-door sound boundary failed at {edge.Point}; camera must not alter collision.");
                    // Each sound samples the current camera, not the start visibility.
                    _entities.WorldToScreen = position => position - center +
                        (edge.Visible ? new Vector2(168, 64) : new Vector2(80, 64));
                    Step(6);
                    FailIf(_sound.PlayRequestsFor(SoundId.SndDoorClose) != 1 ||
                        _keyDoors.Opening || _currentRoom.IsSolid(center),
                        "Door completion must resample screen visibility while opening its world tile.");
                }
                finally { _entities.WorldToScreen = oldScreen; }
            }
            foreach (byte collision in new byte[] { 0, 0x10 })
            {
                Allocate();
                Step(1);
                // Isolate a collision change between allocation and state2.
                // $10 uses allowHoles' zero mask, despite a nonzero collision byte.
                _currentRoom.SetPositionTileAndCollision(center, 0xa1, collision, (long)_animationTicks);
                Step(1);
                FailIf(_keyDoors.Opening || _keyDoors.OpeningCounter != 0 ||
                    _currentRoom.GetMetatile(center) != 0xa1 ||
                    _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0 ||
                    _inventory.GetDungeonSmallKeys(5) != 0,
                    "An already-passable key-door tile must skip animation without overwriting it or refunding the key.");
                Step(6);
                FailIf(_currentRoom.GetMetatile(center) != 0xa1,
                    "A skipped opener must not leave a delayed tile write.");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
