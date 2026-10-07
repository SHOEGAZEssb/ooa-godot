using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownDungeonEntrance()
    {
        CompareCrownKeyholeRom();
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.CrownKey,1);
        var entrance = _roomEvents.Get<CrownDungeonEntranceEvent>();
        byte Door() => _currentRoom.GetMetatile(new(120,24));
        void Arrange()
        {
            _saveData.SetRoomFlag(0,0x0a,0x80,false);
            LoadValidationRoom(0,0x0a);
            _player.WarpTo(new(120,72),recordSafe:false);
            StepGameplayUpdates(2,Vector2.Zero);
            FailIf(!entrance.HasState || entrance.BlocksGameplay || Door() != 0xec || _collision.Collides(_player.Position),
                "Crown$0:$0a must arm its placed controller above a collision-reachable floor.");
            // Independent roomTileChangesAfterLoad07/import visual goldens.
            FailIf(_currentRoom.GetBackgroundSubtileForValidation(12,0) != 0x26 ||
                _currentRoom.GetBackgroundSubtileForValidation(17,3) != 0x27,
                "Closed Crown facade lost its original BG rectangle.");
        }
        void Open()
        {
            for (int i = 0; i < 80 && !entrance.BlocksGameplay; i++) StepGameplayUpdates(1,Vector2.Up);
            FailIf(!entrance.BlocksGameplay,"Crown keyhole failed to acquire source input control.");
        }
        void FinishEntry()
        {
            for (int i = 0; i < 180 && _transitions.IsTransitioning; i++) StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != 4 || _currentRoom.Id != 0xbb,
                $"Opened Crown doorway failed its actual warp to $4:$bb: {_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}.");
        }
        Arrange(); Open(); StepGameplayUpdates(205,Vector2.Zero);
        StepGameplayUpdates(60,Vector2.Up); FinishEntry();
        LoadValidationRoom(0,0x0a);
        FailIf(entrance.HasState || Door() != 0xee || !_inventory.HasTreasure(TreasureId.CrownKey),
            "Crown re-entry must retain its source flag$80/$17:$ee substitution and treasure$43.");
        _player.WarpTo(new(120,72),recordSafe:false);
        StepGameplayUpdates(60,Vector2.Up); FinishEntry();
        foreach (int elapsed in new[] { 10,65 })
        {
            Arrange(); Open(); StepGameplayUpdates(elapsed,Vector2.Zero);
            LoadValidationRoom(0,0x11);
            FailIf(entrance.HasState || _player.CutsceneControlled || _roomEvents.MenusDisabled || _roomCamera.Offset != Vector2.Zero,
                "Cancelling Crown opening leaked input/menu ownership or camera shake.");
            LoadValidationRoom(0,0x0a);
            StepGameplayUpdates(1,Vector2.Zero);
            FailIf(Door() != 0xee || entrance.HasState || _entities.EntityAdapters<KeyholeControllerRoomEntity>().Any(),
                "Cancelled Crown unlock must persist while source state0 deletes its completed controller on re-entry.");
        }
        GD.Print("Validated native Crown keyhole approach, retained key/controller, opening BG/puff slots, clocks, text, input/menu handoff, cues/RNG and repeat; real warp and cancellation goldens retained.");
    }
}
