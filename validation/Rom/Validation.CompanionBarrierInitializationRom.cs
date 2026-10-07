using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareCompanionBarrierInitializationRom()
    {
        int fixture = 0;
        // Permanent/completed/waiting, then death before state0 or after
        // actual mounted initialization while text owns the dispatcher.
        foreach (int gate in new[] {0,1,2,3,4})
        foreach (bool batch in RomHostSchedules(gate >= 3 ? 0 : fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0,0x89);
            _player.WarpTo(new(104,96)); _inventory.EquipA(0); _inventory.EquipB(0);
            FailIf(_currentRoom.IsSolid(_player.Position),"Barrier state0 probe requires original room$0:$89 floor.");
            if (gate is 0 or 4)
            {
                if (gate == 0) _saveData.WriteWramByte(0xc647,0x80);
                int direction = gate == 4 ? 0 : 2;
                _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(new(104,96),direction,0,0x89,Riding:true));
                CompanionRuntimeState.Begin(_runtimeState,0x0c,0x89,new(104,96),direction);
            }
            if (gate == 1) _saveData.WriteWramByte(0xc614,1);
            var barrier = _entities.Entities<CompanionBarrierRoomEntity>().Single();
            var tutorial = _entities.Entities<CompanionTutorialRoomEntity>().Single();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,104,96) { CompanionDispatchEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            if (gate is 0 or 4)
            {
                var mounted = new CompanionRom(0x0c,new(104,96),gate == 4 ? 0 : 2,_currentRoom);
                for (int address = 0xd000; address < 0xd040; address++) rom[address] = mounted[address];
                for (int address = 0xd100; address < 0xd140; address++) rom[address] = mounted[address];
                rom[0xcc2c] = 0xd1; rom[0xcc96] = 1; rom[0xccaa] = 0xff;
            }
            rom[0xd240] = 1; rom[0xd241] = 0xd0; rom[0xd24b] = 0x38; rom[0xd24d] = 0x30;
            rom[0xd340] = 1; rom[0xd341] = 0x71; rom[0xd342] = 2;
            rom[0xd34b] = 0x6d; rom[0xd34d] = 0x38;
            void Step(int count,bool dismount = false)
            {
                int edge = dismount ? 2 : 0;
                StepGameplayUpdates(count,Vector2.Zero,dismount ? ["item"] : [],dismount ? ["item"] : [],batch,afterUpdate:() => {
                    rom.AdvanceDeathPrelude(); rom.UpdateGameplay(edge,dismount ? 2 : 0,0xff,_entities.FrameCounter-1); edge = 0;
                    FailIf(barrier.Finished != (rom[0xd340] == 0) ||
                        !barrier.Finished && barrier.State != rom[0xd344] ||
                        tutorial.Finished != (rom[0xd240] == 0) || !tutorial.Finished && tutorial.State != rom[0xd244],
                        $"Barrier$71:$02/tutorial$d0:$00 state0 text dispatch, gate={gate}, batch={batch}: runtime={barrier.State}/{barrier.Finished}, native=${rom[0xd344]:x2}/${rom[0xd340]:x2}.");
                    if (gate is 3 or 4)
                    {
                        FailIf(_player.IsDying != (rom[0xcdd5] != 0) || _player.HealthQuarters != rom[0xc6aa] ||
                            _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                            _player.DeathAnimationActive != (rom[0xd004] == 3 && rom[0xd005] == 1),
                            "Barrier death lifetime must agree with actual lethal publication, fixed Link and death initialization.");
                        if (_player.DeathAnimationActive)
                            FailIf(_player.DeathAnimationCounter != rom[0xd020] ||
                                _player.DeathAnimationFrame != rom[0xd031] || _player.DeathSpinLoopsRemaining != rom[0xd006],
                                "Barrier death dispatch must preserve native Link animation/counter/loop boundaries.");
                    }
                });
            }
            if (gate == 4)
            {
                Step(1);
                FailIf(barrier.State != 1 || barrier.Finished,"Death-after-init requires actual mounted barrier state1 first.");
                Step(35,dismount:true);
                FailIf(_player.CompanionRideActive || rom[0xcc2c] != 0xd0 || rom[0xd001] != 0 ||
                    _currentRoom.IsSolid(_player.Position),
                    "Initialized barrier must survive an actual upward dismount onto original floor before the death input.");
            }
            _dialogue.ShowGameplayMessage("Barrier initialization",100); rom[0xcba0] = 1;
            if (gate is 3 or 4)
            {
                // Declare lethal publication, then execute real death handling
                // and the original interaction dispatcher on every update.
                FailIf(!_player.ApplyDamage(_player.HealthQuarters) || !_player.IsDying,
                    "Barrier death input must publish pending lethal damage.");
                rom.ApplyLinkDamage(unchecked((byte)(-2*rom[0xc6aa]))); rom[0xd004] = 3;
            }
            Step(3);
            FailIf(!_dialogue.IsOpen || barrier.Finished != (gate is 0 or 1 or 3),
                "Source state0 runs during text: completed files delete before mount; permanent companions delete after mount; others wait.");
            _dialogue.Close(); rom[0xcba0] = 0; Step(3);
            if (gate is 3 or 4)
            {
                FailIf(!barrier.Finished || _entities.Entities<CompanionBarrierRoomEntity>().Any() ||
                    rom[0xd340] != 0 || rom[0xcc8a] != 0,
                    "Pending death must delete state0 during text; initialized state1 must wait until the dispatcher resumes.");
                Step(3);
                if (gate == 3)
                {
                    // Isolated incoming state0 under the same live pending
                    // death byte. Destination enemy AI/loading is excluded.
                    _entities.Clear(); rom.SetOutgoingInteractions(); rom.ClearOutgoingInteractions();
                    rom[0xcd00] = 8;
                    rom[0xd240] = 1; rom[0xd241] = 0xd0; rom[0xd24b] = 0x38; rom[0xd24d] = 0x30;
                    rom[0xd340] = 1; rom[0xd341] = 0x71; rom[0xd342] = 2;
                    rom[0xd34b] = 0x6d; rom[0xd34d] = 0x38;
                    _entities.BeginScreenTransition(0,_currentRoom,new(160,0),_player);
                    rom.AdvanceInteractions(_entities.FrameCounter);
                    FailIf(_entities.Entities<CompanionBarrierRoomEntity>().Any() || rom[0xd340] != 0,
                        "Incoming barrier state0 must observe retained pending death during native scroll preload.");
                    _entities.FinishScreenTransition(); rom[0xcd00] = 1;
                }
            }
            if (gate == 2)
            {
                _saveData.WriteWramByte(0xc614,1); rom[0xc614] = 1;
                Step(2);
                FailIf(!barrier.Finished,"Waiting unmounted barrier must observe completed-file deletion on its next update.");
            }
        }
    }
}
