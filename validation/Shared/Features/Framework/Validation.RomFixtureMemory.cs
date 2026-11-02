using System.IO;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateRomFixtureMemory()
    {
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var fixture = new RomMemoryValidationFixture(game);
            var other = new RomMemoryValidationFixture(game);
            var bytes = ValidationRom.LoadCleanUs(game).Span;
            FailIf(fixture.Peek(0x23) != bytes[0x23] || fixture.Peek(0x4023) != bytes[0x10023],
                $"{game}: ROM fixture lost fixed/selected-bank addressing.");
            fixture.Poke(0xc200, 0x145);
            FailIf(fixture.Peek(0xc200) != 0x45 || other.Peek(0xc200) != 0,
                $"{game}: ROM fixtures must retain independent byte-width RAM.");
            fixture.SetCaller([0x3e, 0x42]);
            FailIf(fixture.Peek(0xc100) != 0x3e || fixture.Peek(0xc101) != 0x42,
                $"{game}: ROM fixture lost its bounded synthetic caller.");
            fixture.Poke(0x2222, 5);
            Reject(() => fixture.Peek(0x4000), "$05", "$4000");
            Reject(() => fixture.Poke(0xc201, 1), "write", "$c201");
            Reject(() => fixture.Peek(0xc102), "read", "$c102");
        }

        static void Reject(Action operation, string detail, string address)
        {
            try { operation(); }
            catch (InvalidDataException error) when (error.Message.Contains("memory policy", StringComparison.Ordinal) &&
                error.Message.Contains(detail, StringComparison.Ordinal) && error.Message.Contains(address, StringComparison.Ordinal)) { return; }
            throw new InvalidOperationException($"ROM fixture must reject undeclared {detail} at {address} with its source context.");
        }
    }
}
