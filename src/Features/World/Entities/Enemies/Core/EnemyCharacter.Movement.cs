using Godot;

namespace oracleofages;

public abstract partial class EnemyCharacter
{
    private OracleRuntimeState? _movementMemory;

    internal void BindMovementMemory(OracleRuntimeState memory) => _movementMemory = memory;

    // Executed getPositionOffsetForVelocity, distinct from pure geometry and
    // presentation lookups. Standalone character fixtures have no room WRAM.
    internal OracleObjectVelocity MovementVelocity(int speed, int angle) =>
        NativeObjectMovement.Velocity(_movementMemory, speed, angle);

    internal Vector2I MovementCircleArcOffset(int distance, int angle) =>
        NativeObjectMovement.CircleArcOffset(_movementMemory, distance, angle);

    protected Vector2 MovementDelta(int speed, int angle)
    {
        OracleObjectVelocity velocity = MovementVelocity(speed, angle);
        return new(velocity.XFixed / 256.0f, velocity.YFixed / 256.0f);
    }

    internal OracleObjectPosition ApplyMovementSpeed(OracleObjectPosition position, int speed, int angle)
    {
        OracleObjectVelocity velocity = MovementVelocity(speed, angle);
        return position.Add(velocity.YFixed, velocity.XFixed);
    }

    protected Vector2 ApplyMovementSpeed(ref Vector2 precisePosition, int speed, int angle)
    {
        OracleObjectPosition position = ApplyMovementSpeed(
            OracleObjectPosition.FromPixels(precisePosition), speed, angle);
        precisePosition = position.PrecisePosition;
        return position.PixelPosition;
    }
}
