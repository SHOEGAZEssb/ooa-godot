namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateUnderwaterSurfacingMasksRom()
    {
        var rom = new FrontendRom();
        UnderwaterSurfacingDatabase masks = UnderwaterSurfacingDatabase.Shared;
        int comparisons = 0;
        bool Compare(int group, int room, int flags, int packed, bool pollutionFixed, int waterLevel)
        {
            rom[0xcc2d] = (byte)group; rom[0xcc30] = (byte)room;
            rom[0xcc34] = (byte)flags; rom[0xcc99] = (byte)packed;
            rom[0xcc39] = (byte)(group == 5 ? 7 : 0xff);
            rom[0xc6d6] = (byte)(pollutionFixed ? 1 : 0); // GLOBALFLAG_WATER_POLLUTION_FIXED $30.
            rom[0xc6e9] = (byte)waterLevel;
            bool expected = rom.CanLinkSurface();
            bool actual = masks.CanSurface(group, room, flags, packed, pollutionFixed,
                group == 5 ? 7 : 0xff, waterLevel);
            FailIf(actual != expected,
                $"Surfacing {group:x1}:{room:x2} flags=${flags:x2}, tile=${packed:x2}, pollution={pollutionFixed}, Jabu=${waterLevel:x2}: runtime={actual}, native={expected}.");
            comparisons++;
            return actual;
        }
        // Every room search, all group aliases, and every playable tile-sized
        // coordinate. Group $04/$05 masks use the large-room row extent.
        for (int group = 0; group < 8; group++)
        for (int room = 0; room < 256; room++)
        {
            FailIf(Compare(group, room, 0, 0x44, false, 0),
                "checkLinkCanSurface must reject a room without TILESETFLAG_UNDERWATER.");
            foreach (bool fixedWater in new[] { false, true })
            foreach (int level in new[] { 0x21, 0x22 })
            for (int y = 0; y < (group is 4 or 5 ? 11 : 8); y++)
            for (int x = 0; x < (group is 4 or 5 ? 15 : 10); x++)
                Compare(group, room, group is 4 or 5 ? 0x40 : 0x41,
                    y * 16 + x, fixedWater, level);
        }
        // Independent source boundaries, in addition to native comparisons.
        FailIf(!Compare(3, 0xb0, 0x41, 0x64, false, 0),
            "The clean-US past room $b0 bugfix must permit surfacing at tile $64.");
        FailIf(Compare(2, 0xa1, 0x41, 0x69, false, 0) ||
            !Compare(2, 0xa1, 0x41, 0x69, true, 0),
            "Pollution flag $30 must select room $a1's contiguous alternate mask at tile $69.");
        FailIf(Compare(2, 0xa2, 0x41, 0x22, false, 0) ||
            !Compare(2, 0xa2, 0x41, 0x22, true, 0),
            "Pollution flag $30 must replace room $a2's restrictions with the outdoor default.");
        FailIf(Compare(5, 0x4c, 0x40, 0x35, false, 0x21) ||
            !Compare(5, 0x4c, 0x40, 0x35, false, 0x22),
            "Jabu level low bits $02 must select the eleven-row alternate mask at tile $35.");
        FailIf(!Compare(2, 0x01, 0x41, 0x44, false, 0) ||
            Compare(4, 0x01, 0x40, 0x44, false, 0),
            "Missing surfacing records must use the outdoor-only default.");
        Godot.GD.Print($"Validated {comparisons} clean-US surfacing decisions: room/group aliases, tile masks, pollution alternatives, Jabu water levels, defaults and US bugfix.");
    }
}
