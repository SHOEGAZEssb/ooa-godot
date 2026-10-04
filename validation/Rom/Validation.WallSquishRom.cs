using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateWallSquishRom()
    {
        int hostCase1 = 0;
        foreach (int orientation in new[] { 0, 8 })
        foreach (int parity in new[] { 0, 1 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9b); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var detector = new WallSquishRoomEntity(_currentRoom, () => _rooms.BlockPushAngle,
                () => 0, "objects/ages/mainData.s:group4Map9bObjectData;interactiondc_subid17");
            _entities.AddEntity(detector);
            Vector2 anchor = new(136.25f, 136.5f);
            _player.Face(Vector2I.Up); _player.WarpTo(anchor);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 0, 136, 136);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom[0xcc21] = 136; rom[0xcc22] = 136; rom[0xcc23] = 0;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            int slot = 0xd040 + _entities.InteractionSlot(detector) * 0x100;
            rom[slot] = 1; rom[slot + 1] = 0xdc; rom[slot + 2] = 0x17;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousDirections = 0;
            void Step(int count = 1, int angle = 0xff)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = angle == 0 ? 0x40 : 0;
                int edge = held & ~previousDirections; previousDirections = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter - 1); edge = 0; update++;
                    string context = $"Crown squish 4:9b orientation=${orientation:x2} parity={parity} update={update} batch={batched}";
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(_player.PrecisePosition != expected || _player.HealthQuarters != rom[0xc6aa] ||
                        _player.Visible != ((rom[0xd01a] & 0x80) != 0) ||
                        _player.PatchCollisionsEnabled != ((rom[0xd024] & 0x80) != 0) ||
                        _player.NativeNormalStateForInteraction != (rom[0xd004] == 1) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        detector.State != rom[slot + 4],
                        context + $": fixed XY/health/visibility/collision/state differs: runtime={_player.PrecisePosition}, native={expected}, state=${rom[0xd004]:x2}/${rom[0xd005]:x2}.");
                    if (_player.SquishAnimation is { State: > 0, Finished: false } animation)
                        FailIf(rom[0xd004] != 0x11 || animation.State != rom[0xd005] ||
                            animation.Current.Graphic != rom[0xd031] || animation.AnimationCounter != rom[0xd020] ||
                            animation.State == 2 && animation.FlickerCounter != rom[0xd006],
                            context + ": squish animation/visible-update counters differ.");
                    int protection = (int)(float)typeof(Player).GetField("_enemyInvincibilityFrames",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
                    FailIf((protection & 0xff) != rom[0xd02b], context + ": recovery protection differs.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": damage/recovery sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            Step(1 + parity);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int approach = 0; _player.Position.Y > 104 && approach < 64; approach++) Step(1, 0);
                Step();
                FailIf(_player.Position != new Vector2(136, 104) || _currentRoom.IsSolid(_player.Position),
                    "Crown squish must approach through the original open collision geometry.");
                _player.SetLocalRespawnPosition(anchor, Vector2I.Up);
                Vector2 point = _player.Position;
                byte original = _currentRoom.GetMetatile(point);
                void Close(byte collision)
                {
                    // Bounded moving-wall publication, not a complete block puzzle.
                    _currentRoom.SetPositionTileAndCollision(point, 0x2e, collision, 0);
                    rom.CopyRoom(_currentRoom);
                }
                foreach (byte open in new byte[] { 0x10, 0x03, 0x0c })
                {
                    Close(open); Step();
                    FailIf(_player.SideScrollSquished || rom[0xcc4f] != 0,
                        "Hole-permitted or one-sided wall probes must reject squashing.");
                }
                _rooms.WriteBlockPushAngle(orientation); rom[0xcca6] = (byte)orientation;
                _dialogue.ShowMessage("Wall pause.", _player.Position.Y); rom[0xcba0] = 1;
                Close(0x0f); Step(6); _dialogue.Close(); rom[0xcba0] = 0;
                Step();
                FailIf(!_player.SideScrollSquished || rom[0xcc4f] != 0x11 || _player.SquishAnimation is not null,
                    "Wall detector must publish state $11 after Link without initializing it.");
                Step(2); Step(44);
                for (int wait = 0; rom[0xd004] != 2 && wait < 48; wait++) Step();
                FailIf(rom[0xd004] != 2 || _player.SquishAnimation is not { Finished: true },
                    "Squish must finish its twenty-visible-update countdown before respawn dispatch.");
                _currentRoom.SetPositionTileAndCollision(point, original, 0, 0); rom.CopyRoom(_currentRoom);
                Step(32);
                FailIf(_player.SideScrollSquished || rom[0xd004] != 1 ||
                    _player.Position != new Vector2(136, 136) || _player.HealthQuarters != 12 - (repeat + 1) * 2,
                    "Squish recovery must restore the local anchor, apply half-heart damage and permit another approach.");
            }
        }
        GD.Print("Validated clean-US Crown wall detector, collision-reachable repeated approaches, hole/partial/text gates, post-Link request, both squish orientations/parities, animation/flicker, full XY, health/protection/collision, respawn and sounds/RNG in split/batched gameplay.");
    }
}
