using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceFriendlySoldiersRom()
    {
        // Literal source tables, independent of the imported script/assets.
        int[] texts = [0x5912,0x5913,0x5911,0x5910,0x5913,0x5911,0x5914,0x5913,
            0x5915,0x5913,0x5912,0x5915,0x5913,0x5913,0x5912,0x5914];
        var placements = NpcDatabase.Shared.AllRecords.Where(npc => npc.Id == 0x40 && npc.SubId == 0x0d).ToArray();
        FailIf(placements.Length != 16, "Palace $40:$0d must retain sixteen source placements.");
        foreach (NpcRecord placement in placements)
        foreach (bool batched in RomHostSchedules(placement.Var03))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Essence, 5);
            _saveData.SetGlobalFlag(0x0b); _saveData.SetGlobalFlag(0x32);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            _saveData.SetRoomFlag(4, 0xfc, 0x80);
            LoadValidationRoom(placement.Group, placement.Room);
            _inventory.EquipA(0); _inventory.EquipB(0);
            NpcCharacter actor = _entities.Entities<NpcCharacter>().Single(npc =>
                npc.Record.Id == 0x40 && npc.Record.SubId == 0x0d && npc.Record.Var03 == placement.Var03);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0,
                (int)_player.Position.X, (int)_player.Position.Y);
            const int slot = 0xd440;
            rom[slot] = 1; rom[slot+1] = 0x40; rom[slot+2] = 0x0d; rom[slot+3] = (byte)placement.Var03;
            rom[slot+0x0b] = (byte)placement.Y; rom[slot+0x0d] = (byte)placement.X;
            int updates = 0;
            var publication = new InteractionMovementPublication(_entities, _runtimeState);
            int frames = placement.Var03 is 0 or 1 or 7 ? 220 : 2;
            StepGameplayUpdates(frames, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                InteractionMovementPublication.Seed(rom);
                rom.AdvanceInteractions(++updates);
                publication.Compare(rom, $"Palace soldier $40:$0d v${placement.Var03:x2}");
                FailIf(actor.Active != (rom[slot] != 0) || actor.Position != new Vector2(rom[slot+0x0d], rom[slot+0x0b]),
                    $"Palace friendly soldier v${placement.Var03:x2} differs at update {updates}: " +
                    $"port={actor.Position}, ROM=({rom[slot+0x0d]},{rom[slot+0x0b]}), ptr=${rom.Word(slot+0x18):x4}.");
            });
            FailIf(actor.TextId != texts[placement.Var03] || rom.Word(slot+0x32) != texts[placement.Var03] ||
                actor.Record.Palette != (placement.Var03 < 7 ? 1 : 2),
                $"Palace friendly soldier v${placement.Var03:x2} did not select its source text/palette.");
        }

        // Stationary actor v$03 at (40,56): walk north through room 1:06's
        // actual collision geometry, then repeat A after closing the message.
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame); _saveData.SetRoomFlag(4, 0xfc, 0x80);
            LoadValidationRoom(1, 0x06); _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new Vector2(40, 88));
            FailIf(_currentRoom.IsSolid(_player.Position), "1:06 soldier approach starts in solid geometry.");
            StepGameplayUpdates(30, Vector2.Up, ["move_up"], batched: batched);
            var actor = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Var03 == 3);
            FailIf(!actor.CanTalkTo(_player), "1:06 v$03 soldier is not reachable through its room geometry.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched: batched);
                FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage !=
                    "Ambi has gone\nalone on a\nsoul-searching\njourney. I hope\nshe returns\nsafely...",
                    "1:06 stationary palace soldier must open source TX_5910 on each A interaction.");
                _dialogue.Close(); StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            }
        }

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame); _saveData.SetRoomFlag(4, 0xfc, 0x80);
            LoadValidationRoom(5, 0xd4); _inventory.EquipA(0); _inventory.EquipB(0);
            var actor = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x40);
            _player.WarpTo(new Vector2(40, 88));
            FailIf(_currentRoom.IsSolid(_player.Position), "5:d4 patroller approach starts in solid geometry.");
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 88);
            const int slot = 0xd440;
            rom[slot]=1; rom[slot+1]=0x40; rom[slot+2]=0x0d; rom[slot+3]=7;
            rom[slot+0x0b]=0x28; rom[slot+0x0d]=0x28;
            int updates = 0;
            void Compare()
            {
                rom.Word(0xd00a, (int)(_player.PrecisePosition.Y*256));
                rom.Word(0xd00c, (int)(_player.PrecisePosition.X*256));
                rom.Update(0,0,++updates);
                FailIf(actor.Position != new Vector2(rom[slot+0x0d],rom[slot+0x0b]) ||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a],
                    $"5:d4 friendly patrol talk handoff differs at update {updates}.");
            }
            StepGameplayUpdates(24,Vector2.Up,["move_up"],batched:batched,afterUpdate:Compare);
            FailIf(!actor.CanTalkTo(_player), "5:d4 friendly patroller is not reachable from the corridor.");
            for (int repeat=0; repeat<2; repeat++)
            {
                rom[slot+0x31]=1;
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);
                StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(!_dialogue.IsOpen || rom.Word(0xcba2) != 0x5d13 || rom[0xcc02] != 0x80,
                    "5:d4 moving soldier must stop for source TX_5913 and own input/menu.");
                Vector2 heldLink = _player.Position;
                StepGameplayUpdates(3,Vector2.Right,["move_right"],batched:batched,afterUpdate:Compare);
                FailIf(_player.Position != heldLink, "Palace patroller dialogue allowed held input to move Link.");
                _dialogue.Close(); rom[0xcba0]=0;
                StepGameplayUpdates(36,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0,
                    "Palace patroller must restore input/menu after its native 30-update pause.");
                // The native patrol has advanced only a few pixels; approach
                // again through collision instead of placing Link in its body.
                StepGameplayUpdates(4,Vector2.Up,["move_up"],batched:batched,afterUpdate:Compare);
                FailIf(!actor.CanTalkTo(_player), "5:d4 patroller did not remain reachable after resuming.");
            }
        }
    }
}
