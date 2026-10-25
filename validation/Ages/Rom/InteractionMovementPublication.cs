using System;

namespace Oracles;

// Reuse an existing gameplay/native comparison. Stage before PARTs and observe
// after INTERACTION-owned event lanes, before later phases reuse these bytes.
internal sealed class InteractionMovementPublication
{
    private readonly byte[] _observed = new byte[4];
    internal int Publications { get; private set; }

    internal InteractionMovementPublication(RoomEntityManager entities, OracleRuntimeState memory)
    {
        Action before = entities.PartPhaseUpdate, after = entities.InteractionPhaseUpdate;
        entities.PartPhaseUpdate = () =>
        {
            before();
            for (int i = 0; i < 4; i++) memory.SetWramByte(0xcec0 + i, (byte)(0xa5 + i));
        };
        entities.InteractionPhaseUpdate = () =>
        {
            after();
            for (int i = 0; i < 4; i++) _observed[i] = memory.ReadWramByte(0xcec0 + i);
        };
    }

    internal static void Seed(SomariaRom native)
    {
        for (int i = 0; i < 4; i++) native[0xcec0 + i] = (byte)(0xa5 + i);
    }

    internal void Compare(SomariaRom native, string context)
    {
        bool published = false;
        for (int i = 0; i < 4; i++)
        {
            published |= native[0xcec0 + i] != (byte)(0xa5 + i);
            if (_observed[i] != native[0xcec0 + i])
                throw new InvalidOperationException($"{context}: INTERACTION movement byte ${0xcec0 + i:x4} differs: port=${_observed[i]:x2}, ROM=${native[0xcec0 + i]:x2}.");
        }
        if (published) Publications++;
    }
}
