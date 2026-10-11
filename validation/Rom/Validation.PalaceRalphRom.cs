using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceRalphRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            _inventory.GiveTreasure(TreasureId.Essence, 5);
            _saveData.SetGlobalFlag(GlobalFlag.Flag0b);
            LoadValidationRoom(1, 0x36);
            _player.WarpTo(new(0x50, 0x7f));
            FailIf(_collision.Collides(_player.Position), "Ralph scroll approach must use the $1:$36 south aisle.");
            _transitions.BeginScroll(_player, Vector2I.Down, 0x46);
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched);
            _inventory.EquipA(0); _inventory.EquipB(0);
            NpcCharacter ralph = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x37);
            var palace = _roomEvents.Get<DekuForestPalaceEvent>();
            var trace = new ValidationCutsceneTrace();
            _roomEvents.CommandTraceSink = trace;
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0,
                (int)_player.Position.X, (int)_player.Position.Y) { CompanionDispatchEnabled = true };
            rom.InitializeLinkGameplay();
            rom.InitializeLinkWalkingAnimation();
            rom[0xcd02] = 2; // Native entry direction for $1:$36 -> $1:$46.
            const int slot = 0xd440; // Isolated native actor; the dispatcher still allocates its effect in slot order.
            rom[slot] = 1; rom[slot + 1] = 0x37; rom[slot + 2] = 9;
            rom[slot + 0x0b] = (byte)ralph.Position.Y;
            rom[slot + 0x0d] = (byte)ralph.Position.X;
            rom.Update(0, 0, 0);
            FailIf(rom[slot] == 0 || !ralph.Active || !palace.BlocksGameplay || !palace.MenusDisabled,
                $"1:46 Ralph $37:$09 must claim input/menu with essence bit $20 before flag $32: " +
                $"ROM=${rom[slot]:x2}, port={ralph.Active}, blocks={palace.BlocksGameplay}, menu={palace.MenusDisabled}, " +
                $"essences=${_inventory.Essences:x2}, completed={_saveData.HasGlobalFlag(0x32)}.");
            Vector2 link = _player.Position;
            int updates = 0, texts = 0, textHold = 0, lastGeneration = 0;
            int[] expectedTexts = [0x2a16, 0x2a17, 0x2a18];
            StepGameplayUpdates(190, Vector2.Right, ["move_right"], batched: batched, afterUpdate: () =>
            {
                rom.UpdateGameplay(0, 0x10, 8, ++updates);
                FailIf(ralph.Active != (rom[slot] != 0) ||
                    ralph.Active && (ralph.Position != new Vector2(rom[slot + 0x0d], rom[slot + 0x0b])),
                    $"1:46 Ralph differs from ROM at update {updates}: port={ralph.Position}/{ralph.Active}, " +
                    $"ROM=({rom[slot + 0x0d]}, {rom[slot + 0x0b]})/{rom[slot] != 0}, " +
                    $"ptr=${rom.Word(slot + 0x18):x4}, counters={rom[slot + 6]}/{rom[slot + 7]}, " +
                    $"trace={string.Join(';', trace.Entries.TakeLast(5))}.");
                if (rom.TextGeneration != lastGeneration)
                {
                    FailIf(texts >= 3 || rom.Word(0xcba2) != expectedTexts[texts] + 0x400 || !_dialogue.IsOpen,
                        $"Ralph native dialogue boundary {texts} differs at update {updates}: ROM TX_${rom.Word(0xcba2):x4}.");
                    texts++; lastGeneration = rom.TextGeneration; textHold = 3;
                }
                if (textHold > 0 && --textHold == 0) { _dialogue.Close(); rom[0xcba0] = 0; }
                if (ralph.Active)
                    FailIf(_player.Position != link || !palace.MenusDisabled,
                        $"Ralph input ownership leaked at update {updates}.");
                else if (rom[0xd004] == 1)
                    FailIf(_player.ScriptedLinkAnimationMode.HasValue || _player.LinkAnimationCounter != rom[0xd020] ||
                        _player.Position != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        $"Ralph completion must restore native standing/walking animation: update={updates}, mode={_player.ScriptedLinkAnimationMode}, " +
                        $"animation counter={_player.LinkAnimationCounter}/{rom[0xd020]}, " +
                        $"position={_player.Position}, ROM=({rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}).");
            });
            FailIf(texts != 3 || ralph.Active || palace.BlocksGameplay || palace.MenusDisabled ||
                !_saveData.HasGlobalFlag(0x32) || rom[0xcc8a] != 0 || rom[0xcc02] != 0,
                "Ralph must finish three dialogues, enter the palace, persist flag $32, and restore controls.");
            ValidatePalacePostRalphSword(rom, batched, "Ralph $37:$09");
            LoadValidationRoom(1, 0x46);
            FailIf(_entities.Entities<NpcCharacter>().Any(npc => npc.Active && npc.Record.Id == 0x37) || palace.BlocksGameplay,
                "Flag $32 must suppress Ralph on palace re-entry.");
            _saveData.SetGlobalFlag(0x32, false);
            LoadValidationRoom(1, 0x46);
            palace.Cancel();
            FailIf(palace.BlocksGameplay || palace.MenusDisabled || _saveData.HasGlobalFlag(0x32),
                "Cancelling Ralph must release controls without completing his script.");
        }
    }

    private void ValidatePalacePostRalphSword(SomariaRom rom, bool batched, string context)
    {
        EquipSomariaMotionItem(rom, TreasureId.Sword, true);
        for (int swing = 0; swing < 2; swing++)
        {
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched, afterUpdate: () =>
                rom.UpdateGameplay(1, 1, 0xff, _entities.FrameCounter));
            FailIf(!_player.IsAttacking || rom[0xd200] == 0 || rom[0xd201] != 0x05 ||
                _player.ScriptedLinkAnimationMode.HasValue,
                $"{context} must restore sword animation after completion (swing {swing}, batch={batched}).");
            StepGameplayUpdates(40, Vector2.Zero, batched: batched, afterUpdate: () =>
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter));
            FailIf(_player.IsAttacking || rom[0xd200] != 0 || _player.ScriptedLinkAnimationMode.HasValue,
                $"{context} sword must return to ordinary animation before repeating (swing {swing}, batch={batched}).");
        }
    }
}
