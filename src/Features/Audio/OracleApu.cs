using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Four shared CGB voices, clocked even when the sound sequencer is disabled.
/// Register semantics follow Pan Docs Audio_Registers / Audio_details (CGB-04/05).
/// Samples integrate the digital waveform between edges before the CGB HPF.
/// </summary>
internal sealed class OracleApu
{
    internal const int ClockRate = 4194304;
    private static readonly int[] DutyPatterns = [0x80, 0x81, 0xe1, 0x7e];
    internal static readonly double HighPassFactor = Math.Pow(0.998943, (double)ClockRate / OracleSoundEngine.SampleRate);
    private readonly byte[] _registers = new byte[0x30];
    private readonly OracleApuVoice[] _voices = [new(), new(), new(), new()];
    private readonly Action<Vector2>? _sampleSink;
    private bool _powered = true;
    private int _divider = 8192;
    private int _frameStep;
    private int _samplePhase;
    private int _sampleClocks;
    private double _leftArea, _rightArea, _leftCapacitor, _rightCapacitor;
    internal long Clocks { get; private set; }

    internal OracleApu(Action<Vector2>? sampleSink = null) => _sampleSink = sampleSink;
    internal OracleApuVoice Voice(int index) => _voices[index];
    internal int Register(int address) => _registers[address - 0xff10];
    internal int Frequency(int index)
    {
        int offset = index * 5;
        return _registers[offset + 3] | ((_registers[offset + 4] & 7) << 8);
    }

    internal int Read(int address)
    {
        if (address == 0xff26)
        {
            int result = _powered ? 0xf0 : 0x70;
            for (int i = 0; i < 4; i++) if (_voices[i].Enabled) result |= 1 << i;
            return result;
        }
        if (address >= 0xff30) return Register(address);
        int mask = address switch
        {
            0xff10 => 0x80, 0xff11 or 0xff16 => 0x3f,
            0xff12 or 0xff17 or 0xff21 or 0xff22 or 0xff24 or 0xff25 => 0,
            0xff14 or 0xff19 or 0xff1e or 0xff23 => 0xbf,
            0xff1a => 0x7f, 0xff1c => 0x9f, _ => 0xff
        };
        return Register(address) | mask;
    }

    internal void Write(int address, int value)
    {
        value &= 255;
        if (address >= 0xff30) { _registers[address - 0xff10] = (byte)value; return; }
        if (address == 0xff26)
        {
            bool powered = (value & 0x80) != 0;
            if (_powered && !powered)
            {
                Array.Clear(_registers, 0, 0x17);
                for (int i = 0; i < 4; i++) _voices[i] = new();
            }
            if (!_powered && powered) _frameStep = 0;
            _powered = powered;
            return;
        }
        if (!_powered || address is 0xff15 or 0xff1f or >= 0xff27) return;
        int offset = address - 0xff10;
        int old = _registers[offset];
        _registers[offset] = (byte)value;
        if (offset >= 0x14) return;
        int index = offset / 5, register = offset % 5;
        OracleApuVoice voice = _voices[index];
        if (register == 0)
        {
            if (index == 2)
            {
                voice.DacEnabled = (value & 0x80) != 0;
                if (!voice.DacEnabled) voice.Enabled = false;
            }
            // The original driver only writes NR10=$08 (sweep disabled).
            else if (index == 0 && value != 8 && value != 0)
                throw new InvalidOperationException($"code/audio.s wrote unsupported NR10 sweep ${value:x2}.");
        }
        else if (register == 1)
            voice.Length = index == 2 ? 256 - value : 64 - (value & 63);
        else if (register == 2 && index != 2)
        {
            // CGB envelope writes can alter a running envelope before the next
            // trigger; notably the driver writes NR42 separately from NR44.
            if (voice.Enabled)
            {
                if ((old & 7) == 0 && voice.EnvelopeRunning) voice.Volume++;
                else if ((old & 8) == 0) voice.Volume += 2;
                if (((old ^ value) & 8) != 0) voice.Volume = 16 - voice.Volume;
                voice.Volume &= 15;
            }
            voice.DacEnabled = (value & 0xf8) != 0;
            if (!voice.DacEnabled) voice.Enabled = false;
        }
        else if (register == 4)
        {
            bool enabled = (value & 0x40) != 0;
            if (!voice.LengthEnabled && enabled && (_frameStep & 1) != 0 && voice.Length > 0)
            {
                if (--voice.Length == 0) voice.Enabled = false;
            }
            voice.LengthEnabled = enabled;
            if ((value & 0x80) != 0) Trigger(index);
        }
    }

    private int Period(int index)
    {
        if (index != 3) return (2048 - Frequency(index)) * (index == 2 ? 2 : 4);
        int nr43 = _registers[0x12], shift = nr43 >> 4, divider = nr43 & 7;
        return (divider == 0 ? 8 : divider * 16) << shift;
    }

