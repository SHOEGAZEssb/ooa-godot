using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMinecartTracksRom()
    {
        // Independent literals from specialObjects/minecart.s @trackData.
        // Direction, current tile, next packed-position offset, exit direction,
        // and the three accepted neighbors, in original source order.
        (int Direction, int Tile, int Offset, int Exit, int[] Allowed)[] rows =
        [
            (0, 0x5e, -0x10, 0, [0x5e, 0x59, 0x5c]),
            (0, 0x59, 1, 1, [0x5d, 0x5a, 0x5c]),
            (0, 0x5c, -1, 3, [0x5d, 0x5b, 0x59]),
            (1, 0x5d, 1, 1, [0x5d, 0x5a, 0x5c]),
            (1, 0x5a, -0x10, 0, [0x5e, 0x59, 0x5c]),
            (1, 0x5c, 0x10, 2, [0x5e, 0x5b, 0x5a]),
            (2, 0x5e, 0x10, 2, [0x5e, 0x5a, 0x5b]),
            (2, 0x5a, -1, 3, [0x5d, 0x5b, 0x59]),
            (2, 0x5b, 1, 1, [0x5d, 0x5a, 0x5c]),
            (3, 0x5d, -1, 3, [0x5d, 0x5b, 0x59]),
            (3, 0x5b, -0x10, 0, [0x5e, 0x5c, 0x59]),
            (3, 0x59, 0x10, 2, [0x5e, 0x5a, 0x5b])
        ];
        int cases = 0;
        Vector2I[] directions = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        int hostCase1 = 0;
        foreach (var row in rows)
        foreach (int next in new[] { 0x59, 0x5a, 0x5b, 0x5c, 0x5d, 0x5e, 0xa0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x00); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            const int center = 0x45;
            static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
            _currentRoom.SetPositionTileAndCollision(Point(center), (byte)row.Tile, 0, 0);
            _currentRoom.SetPositionTileAndCollision(Point(center + row.Offset), (byte)next, 0, 0);
            MinecartRuntimeState.Reset(_runtimeState, []);
            MinecartRuntimeState.BeginRide(_runtimeState, 0, 0x00, Point(center), row.Direction);
            var cart = new MinecartRoomEntity(new ActiveMinecart(-1, 0x00, 72, 88, row.Direction, true),
                _currentRoom, new DungeonInteractionDatabase(), _runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"), _sound.PlaySound);
            _entities.AddEntity(cart);
            _player.WarpTo(new(88.25f, 72.5f));
            _player.Face(directions[row.Direction]);
            _player.FinishMinecartMount(Point(center), row.Direction, 0);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, row.Direction, 88, 72)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom[0xd100] = 3; rom[0xd101] = 0x0a;
            rom[0xd108] = (byte)row.Direction; rom[0xd109] = (byte)(row.Direction * 8);
            rom[0xd10b] = 72; rom[0xd10d] = 88;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Step(int count)
            {
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched, () =>
                {
                    rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter - 1); update++;
                    string context = $"Minecart track dir={row.Direction}, tile=${row.Tile:x2}, next=${next:x2}, update={update}, batch={batched}";
                    Vector2 nativeCart = new(rom.Word(0xd10c) / 256.0f, rom.Word(0xd10a) / 256.0f);
                    Vector2 nativeLink = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(cart.Position != nativeCart || cart.Direction != rom[0xd108] || cart.Angle != rom[0xd109] ||
                        _player.PrecisePosition != nativeLink || !_player.MinecartRideActive,
                        context + $": cart/rider motion differs: cart={cart.Position}/{nativeCart}, dir={cart.Direction}/{rom[0xd108]}, Link={_player.PrecisePosition}/{nativeLink}.");
                    if (update == 1)
                    {
                        int expected = row.Allowed.Contains(next) ? row.Exit : row.Direction ^ 2;
                        FailIf(cart.Direction != expected || nativeCart != Point(center) + (Vector2)directions[expected],
                            context + ": source row must turn or reverse before its first one-pixel movement.");
                    }
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": cart sound cadence differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            Step(7);
            _dialogue.ShowMessage("Track pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(6); _dialogue.Close(); rom[0xcba0] = 0;
            Step(33);
            cases++;
        }
        GD.Print($"Validated {cases} clean-US minecart track cases: all twelve source rows, six track neighbors and invalid-floor reversals, turn-before-move timing, rider fractions, pause/resume, sounds/RNG and split/batched gameplay.");
    }
}
