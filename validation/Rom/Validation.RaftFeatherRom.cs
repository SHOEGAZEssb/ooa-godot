using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRaftFeatherRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (bool near in new[] { false, true })
        foreach (int fraction in new[] { 0, 255 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(0x26);
            LoadValidationRoom(1, 0xa7); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(primary ? TreasureId.Feather : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Feather);
            int raftX = near ? 96 : 80;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool water = y is 5 or 6 && (near ? x is 6 or 7 : x is 4 or 5);
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    water ? (byte)0xfc : (byte)0xa0, water ? (byte)0x10 : (byte)0, 0);
            }
            var raft = new RaftRoomEntity(new RaftSpawn(new(raftX, 88), 0, 1, 0xa7),
                _currentRoom, new RaftDatabase().Behavior, _runtimeState);
            _entities.AddEntity(raft);
            // Both takeoff points are open dry shore. The near point is
            // already inside the inner radius; rising Z must prevent boarding.
            int startY = near ? 83 : 54, startX = near ? 92 : 80;
            _player.WarpTo(new(startX + fraction / 256.0f, startY + 0.5f)); _player.Face(Vector2I.Down);
            var randomStart = _random.CaptureState();
            var rom = new SomariaRom(_saveData, randomStart, _currentRoom, 2, startX, startY)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = (byte)fraction;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            const int slot = 0xd240;
            rom[slot] = 1; rom[slot + 1] = 0xe6; rom[slot + 2] = 1;
            rom[slot + 0x0b] = 88; rom[slot + 0x0d] = (byte)raftX;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            FailIf(rom[slot + 4] != 1 || _currentRoom.IsSolid(_player.Position),
                "Raft Feather takeoff must use initialized placement and open shore geometry.");
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, previousHeld = 0, update = 0;
            bool airborneAllocation = false;
            void Step(int count = 1, int angle = 0xff, bool feather = false)
            {
                Vector2 input = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = (feather ? button : 0) | (angle == 0x10 ? 0x80 : angle == 0 ? 0x40 : 0);
                int pressed = held & ~previousHeld; previousHeld = held;
                StepGameplayUpdates(count, input, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.UpdateGameplay(pressed, held, angle, _entities.FrameCounter - 1); pressed = 0; update++;
                    string context = $"Raft Feather A={primary} near={near} fraction=${fraction:x2} update={update} batch={batched}";
                    Vector2 nativePosition = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(_player.PrecisePosition != nativePosition || _player.RaftRideActive != (rom[0xcc2c] == 0xd1) ||
                        _player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                        (_player.SwitchHookZFixed & 0xffff) != rom.Word(0xd00e) ||
                        (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014) ||
                        _player.HealthQuarters != rom[0xc6aa] || _player.IsDrowning || _player.TopDownSwimming,
                        context + $": motion/ride/air differs: runtime={_player.PrecisePosition}/ride={_player.RaftRideActive}/air={_player.TopDownAirborne}/Z${_player.SwitchHookZFixed & 0xffff:x4}/Vz${_player.TopDownAirSpeedZ & 0xffff:x4}, native={nativePosition}/rider${rom[0xcc2c]:x2}/air${rom[0xcc5c]:x2}/Z${rom.Word(0xd00e):x4}/Vz${rom.Word(0xd014):x4}/companion${rom[0xd100]:x2}:${rom[0xd104]:x2}.");
                    FailIf(Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x17),
                        context + ": Ages Feather must release the launch/rejected parent immediately.");
                    if (rom[0xd100] != 0 && rom[0xd104] == 0 && rom[0xcc5c] != 0)
                    {
                        airborneAllocation = true;
                        FailIf(rom[0xd00f] < 0xfd || (rom[0xd015] & 0x80) != 0,
                            context + ": native interaction accepted high or rising Link.");
                    }
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + $": jump/landing/raft sounds differ: runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - randomStart.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            Step(1, near ? 0xff : 0x10, true);
            FailIf(!_player.TopDownAirborne || rom.Word(0xd00e) != 0xfe20 || rom.Word(0xd014) != 0xfe40 ||
                rom[0xd100] != 0,
                "Feather must launch with Z=-$01e0/Vz=-$01c0 and the rising near-shore update must reject raft allocation.");
            if (near) Step(29, feather: true);
            for (int approach = 0; !raft.LinkRiding && approach < 40; approach++) Step(1, 0x10, true);
            FailIf(!raft.LinkRiding || !airborneAllocation || update != 31 || rom[0xcc5c] != 0,
                "The reachable Feather approach must allocate during update30 and initialize the raft while landing on update31.");
            Step(24, feather: true);
            Step(); Step(8, feather: true);
            FailIf(_player.TopDownAirborne || rom[0xcc5c] != 0,
                "Fresh Feather input while mounted must reject launch.");
            _dialogue.ShowMessage("Raft Feather pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(6, 0, true); _dialogue.Close(); rom[0xcba0] = 0;
            for (int approach = 0; raft.LinkRiding && approach < 100; approach++) Step(1, 0, true);
            FailIf(raft.LinkRiding, "Raft Feather fixture must dismount onto the reachable upper shore.");
            Step(24, feather: true);
            Step(); Step(1, feather: true);
            FailIf(!_player.TopDownAirborne || rom[0xcc5c] == 0,
                "Released and freshly pressed Feather must launch another dry-shore jump after dismount.");
            Step(30, feather: true);
            FailIf(_player.TopDownAirborne || rom[0xcc5c] != 0,
                "Released and freshly pressed Feather must complete another dry-shore jump after dismount.");
        }
        GD.Print("Validated clean-US raft airborne approaches from near/far dry shore, rising/high rejection, final-descent allocation, full XY/Z/gravity/air handoff, A/B rejection while mounted, dialogue, dismount and repeated shore jump with sounds/RNG through split/batched gameplay.");
    }
}
