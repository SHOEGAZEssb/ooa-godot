using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMovingPlatformRiderRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x74); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather,1); _inventory.GiveTreasure(TreasureId.SwitchHook,1);
            _inventory.EquipA(TreasureId.Feather); _inventory.EquipB(TreasureId.SwitchHook);
            _player.Face(Vector2I.Right); _player.WarpTo(new(80,68));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Platform boarding must launch Feather from the actual Skull$4:$74 west shore.");
            var rider = _entities.Spawn<MovingPlatformRoomEntity>(new MovingPlatformSpawn(new(112,64),0x11));
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,80,68);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 4;
            rom[0xcc21] = 68; rom[0xcc22] = 80; rom[0xcc23] = 1;
            rom[0xd240] = 1; rom[0xd241] = 0x79; rom[0xd242] = 0x11; rom[0xd24b] = 64; rom[0xd24d] = 112;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,int button = 0) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                MenuRomActions(button),MenuRomActions(button),batched:batch,afterUpdate:() => {
                int held = button | (angle switch {8=>0x10,24=>0x20,_=>0});
                rom.UpdateGameplay(button,held,angle,_entities.FrameCounter,() => rom[0xcc96] = rom[0xcc8d]); button = 0;
                string context = $"Platform rider update{++update}, batch={batch}";
                FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    _player.TopDownAirborne != (rom[0xcc5c] != 0) || (_player.SwitchHookZFixed&0xffff) != rom.Word(0xd00e) ||
                    _player.HealthQuarters != rom[0xc6aa] || _entities.PlayerRidingObject != (rom[0xcc96] != 0),
                    context+$": Link XY/Z/air/health/support differ: {_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, support={_entities.PlayerRidingObject}/{rom[0xcc96]:x2}, text={_dialogue.IsOpen}/{rom[0xcba0]:x2}, platform={rider.PrecisePosition}/{rom.Word(0xd24a)/256f}, counter={rider.Counter}/{rom[0xd246]}, state={rom[0xd004]:x2}, drowning={_player.IsDrowning}.");
                foreach (var platform in _entities.Entities<MovingPlatformRoomEntity>())
                {
                    int slot = (0xd0+_entities.InteractionSlot(platform))*256+0x40;
                    FailIf(platform.PrecisePosition != new Vector2(rom.Word(slot+0xc)/256f,rom.Word(slot+0xa)/256f) ||
                        platform.Counter != rom[slot+6] || platform.Angle != rom[slot+9] ||
                        platform.LinkRiding != (rom[0xcc96] == slot>>8),
                        context+": source-ordered platform/rider ownership or half-pixel motion differs.");
                }
                var hook = _entities.SwitchHook!.Item;
                bool nativeHook = rom[0xd600] != 0 && rom[0xd601] == 0x0a;
                FailIf((hook is { Finished:false }) != nativeHook || _player.IsUsingSwitchHook != (rom[0xd200] != 0 && rom[0xd201] == 0x0a),
                    context+": carried Switch Hook parent/weapon lifecycle differs.");
                if (nativeHook) FailIf(hook!.State != rom[0xd604] || hook.Counter != rom[0xd606] ||
                    hook.PrecisePosition != new Vector2(rom.Word(0xd60c)/256f,rom.Word(0xd60a)/256f),
                    context+": Switch Hook motion/counter differs while the platform carries Link.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.Where(cue=>cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    context+": native cue/RNG order differs.");
            });
            Step();
            for (int wait = 0; (!rider.Moving || rider.Angle != 0 || rider.PrecisePosition.Y != 72) && wait < 550; wait++) Step();
            FailIf(rider.Angle != 0 || rider.PrecisePosition.Y != 72,"Native platform must reach the original Feather boarding window.");
            Step(angle:8,button:1); Step(26,8);
            FailIf(!rider.LinkRiding || !_player.TopDownAirborne,"Actual Feather approach must acquire the platform while airborne.");
            Vector2 overlapPosition = rider.Position;
            _entities.Spawn<MovingPlatformRoomEntity>(new MovingPlatformSpawn(overlapPosition,0x11));
            rom[0xd340] = 1; rom[0xd341] = 0x79; rom[0xd342] = 0x11;
            rom.Word(0xd34c,(int)(overlapPosition.X*256)); rom.Word(0xd34a,(int)(overlapPosition.Y*256));
            Step(20);
            FailIf(!rider.LinkRiding || _entities.Entities<MovingPlatformRoomEntity>()[1].LinkRiding,
                "Later overlapping platform must not steal the earlier native slot's rider or double its movement.");
            for (int shot = 0; shot < 3; shot++)
            {
                Step(angle:8,button:2); Step(4);
                FailIf(!_player.IsUsingSwitchHook || !rider.LinkRiding,"Standing platform support must permit Switch Hook and retain carry.");
                if (shot == 1) { _playerWorld.ClearItemParents(_player); rom.ClearItemParents(); }
                for (int wait = 0; _player.IsUsingSwitchHook && wait < 96; wait++) Step();
                Step(6);
                FailIf(_player.IsUsingSwitchHook || !rider.LinkRiding,"Hook completion/parent cancellation must retain platform support for repeated use.");
            }
            _dialogue.ShowGameplayMessage("Rider pause",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step();
            // Text clears the shared rider publication. The first resumed
            // Link handler requests lava drowning while still state01, so
            // INTERAC$79 carries once before the forced state is consumed.
            FailIf(!_player.IsDrowning || rom[0xd004] != 1 || !rider.LinkRiding,
                "Resumed lava request must retain native state01 carry until the following Link dispatch.");
            for (int wait = 0; (_player.IsDrowning || rom[0xd004] != 1) && wait < 100; wait++) Step();
            FailIf(_player.IsDrowning || rom[0xd004] != 1 || _player.HealthQuarters != 10 || rider.LinkRiding,
                "Forced drowning must finish native shore recovery with one damage debit and release support.");
            for (int wait = 0; (!rider.Moving || rider.Angle != 0 || rider.PrecisePosition.Y != 72) && wait < 550; wait++) Step();
            FailIf(rider.Angle != 0 || rider.PrecisePosition.Y != 72,"Repeated boarding must wait for the native platform window.");
            Step(angle:8,button:1); Step(26,8); Step(6);
            FailIf(!rider.LinkRiding,"Actual repeated Feather from the respawn shore must reacquire the first platform slot.");
            for (int wait = 0; (rider.Angle != 16 || _player.Position.Y != 64 || _player.TopDownAirborne) && wait < 550; wait++) Step();
            FailIf(rider.Angle != 16 || _player.Position.Y != 64,"Native return window must reach the unchanged west shore.");
            Step(angle:24,button:1);
            for (int wait = 0; _player.TopDownAirborne && wait < 40; wait++) Step(angle:24);
            FailIf(rider.LinkRiding || _player.IsDrowning || _collision.Collides(_player.Position) ||
                _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Actual return Feather must release the platform and land on original safe shore.");
            LoadValidationRoom(4,0x91); LoadValidationRoom(4,0x74);
            FailIf(_entities.Entities<MovingPlatformRoomEntity>().Count != 1 || _entities.PlayerRidingObject,
                "Room cancellation/re-entry must discard the dynamic overlap and shared rider ownership.");
        }
    }
}