    private void Trigger(int index)
    {
        OracleApuVoice voice = _voices[index];
        voice.Enabled = voice.DacEnabled;
        voice.Clocked = true;
        if (voice.Length == 0)
        {
            voice.Length = index == 2 ? 256 : 64;
            if (voice.LengthEnabled && (_frameStep & 1) != 0) voice.Length--;
        }
        voice.Timer = Period(index) + (index < 2 ? voice.Timer & 3 : 0);
        if (index == 2)
            voice.Position = 0; // The previous sample remains latched until sample 1.
        else
        {
            int envelope = _registers[index * 5 + 2];
            voice.Volume = envelope >> 4;
            voice.EnvelopeCounter = (envelope & 7) == 0 ? 8 : envelope & 7;
            if (_frameStep == 7) voice.EnvelopeCounter++;
            voice.EnvelopeRunning = true;
            if (index == 3) voice.Lfsr = 0;
        }
    }

    internal void AdvanceClocks(int clocks)
    {
        while (clocks > 0)
        {
            int step = Math.Min(clocks, _divider);
            step = Math.Min(step, (ClockRate - _samplePhase + OracleSoundEngine.SampleRate - 1) / OracleSoundEngine.SampleRate);
            for (int i = 0; i < 4; i++)
                if (_powered && _voices[i].Clocked) step = Math.Min(step, _voices[i].Timer);
            if (_sampleSink is not null) Integrate(step);
            clocks -= step;
            Clocks += step;
            _divider -= step;
            _sampleClocks += step;
            _samplePhase += step * OracleSoundEngine.SampleRate;
            for (int i = 0; i < 4; i++)
            {
                OracleApuVoice voice = _voices[i];
                if (!_powered || !voice.Clocked) continue;
                voice.Timer -= step;
                if (voice.Timer != 0) continue;
                voice.Timer = Period(i);
                if (i < 2)
                {
                    voice.Position = (voice.Position + 1) & 7;
                    voice.DutyStarted = true;
                }
                else if (i == 2)
                {
                    voice.Position = (voice.Position + 1) & 31;
                    int packed = _registers[0x20 + voice.Position / 2];
                    voice.Sample = (voice.Position & 1) == 0 ? packed >> 4 : packed & 15;
                }
                else if ((_registers[0x12] >> 4) < 14)
                {
                    int bit = (~(voice.Lfsr ^ (voice.Lfsr >> 1))) & 1;
                    voice.Lfsr = (voice.Lfsr >> 1) | (bit << 14);
                    if ((_registers[0x12] & 8) != 0) voice.Lfsr = (voice.Lfsr & ~0x40) | (bit << 6);
                }
            }
            if (_divider == 0)
            {
                _divider = 8192;
                if (_powered) ClockFrameSequencer();
            }
            if (_samplePhase >= ClockRate)
            {
                _samplePhase -= ClockRate;
                if (_sampleSink is not null) EmitSample();
                _sampleClocks = 0;
            }
        }
    }

    private void ClockFrameSequencer()
    {
        for (int index = 0; index < 4; index++)
        {
            OracleApuVoice voice = _voices[index];
            if ((_frameStep & 1) == 0 && voice.LengthEnabled && voice.Length > 0 && --voice.Length == 0)
                voice.Enabled = false;
            if (_frameStep != 7 || index == 2 || !voice.EnvelopeRunning) continue;
            if (--voice.EnvelopeCounter > 0) continue;
            int envelope = _registers[index * 5 + 2], period = envelope & 7;
            voice.EnvelopeCounter = period == 0 ? 8 : period;
            if (period == 0) continue;
            int next = voice.Volume + ((envelope & 8) != 0 ? 1 : -1);
            if (next is < 0 or > 15) voice.EnvelopeRunning = false;
            else voice.Volume = next;
        }
        _frameStep = (_frameStep + 1) & 7;
    }

    internal int DigitalOutput(int index)
    {
        OracleApuVoice voice = _voices[index];
        if (!voice.Enabled) return 0;
        if (index < 2 && !voice.DutyStarted) return 0;
        if (index < 2)
            return ((DutyPatterns[_registers[index * 5 + 1] >> 6] >> voice.Position) & 1) * voice.Volume;
        if (index == 3) return (voice.Lfsr & 1) * voice.Volume;
        int level = (_registers[0x0c] >> 5) & 3;
        return level == 0 ? 0 : voice.Sample >> (level - 1);
    }

    private void Integrate(int clocks)
    {
        double left = 0, right = 0;
        int routing = _registers[0x15], volume = _registers[0x14];
        for (int i = 0; i < 4; i++)
        {
            if (!_voices[i].DacEnabled) continue;
            double analog = 1.0 - DigitalOutput(i) / 7.5;
            if ((routing & (1 << i)) != 0) right += analog;
            if ((routing & (0x10 << i)) != 0) left += analog;
        }
        _leftArea += left * (((volume >> 4) & 7) + 1) / 32 * clocks;
        _rightArea += right * ((volume & 7) + 1) / 32 * clocks;
    }

    private void EmitSample()
    {
        bool connected = false;
        foreach (OracleApuVoice voice in _voices) connected |= voice.DacEnabled;
        double left = 0, right = 0;
        if (connected)
        {
            double inputLeft = _leftArea / _sampleClocks, inputRight = _rightArea / _sampleClocks;
            left = inputLeft - _leftCapacitor;
            right = inputRight - _rightCapacitor;
            _leftCapacitor = inputLeft - left * HighPassFactor;
            _rightCapacitor = inputRight - right * HighPassFactor;
        }
        _sampleSink!(new Vector2((float)Math.Clamp(left, -1, 1), (float)Math.Clamp(right, -1, 1)));
        _leftArea = _rightArea = 0;
    }
}
