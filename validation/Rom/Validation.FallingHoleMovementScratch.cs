using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFallingHoleMovementScratch()
    {
        int fixture = 0;
        foreach (bool centered in new[] { false, true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4,0xa1); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120,40));
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,120,40);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var effect = _entities.Spawn<FallingDownHoleEffect>(
                new FallingDownHoleSpawn(new(centered ? 88.25f : 86,88)));
            int slot = 0xd000+_entities.InteractionSlot(effect)*256+0x40;
            rom[slot] = 1; rom[slot+1] = 0x0f;
            rom[slot+0xb] = 88; rom[slot+0xd] = (byte)(centered ? 88 : 86);
            // Observe the interaction phase before later terrain dispatch
            // reuses wTmpcec0. The native fixture ends at the object pass.
            byte[] observed = new byte[4];
            _entities.AddEntity(new ItemPhaseValidationEntity(() => {
                for (int i = 0; i < 4; i++)
                    observed[i] = _runtimeState.ReadWramByte(0xcec0+i);
            }));
            int updates = 0;
            void Stage()
            {
                for (int i = 0; i < 4; i++)
                {
                    _runtimeState.SetWramByte(0xcec0+i,0xa5);
                    rom[0xcec0+i] = 0xa5;
                }
                _runtimeState.SetWramByte(WramAddress.wTmpcec0,0xff);
                rom[0xcec0] = 0xff;
            }
            Stage();
            StepSomariaMotionRom(rom,34,batch,afterUpdate:() => {
                updates++;
                for (int i = 0; i < 4; i++)
                    FailIf(observed[i] != rom[0xcec0+i],
                        $"INTERAC$0f scratch ${0xcec0+i:x4}, update{updates}, centered={centered}, batch={batch}: runtime=${observed[i]:x2}, native=${rom[0xcec0+i]:x2}.");
                FailIf(effect.Finished != (rom[slot] == 0) ||
                    !effect.Finished && (effect.PrecisePosition !=
                        new Vector2(rom.Word(slot+0xc)/256f,rom.Word(slot+0xa)/256f) ||
                        effect.CurrentParameter != rom[slot+0x21]),
                    $"INTERAC$0f native movement/animation/deletion differs on update{updates}.");
                // Source state0 yields; state1 reaches parameter$ff after
                // 8/12/12 animations and deletes on the following update.
                if (updates == 33)
                    FailIf(effect.Finished || effect.CurrentParameter != 0xff ||
                        effect.PrecisePosition != new Vector2(centered ? 88 : 88.25f,88),
                        "INTERAC$0f must copy whole pixels, stop when high bytes center, and retain its terminal frame.");
                Stage();
            });
            FailIf(!effect.Finished,"INTERAC$0f must delete on update34 including its state0 yield.");
        }
    }
}
