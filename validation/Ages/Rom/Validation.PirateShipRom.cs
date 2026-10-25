using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePirateShipNativeRom()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _saveData.SetGlobalFlag(0x34);
            int group = linked ? 0 : 1, room = linked ? 0xc8 : 0xd7;
            LoadValidationRoom(group, room);
            _player.WarpTo(linked ? new(0x28, 0x58) : new(0x58, 0x48));
            var ship = _entities.EntityAdapters<PirateShipRoomEntity>().Single();
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom,
                2, (int)_player.Position.X, (int)_player.Position.Y);
            rom[0xcc01] = linked ? (byte)1 : (byte)0;
            const int slot = 0xd240;
            rom[slot] = 1; rom[slot + 1] = 0xc2; rom[slot + 2] = linked ? (byte)2 : (byte)1;
            rom[slot + 11] = linked ? (byte)0x58 : (byte)0x18;
            rom[slot + 13] = linked ? (byte)0x38 : (byte)0x58;
            int updates = 0;
            var publication = new InteractionMovementPublication(_entities, _runtimeState);
            void Compare()
            {
                InteractionMovementPublication.Seed(rom);
                rom.AdvanceInteractions(_entities.FrameCounter); updates++;
                publication.Compare(rom, "Pirate departure $c2");
                bool nativeFinished = rom[slot] == 0;
                FailIf(ship.Finished != nativeFinished || !nativeFinished &&
                    (ship.State != rom[slot + 4] || ship.Counter != rom[slot + 6] ||
                    ((int)ship.Node.Position.X & 255) != rom[slot + 13] ||
                    ((int)ship.Node.Position.Y & 255) != rom[slot + 11] ||
                    ship.Node.Visible != ((rom[slot + 0x1a] & 0x80) != 0)),
                    $"Pirate departure $c2/${(linked ? 2 : 1):x2} differs from clean-US ROM on update {updates}: " +
                    $"port={ship.State}/{ship.Counter}, ROM={rom[slot + 4]}/{rom[slot + 6]}, ended={ship.Finished}/{nativeFinished}.");
                FailIf(_player.CutsceneControlled != (rom[0xcc8a] != 0) ||
                    _entities.PlayerMenusDisabled != (rom[0xcc02] != 0), "Pirate departure input/menu masks differ from native code.");
            }
            StepGameplayUpdates(61, Vector2.Zero, batched: batched, afterUpdate: Compare);
            // showText adds $0400 for the four dictionary tables.
            FailIf(rom.Word(0xcba2) != 0x3a0c || !_dialogue.IsOpen,
                $"Pirate departure selected a different text than ROM TX_360c: ${rom.Word(0xcba2):x4}.");
            StepGameplayUpdates(5, Vector2.Zero, batched: batched, afterUpdate: Compare);
            _dialogue.Close(); rom[0xcba0] = 0;
            StepGameplayUpdates(128, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(publication.Publications != 128, "Pirate departure must publish all 128 movements.");

            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, 0xf8);
            _player.WarpTo(new(0x68, 0x58));
            var records = NpcDatabase.Shared.GetRoomNpcs(5, 0xf8);
            rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 2, 0x68, 0x58);
            rom[0xcc01] = 0;
            for (int index = 0; index < records.Count; index++)
            {
                var record = records[index]; int address = slot + index * 256;
                rom[address] = 1; rom[address + 1] = (byte)record.Id; rom[address + 2] = (byte)record.SubId;
                rom[address + 11] = (byte)record.Y; rom[address + 13] = (byte)record.X;
            }
            var crew = _entities.EntityAdapters<PirateCrewRoomEntity>().ToArray();
            void CompareCrew()
            {
                rom.AdvanceInteractions(_entities.FrameCounter);
                foreach (var pirate in crew)
                {
                    var actor = (NpcCharacter)pirate.Node;
                    int address = slot + (actor.Record.SubId + 1) * 256;
                    FailIf(pirate.State != rom[address + 4] || actor.ScriptDrawOffset.Y != unchecked((sbyte)rom[address + 15]) ||
                        actor.CurrentAnimationParameter != rom[address + 0x21] ||
                        actor.ZIndex != ObjectDrawPriority.FromVisible(rom[address + 0x1a]),
                        $"Pirate $c4:${actor.Record.SubId:x2} state/Z/animation/draw priority differs from clean-US ROM at frame {_entities.FrameCounter}.");
                }
                FailIf(_runtimeState.ReadWramByte(0xcfc0) != rom[0xcfc0], "Pirate crew shared signal lifetime differs from clean-US ROM.");
            }
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: CompareCrew);
            _runtimeState.SetWramByte(0xcfc0, 1); rom[0xcfc0] = 1;
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: CompareCrew);
            StepGameplayUpdates(35, Vector2.Zero, batched: batched, afterUpdate: CompareCrew);
        }
        RunDialogueRom(0x360c, PirateShipDatabase.Shared.DepartureText, 3, batched: true, accelerate: true);
        GD.Print("Compared both pirate ship departure state machines, text/movement freezes, crew Z/animation/signals, and TX_360c formatting with clean-US ROM.");
    }
}
