using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateZeldaRescueRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _inventory.GiveTreasure(TreasureId.Essence, 2);
            _saveData.SetGlobalFlag(0x39);
            _saveData.SetGlobalFlag(0x3c);
            LoadValidationRoom(0, 0x65);
            _player.WarpTo(new(0x50, 0x78)); _player.Face(Vector2I.Up);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x50, 0x78)
                { CompanionDispatchEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcc01] = 1; // File load copies wFileIsLinkedGame to wIsLinkedGame.
            var placed = _entities.Entities<NpcCharacter>().Where(n => n.Record.Id is 0x31 or 0x4c).ToArray();
            foreach (var npc in placed)
            {
                int address = 0xd040 + 0x100 * _entities.InteractionSlot(npc);
                rom[address] = 1; rom[address+1]=(byte)npc.Record.Id; rom[address+2]=(byte)npc.Record.SubId;
                rom[address+11]=(byte)npc.Record.Y; rom[address+13]=(byte)npc.Record.X;
            }
            var trace = new ValidationCutsceneTrace(); _roomEvents.CommandTraceSink = trace;
            int texts = 0, generation = 0, hold = 0, updates = 0;
            int[] expected = [0x0128, 0x0603, 0x0604, 0x3217, 0x012a, 0x0608];
            string[] sourcePhrases = ["You're safe!", "Withering away!", "mighty hand", "Please! Please!", "north-", "safe\nreturn."];
            StepGameplayUpdates(750, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.UpdateGameplay(0, 0, 0xff, ++updates);
                foreach (var actor in _entities.EntityAdapters<ZeldaRescueActor>().Where(a => !a.Finished))
                {
                    var npc = (NpcCharacter)actor.Node;
                    int address = 0xd040 + 0x100 * _entities.InteractionSlot(npc);
                    if ((updates & 31) == 1) CompareInteractionOamRom(rom, npc, address, $"0:65 rescue ${npc.Record.Id:x2} update{updates}");
                    FailIf(npc.CurrentAnimationCounter != rom[address+0x20] || npc.CurrentAnimationParameter != rom[address+0x21] ||
                        npc.ZIndex != ObjectDrawPriority.FromVisible(rom[address+0x1a]) || !npc.Visible,
                        $"0:65 ${npc.Record.Id:x2} presentation update {updates}: animation={npc.CurrentAnimationCounter}/{rom[address+0x20]}, parameter={npc.CurrentAnimationParameter}/{rom[address+0x21]}, priority={npc.ZIndex}/{ObjectDrawPriority.FromVisible(rom[address+0x1a])}.");
                    FailIf(rom[address] == 0 || npc.Position != new Vector2(rom[address+13], rom[address+11]) ||
                        (int)npc.ScriptDrawOffset.Y != unchecked((sbyte)rom[address+15]),
                        $"0:65 ${npc.Record.Id:x2} update {updates} position/Z differs: port={npc.Position}/{npc.ScriptDrawOffset.Y}, ROM=({rom[address+13]}, {rom[address+11]})/{unchecked((sbyte)rom[address+15])}, state={actor.State}, ptr=${rom.Word(address+0x18):x4}; {string.Join(';',trace.Entries.TakeLast(5))}.");
                }
                FailIf(_entities.RuntimeState.ReadWramByte(0xcfd0) != rom[0xcfd0] ||
                    _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != rom[0xcc02] ||
                    _player.Position != new Vector2(rom.Word(0xd00c)/256f, rom.Word(0xd00a)/256f),
                    $"0:65 update {updates} signal/masks/Link differs: signal={_entities.RuntimeState.ReadWramByte(0xcfd0)}/{rom[0xcfd0]}, mask={_entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)}/{rom[0xcc8a]}, menu={_entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled)}/{rom[0xcc02]}, Link={_player.Position}/({rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}); {string.Join(';',trace.Entries.TakeLast(5))}.");
                if (rom.TextGeneration != generation)
                {
                    FailIf(texts >= expected.Length || rom.Word(0xcba2) != expected[texts] + 0x400 || !_dialogue.IsOpen,
                        $"0:65 text boundary {texts} differs at {updates}: ROM=${rom.Word(0xcba2):x4}, port={_dialogue.CurrentMessage}.");
                    FailIf(!_dialogue.CurrentMessage.Contains(sourcePhrases[texts], StringComparison.Ordinal),
                        $"0:65 TX_${expected[texts]:x4} lost source-derived dialogue content.");
                    texts++; generation=rom.TextGeneration; hold=5;
                }
                if (hold > 0 && --hold == 0) { _dialogue.Close(); rom[0xcba0]=0; }
            });
            FailIf(texts != expected.Length || !_saveData.HasGlobalFlag(0x38) || _roomEvents.MenusDisabled ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wScrollMode) != 1 ||
                _entities.Entities<NpcCharacter>().Any(n => n.Active && n.Record.Id is 0x31 or 0x4c),
                "0:65 rescue must complete all six messages, delete Impa/bird, persist $38, restore controls/scroll and retain Zelda.");
            LoadValidationRoom(0, 0x65); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Any(n => n.Active), "0:65 completed rescue actors returned on re-entry.");
        }
        GD.Print("Validated room $0:$65 clean-US rescue actor order, position/Z, Link movement, six texts, shared signals, masks, completion and re-entry in split/batched gameplay.");
    }
}
