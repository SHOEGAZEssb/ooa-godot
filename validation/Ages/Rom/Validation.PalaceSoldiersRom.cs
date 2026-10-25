using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceSoldierGatesRom()
    {
        // soldier.s: $03 survives GLOBALFLAG_0b; $05 does not. Both delete
        // when soldierCheckBeatD6 finds a highest essence bit >= $05.
        // Execute the retail interaction dispatcher, including native init,
        // scripts and animation, rather than duplicating that gate in a test.
        int fixture = 0;
        foreach (int essences in new[] { 0, 0x10, 0x20, 0x40 })
        foreach (bool completed in new[] { false, true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            for (int bit = 0; bit < 8; bit++)
                if ((essences & (1 << bit)) != 0) _inventory.GiveTreasure(TreasureId.Essence, bit);
            _saveData.SetGlobalFlag(AgesGlobalFlag.Flag0b, completed);
            // The throne has independent gates: Ambi $00 survives the Bomb
            // flag, whereas Nayru $01 survives D6 but deletes with that flag.
            LoadValidationRoom(1, 0x16);
            int[] throneIds = [0x40, 0x40, 0x4d, 0x36];
            int[] throneSubids = [4, 6, 0, 1];
            int[] throneXs = [0x38, 0x50, 0x50, 0x58];
            int[] throneYs = [0x38, 0x68, 0x28, 0xf0];
            var throneRom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x50, 0x70);
            for (int index = 0; index < 4; index++)
            {
                int slot = 0xd240 + index * 0x100;
                throneRom[slot] = 1; throneRom[slot + 1] = (byte)throneIds[index];
                throneRom[slot + 2] = (byte)throneSubids[index];
                throneRom[slot + 0x0b] = (byte)throneYs[index];
                throneRom[slot + 0x0d] = (byte)throneXs[index];
            }
            throneRom.Update(0, 0, 0);
            for (int index = 0; index < 4; index++)
            {
                var actor = _entities.Entities<NpcCharacter>().Single(npc =>
                    npc.Record.Id == throneIds[index] && npc.Record.SubId == throneSubids[index]);
                FailIf(actor.Active != (throneRom[0xd240 + index * 0x100] != 0),
                    $"1:16 native gate ${throneIds[index]:x2}:${throneSubids[index]:x2} " +
                    $"essences=${essences:x2}, flag$0b={completed} differs from ROM.");
            }
            LoadValidationRoom(1, 0x36);
            _inventory.EquipA(0); _inventory.EquipB(0);
            FailIf(_inventory.Essences != essences || _saveData.ReadWramByte(AgesWramAddress.wEssencesObtained) != essences,
                $"Palace ROM fixture lost essence mask ${essences:x2}.");
            _player.WarpTo(new Vector2(0x50, 0x70));
            var random = _random.CaptureState();
            var rom = new SomariaRom(_saveData, random, _currentRoom, 0, 0x50, 0x70);
            int[] xs = [0x50, 0x48, 0x58, 0x38, 0x68];
            int[] ys = [0x68, 0x18, 0x18, 0x28, 0x28];
            for (int index = 0; index < 5; index++)
            {
                int slot = 0xd240 + index * 0x100;
                rom[slot] = 1; rom[slot + 1] = 0x40;
                rom[slot + 2] = (byte)(index == 0 ? 5 : 3);
                rom[slot + 0x0b] = (byte)ys[index];
                rom[slot + 0x0d] = (byte)xs[index];
            }
            rom.AdvanceInteractions(0);
            var guards = _entities.Entities<NpcCharacter>()
                .Where(npc => npc.Active && npc.Record.Id == 0x40).ToArray();
            int expected = Enumerable.Range(0, 5).Count(index => rom[0xd240 + index * 0x100] != 0);
            FailIf(guards.Length != expected,
                $"1:36 soldier initialization essences=${essences:x2}, flag$0b={completed}: " +
                $"port {guards.Length}, ROM {expected}.");
            if (essences >= 0x20 || !completed) continue;

            // Walk through the actual center aisle. The top pair move outward
            // once Link's high Y byte is $60, for applyspeed $10 (15 moves).
            FailIf(_currentRoom.IsSolid(_player.Position), "1:36 approach starts in solid geometry.");
            bool sawTrigger = false;
            int updates = 0;
            int publications = 0;
            var moveSources = new DekuForestPalaceEventDatabase().SideGuardCommands
                .OfType<CutsceneApplySpeedCommand>().Select(command => command.Source).ToHashSet();
            _roomEvents.CommandTraceSink = new MovementCommandScratchTrace(entry =>
            {
                // counter2=$10 is the setup yield; zero is the terminal
                // yield. Only $0f..$01 execute objectApplySpeed.
                if (entry.Counter is <= 0 or >= 0x10 || !moveSources.Contains(entry.Source)) return;
                // Source order: left then right, fifteen SPEED_100 calls
                // each. Observe before another host or the warp query runs.
                AssertMovementPublication(_runtimeState, 0, (publications & 1) == 0 ? -256 : 256,
                    "Palace side guards $40:$03");
                publications++;
            });
            StepGameplayUpdates(36, Vector2.Up, ["move_up"], batched: batch, afterUpdate: () =>
            {
                // Link updates before interactions; supply its actual current
                // position to the isolated original interaction pass.
                rom.Word(0xd00a, (int)(_player.PrecisePosition.Y * 256));
                rom.Word(0xd00c, (int)(_player.PrecisePosition.X * 256));
                rom.AdvanceInteractions(++updates);
                sawTrigger |= rom[0xd00b] == 0x60;
                for (int index = 1; index < 5; index++)
                {
                    var actor = guards.Single(npc => npc.BaseRecord.X == xs[index] && npc.BaseRecord.Y == ys[index]);
                    FailIf(actor.Position != new Vector2(rom[0xd240 + index * 0x100 + 0x0d], rom[0xd240 + index * 0x100 + 0x0b]),
                        $"1:36 side soldier {index} differs from ROM at update {updates}: " +
                        $"port={actor.Position}, ROM=({rom[0xd240 + index * 0x100 + 0x0d]}, {rom[0xd240 + index * 0x100 + 0x0b]}), Link={_player.PrecisePosition}.");
                }
            });
            _roomEvents.CommandTraceSink = null;
            FailIf(publications != 30 || !sawTrigger || guards.Single(npc => npc.BaseRecord.X == 0x48).Position.X != 0x39 ||
                guards.Single(npc => npc.BaseRecord.X == 0x58).Position.X != 0x67,
                "1:36 must open the center aisle with exactly 15 outward soldier updates.");
            var palace = _roomEvents.Get<DekuForestPalaceEvent>();
            palace.Cancel();
            FailIf(guards.Any(npc => npc.Active) || palace.BlocksGameplay, "Palace cancellation retained actors or control.");
            LoadValidationRoom(1, 0x36);
            FailIf(_entities.Entities<NpcCharacter>().Count(npc => npc.Active && npc.Record.Id == 0x40) != 4,
                "Completed palace re-entry must restore all four side soldiers.");
        }
    }
}
