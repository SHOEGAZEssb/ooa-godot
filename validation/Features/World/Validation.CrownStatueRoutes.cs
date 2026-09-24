using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownStatueRoutes()
    {
        // Original room04bc layout: statues $46/$48/$68, buttons $34/$3a/$7a.
        // interactableTiles $2a:$80 permits all directions without a bracelet;
        // pushableTiles $2a,$a0,$2a,$01 preserves a reusable statue.
        foreach (int route in new[] { 0, 1, 2 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            LoadValidationRoom(4, 0xbc);
            int source = route == 0 ? 0x46 : route == 1 ? 0x48 : 0x68;
            int target = route == 0 ? 0x34 : route == 1 ? 0x3a : 0x7a;
            Vector2 vertical = route == 2 ? Vector2.Down : Vector2.Up;
            Vector2 horizontal = route == 0 ? Vector2.Left : Vector2.Right;
            _player.WarpTo(new(route == 0 ? 104 : 136, 88));
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.Layout[source] != 0x2a,
                "Crown statue route must start on original floor beside an original statue.");
            void Step(Vector2 movement)
            {
                StepGameplayUpdates(1, movement, [], [], false);
                FailIf(_currentRoom.IsSolid(_player.Position),
                    $"Statue route{route} crossed solid geometry at {_player.Position}.");
            }
            void Push(int from, int to, Vector2 direction)
            {
                for (int i = 0; _currentRoom.Layout[to] != 0x2a && i < 100; i++) Step(direction);
                FailIf(_currentRoom.Layout[to] != 0x2a || _currentRoom.Layout[from] != 0xa0 || _pushBlocks.Active,
                    $"Crown statue route{route} failed push ${from:x2}->${to:x2} at {_player.Position}.");
                Step(Vector2.Zero);
            }
            void WalkAxis(bool x, float destination)
            {
                for (int i = 0; i < 80; i++)
                {
                    float distance = destination - (x ? _player.Position.X : _player.Position.Y);
                    if (Math.Abs(distance) <= 0.75f) return;
                    Step(x ? new(Math.Sign(distance), 0) : new(0, Math.Sign(distance)));
                }
                throw new InvalidOperationException($"Statue route{route} could not walk around the statue.");
            }
            int turn = source + (route == 2 ? 16 : -16);
            Push(source, turn, vertical);
            WalkAxis(true, 120);
            WalkAxis(false, route == 2 ? 120 : 56);
            int middle = turn + (route == 0 ? -1 : 1);
            Push(turn, middle, horizontal);
            Push(middle, target, horizontal);
            var button = _entities.Entities<GroundButtonRoomEntity>().Single(b => b.PackedPosition == target);
            FailIf(!button.Pressed || _currentRoom.GetUnderlyingMetatile(button.Position) != 0x0d ||
                _currentRoom.Layout[target] != 0x2a,
                "A normally pushed Crown statue must hold its button and preserve the pressed underlying tile.");
            StepGameplayUpdates(30, Vector2.Zero, [], [], true);
            FailIf(!button.Pressed || button.ReleaseCounter != 28,
                "A stationary statue must hold pressure beyond the button's release delay.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
