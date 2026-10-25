using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSavedMonkeyTalkRom()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool finished in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone); _saveData.SetGlobalFlag(AgesGlobalFlag.SavedNayru);
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, finished);
            LoadValidationRoom(0, 0x5a); _player.WarpTo(new(104, 104));
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 104, 104);
            foreach (var pair in InteractionAdmissionPool.ToArray())
            {
                if (pair.Key.Node is not NpcCharacter npc) continue;
                int at = 0xd040 + pair.Value * 256;
                rom[at] = 1; rom[at + 1] = (byte)npc.Record.Id; rom[at + 2] = (byte)npc.Record.SubId;
                rom.Word(at + 10, (int)(npc.Position.Y * 256)); rom.Word(at + 12, (int)(npc.Position.X * 256));
            }
            void Compare()
            {
                FailIf(_dialogue.IsOpen != (rom[0xcba0] != 0) ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != rom[0xcc02] ||
                    _random.CaptureState().Rng1 != rom[0xff94] || _random.CaptureState().Rng2 != rom[0xff95],
                    $"Monkey talk masks/text/RNG differ at update {_entities.FrameCounter}, finished={finished}, batch={batch}: " +
                    $"text={_dialogue.IsOpen}/{rom[0xcba0]}, objects=${_runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects):x2}/${rom[0xcc8a]:x2}.");
                if (_dialogue.IsOpen)
                    FailIf(_dialogue.SourceTextId != (rom[0xcba2] | (rom[0xcba3] - 4) << 8), "Monkey script chose a different native dialogue index.");
            }
            void Step(int count = 1, bool press = false) => StepGameplayUpdates(count, Vector2.Zero,
                press ? ["attack"] : [], press ? ["attack"] : [], batch, () => {
                    rom.UpdateGameplay(press ? 1 : 0, press ? 1 : 0, 0xff, _entities.FrameCounter); press = false; Compare();
                });
            Step(3);
            foreach (int index in new[] { 8, 0 })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var monkey = _entities.EntityAdapters<MonkeyRoomEntity>().Single(m => m.Index == index);
                ApproachLibraryActor((NpcCharacter)monkey.Node, batch, _ => {
                    PositionLibraryRomAtPlayer(rom); rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); Compare();
                }, () => PositionLibraryRomAtPlayer(rom));
                PositionLibraryRomAtPlayer(rom); Step(press: true);
                for (int wait = 0; wait < 90 && !_dialogue.IsOpen; wait++) Step();
                FailIf(!_dialogue.IsOpen, $"Native monkey ${index:x2} must respond through the room's collision geometry.");
                Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step(4);
            }
        }
    }

    private void ValidateSavedMonkeyAdmissionRom()
    {
        foreach (int free in new[] { 0, 1, 8, 9, 10 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone); _saveData.SetGlobalFlag(AgesGlobalFlag.SavedNayru);
            LoadValidationRoom(0, 0x5a); IsolateHerosCave(e => e is MonkeyRoomEntity);
            _player.WarpTo(new(104, 104));
            var parent = _entities.EntityAdapters<MonkeyRoomEntity>().Single();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 104, 104);
            int at = 0xd040 + _entities.NativeInteractionSlot(parent) * 256;
            rom[at] = 1; rom[at + 1] = 0x39; rom[at + 2] = 5;
            FillInteractionAdmissionPool(rom, free);
            StepGameplayUpdates(2, Vector2.Zero, afterUpdate: () => rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter));
            foreach (var monkey in _entities.EntityAdapters<MonkeyRoomEntity>())
            {
                int address = 0xd040 + _entities.NativeInteractionSlot(monkey) * 256;
                FailIf(rom[address + 1] != 0x39 || rom[address + 3] != monkey.Index,
                    $"Monkey exhausted pool free={free}: descending child identity differs at ${address:x4}.");
            }
            FailIf(_entities.EntityAdapters<MonkeyRoomEntity>().Count() != 1 + Math.Min(9, free) ||
                _entities.EntityAdapters<AccessoryRoomEntity>().Count() != (free == 10 ? 1 : 0) ||
                _random.CaptureState().Rng1 != rom[0xff94] || _random.CaptureState().Rng2 != rom[0xff95],
                $"Monkey exhausted pool free={free}: child/bowtie allocation or RNG differs from the source.");
        }
    }

    private void ValidateSavedMonkeysRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _saveData.SetGlobalFlag(AgesGlobalFlag.SavedNayru);
            LoadValidationRoom(0, 0x5a);
            _player.WarpTo(new(104, 104));
            FailIf(_collision.Collides(_player.Position), "Monkey fixture must start on source floor.");
            var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 104, 104);
            foreach (var pair in slots.ToArray())
            {
                NpcCharacter? npc = pair.Key.Node as NpcCharacter;
                if (npc is null) continue;
                int address = 0xd040 + pair.Value * 256;
                rom[address] = 1; rom[address + 1] = (byte)npc.Record.Id;
                rom[address + 2] = (byte)npc.Record.SubId;
                rom.Word(address + 10, (int)(npc.Position.Y * 256));
                rom.Word(address + 12, (int)(npc.Position.X * 256));
            }
            int update = 0;
            StepGameplayUpdates(240, Vector2.Zero, batched: batch, afterUpdate: () =>
            {
                rom.UpdateGameplay(0, 0, 0xff, ++update);
                var monkeys = _entities.EntityAdapters<MonkeyRoomEntity>().ToArray();
                foreach (var monkey in monkeys)
                {
                    int address = 0xd040 + slots[monkey] * 256;
                    FailIf(rom[address + 1] != 0x39 || rom[address + 3] != monkey.Index ||
                        monkey.Node.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        (monkey.ZFixed & 0xffff) != rom.Word(address + 14) ||
                        (monkey.SpeedZ & 0xffff) != rom.Word(address + 0x14) ||
                        rom[address + 4] != 0 && (((NpcCharacter)monkey.Node).CurrentAnimationCounter != rom[address + 0x20] ||
                        ((NpcCharacter)monkey.Node).CurrentAnimationParameter != rom[address + 0x21]),
                        $"Saved monkey ${monkey.Index:x2}, update={update}, batch={batch}, slot=${slots[monkey]:x2}: " +
                        $"port XY/Z/speed={monkey.Node.Position}/{monkey.ZFixed}/{monkey.SpeedZ}; " +
                        $"ROM enabled/state/id/index=${rom[address]:x2}/${rom[address+4]:x2}/${rom[address+1]:x2}/${rom[address+3]:x2}, XY={rom.Word(address+12)/256f},{rom.Word(address+10)/256f}, Z/speed={rom.Word(address+14)}/{rom.Word(address+0x14)}, animation={((NpcCharacter)monkey.Node).CurrentAnimationCounter}/{((NpcCharacter)monkey.Node).CurrentAnimationParameter} vs {rom[address+0x20]}/{rom[address+0x21]}.");
                }
                FailIf(_random.CaptureState().Rng1 != rom[0xff94] || _random.CaptureState().Rng2 != rom[0xff95],
                    $"Saved monkey allocation must preserve global RNG order at update {update}.");
            });
            FailIf(_entities.EntityAdapters<MonkeyRoomEntity>().Count() != 10 || _entities.EntityAdapters<AccessoryRoomEntity>().Count() != 1,
                "Saved Nayru's room must contain ten monkeys and one bowtie.");
            foreach (int index in new[] { 8, 0 })
            foreach (bool finished in new[] { false, true })
            {
                _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, finished);
                LoadValidationRoom(0, 0x5a); StepGameplayUpdates(3, Vector2.Zero, batched: batch);
                var monkey = _entities.EntityAdapters<MonkeyRoomEntity>().Single(m => m.Index == index);
                var actor = (NpcCharacter)monkey.Node;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    ApproachLibraryActor(actor, batch);
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
                    for (int wait = 0; wait < 90 && !_dialogue.IsOpen; wait++) StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                    int textId = _dialogue.SourceTextId ?? -1;
                    FailIf(!_dialogue.IsOpen || (index == 8 ? textId != (finished ? 0x570c : 0x5707) :
                        textId < (finished ? 0x570d : 0x5708) || textId > (finished ? 0x5710 : 0x570b)),
                        $"Monkey ${index:x2} finished={finished}, repeat={repeat}: wrong source dialogue ${textId:x4}.");
                    FailIf(index == 0 && (monkey.ZFixed != 0 || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81),
                        "Ordinary monkey must land before showing text while retaining native mask $81.");
                    _dialogue.Close(); StepGameplayUpdates(4, Vector2.Zero, batched: batch);
                    FailIf(_runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0,
                        "Monkey conversation must release native controls before another A press.");
                }
            }
            var talkingMonkey = _entities.EntityAdapters<MonkeyRoomEntity>().Single(m => m.Index == 0);
            ApproachLibraryActor((NpcCharacter)talkingMonkey.Node, batch);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
            for (int wait = 0; wait < 90 && !_dialogue.IsOpen; wait++) StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(!_dialogue.IsOpen || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0x80, "Cancellation fixture must begin during an active monkey conversation.");
            int retiredObjects = -1, retiredMenu = -1;
            ((NpcCharacter)talkingMonkey.Node).Retired += _ => {
                retiredObjects = _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects); retiredMenu = _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled);
            };
            LoadValidationRoom(0, 0x38);
            FailIf(retiredObjects != 0 || retiredMenu != 0 || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                "Cancelling a monkey conversation must release its native object/menu masks at retirement.");
            LoadValidationRoom(0, 0x5a); StepGameplayUpdates(3, Vector2.Zero, batched: batch);
            FailIf(_entities.EntityAdapters<MonkeyRoomEntity>().Count() != 10, "Monkey ensemble must reconstruct on re-entry.");
            _saveData.SetGlobalFlag(AgesGlobalFlag.SavedNayru, false);
            LoadValidationRoom(0, 0x5a); StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(_entities.EntityAdapters<MonkeyRoomEntity>().Count() != 0,
                "Saved-Nayru gate must delete the ensemble before allocating children.");
        }
    }
}
