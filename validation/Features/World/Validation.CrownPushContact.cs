using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownPushContact()
    {
        foreach (bool batched in new[] { false, true })
        foreach (float fraction in new[] { 0f, 0.25f, 0.75f })
        {
            LoadValidationRoom(4, 0x9e);
            _player.WarpTo(new(120 + fraction, 120 + fraction));
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int i = 0; !_pushBlocks.Active && i < 80; i++)
                {
                    StepGameplayUpdates(1, Vector2.Up);
                    FailIf(_currentRoom.IsSolid(_player.Position), "Statue contact approach must stay on actual floor.");
                }
                // Source $14 starts at tile center Y-2, moves $0080, then
                // clamps Link.yh to block.yh+12 without touching Link.yl.
                float expectedY = 98 - repeat * 16 + fraction;
                FailIf(!_pushBlocks.Active || _player.PrecisePosition != new Vector2(120 + fraction, expectedY),
                    $"Initial native block contact must retain fractions: got {_player.PrecisePosition}, expected Y={expectedY}.");
                StepGameplayUpdates(2, Vector2.Up, batched: batched);
                FailIf(_player.PrecisePosition != new Vector2(120 + fraction, expectedY - 2),
                    $"Link must move before the block's next two clamp updates: got {_player.PrecisePosition}, expected Y={expectedY - 2}.");
                StepGameplayUpdates(2, Vector2.Up, batched: batched);
                FailIf(_player.PrecisePosition != new Vector2(120 + fraction, expectedY - 3),
                    "Following a half-pixel block must advance Link one pixel per two updates while preserving fractions.");
                StepGameplayUpdates(27, Vector2.Up, batched: batched);
                FailIf(_pushBlocks.Active || _player.PrecisePosition != new Vector2(120 + fraction, 82 - repeat * 16 + fraction),
                    "The final object clamp must precede tile placement on movement update32.");
            }
        }
        foreach (bool batched in new[] { false, true })
        foreach (float fraction in new[] { 0f, 0.25f, 0.75f })
        foreach (Vector2 direction in new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left })
        {
            LoadValidationRoom(4, 0xbc);
            Vector2 center = new(104, 72);
            _player.WarpTo(center - direction * 16 + Vector2.One * fraction);
            for (int i = 0; !_pushBlocks.Active && i < 80; i++)
            {
                StepGameplayUpdates(1, direction);
                FailIf(_currentRoom.IsSolid(_player.Position),
                    "Four-direction statue contact must approach through original floor.");
            }
            FailIf(!_pushBlocks.Active, "Original statue $46 must accept each push direction.");
            // Source object center is (104,70), speed $0080, radii 6+6.
            // By update16 sustained input reaches its trailing high-byte
            // boundary. Odd updates floor the object's half-pixel position.
            int previous = 1;
            foreach (int update in new[] { 16, 31, 32 })
            {
                StepGameplayUpdates(update - previous, direction, batched: batched);
                Vector2 block = (new Vector2(104, 70) + direction * (update * 0.5f)).Floor();
                Vector2 expected = center + Vector2.One * fraction;
                if (direction.X != 0) expected.X = block.X - direction.X * 12 + fraction;
                else expected.Y = block.Y - direction.Y * 12 + fraction;
                FailIf(_player.PrecisePosition != expected,
                    $"Statue contact {direction} update{update}: got {_player.PrecisePosition}, expected {expected}.");
                FailIf(_pushBlocks.Active != (update < 32),
                    "Four-direction statue contact must complete on update32.");
                previous = update;
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
