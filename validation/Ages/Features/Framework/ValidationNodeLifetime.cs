using System;
using Godot;

namespace Oracles;

internal static class ValidationNodeLifetime
{
    internal static ulong[] OrphanIds()
    {
        if (!OS.IsDebugBuild())
            throw new InvalidOperationException("Validation node-lifetime checks require a debug Godot build with Node.GetOrphanNodeIds support.");
        // Godot 4.7's generated Array<int> accessor narrows 64-bit ObjectIDs.
        // The untyped view preserves the original Variant integer values.
        using var orphans = (Godot.Collections.Array)Node.GetOrphanNodeIds();
        var ids = new ulong[orphans.Count];
        for (int index = 0; index < ids.Length; index++)
            ids[index] = orphans[index].AsUInt64();
        return ids;
    }
}
