using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownRaisedFloor()
    {
        // cutscenes2.s @data_7d63: lowered $28/$00 becomes raised $0e/$1e.
        // Isolate Link's consumer after that tile change; the toggle cutscene
        // and reaching the room are outside this check.
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(4, 0x9f);
            _entities.Clear();
            var runtime = _entities.RuntimeState;
            var detector = new WallSquishRoomEntity(_currentRoom, () => _rooms.BlockPushAngle,
                () => runtime.ReadWramByte(OracleRuntimeState.LinkRaisedFloorOffsetAddress), "raised-floor validation");
            typeof(RoomEntityManager).GetMethod("AddEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_entities, [detector]);
            int Offset() => unchecked((sbyte)runtime.ReadWramByte(OracleRuntimeState.LinkRaisedFloorOffsetAddress));
            Vector2 floor = new(120, 104);
            _player.WarpTo(new(120, 120));
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(Offset() != 0 || !_currentRoom.IsSolid(floor), "Raised-floor entry must start outside the solid $0e ring.");
            _currentRoom.SetPositionTileAndCollision(floor, 0x28, 0, (long)_animationTicks);
            StepGameplayUpdates(16, Vector2.Up, batched: batched);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(_player.Position) != 0x28,
                "Link must approach the lowered tile through actual floor.");
            _currentRoom.SetPositionTileAndCollision(floor, 0x0e, 0x1e, (long)_animationTicks);
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(Offset() != -3 || _collision.Collides(_player.Position) || _player.SideScrollSquished,
                "A raised tile under grounded Link must publish $fd and allow collision $1e.");
            Vector2 start = _player.Position;
            StepGameplayUpdates(8, Vector2.Right, batched: batched);
            FailIf(_player.Position.X <= start.X || Offset() != -3,
                "Raised-floor Link must walk onto the neighboring raised tile.");

            _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
            _inventory.EquipA(InventoryState.ItemFeather);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(!_player.TopDownAirborne, "Raised-floor retention check must start an actual feather jump.");
            // Replace all raised tiles after takeoff: airborne Link retains
            // the old offset until linkUpdateInAir's landing tile dispatch.
            for (int p = 0; p < 0xb0; p++)
            {
                Vector2 point = new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
                if (_currentRoom.GetMetatile(point) == 0x0e)
                    _currentRoom.SetPositionTileAndCollision(point, 0x28, 0, (long)_animationTicks);
            }
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(!_player.TopDownAirborne || Offset() != -3,
                "Airborne tile changes must retain wLinkRaisedFloorOffset.");
            for (int i = 0; _player.TopDownAirborne && i < 60; i++)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_player.TopDownAirborne || Offset() != 0,
                "Landing on lowered floor must clear the shared offset.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
