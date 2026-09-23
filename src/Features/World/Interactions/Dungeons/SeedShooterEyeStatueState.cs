using System;

namespace oracleofages;

/// <summary>PART $46 handler, after the native part-status wrapper.</summary>
internal sealed class SeedShooterEyeStatueState
{
    private readonly byte _subid;
    private readonly byte _activeCounter;
    private readonly Action<int,bool> _setTrigger;

    internal bool Initialized { get; private set; }
    internal byte Counter { get; private set; }
    internal bool Visible { get; private set; }

    internal SeedShooterEyeStatueState(byte subid, byte activeCounter, Action<int,bool> setTrigger)
    {
        if (subid > 7 || activeCounter == 0)
            throw new ArgumentOutOfRangeException(nameof(subid),"PART $46 requires a native trigger index and nonzero activation counter.");
        _subid = subid;
        _activeCounter = activeCounter;
        _setTrigger = setTrigger;
    }

    // partCode46 receives NZ for either JUST_HIT or DEAD. Collision acceptance,
    // invincibility and update eligibility belong to the native part wrapper.
    internal void Update(bool nonzeroPartStatus)
    {
        if (nonzeroPartStatus)
        {
            Counter = _activeCounter;
            _setTrigger(_subid & 7,true);
            Visible = true;
        }
        if (!Initialized)
        {
            Initialized = true;
            return;
        }
        if (Counter != 0) Counter--;
        if (Counter != 0) return;
        _setTrigger(_subid,false);
        Visible = false;
    }
}
