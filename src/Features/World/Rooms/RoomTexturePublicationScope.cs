using System;

namespace Oracles;

// Stack-owned scope: do not retain across original updates or host frames.
internal readonly record struct RoomTexturePublicationScope(RoomTexturePublication Owner) : IDisposable
{
    public void Dispose() => Owner.Complete();
}
