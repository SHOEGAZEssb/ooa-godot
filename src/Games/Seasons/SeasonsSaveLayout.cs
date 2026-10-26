using System;

namespace Oracles;

// include/wram.s and code/fileManagement.s, supported US Seasons.
internal static class SeasonsSaveLayout
{
    internal const int GlobalFlagsOffset = 0x11a;
    internal static ReadOnlySpan<byte> VerificationString => "Z11216-0"u8;
}
