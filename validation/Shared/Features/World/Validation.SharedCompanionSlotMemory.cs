
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedCompanionSlotMemory()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var memory = OracleRuntimeState.CreateInitialized(game);
            for (int offset = 0; offset < 0x40; offset++) memory.SetWramByte(0xd100 + offset, (byte)(0x80 + offset));
            memory.SetWramByte(0xd100, 3); memory.SetWramByte(0xd101, 0x0c);
            memory.SetWramByte(0xd0ff, 0xa5); memory.SetWramByte(0xd140, 0x5a);
            byte[] before = Enumerable.Range(0, 0x40).Select(offset => memory.ReadWramByte(0xd100 + offset)).ToArray();
            CompanionRuntimeState.Update(memory, 0x0c, new(0x68 + .75f, 0x38 + .25f), 1);
            // include/structs.s: direction+$08; Y=$0a/$0b; X=$0c/$0d.
            foreach (int offset in Enumerable.Range(0, 0x40).Except(new[] { 8, 10, 11, 12, 13 }))
                FailIf(memory.ReadWramByte(0xd100 + offset) != before[offset],
                    $"{game}: motion must preserve native slot byte $d1{offset:x2}, including subid/var03/state/substate.");
            FailIf(memory.ReadWramByte(0xd108) != 1 || memory.ReadWramByte(0xd10a) != 0x40 || memory.ReadWramByte(0xd10b) != 0x38 ||
                memory.ReadWramByte(0xd10c) != 0xc0 || memory.ReadWramByte(0xd10d) != 0x68 ||
                CompanionRuntimeState.Read(memory).Position != new Vector2(104.75f, 56.25f),
                $"{game}: motion must retain native 8.8 coordinate words, not overwrite state bytes with coordinate highs.");
            CompanionRuntimeState.Update(memory, 0x0c, new(-.25f, 255.5f), 3);
            FailIf(memory.ReadWramByte(0xd10c) != 0xc0 || memory.ReadWramByte(0xd10d) != 0xff ||
                memory.ReadWramByte(0xd10a) != 0x80 || memory.ReadWramByte(0xd10b) != 0xff,
                $"{game}: native coordinate words must wrap at 16 bits.");
            CompanionRuntimeState.Clear(memory, 0x0b);
            FailIf(!CompanionRuntimeState.IsActive(memory, 0x0c), $"{game}: another ID must not retire the reserved owner.");
            CompanionRuntimeState.Clear(memory, 0x0c);
            FailIf(Enumerable.Range(0, 0x40).Any(offset => memory.ReadWramByte(0xd100 + offset) != 0) ||
                memory.ReadWramByte(0xd0ff) != 0xa5 || memory.ReadWramByte(0xd140) != 0x5a,
                $"{game}: native itemDelete clears exactly $40 reserved-slot bytes, preserving both adjacent categories.");
            CompanionRuntimeState.Begin(memory, 0x0c, new(104.75f, 56.25f), 1);
            FailIf(memory.ReadWramByte(0xd100) != 3 || memory.ReadField("wLinkObjectIndex") != 0xd0 ||
                CompanionRuntimeState.Read(memory).Position != new Vector2(104.75f, 56.25f),
                $"{game}: retained enabled $03 does not itself establish that Link rides the slot.");
            foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
            {
                CompanionRuntimeState.Begin(memory, id, new(104.75f, 56.25f), 3);
                for (int offset = 2; offset < 0x40; offset++) memory.SetWramByte(0xd100 + offset, (byte)(0x80 + offset));
                memory.SetWramByte(0xd108, 3);
                before = Enumerable.Range(0, 0x40).Select(offset => memory.ReadWramByte(0xd100 + offset)).ToArray();
                CompanionRuntimeState.DismountAnimal(memory, id);
                // commonCode.s:companionDismount writes only these companion bytes.
                for (int offset = 0; offset < 0x40; offset++)
                {
                    byte expected = offset switch
                    {
                        0 => 1, 9 => 24, 0x1c => before[0x1b],
                        0x25 or 0x2b or 0x2c or 0x2d or 0x3c => 0,
                        _ => before[offset]
                    };
                    FailIf(memory.ReadWramByte(0xd100 + offset) != expected,
                        $"{game}: ${id:x2} dismount must retain the slot and preserve native byte $d1{offset:x2}.");
                }
                memory.SetWramByte(0xd100, 0x81);
                CompanionRuntimeState.MarkForRoomDeletion(memory);
                FailIf(memory.ReadWramByte(0xd100) != 0x82,
                    $"{game}: setObjectsEnabledTo2 must preserve the upper enabled bits.");
                CompanionRuntimeState.ClearMarkedForRoomDeletion(memory);
                FailIf(Enumerable.Range(0, 0x40).Any(offset => memory.ReadWramByte(0xd100 + offset) != 0),
                    $"{game}: clearObjectsWithEnabled2 must delete the entire idle slot.");
            }
            CompanionRuntimeState.Begin(memory, 0x0c, new(104.75f, 56.25f), 1);
            memory.SetWramByte(0xd100, 0x83);
            CompanionRuntimeState.MarkForRoomDeletion(memory);
            CompanionRuntimeState.ClearMarkedForRoomDeletion(memory);
            FailIf(memory.ReadWramByte(0xd100) != 0x83, $"{game}: retained $03 must survive room deletion.");
            before = Enumerable.Range(0, 0x40).Select(offset => memory.ReadWramByte(0xd100 + offset)).ToArray();
            CompanionRuntimeState.ReleaseCarried(memory, 0x0c);
            for (int offset = 0; offset < 0x40; offset++)
                FailIf(memory.ReadWramByte(0xd100 + offset) != (offset == 0 ? 0x81 : before[offset]),
                    $"{game}: releasing Dimitri must clear only enabled bit 1 at $d1{offset:x2}.");
            FailIf(memory.ReadWramByte(0xd0ff) != 0xa5 || memory.ReadWramByte(0xd140) != 0x5a,
                $"{game}: companion lifetime writes must preserve both adjacent categories.");
        }
    }
}
