using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuWaterControllerRom()
    {
        foreach (bool batch in new[] {false,true})
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wJabuWaterLevel,0);
            LoadValidationRoom(5,0x6d);
            FailIf(_entities.Entities<JabuWaterControllerRoomEntity>().Count != 1,
                "Room$5:$6d must construct INTERAC$90:$0a after its three PART$09 source records.");
            _entities.Clear(); _inventory.EquipA(0); _inventory.EquipB(0);
            var data=new DungeonMechanicDatabase();
            var waterData=new JabuDungeonDatabase();
            var controller=new JabuWaterControllerRoomEntity(_saveData,_runtimeState,
                () => (byte)_entities.ActiveTriggers,_entities.OnNativeSoundRequested,_entities.BeginScreenShake,
                id => _entities.OnRoomEntityDialogueRequested(id,waterData.Text(id),Vector2.Zero),_entities.NativeActiveMusicSource);
            _entities.AddEntity(controller);
            foreach (var record in data.GetRoomRecords(5,0x6d).Where(record => record.Id == 9))
                _entities.AddEntity(new GroundButtonRoomEntity(record,_currentRoom,data,_entities.SetTrigger,
                    (position,tile) => _rooms.TrySetTile(position,tile),_sound.PlaySound));
            var buttons=_entities.Entities<GroundButtonRoomEntity>().OrderBy(button => button.SubId).ToArray();
            FailIf(buttons.Length != 3 || !buttons.Select(button => button.SubId).SequenceEqual(new[] {0x84,0x85,0x87}) ||
                !buttons.Select(button => button.PackedPosition).SequenceEqual(new[] {0x36,0x5a,0x76}),
                "Source room$5:$6d water controls require PART$09:$84/$85/$87 at $36/$5a/$76.");
            _player.WarpTo(new(104,72)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Water-button approach must start on real floor outside button$36.");
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,104,72) {HostilePartsEnabled=true};
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcc39]=7; rom[0xcc3b]=2; rom[0xcc35]=(byte)_sound.NativeActiveMusic;
            int slot=0xd040+_entities.InteractionSlot(controller)*256;
            rom[slot]=1; rom[slot+1]=0x90; rom[slot+2]=0x0a;
            var partSlots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            foreach(var button in buttons)
            {
                int part=0xd0c0+partSlots[button]*256;
                rom[part]=1; rom[part+1]=9; rom[part+2]=(byte)button.SubId;
                rom[part+11]=(byte)button.Position.Y; rom[part+13]=(byte)button.Position.X;
            }
            var sounds=_sound.AttachPlayRequestAudit(); int update=0;
            void Step(int count=1,int angle=0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.CardinalVector(angle),batched:batch,
                afterUpdate:() => {
                    int held=angle switch {0=>0x40,8=>0x10,16=>0x80,24=>0x20,_=>0};
                    rom.UpdateGameplay(0,held,angle,_entities.FrameCounter);
                    rom.AdvanceScreenShake(); rom.AdvanceTileGraphics();
                    string context=$"Jabu water batch={batch}, update{++update}";
                    CompareSomariaMotionRom(rom,context);
                    FailIf(controller.State != rom[slot+4] || controller.Counter != rom[slot+6] ||
                        controller.LastTriggers != rom[slot+0x30] ||
                        _saveData.ReadWramByte(WramAddress.wJabuWaterLevel) != rom[0xc6e9] ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != rom[0xcdd3] ||
                        _entities.ActiveTriggers != rom[0xcca0] ||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                        _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02] ||
                        _entities.ScreenShakeCounter != rom[0xcd18] || _entities.HorizontalScreenShakeCounter != rom[0xcd19],
                        context+$": controller/water/trigger/lock/shake differs, state {controller.State}/{rom[slot+4]}, counter{controller.Counter}/{rom[slot+6]}.");
                    var random=_random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context+": ordered water cues/shared shake RNG differs.");
                });
            Step();
            void Approach(GroundButtonRoomEntity button,int expectedWater)
            {
                Vector2 start=button.Position+Vector2.Down*16;
                FailIf(_collision.Collides(start),$"Room$5:$6d button${button.PackedPosition:x2} requires a reachable floor approach.");
                _player.WarpTo(start); _player.Face(Vector2I.Up);
                rom.Word(0xd00a,(int)start.Y*256); rom.Word(0xd00c,(int)start.X*256);
                rom[0xd008]=0; rom[0xd009]=0;
                for (int walk=0;controller.State == 1 && !button.Pressed && walk<24;walk++) Step(angle:0);
                FailIf(!button.Pressed || _saveData.ReadWramByte(WramAddress.wJabuWaterLevel) != expectedWater,
                    $"Jabu button${button.SubId:x2} must produce source water${expectedWater:x2}.");
            }
            void Complete()
            {
                Vector2 locked=_player.Position;
                Step(59,8);
                FailIf(controller.State != 2 || controller.Counter != 1 || _player.Position != locked,
                    "Jabu fill/drain must retain Link and delay counter1 through update59.");
                Step();
                FailIf(controller.State != 3 || controller.Counter != 240,"Jabu floodgates must begin on update60 with 240 updates.");
                Step(239,8);
                FailIf(controller.State != 3 || controller.Counter != 1 || _dialogue.IsOpen || _player.Position != locked,
                    "Jabu water must remain modal through shake update239.");
                Step();
                FailIf(controller.State != 1 || !_dialogue.IsOpen,"Jabu final shake update must show TX_1208/TX_1209 and release masks.");
                _dialogue.Close(); rom[0xcba0]=0;
                Step(2,16); // Walk off the button; publish its falling pressure edge.
            }
            Approach(buttons[0],0x11); Complete();
            Approach(buttons[0],0x11); Step(3); // Already-used fill cannot run again.
            FailIf(controller.State != 1,"Persistent high-nibble fill history must suppress repeated fill.");
            Step(12,16);
            Approach(buttons[1],0x32); Complete();
            Approach(buttons[2],0); Complete();
            // Leaving the drain after completion must keep water zero.
            Step(12,16);
            FailIf(controller.State != 1 || _saveData.ReadWramByte(WramAddress.wJabuWaterLevel) != 0,
                "Drain release at level zero must remain idle.");
            Approach(buttons[0],0x11); Complete();
            _saveData.WriteWramByte(WramAddress.wJabuWaterLevel,0x32);
            LoadValidationRoom(5,0x6d);
            StepGameplayUpdates(1,Vector2.Zero,batched:batch);
            FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x30,
                "Re-entering room$5:$6d must reconstruct fill history from persisted water byte$32.");
        }
        GD.Print("Validated clean-US Jabu water controller, real button approaches, 60/240-update locks, cues/shake RNG, fill reuse, drain, refill and re-entry in split/batched gameplay.");
    }
}
