using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMinecartDoorsRom() => ValidateMinecartDoorControlRom(false);
    private void ValidateMinecartDoorControllersRom() => ValidateMinecartDoorControlRom(true);
    private void ValidateMinecartShutterRespawnRom() => ValidateMinecartDoorControlRom(true, true);

    private void ValidateMinecartDoorControlRom(bool persistent, bool cramped = false)
    {
        Vector2I[] directions = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        int respawnCases = 0;
        int hostCase1 = 0;
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int door in Enumerable.Range(0x7c, 4))
        foreach (bool delayed in persistent && !cramped ? new[] { false, true } : new[] { false })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            if (persistent && direction != door - 0x7c) continue;
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, persistent ? 0x78 : 0x00); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 center = persistent ? new(120, 88) : new(88, 72);
            Vector2 doorway = center + (Vector2)directions[direction] * 16;
            Vector2 rideStart = delayed ? center - (Vector2)directions[direction] * 8 : center;
            for (int y = 0; y < _currentRoom.HeightInTiles; y++)
            for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            _currentRoom.SetPositionTileAndCollision(center, (byte)(direction % 2 == 0 ? 0x5e : 0x5d), 0, 0);
            _currentRoom.SetPositionTileAndCollision(doorway, (byte)door, 0x0f, 0);
            if (persistent && !cramped)
                for (int distance = 2; distance <= 3; distance++)
                    _currentRoom.SetPositionTileAndCollision(center + (Vector2)directions[direction] * (16 * distance),
                        (byte)(direction % 2 == 0 ? 0x5e : 0x5d), 0, 0);
            MinecartRuntimeState.Reset(_runtimeState, []);
            MinecartRuntimeState.BeginRide(_runtimeState, 0, _currentRoom.Id, rideStart, direction);
            var cart = new MinecartRoomEntity(new ActiveMinecart(-1, _currentRoom.Id, (int)rideStart.Y, (int)rideStart.X, direction, true),
                _currentRoom, new DungeonInteractionDatabase(), _runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"), _sound.PlaySound);
            _entities.AddEntity(cart);
            _player.WarpTo(rideStart + new Vector2(0.25f, 0.5f)); _player.Face(directions[direction]);
            _player.FinishMinecartMount(rideStart, direction, 0);
            var initialRandom = _random.CaptureState();
            int initialHealth = _player.HealthQuarters;
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, direction, (int)rideStart.X, (int)rideStart.Y)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom[0xd100] = 3; rom[0xd101] = 0x0a;
            rom[0xd108] = (byte)direction; rom[0xd109] = (byte)(direction * 8);
            rom[0xd10b] = (byte)rideStart.Y; rom[0xd10d] = (byte)rideStart.X;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            MinecartShutterRoomEntity? controller = null;
            if (persistent)
            {
                controller = new MinecartShutterRoomEntity(_currentRoom.GetPackedPosition(doorway), door, false,
                    _currentRoom, new DungeonMechanicDatabase(), point => point, () => _entities.FrameCounter, _sound.PlaySound, _entities.UpdateBossShutterSignal);
                _entities.AddEntity(controller);
                rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = (byte)(door - 0x70);
                rom[0xd24b] = (byte)_currentRoom.GetPackedPosition(doorway);
                _player.SetLocalRespawnPosition(doorway, directions[direction]);
                rom[0xcc21] = (byte)doorway.Y; rom[0xcc22] = (byte)doorway.X;
                rom[0xcc23] = (byte)direction;
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int packed = _currentRoom.GetPackedPosition(doorway), update = 0;
            bool sawRespawn = false, sawHidden = false;
            void Step(int count)
            {
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched, () =>
                {
                    rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter - 1); update++;
                    sawRespawn |= rom[0xd004] == 2;
                    sawHidden |= (rom[0xd01a] & 0x80) == 0;
                    string context = $"Minecart door=${door:x2}, direction={direction}, persistent={persistent}, delayed={delayed}, cramped={cramped}, update={update}, batch={batched}";
                    Vector2 nativeCart = new(rom.Word(0xd10c) / 256.0f, rom.Word(0xd10a) / 256.0f);
                    Vector2 nativeLink = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(cart.Position != nativeCart || cart.Direction != rom[0xd108] || cart.Angle != rom[0xd109] ||
                        _player.PrecisePosition != nativeLink || !_player.MinecartRideActive,
                        context + $": cart/rider motion differs: cart={cart.Position}/{nativeCart}, direction={cart.Direction}/{rom[0xd108]}, Link={_player.PrecisePosition}/{nativeLink}.");
                    FailIf(_currentRoom.GetMetatile(doorway) != rom[0xcf00 + packed] ||
                        _currentRoom.GetTerrainInfo(doorway).Collision != rom[0xce00 + packed],
                        context + $": door layout/collision differ: runtime=${_currentRoom.GetMetatile(doorway):x2}/${_currentRoom.GetTerrainInfo(doorway).Collision:x2}, native=${rom[0xcf00 + packed]:x2}/${rom[0xce00 + packed]:x2}, controller=${rom[0xd244]:x2}/${rom[0xd245]:x2}, counter={rom[0xd246]}.");
                    if (persistent) FailIf(_player.LocalRespawnPosition != new Vector2(rom[0xcc22], rom[0xcc21]) ||
                        CarriedObjectMotion.DirectionIndex(_player.LocalRespawnFacingVector) != rom[0xcc23],
                        context + $": local respawn differs: runtime={_player.LocalRespawnPosition}, native={rom[0xcc22]},{rom[0xcc21]}.");
                    if (cramped) FailIf(_player.HealthQuarters != rom[0xc6aa] ||
                        _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                        _player.Visible != ((rom[0xd01a] & 0x80) != 0) ||
                        _player.NativeNormalStateForInteraction != (rom[0xd004] == 1),
                        context + $": riding respawn state/health/visibility differs: health={_player.HealthQuarters}/{rom[0xc6aa]}, visible={_player.Visible}/${rom[0xd01a]:x2}, native state=${rom[0xd004]:x2}.");
                    FailIf(_entities.BossEntrySignal != rom[0xcc93],
                        context + $": shared shutter count differs: runtime=${_entities.BossEntrySignal:x2}, native=${rom[0xcc93]:x2}.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + $": ordered cart/door sounds differ: runtime=[{string.Join(',', sounds.Requests)}], native=[{string.Join(',', rom.Sounds)}], controller={controller?.State}, native=${rom[0xd244]:x2}/${rom[0xd245]:x2}, sub=${rom[0xd242]:x2}, XY={rom[0xd24d]},{rom[0xd24b]}, radius={rom[0xd266]},{rom[0xd267]}, angle=${rom[0xd249]:x2}, index=${rom[0xcc2c]:x2}.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            Step(3);
            _dialogue.ShowMessage("Door pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(6); _dialogue.Close(); rom[0xcba0] = 0;
            Step(persistent ? 125 : 45);
            if (sawRespawn)
            {
                respawnCases++;
                FailIf(!sawHidden || rom[0xd004] != 1 || !_player.NativeNormalStateForInteraction ||
                    _player.HealthQuarters != initialHealth - 2 || _player.InvincibilityFrames != 0 || !_player.Visible,
                    $"Riding shutter respawn must hide/reveal, subtract two quarters and complete protection/recovery without stopping the cart: door=${door:x2}, hidden={sawHidden}, nativeState=${rom[0xd004]:x2}, health={_player.HealthQuarters}, protection={_player.InvincibilityFrames}, visible={_player.Visible}.");
            }
            if (persistent) FailIf(controller is not { Finished: true } || rom[0xd240] != 0,
                "The layout minecart controller must delete after closing and leave subsequent opening to newly allocated one-shot controllers.");
            if (!persistent) FailIf(_entities.Entities<MinecartShutterRoomEntity>().Any() || rom[0xd240] != 0 ||
                _currentRoom.GetMetatile(doorway) != ((door & 1) == 0 ? 0x5e : 0x5d) ||
                _currentRoom.IsSolid(doorway),
                $"The one-shot native opener must finish, delete itself and release the doorway collision: door=${door:x2}, dir={direction}, entities={_entities.Entities<MinecartShutterRoomEntity>().Count}, native=${rom[0xd240]:x2}, collision=${_currentRoom.GetTerrainInfo(doorway).Collision:x2}, tile=${_currentRoom.GetMetatile(doorway):x2}.");
        }
        FailIf(cramped && respawnCases == 0, "The short-turnaround fixture must actually execute riding Link's state02 handoff.");
        GD.Print(cramped ? "Validated clean-US minecart shutter closure on riding Link: native force-state consumption, instant respawn/reveal/recovery while the cart continues, full rider/cart XY, health/visibility, respawn relocation, shutter count, repeated passes, sounds/RNG and split/batched gameplay."
            : persistent
            ? "Validated clean-US persistent minecart doors in four directions: repeated cart passes, simultaneous one-shot opener, initialization, opening/closing, separate layout/collision publication, dialogue pause, cart/rider motion and sounds/RNG in split/batched gameplay."
            : "Validated clean-US minecart-created one-shot door openers: all four shutter tiles and incoming directions, allocation/init/interleave/six-update completion, independent layout/collision publication, cart/rider movement, dialogue pause, deletion, sounds/RNG and split/batched gameplay.");
    }
}
