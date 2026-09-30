using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSoundMixerBoundaries()
    {
        var bulkSamples = new List<Vector2>();
        var splitSamples = new List<Vector2>();
        var bulk = new OracleApu(bulkSamples.Add);
        var split = new OracleApu(splitSamples.Add);
        void Write(int address, int value)
        {
            bulk.Write(address, value);
            split.Write(address, value);
            bulk.AdvanceClocks(0);
            split.AdvanceClocks(0);
        }
        void Advance(int clocks)
        {
            bulk.AdvanceClocks(clocks);
            // Instruction-sized fragments, including single-clock calls,
            // must publish mixer edges at exactly the same sample phase.
            int[] chunks = [1, 2, 3, 4, 7, 31, 97, 256];
            for (int i = 0; clocks > 0; i++)
            {
                int step = Math.Min(clocks, chunks[i % chunks.Length]);
                split.AdvanceClocks(step);
                clocks -= step;
            }
            FailIf(bulk.Clocks != split.Clocks || bulkSamples.Count != splitSamples.Count,
                "APU clock fragmentation changed sample count or elapsed clocks.");
            for (int voice = 0; voice < 4; voice++)
                FailIf(bulk.DigitalOutput(voice) != split.DigitalOutput(voice),
                    $"APU clock fragmentation changed voice {voice} output.");
        }
        Write(0xff26, 0x80);
        Write(0xff24, 0x77);
        Write(0xff25, 0xff);
        for (int i = 0; i < 16; i++) Write(0xff30 + i, (i * 13 + 7) & 255);
        Write(0xff10, 8);
        Write(0xff11, 0x80); Write(0xff12, 0xa1); Write(0xff13, 0xfc); Write(0xff14, 0x87);
        Write(0xff16, 0x40); Write(0xff17, 0x59); Write(0xff18, 0xd3); Write(0xff19, 0x86);
        Write(0xff1a, 0x80); Write(0xff1b, 0); Write(0xff1c, 0x20); Write(0xff1d, 0xfd); Write(0xff1e, 0x87);
        Write(0xff20, 0); Write(0xff21, 0xf1); Write(0xff22, 0x03); Write(0xff23, 0x80);
        Advance(95); Advance(1); Advance(1);
        Advance(65536 - 97); Advance(1); // sample and frame-sequencer boundaries
        Write(0xff25, 0xa5); Advance(8191);
        Write(0xff24, 0x23); Advance(1); Advance(8192);
        Write(0xff11, 0xc0); Write(0xff12, 0x08); Advance(8192);
        Write(0xff12, 0x20); Write(0xff14, 0x87); Advance(65536);
        Write(0xff1c, 0x60); Write(0xff30, 0xef); Advance(65536);
        Write(0xff22, 0x0b); Write(0xff23, 0x80); Advance(65536);
        // Length expiration, DAC disconnect/reconnect, and power cycling.
        Write(0xff16, 0x3f); Write(0xff19, 0xc6); Advance(16384);
        Write(0xff1b, 0xff); Write(0xff1e, 0xc7); Advance(16384);
        Write(0xff12, 0); Write(0xff17, 0); Write(0xff1a, 0); Write(0xff21, 0);
        Advance(8192);
        Write(0xff26, 0); Write(0xff30, 0xab); Advance(8192);
        Write(0xff26, 0x80); Write(0xff24, 0x77); Write(0xff25, 0xff);
        Write(0xff12, 0xf0); Write(0xff14, 0x80); Advance(65536);
        FailIf(!CollectionsMarshal.AsSpan(bulkSamples).SequenceEqual(CollectionsMarshal.AsSpan(splitSamples)),
            "APU mixer PCM changed with instruction-sized clock fragments.");
        string hash = Convert.ToHexString(SHA256.HashData(
            MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(bulkSamples))));
        // Captured from the existing mixer before its invalidation cache was
        // introduced. This checks bit-exact optimization equivalence; it is
        // not a replacement for the independent ROM sound-driver fixtures.
        FailIf(bulkSamples.Count != 4220 ||
            hash != "BF83A8A56426A8175B1F63A08C7A6EE23DABFAC546452FE7855BCFD0AB89038F",
            $"APU mixer changed pre-optimization PCM: {bulkSamples.Count} samples, SHA-256={hash}.");
    }
}
