using Godot;
using System;

namespace oracleofages;

/// <summary>Converts timed mixer steps to band-limited 44100 Hz samples.</summary>
internal sealed class OracleAudioResampler
{
    private const int Taps = 64;
    private const int Phases = 256;
    private const int RingSize = 128;
    private static readonly double[][] Impulses = BuildImpulses();
    private readonly double[] _left = new double[RingSize];
    private readonly double[] _right = new double[RingSize];
    private int _position;
    private double _inputLeft, _inputRight, _outputLeft, _outputRight;

    internal void SetInput(double left, double right, int samplePhase)
    {
        double deltaLeft = left - _inputLeft, deltaRight = right - _inputRight;
        if (deltaLeft == 0 && deltaRight == 0) return;
        _inputLeft = left;
        _inputRight = right;
        double phase = (double)samplePhase * Phases / OracleApu.ClockRate;
        int phaseIndex = (int)phase;
        double fraction = phase - phaseIndex;
        double[] impulse = Impulses[phaseIndex], next = Impulses[phaseIndex + 1];
        for (int i = 0; i < Taps; i++)
        {
            int index = (_position + i) & (RingSize - 1);
            double coefficient = impulse[i] + fraction * (next[i] - impulse[i]);
            _left[index] += deltaLeft * coefficient;
            _right[index] += deltaRight * coefficient;
        }
    }

    internal Vector2 Read()
    {
        _outputLeft += _left[_position];
        _outputRight += _right[_position];
        _left[_position] = _right[_position] = 0;
        _position = (_position + 1) & (RingSize - 1);
        return new Vector2((float)_outputLeft, (float)_outputRight);
    }

    private static double[][] BuildImpulses()
    {
        // A fractional-delay, Blackman-windowed sinc. Integrating these
        // impulses reconstructs each mixer step, with a fixed ~0.72 ms delay.
        // Box-averaging an APU waveform alone cannot reject frequencies above
        // Nyquist: their aliases are already audible before any later filter.
        const double cutoff = 0.45; // cycles per output sample, below Nyquist
        var phases = new double[Phases + 1][];
        for (int phase = 0; phase <= Phases; phase++)
        {
            var impulse = phases[phase] = new double[Taps];
            double sum = 0;
            for (int i = 0; i < Taps; i++)
            {
                double x = i - (Taps - 1) / 2.0 - (double)phase / Phases;
                double windowAngle = 2 * Math.PI * x / Taps;
                double window = Math.Abs(x) >= Taps / 2.0 ? 0 :
                    0.42 + 0.5 * Math.Cos(windowAngle) + 0.08 * Math.Cos(2 * windowAngle);
                double sinc = x == 0 ? 2 * cutoff : Math.Sin(2 * Math.PI * cutoff * x) / (Math.PI * x);
                sum += impulse[i] = sinc * window;
            }
            // Preserve DC and the original mixer's relative channel gains.
            for (int i = 0; i < Taps; i++) impulse[i] /= sum;
        }
        return phases;
    }
}
