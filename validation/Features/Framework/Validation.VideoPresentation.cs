using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFrontendVideoPresentation()
    {
        // Independent loadGraphics.s:gbaModePaletteData expectations. The
        // original GBA profile saturates RGB5 $1c, three steps before $1f.
        byte[] table = OracleGraphicsData.ReadBytes("res://assets/oracle/metadata/gba_palette_components.bin", 32);
        FailIf(!table.SequenceEqual(new byte[] {
            0,5,7,8,10,11,12,14,16,17,18,19,20,21,22,23,
            24,25,26,27,27,28,28,29,29,30,30,30,31,31,31,31 }),
            "GBA palette conversion differs from the clean source's RGB component table.");

        // Pinned native LCD output: four scanlines to settle, then one frame
        // before blank publication; the first frame after enabling is blank.
        // These boundaries are independent of any game update or movie frame.
        var observed = new List<bool>();
        int installed = 0;
        var video = new OracleVideoPresentation(observed.Add);
        video.SetLcd(true, 0);
        video.AdvanceTo(271776);
        FailIf(!observed.SequenceEqual(new[] { false }), "Cold LCD enable did not discard its first frame.");
        video.Stage([() => installed++], loading: true);
        video.SetLcd(false, 300000);
        video.AdvanceTo(444095);
        FailIf(installed != 0 || observed.Count != 1,
            "LCD-off exposed a destination or blanked before a completed blank frame.");
        video.AdvanceTo(444096);
        FailIf(installed != 1 || observed[^1] != true,
            "Destination graphics were not installed behind the first completed white frame.");
        video.SetLcd(true, 500000);
        video.CompleteLoading();
        video.AdvanceTo(771775);
        FailIf(observed[^1] != true, "LCD enable exposed the first discarded frame.");
        video.AdvanceTo(771776);
        FailIf(observed[^1] || installed != 1, "LCD frame publication retained white or reinstalled the page.");

        // Enabling during the settle interval cancels the pending blank.
        video.Stage([() => installed++], loading: true);
        video.SetLcd(false, 800000);
        video.SetLcd(true, 801000);
        video.CompleteLoading();
        video.AdvanceTo(1072775);
        FailIf(installed != 1 || observed[^1], "A cancelled short LCD pulse introduced a white flash.");
        video.AdvanceTo(1072776);
        FailIf(installed != 2 || observed[^1], "Short LCD restart did not publish the completed destination frame.");

        // VBlank uploads affect the following scanout. The next foreground
        // update may already be staging a cursor while the panel is pending.
        int panel = 0, cursor = 0;
        var uploads = new OracleVideoPresentation(_ => { });
        uploads.SetLcd(true, 0);
        uploads.Stage([() => panel++], loading: true);
        uploads.AdvanceTo(271776);
        uploads.CompleteLoading();
        FailIf(panel != 0, "LCD-enabled loading published during its upload VBlank.");
        uploads.Stage([() => cursor++], loading: false);
        uploads.AdvanceTo(412224);
        FailIf(panel != 1 || cursor != 0, "A staged cursor leaked into the preceding panel's completed frame.");
        uploads.CompleteLoading();
        uploads.AdvanceTo(552672);
        FailIf(panel != 1 || cursor != 1, "The following completed frame lost or duplicated its OAM upload.");

        // Host batching cannot collapse the retained/blank/reveal sequence.
        foreach (int quantum in new[] { 4, 912, 140448, 1000000 })
        {
            var frames = new List<bool>();
            var batched = new OracleVideoPresentation(frames.Add);
            batched.SetLcd(true, 0);
            batched.AdvanceTo(300000);
            batched.SetLcd(false, 300000);
            for (int clock = 300000; clock < 500000; clock += quantum) batched.AdvanceTo(clock);
            batched.SetLcd(true, 500000);
            for (int clock = 500000; clock < 800000; clock += quantum) batched.AdvanceTo(clock);
            batched.AdvanceTo(800000);
            FailIf(!frames.SequenceEqual(new[] { false, true, true, false }),
                $"Host chunks of {quantum} clocks changed the LCD frame sequence.");
        }
    }
}
