namespace oracleofages;

internal sealed class GoronExplosion(NpcCharacter actor, int slot)
{
    internal NpcCharacter Actor => actor;
    internal int Slot => slot;
    private bool _initialized;
    internal void Update(OracleSoundEngine sound)
    {
        if (!_initialized) { _initialized=true; sound.PlaySound(SoundId.SndExplosion); return; }
        if (actor.CurrentAnimationParameter==0xff) actor.SetActive(false);
        else actor.AdvanceAnimationUpdates(1);
    }
}
