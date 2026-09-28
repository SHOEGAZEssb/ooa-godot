// Headless transport for the unmodified libgambatte.dll shipped with BizHawk
// 1.11.5. Frame pacing follows Gambatte.cs:FrameAdvance, EqualLengthFrames=true.
// Compiled separately as x86/.NET Framework; never part of the Godot game.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

internal static class TasReference
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int InputGetter();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Execute(uint address);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr gambatte_create();
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern void gambatte_destroy(IntPtr core);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern int gambatte_load(IntPtr core, byte[] rom, uint length, long now, uint flags);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern void gambatte_reset(IntPtr core, long now);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern int gambatte_runfor(IntPtr core, short[] sound, ref uint samples);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern void gambatte_setinputgetter(IntPtr core, InputGetter callback);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern void gambatte_setexeccallback(IntPtr core, Execute callback);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern bool gambatte_getmemoryarea(IntPtr core, int area, ref IntPtr data, ref int length);

    static int Main(string[] args)
    {
        IntPtr core = IntPtr.Zero;
        try
        {
            byte[] rom = File.ReadAllBytes(args[0]);
            byte[] input = File.ReadAllBytes(args[1]);
            int limit = Math.Min(int.Parse(args[2]), input.Length / 2);
            core = gambatte_create();
            // The published movie uses GBACGB=true. RTC is irrelevant to MBC5.
            if (gambatte_load(core, rom, (uint)rom.Length, 0, 2) != 0)
                throw new Exception("Gambatte rejected the ROM.");
            IntPtr wram = IntPtr.Zero, hram = IntPtr.Zero;
            int wlen = 0, hlen = 0;
            if (!gambatte_getmemoryarea(core, 2, ref wram, ref wlen) || wlen != 32768 ||
                !gambatte_getmemoryarea(core, 5, ref hram, ref hlen) || hlen < 127)
                throw new Exception("Unexpected pinned Gambatte memory domains.");
            int buttons = 0, sampled = 0, pressed = 0, frame = 0, update = 0, epoch = 0;
            var serializer = new JavaScriptSerializer();
            InputGetter getInput = delegate { return buttons; };
            Execute onExecute = delegate(uint address)
            {
                if (address == 0x0936)
                {
                    sampled = Marshal.ReadByte(wram, 0x481);
                    pressed = Marshal.ReadByte(wram, 0x482);
                }
                if (address != 0x0933) return;
                byte[] ram = new byte[wlen], high = new byte[127];
                Marshal.Copy(wram, ram, 0, ram.Length);
                Marshal.Copy(hram, high, 0, high.Length);
                Console.WriteLine("TAS_REFERENCE " + serializer.Serialize(new {
                    update = update++, movieFrame = frame, resetEpoch = epoch,
                    input = sampled, pressed = pressed,
                    wram = Convert.ToBase64String(ram), hram = Convert.ToBase64String(high)
                }));
                // Backpressure: execute no next game update before the caller
                // has checked this one. EOF/stop terminates without a save write.
                if (Console.ReadLine() != "continue") Environment.Exit(0);
            };
            gambatte_setinputgetter(core, getInput);
            gambatte_setexeccallback(core, onExecute);
            uint overflow = 0;
            short[] sound = new short[(35112 + 2064) * 2];
            for (frame = 0; frame < limit; frame++)
            {
                buttons = input[frame * 2];
                if (input[frame * 2 + 1] != 0)
                {
                    gambatte_reset(core, 0);
                    epoch++;
                    sampled = pressed = 0;
                }
                do
                {
                    uint samples = 35112 - overflow;
                    gambatte_runfor(core, sound, ref samples);
                    overflow += samples;
                } while (overflow < 35112);
                overflow -= 35112;
            }
            Console.WriteLine("TAS_END " + serializer.Serialize(new { movieFrames = frame, snapshots = update, complete = frame == input.Length / 2 }));
            GC.KeepAlive(getInput);
            GC.KeepAlive(onExecute);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
        finally { if (core != IntPtr.Zero) gambatte_destroy(core); }
    }
}
