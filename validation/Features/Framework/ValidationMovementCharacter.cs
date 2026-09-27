namespace oracleofages;

// Geometry-only fixtures do not initialize animation or join the live room.
internal sealed partial class ValidationMovementCharacter : EnemyCharacter
{
    internal override bool InitializationPending => false;
}
