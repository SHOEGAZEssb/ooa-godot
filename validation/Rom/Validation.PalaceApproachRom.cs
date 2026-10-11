using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceApproachRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            _saveData.SetRoomFlag(0, 0x83, 0x80);
            LoadValidationRoom(1, 0x46); _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = Enumerable.Range(0x38, 0x30).Select(x => new Vector2(x, 0x60))
                .First(position => Enumerable.Range(0, 40).All(distance => !_collision.Collides(position + Vector2.Up * distance)));
            FailIf(_collision.Collides(start), "Palace approach $6b:$02 starts in solid geometry.");
            _player.WarpTo(start); _player.Face(Vector2I.Up);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)start.X, (int)start.Y)
                { CompanionDispatchEnabled = true };
            const int trigger = 0xd240;
            rom[trigger] = 1; rom[trigger + 1] = 0x6b; rom[trigger + 2] = 2;
            rom[trigger + 11] = 0x38; rom[trigger + 13] = 0x50;
            int update = 0, texts = 0, textHold = 0, generation = 0;
            void Compare(int held, int angle)
            {
                rom.UpdateGameplay(0, held, angle, ++update);
                var ralph = _entities.EntityAdapters<PalaceApproachRoomEntity>().FirstOrDefault(actor => actor.IsRalph);
                int nativeRalph = Enumerable.Range(2, 14).Select(slot => 0xd040 + slot * 256)
                    .FirstOrDefault(slot => rom[slot] != 0 && rom[slot + 1] == 0x37 && rom[slot + 2] == 1);
                FailIf((ralph is not null) != (nativeRalph != 0), $"Palace approach Ralph allocation differs at update={update}, batch={batch}.");
                if (ralph is not null)
                    FailIf(ralph.Node.Position != new Vector2(rom.Word(nativeRalph + 12) / 256f, rom.Word(nativeRalph + 10) / 256f) ||
                        ralph.ZFixed != unchecked((short)rom.Word(nativeRalph + 14)),
                        $"Palace approach Ralph movement/jump differs at update={update}: port={ralph.Node.Position}/{ralph.ZFixed}, ROM={rom.Word(nativeRalph+12)/256f},{rom.Word(nativeRalph+10)/256f}/{unchecked((short)rom.Word(nativeRalph+14))}.");
                FailIf(_player.Position != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                    $"Palace approach Link control differs at update={update}, batch={batch}: port={_player.Position}, ROM={rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                if (rom.TextGeneration != generation)
                {
                    FailIf(++texts != 1 || rom.Word(0xcba2) != 0x2e08 || !_dialogue.IsOpen,
                        "Palace approach must show source TX_2a08 once.");
                    FailIf(!_dialogue.CurrentMessage.Contains("Wait here! I'm\ngoing!") ||
                        !_dialogue.CurrentMessage.Contains("NAYRU") || _dialogue.CurrentMessage.Contains("\\jump("),
                        "TX_2a08 must retain its TX_2a06 jump tail and formatting.");
                    generation = rom.TextGeneration; textHold = 3;
                }
                if (textHold > 0 && --textHold == 0) { _dialogue.Close(); rom[0xcba0] = 0; }
            }
            StepGameplayUpdates(40, Vector2.Up, ["move_up"], batched: batch, afterUpdate: () => Compare(0x40, 0));
            StepGameplayUpdates(450, Vector2.Zero, batched: batch, afterUpdate: () => Compare(0, 0xff));
            FailIf(texts != 1 || !_saveData.HasRoomFlag(1, 0x46, 0x40) || (rom[0xc846] & 0x40) == 0 ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0,
                "Palace approach must complete Ralph's dialogue, persist room flag$40 and release controls.");
            Vector2 releasedPosition = _player.Position;
            StepGameplayUpdates(8, Vector2.Down, ["move_down"], batched: batch, afterUpdate: () => Compare(0x80, 16));
            FailIf(_player.Position != releasedPosition + Vector2.Down * 8 || _player.ScriptedLinkAnimationMode.HasValue ||
                _player.LinkAnimationCounter != rom[0xd020],
                $"Ralph $37:$01 must restore ordinary walking animation after completion, batch={batch}.");
            ValidatePalacePostRalphSword(rom, batch, "Ralph $37:$01");
            LoadValidationRoom(1, 0x46);
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(_entities.EntityAdapters<PalaceApproachRoomEntity>().Any(), "Completed palace approach repeats on re-entry.");
        }
        ReinitializeGameplayForValidation();
        LoadValidationRoom(1, 0x46);
        StepGameplayUpdates(2, Vector2.Zero);
        FailIf(_entities.EntityAdapters<PalaceApproachRoomEntity>().Any(),
            "Palace Ralph approach must not start before Ambi's Bomb reward (present room $83 flag$80).");
    }
}
