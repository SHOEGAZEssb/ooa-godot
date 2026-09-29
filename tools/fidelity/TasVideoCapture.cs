// Validation transport only. Observe video memory at the start of scanout, then
// retain that observation until Gambatte publishes the completed frame.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal sealed class TasVideoCapture
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Scanline();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void DataFunction(IntPtr data, int length, string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void SectionFunction(string name);
    [StructLayout(LayoutKind.Sequential)] struct StateFunctions { public DataFunction Save, Load; public SectionFunction Enter, Exit; }
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern void gambatte_setscanlinecallback(IntPtr core, Scanline callback, int line);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern bool gambatte_getmemoryarea(IntPtr core, int area, ref IntPtr data, ref int length);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern byte gambatte_cpuread(IntPtr core, ushort address);
    [DllImport("libgambatte.dll", CallingConvention=CallingConvention.Cdecl)] static extern void gambatte_newstatesave_ex(IntPtr core, ref StateFunctions funcs);

    readonly IntPtr core, vram, oam;
    readonly Scanline scanline;
    readonly List<string> sections = new List<string>();
    StateFunctions functions;
    byte[] bg = new byte[64], obj = new byte[64];
    bool blank;
    uint clock;
    int seenFields;
    object pending, displayed;
    long scanouts, publishedScanout;
    public long Serial { get; private set; }

    public TasVideoCapture(IntPtr instance)
    {
        core = instance;
        vram = Domain(0, 16384); oam = Domain(4, 160);
        functions = new StateFunctions {
            Save = SaveField, Load = null,
            Enter = delegate(string name) { sections.Add(name); },
            Exit = delegate(string name) { sections.RemoveAt(sections.Count - 1); }
        };
        scanline = Capture;
        gambatte_setscanlinecallback(core, scanline, 0);
    }

    IntPtr Domain(int area, int expected)
    {
        IntPtr data = IntPtr.Zero; int length = 0;
        if (!gambatte_getmemoryarea(core, area, ref data, ref length) || length != expected)
            throw new InvalidOperationException("Unexpected Gambatte video domain " + area);
        return data;
    }

    void SaveField(IntPtr data, int length, string name)
    {
        string path = string.Join("/", sections) + "/" + name;
        if (path == "p_->cpu/cycleCounter_" && length == 4) { clock = unchecked((uint)Marshal.ReadInt32(data)); seenFields |= 1; }
        if (path == "p_->cpu/memory/blanklcd" && length == 1) { blank = Marshal.ReadByte(data) != 0; seenFields |= 2; }
        if (path == "p_->cpu/memory/display/bgpData" && length == 64) { Marshal.Copy(data, bg, 0, 64); seenFields |= 4; }
        if (path == "p_->cpu/memory/display/objpData" && length == 64) { Marshal.Copy(data, obj, 0, 64); seenFields |= 8; }
    }

    public uint ReadClock()
    {
        seenFields = 0;
        gambatte_newstatesave_ex(core, ref functions);
        if (seenFields != 15) throw new InvalidOperationException("Pinned Gambatte video serializer fields changed.");
        return clock;
    }

    void Capture()
    {
        scanouts++;
        ReadClock();
        byte lcd = gambatte_cpuread(core, 0xff40);
        int map = (lcd & 8) != 0 ? 0x1c00 : 0x1800;
        byte[] tiles = new byte[1024], attributes = new byte[1024], sprites = new byte[160];
        Marshal.Copy(IntPtr.Add(vram, map), tiles, 0, 1024);
        Marshal.Copy(IntPtr.Add(vram, map + 0x2000), attributes, 0, 1024);
        Marshal.Copy(oam, sprites, 0, 160);
        pending = new { blank, lcd, scx = gambatte_cpuread(core, 0xff43), scy = gambatte_cpuread(core, 0xff42),
            tiles = Convert.ToBase64String(tiles), attributes = Convert.ToBase64String(attributes),
            oam = Convert.ToBase64String(sprites), bg = Convert.ToBase64String(bg), obj = Convert.ToBase64String(obj) };
    }

    public void Publish()
    {
        // LCD-off and the first discarded LCD-on frame have no new scanout.
        // Do not relabel stale video memory as newly displayed content.
        displayed = (gambatte_cpuread(core, 0xff40) & 0x80) == 0 || scanouts == publishedScanout
            ? new { blank = true } : pending;
        publishedScanout = scanouts;
        Serial++;
    }

    public object Displayed { get { return displayed; } }
}
