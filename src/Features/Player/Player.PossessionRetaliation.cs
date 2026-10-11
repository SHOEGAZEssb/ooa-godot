namespace oracleofages;

public partial class Player
{
    // veranPossessionBoss_wasHit writes raw damage directly, bypassing rings.
    internal void ApplyPossessionRetaliation(int angle)
    {
        _pendingContactDamageRaw=8; _nativeContactSignal=true;
        _enemyInvincibilityFrames=40; _enemyKnockbackFrames=21;
        _enemyKnockbackDirection=OracleObjectMovement.Shared.Direction(angle);
    }
}
