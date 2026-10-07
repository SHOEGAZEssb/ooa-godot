using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareCircularSidePlatformsRom()
    {
        CompareCircularPlatformAllocationRom();
        // Original 4:$2b placements follow INTERAC$20:$03. The boss and its
        // reward producer are isolated; Link and all three $a4 handlers execute.
        var rows = new WingDungeonDatabase().GetRoomRecords(4, 0x2b)
            .Where(row => row.Kind == DungeonObjectKind.CircularSidePlatform).ToArray();
        FailIf(string.Join(',', rows.Select(row => $"{row.Order}:{row.SubId:x2}:{row.Y:x2}:{row.X:x2}")) !=
            "1:00:00:00,2:01:00:00,3:02:00:00",
            "4:$2b must preserve source-ordered INTERAC$a4:$00-$02 placements.");
        foreach (bool batched in new[] { false, true })
        {
            var rom = PrepareSideViewRom(6, 0x2b, new(24, 8), feather: true);
            var sounds = _sound.AttachPlayRequestAudit();
            var visual = new DungeonInteractionVisualDatabase().Visual("circular-side-platform");
            var platforms = rows.Select(row => new CircularSideScrollPlatformRoomEntity(
                row, visual, SideViewInteractionData.CircularSidePlatform(row.SubId))).ToArray();
            foreach (var platform in platforms)
            {
                _entities.AddEntity(platform);
                int page = 0xd0 + _entities.InteractionSlot(platform);
                rom.AddPlatform(page, rows[Array.IndexOf(platforms, platform)].SubId, Vector2.Zero);
                rom[(page << 8) + 0x41] = 0xa4;
            }
            void Compare()
            {
                foreach (var platform in platforms)
                {
                    int page = 0xd0 + _entities.InteractionSlot(platform), slot = page << 8;
                    Vector2 expected = new(rom.Word(slot + 0x4c) / 256f, rom.Word(slot + 0x4a) / 256f);
                    FailIf(platform.PrecisePosition != expected || platform.Angle != rom[slot + 0x49] ||
                        platform.Counter != rom[slot + 0x46] || platform.LinkRiding != (rom[slot + 0x74] != 0) ||
                        rom[slot + 0x76] != (byte)platform.Position.Y || rom[slot + 0x77] != (byte)platform.Position.X,
                        $"INTERAC$a4 slot=${page:x2}: runtime XY={platform.PrecisePosition}, angle=${platform.Angle:x2}, counter=${platform.Counter:x2}, rider={platform.LinkRiding}; ROM XY={expected}, angle=${rom[slot + 0x49]:x2}, counter=${rom[slot + 0x46]:x2}, rider=${rom[slot + 0x74]:x2}, savedHigh=${rom[slot + 0x76]:x2}/${rom[slot + 0x77]:x2}.");
                    FailIf(rom[slot + 0x44] != 1 || rom[slot + 0x50] != 0x1e ||
                        rom[slot + 0x66] != 8 || rom[slot + 0x67] != 8,
                        $"INTERAC$a4 slot=${page:x2}: native initialized state/speed/radii changed.");
                }
            }
            FailIf(platforms.Any(platform => !platform.UpdatesDuringDialogue || platform.Visible),
                "Pending INTERAC$a4 must await state0 with hidden graphics and zero motion.");
            _dialogue.ShowMessage("Circular platform initialization.", _player.Position.Y);
            rom[0xcba0] = 1;
            StepSideViewRom(rom, 1, batched, afterUpdate: Compare);
            Vector2[] initial = [new(120, 33), new(173, 86), new(120, 139)];
            for (int index = 0; index < 3; index++)
                FailIf(platforms[index].PrecisePosition != initial[index] || platforms[index].Counter != 7 ||
                    platforms[index].Angle != 8 + index * 8 || platforms[index].UpdatesDuringDialogue,
                    $"INTERAC$a4:${index:x2} state0 must install its original cardinal arc and seven-update initial counter without movement.");
            StepSideViewRom(rom, 3, batched, afterUpdate: Compare);
            _dialogue.Close(); rom[0xcba0] = 0;
            // Seven initial updates followed by one complete 32-angle circuit.
            // Every update compares both object fixed point and actual Link dispatch.
            StepSideViewRom(rom, 7 + 32 * 14, batched, afterUpdate: Compare);
            _dialogue.ShowMessage("Circular platform pause.", _player.Position.Y); rom[0xcba0] = 1;
            StepSideViewRom(rom, 3, batched, afterUpdate: Compare);
            _dialogue.Close(); rom[0xcba0] = 0;
            StepSideViewRom(rom, 8, batched, afterUpdate: Compare);
            // Descend the original left ladder to its middle ledge, then jump
            // across the open field. No platform or collision coordinates move.
            StepSideViewRom(rom, 40, batched, 16, afterUpdate: Compare);
            StepSideViewRom(rom, 24, batched, 8, afterUpdate: Compare);
            bool boarded = false;
            void CompareBoarding()
            {
                Compare();
                boarded |= platforms.Any(platform => platform.LinkRiding);
            }
            StepSideViewRom(rom, 1, batched, 8, jump: true, afterUpdate: CompareBoarding);
            StepSideViewRom(rom, 35, batched, 8, afterUpdate: CompareBoarding);
            StepSideViewRom(rom, 50, batched, afterUpdate: CompareBoarding);
            FailIf(!boarded || !platforms.Any(platform => platform.LinkRiding),
                "4:$2b original ladder/ledge Feather approach must board and retain a circular platform rider.");
            StepSideViewRom(rom, 32 * 14, batched, afterUpdate: Compare);
            FailIf(!platforms.Any(platform => platform.LinkRiding),
                "INTERAC$a4 must carry Link through all 32 native turn angles.");
            _dialogue.ShowMessage("Circular rider pause.", _player.Position.Y); rom[0xcba0] = 1;
            StepSideViewRom(rom, 3, batched, 24, jump: true, afterUpdate: Compare);
            _dialogue.Close(); rom[0xcba0] = 0;
            StepSideViewRom(rom, 1, batched, 24, afterUpdate: Compare);
            StepSideViewRom(rom, 1, batched, 24, jump: true, afterUpdate: Compare);
            StepSideViewRom(rom, 24, batched, 24, afterUpdate: Compare);
            StepSideViewRom(rom, 40, batched, afterUpdate: Compare);
            FailIf(platforms.Any(platform => platform.LinkRiding),
                "Fresh Feather edge must leave circular-platform ownership and reach original geometry.");
            FailIf(!sounds.Requests.Where(id => id is SoundId.SndJump or SoundId.SndLand).SequenceEqual(rom.Sounds),
                "Circular boarding, carrying and dismount jump/land cues differ from native order.");
            var nonInteractions = _entities.NonInteractionObjectsDisabledSource;
            var initialized = _entities.InitializedObjectsDisabledSource;
            try
            {
                // DISABLE_ENEMIES|DISABLE_ITEMS leaves the interaction pass
                // enabled. DISABLE_INTERACTIONS admits only pending state0.
                _entities.NonInteractionObjectsDisabledSource = () => true;
                rom[0xcc8a] = 0x14;
                StepSideViewRom(rom, 3, batched, afterUpdate: Compare);
                _entities.NonInteractionObjectsDisabledSource = nonInteractions;
                _entities.InitializedObjectsDisabledSource = () => true;
                rom[0xcc8a] = 2;
                StepSideViewRom(rom, 3, batched, afterUpdate: Compare);
                _entities.InitializedObjectsDisabledSource = initialized;
                rom[0xcc8a] = 0;
                StepSideViewRom(rom, 1, batched, afterUpdate: Compare);
            }
            finally
            {
                _entities.NonInteractionObjectsDisabledSource = nonInteractions;
                _entities.InitializedObjectsDisabledSource = initialized;
                rom[0xcc8a] = 0;
            }
            LoadValidationRoom(6, 0x10);
            FailIf(_entities.Entities<CircularSideScrollPlatformRoomEntity>().Count != 0,
                "Room replacement must retire INTERAC$a4 slots and riders.");
        }
        GD.Print("Validated executed-ROM three circular $a4 placements, state0 text admission, fixed-point circuit/counters, reachable boarding, full rider circuit, pause, dismount/cues and cancellation through split/batched gameplay.");
    }
}
