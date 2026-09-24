using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownSquishDeath()
    {
        LoadValidationRoom(4, 0x9b);
        _inventory.RefillHealth();
        _player.WarpTo(new(136, 136));
        void Step(int count = 1, Vector2 movement = default) =>
            StepGameplayUpdates(count, movement, [], [], false);
        for (int i = 0; _player.Position.Y > 104 && i < 60; i++) Step(movement: Vector2.Up);
        FailIf(_player.Position != new Vector2(136, 104) || _currentRoom.IsSolid(_player.Position),
            "Lethal crush fixture must approach Crown floor$68 through its actual geometry.");
        var detector = _entities.Entities<WallSquishRoomEntity>().Single();
        FailIf(detector.State != 1, "Crown wall detector must initialize before the lethal hit.");
        // Model the completed block destination and lethal damage between
        // Link dispatch and INTERACTION dispatch, as in updateAllObjects.
        _currentRoom.SetPositionTileAndCollision(_player.Position, 0x2e, 0x0f, (long)_animationTicks);
        FailIf(!_player.ApplyDamage(_player.MaxHealthQuarters) || !_player.IsDying,
            "Lethal damage must publish the pending death flag.");
        _entities.Update(1.0 / 60, _player);
        FailIf(!_player.SideScrollSquished || _player.SquishAnimation is not null || _player.DeathAnimationActive,
            "The detector must queue state$11 while lethal damage is pending, without initializing either state.");
        Step();
        FailIf(!_player.DeathAnimationActive || _player.SquishAnimation is not null || !_player.SideScrollSquished,
            "Link must enter dying before consuming the retained crush request.");
        Step(3);
        FailIf(!_player.DeathAnimationActive || _player.SquishAnimation is not null,
            "The retained request must not interrupt Link's death animation.");
    }
}
