using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMinecartShieldRom()
    {
        int cases = 0;
        Vector2I[] directions = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int level in new[] { 1, 2, 3 })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x00); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Shield, level);
            _inventory.EquipA(primary ? TreasureId.Shield : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Shield);
            Vector2 center = new(88, 72);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            byte track = (byte)(direction % 2 == 0 ? 0x5e : 0x5d);
            _currentRoom.SetPositionTileAndCollision(center, track, 0, 0);
            _currentRoom.SetPositionTileAndCollision(center + (Vector2)directions[direction] * 16, track, 0, 0);
            _currentRoom.SetPositionTileAndCollision(center - (Vector2)directions[direction] * 16, 0x5f, 0, 0);
            MinecartRuntimeState.Reset(_runtimeState, []);
            MinecartRuntimeState.BeginRide(_runtimeState, 0, 0x00, center, direction);
            var cart = new MinecartRoomEntity(new ActiveMinecart(-1, 0x00, 72, 88, direction, true),
                _currentRoom, new DungeonInteractionDatabase(), _runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"), _sound.PlaySound);
            _entities.AddEntity(cart);
            _player.WarpTo(center + new Vector2(0.25f, 0.5f)); _player.Face(directions[direction]);
            _player.FinishMinecartMount(center, direction, 0);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, direction, 88, 72)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom[0xd100] = 3; rom[0xd101] = 0x0a;
            rom[0xd108] = (byte)direction; rom[0xd109] = (byte)(direction * 8);
            rom[0xd10b] = 72; rom[0xd10d] = 88;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousHeld = 0;
            int shieldButton = primary ? 1 : 2;
            T Private<T>(string field) => (T)typeof(Player).GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count, bool held)
            {
                int buttons = held ? shieldButton : 0;
                int edge = buttons & ~previousHeld; previousHeld = buttons;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(buttons), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, buttons, 0xff, _entities.FrameCounter - 1); edge = 0; update++;
                    string context = $"Minecart Shield L{level}, dir={direction}, A={primary}, update={update}, batch={batched}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        _player.MinecartRideActive != (rom[0xcc2c] == 0xd1), context + ": rider XY/ownership differs.");
                    FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                        Private<int>("_shieldParentButton") != (rom[0xd500] == 0 ? 0 : rom[0xd503]) ||
                        Private<bool>("_shieldParentInitialized") != (rom[0xd504] != 0),
                        context + $": Shield parent/raised state differs: runtime={_player.IsUsingShield}/{Private<int>("_shieldParentButton")}, native=${rom[0xcc6f]:x2}/${rom[0xd500]:x2}/${rom[0xd504]:x2}.");
                    for (int address = 0xcd80; address < 0xcdc0; address++)
                        FailIf(_runtimeState.ReadWramByte(address) != rom[address], context + $": static byte ${address:x4} differs.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds), context + ": sounds differ.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": RNG differs.");
                });
            }
            Step(8, true);
            FailIf(_player.IsUsingShield || rom[0xd500] != 0, "Minecart ride must reject Shield without initialization or sound.");
            _dialogue.ShowMessage("Shield cart pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(6, true); _dialogue.Close(); rom[0xcba0] = 0;
            Step(40, true);
            FailIf(!cart.Dismounting, "Shield handoff fixture must reach the platform and its airborne dismount.");
            Step(48, true);
            FailIf(!_player.IsUsingShield || rom[0xcc6f] != level || _player.MinecartJumpActive,
                "Held Shield must initialize after dismount landing restores ordinary allocation.");
            Step(4, false); Step(3, true); Step(1, false);
            cases++;
        }
        GD.Print($"Validated {cases} clean-US minecart Shield cases: A/B, three levels, four directions, riding rejection, dialogue, airborne allocation gate, held landing initialization, release/repeat, rider/static bytes and sounds/RNG through split/batched gameplay.");
    }
}
