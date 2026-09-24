using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmasherCornerKnockback()
    {
        foreach (bool batch in new[] { false, true })
        {
            _saveData.SetRoomFlag(4, 0xb4, 0x80, false);
            LoadValidationRoom(4, 0xb4);
            _player.WarpTo(new(48, 136));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, [], [], batch);
            Step(2);
            var parent = _entities.Entities<SmasherCharacter>().Single(actor => !actor.IsBall);
            var ball = _entities.Entities<SmasherCharacter>().Single(actor => actor.IsBall);
            ball.Position = new(120, 88);
            // Source right-facing cumulative probes: center twice, then
            // (x+6,y-1), (x+6,y+5). Find an upper-only wall in original geometry.
            Vector2? corner = null;
            bool Solid(int x, int y) => _currentRoom.IsSolidForEnemyMovement(new(x, y), holesAreWalls: false);
            for (int y = 8; y < _currentRoom.Height - 8 && corner is null; y++)
            for (int x = 8; x < _currentRoom.Width - 8 && corner is null; x++)
                if (!Solid(x, y) && Solid(x + 6, y - 1) && !Solid(x + 6, y + 5))
                    corner = new(x, y);
            FailIf(corner is null, "Smasher corner fixture requires an upper-only wall in room4:b4.");
            FailIf(parent.Speed >= 0x32, "Smasher walking speed must be below SPEED_140.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                parent.Position = corner!.Value;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                typeof(EnemyCharacter).GetProperty("KnockbackAngle", flags)!.SetValue(parent, 8);
                typeof(EnemyCharacter).GetProperty("KnockbackCounter", flags)!.SetValue(parent, 16);
                parent.InvincibilityCounter = 32;
                int state = parent.State, counter = parent.Counter1, z = parent.ZFixed;
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(2);
                    FailIf(parent.Position != corner.Value || parent.KnockbackCounter != 16,
                        "Frozen Smasher must preserve corner recoil.");
                }
                finally { _entities.TextActiveSource = text; }
                Step(2);
                // ecom_applyGivenVelocityGivenAdjacentWalls adds $0060 to Y
                // per update and sets hFF8D from Enemy.speed, not SPEED_200.
                FailIf(parent.Position != corner.Value + new Vector2(0, 0.75f) ||
                    parent.KnockbackCounter != 14 || parent.InvincibilityCounter != 30 ||
                    parent.State != state || parent.Counter1 != counter || parent.ZFixed != z,
                    "Smasher corner slides must retain recoil using its stored speed and suspend AI/Z updates.");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
