using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownPassageReturns()
    {
        _player.ApplicationUpdateOwned = true;
        // group6WarpSources and group4WarpDestTable: upper-left/right
        // ladder exits, plus room$96's lower-right exit (mask$08).
        foreach (var route in new (int Room, int Column, bool Down, int Dest, int Arrival)[]
        {
            (0x93,2,false,0xa4,0x64), (0x94,12,false,0xa2,0x87),
            (0x95,2,false,0xad,0x27), (0x96,13,true,0x9c,0x87),
            (0x97,1,false,0xa0,0x22), (0x97,13,false,0xa3,0x17),
            (0x98,1,false,0x99,0x23), (0x98,13,false,0x9b,0x12)
        })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            LoadValidationRoom(6, route.Room);
            _entities.Clear();
            Vector2 start = new(route.Column * 16 + 8, route.Down ? _currentRoom.Height - 24 : 24);
            FailIf(_collision.Collides(start), $"Passage6:{route.Room:x2} exit approach must be free of solid geometry.");
            _player.WarpTo(start);
            Vector2 direction = route.Down ? Vector2.Down : Vector2.Up;
            for (int i = 0; !_transitions.IsTransitioning && i < 80; i++) StepGameplayUpdates(1, direction);
            FailIf(!_transitions.IsTransitioning,
                $"Actual ladder movement must reach passage6:{route.Room:x2}'s selected exit from{start}; ended{_player.Position}.");
            for (int i = 0; _rooms.ActiveGroup == 6 && i < 160; i++) StepGameplayUpdates(1, Vector2.Zero);
            Vector2 arrival = new((route.Arrival & 15) * 16 + 8, (route.Arrival >> 4) * 16 + 8);
            FailIf(_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != route.Dest || _player.Position != arrival,
                $"Passage6:{route.Room:x2} must return to4:{route.Dest:x2}/${route.Arrival:x2}; " +
                $"got{_rooms.ActiveGroup:x1}:{_rooms.CurrentRoom.Id:x2} at{_player.Position}.");
            for (int i = 0; _transitions.IsTransitioning && i < 160; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_transitions.IsTransitioning, "Passage return must complete its arrival fade.");
            StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_transitions.IsTransitioning || _rooms.CurrentRoom.Id != route.Dest,
                "Returning onto a Crown staircase must suppress immediate re-entry until Link leaves it.");
            LoadValidationRoom(0, 0x60);
        }
    }
}
