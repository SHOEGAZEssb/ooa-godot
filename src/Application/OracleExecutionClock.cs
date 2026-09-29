using System;

namespace oracleofages;

/// <summary>
/// CGB double-speed CPU time. Foreground work stops during the sound interrupt;
/// hardware time and the timer continue during that interrupt and during HALT.
/// </summary>
internal sealed class OracleExecutionClock
{
    internal const int CpuClocksPerSecond = 8_388_608;
    internal const int LcdFrameClocks = 140_448;
    private const int TimerDivider = 1024;
    private const int TimerReload = 0x77;
    private readonly Func<bool, int> _interrupt;
    private readonly Action<long>? _advanceHardware;
    private long _nextTimer = long.MaxValue;
    private readonly int _dividerPhase;
    private bool _servicingTimer;

    internal long Clocks { get; private set; }
    internal long TimerTicks { get; private set; }
    internal byte TimerCorrectionCounter { get; private set; }
    internal long NextTimer => _nextTimer;

    internal OracleExecutionClock(Func<bool, int> interrupt, long clocks = 0,
        int dividerPhase = 0, byte correctionCounter = 0, Action<long>? advanceHardware = null)
    {
        ArgumentNullException.ThrowIfNull(interrupt);
        if (clocks < 0 || dividerPhase is < 0 or >= TimerDivider)
            throw new ArgumentOutOfRangeException(nameof(clocks));
        _interrupt = interrupt;
        _advanceHardware = advanceHardware;
        Clocks = clocks;
        _dividerPhase = dividerPhase;
        TimerCorrectionCounter = correctionCounter;
    }

    internal void DisableTimer() => _nextTimer = long.MaxValue;

    internal void EnableTimer()
    {
        // bank0.s:enableTimer writes TIMA/TMA=$77, preserving DIV and hFFB8.
        // The timer reload/interrupt request follows overflow by four clocks.
        long edge = Clocks - PositiveModulo(Clocks - _dividerPhase, TimerDivider);
        _nextTimer = edge + (0x100 - TimerReload) * TimerDivider + 4;
    }

    internal void ConsumeCpuWork(long clocks)
    {
        if (clocks < 0) throw new ArgumentOutOfRangeException(nameof(clocks));
        while (clocks > 0)
        {
            DispatchTimer();
            long step = Math.Min(clocks, _nextTimer - Clocks);
            Advance(step);
            clocks -= step;
        }
        DispatchTimer();
    }

    internal void WaitUntil(long deadline)
    {
        if (deadline < Clocks) return;
        while (Clocks < deadline)
        {
            Advance(Math.Min(deadline, _nextTimer) - Clocks);
            DispatchTimer();
        }
    }

    internal void ConsumeUntil(ref long work, long deadline)
    {
        while (work > 0 && Clocks < deadline)
        {
            DispatchTimer();
            if (Clocks >= deadline) return;
            long step = Math.Min(work, Math.Min(deadline, _nextTimer) - Clocks);
            Advance(step);
            work -= step;
        }
        DispatchTimer();
    }

    internal void ConsumeUninterruptible(long clocks) => Advance(clocks);

    // The sound driver already advanced the APU for these CPU instructions.
    internal void AccountSoundWork(long clocks) => Clocks += clocks;

    private void Advance(long clocks)
    {
        Clocks += clocks;
        _advanceHardware?.Invoke(clocks);
    }

    private void DispatchTimer()
    {
        if (_servicingTimer || Clocks < _nextTimer) return;
        _servicingTimer = true;
        try
        {
            TimerCorrectionCounter = unchecked((byte)(TimerCorrectionCounter - 1));
            bool correction = TimerCorrectionCounter == 0;
            if (correction) TimerCorrectionCounter = 7;
            // The correction writes TMA-1 to TIMA before the next divider
            // edge. Do not reset this phase when the foreground loop stalls.
            if (correction)
            {
                // TIMA is rewritten 212 clocks after the vector, even when a
                // masked VBlank delayed service beyond the original overflow.
                long write = Clocks + 20 + 212;
                _nextTimer = write - PositiveModulo(write - _dividerPhase, TimerDivider) + 138 * TimerDivider + 4;
            }
            else _nextTimer += 137 * TimerDivider;
            TimerTicks++;
            int interruptWork = _interrupt(correction);
            if (interruptWork < 0 || interruptWork >= 137 * TimerDivider)
                throw new InvalidOperationException("bank0.s:timerInterrupt exceeded one timer period.");
            Clocks += 20 + interruptWork;
        }
        finally { _servicingTimer = false; }
    }

    private static long PositiveModulo(long value, int divisor) =>
        (value % divisor + divisor) % divisor;
}
