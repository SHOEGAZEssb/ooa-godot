using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMinecartMountRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x78); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var records = new StaticDungeonObjectDatabase().Minecarts(4);
            MinecartRuntimeState.Reset(_runtimeState, records);
            var cart = new MinecartRoomEntity(new ActiveMinecart(2, 0x78, 0x88, 0x78, -1, false),
                _currentRoom, new DungeonInteractionDatabase(), _runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"), _sound.PlaySound);
            _entities.AddEntity(cart);
            Vector2 start = new(136.25f, 136.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Left);
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Skull 4:78 minecart boarding must begin on the original safe platform.");
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 3, 136, 136)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            const int slot = 0xd240;
            for (int address = 0xcd80; address < 0xcdc0; address++) rom[address] = _runtimeState.ReadWramByte(address);
            rom[slot] = 1; rom[slot + 1] = 0x16;
            rom[slot + 0x0b] = 0x88; rom[slot + 0x0d] = 0x78;
            rom[slot + 0x16] = 0x90; rom[slot + 0x17] = 0xcd;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousDirections = 0;
            int Field(string name) => (int)typeof(Player).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count = 1, int angle = 0xff)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = angle == 0x18 ? 0x20 : 0;
                int edge = held & ~previousDirections; previousDirections = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter - 1); edge = 0; update++;
                    string context = $"Minecart 4:78 update={update} batch={batched}";
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    int z = unchecked((short)rom.Word(0xd00e));
                    FailIf(_player.PrecisePosition != expected || Field("_topDownAirZFixed") != z ||
                        _player.MinecartRideActive != (rom[0xcc2c] == 0xd1) ||
                        _player.HealthQuarters != rom[0xc6aa] ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        context + $": fixed Link XY/Z/ride/facing differs: runtime={_player.PrecisePosition}, native={expected}, Z={Field("_topDownAirZFixed")}/{z}, inAir=${rom[0xcc5c]:x2}, native cart=${rom[0xd100]:x2}/${rom[0xd104]:x2}, interaction=${rom[slot + 4]:x2}.");
                    if (cart.Mounting && rom[slot] != 0)
                        FailIf(rom[slot + 4] != 2 || cart.PushCounter != rom[slot + 6],
                            context + $": push countdown/boarding state differs: runtime={cart.PushCounter}, native={rom[slot + 6]}, state=${rom[slot + 4]:x2}, Link={_player.PrecisePosition}.");
                    if (rom[0xd100] != 0 && rom[0xd104] == 1)
                        FailIf(cart.Position != new Vector2(rom[0xd10d], rom[0xd10b]) ||
                            cart.Direction != rom[0xd108] || cart.Angle != rom[0xd109],
                            context + ": native track position/direction differs.");
                    for (int address = 0xcd80; address < 0xcdc0; address++)
                        FailIf(_runtimeState.ReadWramByte(address) != rom[address],
                            context + $": static-cart byte ${address:x4} differs: runtime=${_runtimeState.ReadWramByte(address):x2}, native=${rom[address]:x2}.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": jump/cart sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int approach = 0; !cart.Mounting && approach < 40; approach++) Step(1, 0x18);
                FailIf(!cart.Mounting || rom[slot + 4] != 2, "Four-update reachable push must begin native minecart boarding.");
                for (int wait = 0; !cart.Riding && wait < 64; wait++) Step();
                FailIf(!cart.Riding || rom[0xcc2c] != 0xd1, "Minecart boarding must transfer into the native companion slot.");
                Step(4);
                _dialogue.ShowMessage("Cart pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6); _dialogue.Close(); rom[0xcba0] = 0;
                for (int travel = 0; !cart.Dismounting && travel < 256; travel++) Step();
                FailIf(!cart.Dismounting || rom[0xcc2c] != 0xd0,
                    "Bounded Skull rail route must stop at its native platform and dismount.");
                Step(48);
                FailIf(cart.Dismounting || _player.MinecartJumpActive || _player.MinecartRideActive ||
                    _currentRoom.IsSolid(_player.Position),
                    "Completed cart dismount must land on reachable floor before another approach.");
            }
        }
        GD.Print("Validated clean-US collision-reachable Skull 4:78 minecart push, jump, full fixed XY/Z, companion handoff, bounded native rail route, pause, static-cart bytes, dismount/landing, sounds/RNG and repeated split/batched gameplay.");
    }
}
