using System;

namespace Oracles;

// Clean-US Ages SRAM verification and serialized WRAM offsets.
internal static class AgesSaveLayout
{
    internal const int GlobalFlagsOffset = 0x120;
    internal static ReadOnlySpan<byte> VerificationString => "Z21216-0"u8;
}
