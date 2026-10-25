using System;

namespace Oracles;

// Clean-US Ages SRAM verification and serialized WRAM offsets.
internal static class AgesSaveLayout
{
    internal const int WramBase = 0xc5b0;
    internal const int ChecksumOffset = 0x000;
    internal const int VerificationOffset = 0x002;
    internal const int UnappraisedRingsOffset = 0x010;
    internal const int GlobalFlagsOffset = 0x120;
    internal const int Group0RoomFlagsOffset = 0x150;
    internal const int Group1RoomFlagsOffset = 0x250;
    internal const int Group4RoomFlagsOffset = 0x350;
    internal const int Group5RoomFlagsOffset = 0x450;
    internal static ReadOnlySpan<byte> VerificationString => "Z21216-0"u8;
}
