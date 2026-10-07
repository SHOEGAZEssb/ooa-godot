using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMovingPlatformHoleRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x15); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather,1); _inventory.EquipA(TreasureId.Feather); _inventory.EquipB(0);
            _player.Face(Vector2I.Left); _player.WarpTo(new(72.25f,136.5f));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Hole-platform boarding must launch from original Spirits' Grave$4:$15 east shore.");
            // Retain the original INTERAC$79:$05 placement. Chest/button
            // consumers are excluded; all Link/item/platform updates are live.
            var platform = _entities.Spawn<MovingPlatformRoomEntity>(new MovingPlatformSpawn(new(48,144),5));
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,72,136);
            rom.Word(0xd00c,72*256+64); rom.Word(0xd00a,136*256+128);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 1;
            rom[0xcc21] = 136; rom[0xcc22] = 72; rom[0xcc23] = 3;
            rom[0xd240] = 1; rom[0xd241] = 0x79; rom[0xd242] = 5; rom[0xd24b] = 144; rom[0xd24d] = 48;
            Vector2 observed = default, nativeBefore = default;
            var observer = new ItemPhaseValidationEntity(() => observed = _player.PrecisePosition);
            _entities.RegisterEnemySlot(observer,0);
            _entities.AddEntity(observer);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,bool jump = false) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                jump ? ["attack"] : [],jump ? ["attack"] : [],batched:batch,afterUpdate:() => {
                int button = jump ? 1 : 0; jump = false;
                rom.UpdateGameplay(button,button|(angle switch {8=>0x10,24=>0x20,_=>0}),angle,_entities.FrameCounter,
                    () => { nativeBefore = new(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f); rom[0xcc96] = rom[0xcc8d]; });
                string context = $"Hole platform batch={batch}, update{++update}";
                FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    _player.TopDownAirborne != (rom[0xcc5c] != 0) || (_player.SwitchHookZFixed&0xffff) != rom.Word(0xd00e) ||
                    _player.HealthQuarters != rom[0xc6aa] || _player.NativeNormalStateForInteraction != (rom[0xd004] == 1) ||
                    platform.LinkRiding != (rom[0xcc96] == 0xd2) || _entities.PlayerRidingObject != (rom[0xcc96] != 0) ||
                    platform.PrecisePosition != new Vector2(rom.Word(0xd24c)/256f,rom.Word(0xd24a)/256f) || platform.Counter != rom[0xd246],
                    context+$": Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, before={observed}/{nativeBefore}, platform={platform.PrecisePosition}/{rom.Word(0xd24c)/256f},{rom.Word(0xd24a)/256f}, state={rom[0xd004]:x2}, support={platform.LinkRiding}/{rom[0xcc96]:x2}, hole={_player.IsPullingIntoHole}:{SomariaPrivate<int>(_player,"_holePullCounter")}/{rom[0xcc9b]}.");
                if (_player.IsPullingIntoHole) FailIf(SomariaPrivate<int>(_player,"_holePullCounter") != rom[0xcc9b],context+": native partial hole-pull counter differs.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.Where(cue=>cue != SoundId.SndText).SequenceEqual(rom.Sounds),context+": cue/RNG order differs.");
            });
            for (int repeat = 0; repeat < 2; repeat++)
            {
                if (repeat != 0)
                {
                    for (int wait = 0; platform.PrecisePosition.Y != 144 && wait < 280; wait++) Step();
                    FailIf(platform.PrecisePosition.Y != 144,"The native vertical route must return to its actual boarding shore.");
                }
                Step(angle:24,jump:true); Step(11,24); Step(24);
                FailIf(!platform.LinkRiding || _player.TopDownAirborne || _player.IsPullingIntoHole || _player.IsFallingInHole,
                    "Reachable Feather boarding and landing must retain support above the original hole geometry.");
                _dialogue.ShowGameplayMessage("Hole platform",120); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step();
                if (repeat == 1)
                {
                    FailIf(!_player.IsFallingInHole || rom[0xd004] != 2 || !platform.LinkRiding,
                        "The repeated boarding's centered hole position must enter native state02 immediately despite the later rider claim.");
                    for (int wait = 0; (_player.IsFallingInHole || rom[0xd004] != 1) && wait < 100; wait++) Step();
                    FailIf(_player.IsFallingInHole || rom[0xd004] != 1 || _player.HealthQuarters != 10 || platform.LinkRiding,
                        "Native falling must complete on the original local shore with one half-heart debit and no retained rider.");
                    continue;
                }
                FailIf(!_player.IsPullingIntoHole || !platform.LinkRiding,
                    "After text clears shared support, Link must begin a partial hole pull before the platform claims him again.");
                Step();
                FailIf(_player.IsPullingIntoHole || !platform.LinkRiding,
                    "The following Link update must consume native support and cancel the partial hole pull.");
                for (int wait = 0; (platform.PrecisePosition.Y != 140 || platform.Angle != 16) && wait < 280; wait++) Step();
                FailIf(platform.PrecisePosition.Y != 140 || platform.Angle != 16,
                    "The native downward return must reach the original east-shore jump window before turning upward.");
                Step(angle:8,jump:true); Step(18,8); Step(30);
                FailIf(platform.LinkRiding || _player.TopDownAirborne || _player.IsPullingIntoHole || _player.IsFallingInHole ||
                    _player.HealthQuarters != 12 || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None || _collision.Collides(_player.Position),
                    $"Actual return jump must reach the original safe shore before repeat boarding: XY={_player.PrecisePosition}, support={platform.LinkRiding}, air={_player.TopDownAirborne}, health={_player.HealthQuarters}, hazard={_currentRoom.GetTerrainInfo(_player.Position).Hazard}.");
            }
            LoadValidationRoom(4,0x14);
            FailIf(_entities.PlayerRidingObject,"Room replacement must clear the hole-platform rider publication.");
        }
    }
}
