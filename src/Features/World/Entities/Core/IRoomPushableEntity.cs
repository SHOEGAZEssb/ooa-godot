using Godot;

namespace oracleofages;

/// <summary>
/// A placed object which consumes Link's resolved cardinal push attempt.
/// </summary>
internal interface IRoomPushableEntity
{
    void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput);
}
