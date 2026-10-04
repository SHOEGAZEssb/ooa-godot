using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private DungeonInteractionDatabase? _sideViewInteractionData;
    private DungeonInteractionDatabase SideViewInteractionData => _sideViewInteractionData ??= new();
    private SideViewRom PrepareSideViewRom(int group, int room, Vector2 start, bool flippers = false,
        bool feather = false, bool platforms = false)
    {
        ReinitializeGameplayForValidation();
        if (flippers) _inventory.GiveTreasure(TreasureId.Flippers, 0);
        if (feather)
        {
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(TreasureId.Feather);
        }
        LoadValidationRoom(group, room);
        _entities.Clear();
        _player.WarpTo(start);
        FailIf(_collision.Collides(start), $"Sideview ${group:x1}:${room:x2} start {start} must be reachable open geometry.");
        var rom = new SideViewRom(_saveData, _random.CaptureState(), _currentRoom, start, _entities.FrameCounter);
        if (platforms)
        {
            var visual = new DungeonInteractionVisualDatabase().Visual("moving-side-platform");
            foreach (var placement in new MovingSideScrollPlatformDatabase().GetRoomRecords(group - 2, room))
            {
                var platform = new MovingSideScrollPlatformRoomEntity(placement,
                    SideViewInteractionData.SidePlatform(placement.SubId), visual);
                _entities.AddEntity(platform);
                rom.AddPlatform(0xd0 + _entities.InteractionSlot(platform), placement.SubId, placement.Position);
            }
        }
        return rom;
    }

    private void CompareSideViewRom(SideViewRom rom, string context)
    {
        FailIf(_player.PrecisePosition != rom.Position ||
            _player.SideScrollAirborne != ((rom[0xcc5c] & 15) != 0) ||
            _player.SideScrollSwimmingState != (rom[0xcc5d] & 15) ||
            _player.SideScrollClimbing != (rom[0xcc68] != 0),
            $"{context}: runtime XY={_player.PrecisePosition}, air={_player.SideScrollAirborne}, swim={_player.SideScrollSwimmingState}, climb={_player.SideScrollClimbing}, speed/angle=${_player.SideScrollSpeedRaw:x2}/${_player.SideScrollAngle:x2}; ROM XY={rom.Position}, air=${rom[0xcc5c]:x2}, swim=${rom[0xcc5d]:x2}, climb=${rom[0xcc68]:x2}, speed/angle=${rom[0xd010]:x2}/${rom[0xd009]:x2}, counter/interval=${rom[0xd012]:x2}/${rom[0xd013]:x2}, state/sub=${rom[0xd004]:x2}/${rom[0xd005]:x2}, force=${rom[0xcc4f]:x2}.");
        if (_player.SideScrollAirborne)
            FailIf(_player.SideScrollSpeedZ != rom.SpeedZ,
                $"{context}: speedZ runtime=${_player.SideScrollSpeedZ & 0xffff:x4}, ROM=${rom.SpeedZ & 0xffff:x4}.");
        if (_player.SideScrollSwimmingState == 2 && rom[0xd004] == 1)
        {
            int Field(string name) => (int)typeof(Player).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            FailIf(_player.SideScrollAngle != rom[0xd009] || _player.SideScrollSpeedRaw != rom[0xd010] ||
                Field("_sideScrollTargetSpeedRaw") != rom[0xd011] ||
                Field("_sideScrollVelocityCounter") != rom[0xd012] || Field("_sideScrollVelocityInterval") != rom[0xd013] ||
                _player.SideScrollSwimBurstState != rom[0xd035] ||
                _player.SideScrollSwimBurstState != 0 && _player.SideScrollSwimBurstCounter != rom[0xd006] ||
                _player.SideScrollSwimAnimationCounter != rom[0xd020],
                $"{context}: swim velocity, burst or animation differs from native speed/target/counter/interval=${rom[0xd010]:x2}/${rom[0xd011]:x2}/${rom[0xd012]:x2}/${rom[0xd013]:x2}, burst/counter=${rom[0xd035]:x2}/${rom[0xd006]:x2}, animCounter=${rom[0xd020]:x2}.");
        }
        var random = _random.CaptureState();
        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
            random.Calls - rom.InitialRandomCalls != rom.RandomCalls,
            $"{context}: shared RNG runtime=${random.Rng1:x2}/${random.Rng2:x2}/{random.Calls - rom.InitialRandomCalls}, ROM=${rom[0xff94]:x2}/${rom[0xff95]:x2}/{rom.RandomCalls}.");
        FailIf(_entities.PlayerRidingObject != (rom[0xcc96] != 0),
            $"{context}: shared rider runtime={_entities.PlayerRidingObject}, ROM=${rom[0xcc96]:x2}.");
        FailIf(_player.HealthQuarters != rom[0xc6aa] ||
            _player.Visible != ((rom[0xd01a] & 0x80) != 0) ||
            _player.NativeNormalStateForInteraction != (rom[0xd004] == 1),
            $"{context}: health/visible/normal runtime={_player.HealthQuarters}/{_player.Visible}/{_player.NativeNormalStateForInteraction}, ROM={rom[0xc6aa]}/{rom[0xd01a]:x2}/{rom[0xd004]:x2}, force=${rom[0xcc4f]:x2}.");
        foreach (var platform in _entities.Entities<MovingSideScrollPlatformRoomEntity>())
        {
            int page = 0xd0 + _entities.InteractionSlot(platform), slot = page << 8;
            Vector2 expected = new(rom.Word(slot + 0x4c) / 256f, rom.Word(slot + 0x4a) / 256f);
            FailIf(platform.PrecisePosition != expected || platform.LinkRiding != (rom[slot + 0x74] != 0),
                $"{context}: INTERAC$a1 slot=${page:x2} runtime XY={platform.PrecisePosition}, rider={platform.LinkRiding}; ROM XY={expected}, var34=${rom[slot + 0x74]:x2}, state=${rom[slot + 0x44]:x2}.");
            if (!platform.UpdatesDuringDialogue)
            {
                int subid = rom[slot + 0x42];
                var record = SideViewInteractionData.SidePlatform(subid);
                var command = record.Commands[platform.CommandIndex];
                int state = command.Direction switch { MovingSideScrollPlatformDirection.Up => 8,
                    MovingSideScrollPlatformDirection.Right => 9, MovingSideScrollPlatformDirection.Down => 10,
                    MovingSideScrollPlatformDirection.Left => 11, _ => 12 };
                int endpoint = state is 8 or 10 ? rom[slot + 0x72] : rom[slot + 0x73];
                FailIf(rom[slot + 0x44] != state || state == 12 && platform.WaitCounter != rom[slot + 0x46] ||
                    state != 12 && command.Endpoint != endpoint ||
                    record.Speed != rom[slot + 0x50] || record.Direction != rom[slot + 0x48] ||
                    record.RadiusY != rom[slot + 0x66] || record.RadiusX != rom[slot + 0x67],
                    $"{context}: INTERAC$a1:${subid:x2} command/radii/speed import differs from native state=${rom[slot + 0x44]:x2}, endpoint=${endpoint:x2}, wait=${rom[slot + 0x46]:x2}.");
            }
        }
    }

    private void StepSideViewRom(SideViewRom rom, int count, bool batched, int angle = 0xff,
        bool jump = false, bool burst = false, bool directionPressed = false, Action? afterUpdate = null)
    {
        Vector2 input = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Delta(0x28, angle).Normalized();
        int keys = angle switch { 0 => 0x40, 4 => 0x50, 8 => 0x10, 12 => 0x90,
            16 => 0x80, 20 => 0xa0, 24 => 0x20, 28 => 0x60, _ => 0 };
        string[] pressed = jump || burst ? ["attack"] : directionPressed ?
            [angle switch { 0 => "move_up", 8 => "move_right", 16 => "move_down", _ => "move_left" }] : [];
        int update = 0;
        StepGameplayUpdates(count, input, held: jump || burst ? ["attack"] : [], pressed: pressed,
            batched: batched, afterUpdate: () =>
            {
                rom.Update(angle, update == 0 ? (jump || burst ? 1 : directionPressed ? keys : 0) : 0,
                    keys | (jump || burst ? 1 : 0));
                CompareSideViewRom(rom, $"Sideview ${_currentRoom.Group:x1}:${_currentRoom.Id:x2} angle=${angle:x2}, update={update++}, tick={rom[0xcc00]}, jump={jump}, batch={batched}");
                afterUpdate?.Invoke();
            });
    }

    private void ValidateSideViewLaddersRom()
    {
        int hostCase3 = 0;
        foreach (float fraction in new[] { 0f, 255 / 256f })
        foreach (bool batched in RomHostSchedules(hostCase3++))
        {
            var rom = PrepareSideViewRom(6, 0x10, new(200 + fraction, 136 + fraction));
            // room0410.bin: right ladder $18, top $19; source top clamp is
            // (yh & $f0)+$09, retaining yl. No collision edits make it reachable.
            FailIf(_currentRoom.GetMetatile(new(200, 104)) != 0x19 ||
                _currentRoom.GetMetatile(new(200, 120)) != 0x18,
                "6:$10 must retain source right ladder $18/$19.");
            var sounds = _sound.AttachPlayRequestAudit();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                StepSideViewRom(rom, 10, batched, 0);
                FailIf(!_player.SideScrollClimbing, "6:$10 must climb before its modal pause.");
                Vector2 middle = rom.Position;
                _dialogue.ShowMessage("Climbing pause.", _player.Position.Y); rom[0xcba0] = 1;
                StepSideViewRom(rom, 5, batched, 16);
                FailIf(rom.Position != middle || _player.SideScrollClimbing,
                    "Paused Link must retain ladder coordinates while clearing the climbing publication.");
                _dialogue.Close(); rom[0xcba0] = 0;
                StepSideViewRom(rom, 60, batched, 0);
                FailIf(rom.Position.Y != 89 + fraction || _player.SideScrollAirborne,
                    "6:$10 ladder-top clamp must stop at Y=$59 with the original fractional byte.");
                Vector2 paused = rom.Position;
                _dialogue.ShowMessage("Ladder pause.", _player.Position.Y); rom[0xcba0] = 1;
                StepSideViewRom(rom, 5, batched, 16);
                FailIf(rom.Position != paused, "Text must freeze ladder input.");
                _dialogue.Close(); rom[0xcba0] = 0;
                StepSideViewRom(rom, 47, batched, 16);
            }
            FailIf(sounds.Requests.Any(id => id is SoundId.SndJump or SoundId.SndLand) || rom.Sounds.Count != 0,
                "Continuous ladder traversal must not launch gravity or emit jump/landing sounds.");
        }
        GD.Print("Validated executed-ROM sideview ladders, top clamp/fractions, text pause and repeated ascent/descent in split/batched gameplay.");
    }

    private void ValidateSideViewWaterRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var rom = PrepareSideViewRom(7, 0x05, new(40, 56), flippers: true);
            FailIf(_currentRoom.GetMetatile(_player.Position) != 0x1b,
                "7:$05 must retain source water $1b in its left shaft.");
            StepSideViewRom(rom, 1, batched);
            FailIf(rom[0xcc5d] != 2 || rom[0xd010] != 0x14,
                "Sideview water entry must select swim state$02/SPEED_80.");
            foreach (int angle in new[] { 8, 24, 0, 16, 0xff })
                StepSideViewRom(rom, 6, batched, angle);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                StepSideViewRom(rom, 1, batched, 0, burst: true);
                FailIf(rom[0xd035] != 1 || rom[0xd006] != 0x0c,
                    "Sideview Flippers burst must decrement $0d to $0c on its first update.");
                StepSideViewRom(rom, 24, batched, 16);
                FailIf(rom[0xd035] != 0, "Sideview burst must complete on update25.");
            }
            _dialogue.ShowMessage("Water pause.", _player.Position.Y); rom[0xcba0] = 1;
            StepSideViewRom(rom, 6, batched, 8, burst: true);
            _dialogue.Close(); rom[0xcba0] = 0;
            StepSideViewRom(rom, 8, batched, 24);

            // 7:$06 has an actual $1a water ladder at x=$18 and open $1c
            // surface at x=$48. Current y+$08 determines whether to hop.
            foreach (bool ladder in new[] { false, true })
            {
                rom = PrepareSideViewRom(7, 0x06, new(ladder ? 24 : 72, 120), flippers: true);
                var sounds = _sound.AttachPlayRequestAudit();
                StepSideViewRom(rom, 1, batched);
                bool wasSwimming = true, sawExit = false;
                StepSideViewRom(rom, 55, batched, 0, afterUpdate: () =>
                {
                    if (wasSwimming && rom[0xcc5d] == 0)
                    {
                        sawExit = true;
                        FailIf(ladder ? rom[0xcc5c] != 0 || rom.SpeedZ != 0 || rom[0xcc9d] != 0x30 :
                            rom[0xcc5c] != 2 || rom.SpeedZ != -0x17c || rom[0xd010] != 0x14,
                            "7:$06 first surface update must suppress the $1a ladder hop or integrate -$01a0+$24 with retained SPEED_80.");
                    }
                    wasSwimming = rom[0xcc5d] != 0;
                });
                FailIf(!sawExit || rom[0xcc5d] != 0 || ladder && rom[0xcc5c] != 0,
                    "7:$06 must leave source water; the $1a ladder must suppress the surface hop.");
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), "Sideview surface splash/landing sound order differs from ROM.");
            }
        }
        GD.Print("Validated executed-ROM sideview water entry, steering/braking, burst/repeat, pause and open/ladder surfaces through split/batched gameplay.");
    }

    private void ValidateSideViewPitsRom()
    {
        int hostCase2 = 0;
        foreach (float fraction in new[] { 0f, 255 / 256f })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            var rom = PrepareSideViewRom(6, 0x68, new(56 + fraction, 25 + fraction));
            FailIf(_currentRoom.GetMetatile(new(72, 168)) != 0xf4,
                "6:$68 must retain native bottom pit $f4.");
            var sounds = _sound.AttachPlayRequestAudit();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                StepSideViewRom(rom, 18, batched, 8);
                StepSideViewRom(rom, 100, batched);
                FailIf(rom[0xd004] != 1 || _player.HealthQuarters != 12 - (attempt + 1) * 2,
                    "6:$68 pit must complete two-update invisibility, damage$02 and16-update recovery before a repeated fall.");
            }
            FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), "Sideview pit damage sound order differs from ROM.");
        }
        GD.Print("Validated executed-ROM sideview ledge fall/pit boundary, fractional respawn, damage/visibility/control recovery and repeated falls in split/batched gameplay.");
    }

    private void ValidateSideViewPlatformsRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var rom = PrepareSideViewRom(6, 0x68, new(24, 8), feather: true, platforms: true);
            var platforms = _entities.Entities<MovingSideScrollPlatformRoomEntity>().ToArray();
            FailIf(platforms.Length != 2 || platforms[0].Position != new Vector2(88, 72) ||
                platforms[1].Position != new Vector2(152, 72), "6:$68 must retain ordered INTERAC$a1:$01 source placements.");
            var sounds = _sound.AttachPlayRequestAudit();
            StepSideViewRom(rom, 32, batched, 16);
            StepSideViewRom(rom, 1, batched, 8);
            StepSideViewRom(rom, 1, batched, 8, jump: true);
            StepSideViewRom(rom, 25, batched, 8);
            StepSideViewRom(rom, 20, batched);
            FailIf(_player.Position != new Vector2(50, 25), "6:$68 must reach the left pillar by actual ladder/Feather input.");
            StepSideViewRom(rom, 10, batched, 8);
            StepSideViewRom(rom, 1, batched, 8, jump: true);
            StepSideViewRom(rom, 25, batched, 8);
            StepSideViewRom(rom, 8, batched, 24);
            StepSideViewRom(rom, 50, batched);
            FailIf(!platforms[0].LinkRiding, "6:$68 reachable Feather approach must board INTERAC$a1:$01.");
            StepSideViewRom(rom, 280, batched);
            FailIf(!platforms[0].LinkRiding || _player.SideScrollAirborne,
                "Platform rider must survive upward/downward reversals and both endpoint dispatches.");
            _dialogue.ShowMessage("Platform pause.", _player.Position.Y); rom[0xcba0] = 1;
            StepSideViewRom(rom, 8, batched, 8, jump: true);
            _dialogue.Close(); rom[0xcba0] = 0;
            StepSideViewRom(rom, 1, batched, 8);
            StepSideViewRom(rom, 1, batched, 8, jump: true);
            StepSideViewRom(rom, 24, batched, 8);
            FailIf(platforms[0].LinkRiding, "Fresh Feather edge must release platform ownership.");
            var traversalSounds = sounds.Requests.Where(id => id is SoundId.SndJump or SoundId.SndLand).ToArray();
            FailIf(!traversalSounds.SequenceEqual(rom.Sounds),
                $"Platform traversal sounds runtime=[{string.Join(',', traversalSounds.Select(id => $"${id:x2}"))}], ROM=[{string.Join(',', rom.Sounds.Select(id => $"${id:x2}"))}].");
            LoadValidationRoom(6, 0x10);
            FailIf(_entities.Entities<MovingSideScrollPlatformRoomEntity>().Count != 0,
                "Room replacement must retire platform riders.");
        }
        GD.Print("Validated executed-ROM ordered moving platforms, reachable boarding, rider carry/endpoint reversals, pause, dismount and room cancellation through split/batched gameplay.");
    }

    private void ValidateSideViewPlatformScriptsRom()
    {
        // Every placed $a1 subid $00-$0e, including 30-update waits and
        // four-direction scripts, executes its native ROM program. Link waits
        // on each room's actual ladder/floor; source enemies are isolated.
        int hostCase1 = 0;
        foreach (var test in new (int Group, int Room, Vector2 Start)[] { (6, 0x29, new Vector2(24,56)),
            (6, 0x2a, new Vector2(216,24)), (6, 0x68, new Vector2(24,24)),
            (6, 0x95, new Vector2(40,56)), (6, 0x96, new Vector2(24,89)),
            (6, 0x97, new Vector2(24,56)), (7, 0x02, new Vector2(184,24)),
            (7, 0x06, new Vector2(24,56)), (7, 0x11, new Vector2(200,24)),
            (7, 0xe7, new Vector2(56,105)) })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            // Independent rows traced from objects/ages/mainData.s, including
            // $7:$e7's preceding $b8 object and hence source order1.
            string expectedRows = (test.Group, test.Room) switch
            {
                (6, 0x29) => "0:06:58:58,1:07:68:98",
                (6, 0x2a) => "0:08:98:58,1:09:48:d8",
                (6, 0x68) => "0:01:48:58,1:01:48:98",
                (6, 0x95) => "0:0a:68:88",
                (6, 0x96) => "0:02:68:30,1:03:88:90",
                (6, 0x97) => "0:04:88:58,1:05:58:78,2:05:38:98",
                (7, 0x02) => "0:0c:38:40,1:0d:68:40",
                (7, 0x06) => "0:0b:68:68",
                (7, 0x11) => "0:0e:58:78",
                (7, 0xe7) => "1:00:78:58",
                _ => throw new InvalidOperationException("Missing independent sideview platform placement expectations.")
            };
            var rows = new MovingSideScrollPlatformDatabase().GetRoomRecords(test.Group - 2, test.Room);
            string actualRows = string.Join(',', rows.Select(row => $"{row.Order}:{row.SubId:x2}:{row.Y:x2}:{row.X:x2}"));
            FailIf(actualRows != expectedRows,
                $"Sideview ${test.Group:x1}:${test.Room:x2} source $a1 placements expected=[{expectedRows}], imported=[{actualRows}].");
            var rom = PrepareSideViewRom(test.Group, test.Room, test.Start, platforms: true);
            var platforms = _entities.Entities<MovingSideScrollPlatformRoomEntity>().ToArray();
            _dialogue.ShowMessage("Platform initialization.", _player.Position.Y); rom[0xcba0] = 1;
            StepSideViewRom(rom, 4, batched);
            FailIf(platforms.Any(platform => platform.UpdatesDuringDialogue || platform.CommandIndex != 0),
                "Native platform state0 must initialize under text without advancing its first segment.");
            _dialogue.Close(); rom[0xcba0] = 0;
            int[] previous = new int[platforms.Length], loops = new int[platforms.Length];
            for (int block = 0; block < 75; block++)
            {
                StepSideViewRom(rom, 8, batched);
                for (int index = 0; index < platforms.Length; index++)
                {
                    if (platforms[index].CommandIndex < previous[index]) loops[index]++;
                    previous[index] = platforms[index].CommandIndex;
                }
            }
            FailIf(loops.Any(count => count == 0),
                $"Sideview ${test.Group:x1}:${test.Room:x2} must execute every platform's complete native script loop.");
        }
        GD.Print("Validated executed-ROM all17 source-placed sideview platforms ($a1:$00-$0e), state0 text gate, horizontal/vertical endpoints, waits and looping programs in split/batched gameplay.");
    }
}
