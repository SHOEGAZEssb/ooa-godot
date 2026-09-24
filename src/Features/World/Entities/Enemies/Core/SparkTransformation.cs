using Godot;
using System;

namespace oracleofages;

// spark_state9/stateA are also Whisp's handlers. relatedObj2 addresses the
// live interaction page, not an effect reference: a deleted puff reads zero
// until that slot is reused, even if the enemy was frozen at its terminal frame.
internal sealed class SparkTransformation(
    EnemyCharacter enemy,
    Func<Vector2, int> tryCreatePuff,
    Func<int, int> interactionAnimationParameter,
    Action<Vector2, int> tryCreateFairy,
    Action deleteEnemy)
{
    private bool _hitPending;
    private int _puffSlot;
    internal int State { get; private set; } = 8;
    internal bool Completed { get; private set; }

    internal void Hit() => _hitPending = true;

    internal bool Update(int angle, bool createsFairy)
    {
        if (_hitPending)
        {
            _hitPending = false;
            if (State < 9) State = 9;
        }
        if (State < 9) return false;
        if (State == 9)
        {
            int slot = tryCreatePuff(OracleObjectMath.ToPixelPosition(enemy.Position));
            if (slot < 0) return true;
            _puffSlot = slot;
            State = 0x0a;
            enemy.Visible = false;
            return true;
        }
        if (interactionAnimationParameter(_puffSlot) != 0xff) return true;
        if (createsFairy)
            tryCreateFairy(OracleObjectMath.ToPixelPosition(enemy.Position), angle);
        Completed = true;
        deleteEnemy();
        return true;
    }
}
