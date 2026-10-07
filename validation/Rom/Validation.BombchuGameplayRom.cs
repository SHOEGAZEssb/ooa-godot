using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBombchuGameplayRom()
    {
        var walkingField = typeof(BombchuItem).GetField("_walking", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var animationCounter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int fixture = 0;
        foreach (bool sideview in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int direction in sideview ? new[] { 1, 3 } : Enumerable.Range(0, 4))
        foreach (int jumpUpdates in direction == 1 ? new[] { 0, 1, 15, 29 } : new[] { 0 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            SomariaRom rom = PrepareBombchuGameplayRom(direction, primary, sideview);
            _inventory.GiveTreasure(TreasureId.Bombchus, 0x10);
            EquipSomariaMotionItem(rom, TreasureId.Bombchus, primary);
            if (jumpUpdates != 0)
            {
                _inventory.GiveTreasure(TreasureId.Feather, 1);
                if (primary) _inventory.EquipB(TreasureId.Feather); else _inventory.EquipA(TreasureId.Feather);
                for (int address = 0xc600; address < 0xc800; address++) rom[address] = _saveData.ReadWramByte(address);
                // Parent creation copies all eight direction/XYZ bytes. Keep
                // fractional source inputs across the later high-byte placement.
                _player.SetScriptedPosition(_player.PrecisePosition + new Vector2(0.25f, 0.5f));
                rom.Word(0xd00a, (int)(_player.PrecisePosition.Y * 256)); rom.Word(0xd00c, (int)(_player.PrecisePosition.X * 256));
            }
            rom[0xc6b3] = 0x10; rom.InitializeLinkWalkingAnimation();
            // The native constructor's facing angle is not the stationary
            // angle retained by Link after the real side-view floor approach.
            if (sideview) rom[0xd009] = (byte)_player.SideScrollAngle;
            var sounds = _sound.AttachPlayRequestAudit(); var seed = _random.CaptureState();
            int button = primary ? 1 : 2, previous = 0, updates = 0;
            void Compare()
            {
                string context = $"Bombchu side={sideview}, A={primary}, dir={direction}, jump={jumpUpdates}, batch={batched}, update={++updates}";
                var children = _entities.Entities<BombchuItem>();
                int[] native = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                    .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x0d).ToArray();
                var parent = _entities.BombchuParent;
                int parentSlot = rom[0xd300] != 0 && rom[0xd301] == 0x0d ? 0xd300 :
                    rom[0xd400] != 0 && rom[0xd401] == 0x0d ? 0xd400 : 0;
                FailIf(children.Count != native.Length || parent.Active != (parentSlot != 0) || _inventory.Bombchus != rom[0xc6b3],
                    context + $": child/parent/ammo runtime={children.Count}/{parent.Active}/${_inventory.Bombchus:x2}, native={native.Length}/${parentSlot:x4}/${rom[0xc6b3]:x2}.");
                if (parent.Active)
                    FailIf(parent.Slot != ((parentSlot >> 8) & 15) || parent.Counter != rom[parentSlot + 0x20] ||
                        parent.Parameter != rom[parentSlot + 0x21] || parent.Mode != rom[parentSlot + 0x30],
                        context + ": independent parent slot/clock/mode differs.");
                if (children.SingleOrDefault() is { } child)
                {
                    int slot = native.Single();
                    FailIf(child.ItemState != rom[slot + 4] || child.Counter1 != rom[slot + 6] || child.Counter2 != rom[slot + 7] ||
                        child.Position != new Vector2(rom[slot + 0xd], rom[slot + 0xb]) ||
                        (ushort)child.ZFixed != rom.Word(slot + 0xe) || (ushort)child.SpeedZ != rom.Word(slot + 0x14) ||
                        child.SpeedRaw != rom[slot + 0x10] || child.SpeedTmp != rom[slot + 0x11] ||
                        child.Radius != rom[slot + 0x26] || child.Radius != rom[slot + 0x27],
                        context + $": state/counters/XY/Z/speed/radius runtime={child.ItemState}/{child.Counter1},{child.Counter2}/{child.Position}/{child.ZFixed},{child.SpeedZ}/{child.SpeedRaw},{child.SpeedTmp}/{child.Radius}, " +
                        $"native={rom[slot+4]}/{rom[slot+6]},{rom[slot+7]}/{rom[slot+0xd]},{rom[slot+0xb]}/{rom.Word(slot+0xe):x4},{rom.Word(slot+0x14):x4}/{rom[slot+0x10]},{rom[slot+0x11]}/{rom[slot+0x26]}.");
                    if (child.ItemState != 0xff)
                    {
                        var steering = child.Steering!;
                        var animation = (EnemyAnimationPlayer)walkingField.GetValue(child)!;
                        FailIf(steering.Angle != rom[slot + 9] || steering.Direction != rom[slot + 8] || steering.Turn != rom[slot + 0x31] ||
                            (steering.Clinging ? 1 : 0) != rom[slot + 0x32] || steering.FormerAngle != rom[slot + 0x33] ||
                            (steering.Ceiling ? 1 : 0) != rom[slot + 0x34] || child.ScanSlot + 0xd0 != rom[slot + 0x30] ||
                            child.VerticalFlags != rom[slot + 0x3b] ||
                            child.WalkingParameter != rom[slot + 0x21] || (int)animationCounter.GetValue(animation)! != rom[slot + 0x20],
                            context + ": steering/search/animation state differs.");
                    }
                    else FailIf(child.AnimationCounter != rom[slot + 0x20] || child.Damage != -(sbyte)rom[slot + 0x28],
                        context + ": shared explosion clock/damage differs.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered sound/RNG differs.");
                if (jumpUpdates != 0)
                    FailIf((sideview ? _player.SideScrollAirborne : _player.TopDownAirborne) != (rom[0xcc5c] != 0) ||
                        (ushort)_player.ItemCreationZFixed != rom.Word(0xd00e) ||
                        (ushort)(sideview ? _player.SideScrollSpeedZ : _player.TopDownAirSpeedZ) != rom.Word(0xd014) ||
                        children.SingleOrDefault() is { } airChild && airChild.PrecisePosition !=
                            new Vector2(rom.Word(native.Single() + 0xc) / 256f, rom.Word(native.Single() + 0xa) / 256f),
                        context + $": full Link air/Z or child XY fractions differ: air={(sideview ? _player.SideScrollAirborne : _player.TopDownAirborne)}/{rom[0xcc5c]}, " +
                        $"Link Z=${(ushort)_player.ItemCreationZFixed:x4}/${rom.Word(0xd00e):x4}, Vz=${(ushort)(sideview ? _player.SideScrollSpeedZ : _player.TopDownAirSpeedZ):x4}/${rom.Word(0xd014):x4}, " +
                        $"child XY={children.SingleOrDefault()?.PrecisePosition}, native=" +
                        (native.Length == 0 ? "empty" : $"${rom.Word(native.Single() + 0xc):x4}/${rom.Word(native.Single() + 0xa):x4}"));
            }
            void Step(int count = 1, int held = 0, int angle = 0xff)
            {
                int edge = held & ~previous; previous = held;
                StepSomariaMotionRom(rom, count, batched, angle, held, edge, Compare);
            }
            if (jumpUpdates != 0)
            {
                Step(jumpUpdates, primary ? 2 : 1);
                FailIf(!(sideview ? _player.SideScrollAirborne : _player.TopDownAirborne),
                    "Bombchu air fixture must launch through equipped Feather.");
                Step(1, button, 24); Step(4, angle: 24);
                _dialogue.ShowMessage("Airborne Bombchu pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step(32);
                FailIf(sideview ? _player.SideScrollAirborne : _player.TopDownAirborne,
                    "Bombchu parent must release and retain the native Feather landing.");
                _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); Step();
                Step(1, button); Step(3); // Fresh use after landing and explicit cancellation.
                continue;
            }
            Step(1, button); Step(4);
            _dialogue.ShowMessage("Bombchu pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(5); Step(1, button); // Active child cap: failed allocation must retain ammo and one-update lock.
            Step(220);
            FailIf(_entities.Entities<BombchuItem>().Count != 0, "Bombchu must finish its bounded fuse/explosion lifetime.");
            Step(1, button); Step(3); _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems();
            CompareSomariaMotionRom(rom, "Bombchu whole-item cancellation"); Compare();
            Step(2); Step(1, button); Step(2);
        }
        CompareBombchuTargetRom();
        CompareBombchuParentOrderRom();
        CompareBombchuHazardsRom();
        CompareBombchuMasksRom();
        CompareBombchuWaterGatesRom();
        CompareBombchuSwimEntryRom();
        ValidateRaftControlRom(false, true, bombchuButton: 1);
        ValidateRaftControlRom(false, true, bombchuButton: 2);
        ValidateMinecartMountGameplayRom(true);
        CompareBombExplosionContactsRom(bombchu: true);
        GD.Print("Validated clean-US Bombchu A/B gameplay: independent parent slot/clock, top-down/side-view physical scan/movement/fuse/explosion, text hold, cap failure, cancellation/repeat, cues and RNG with split/batched host updates.");
    }

    private SomariaRom PrepareBombchuGameplayRom(int direction, bool primary, bool sideview)
    {
        if (!sideview) return PrepareSomariaMotionRom(direction, primary: primary);
        ReinitializeGameplayForValidation(); LoadValidationRoom(6, 0x93); _entities.Clear();
        Vector2 start = Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
            .SelectMany(y => Enumerable.Range(2, _currentRoom.WidthInTiles - 4)
                .Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
            .First(point => (_playerWorld.GetSideScrollTerrain(point).CombinedType &
                (SideScrollTileType.Ladder | SideScrollTileType.LadderTop)) == 0 && !_collision.Collides(point) &&
                _currentRoom.GetTerrainInfo(point + new Vector2(0, 16)).Collision == 0x0f);
        _player.WarpTo(start); StepGameplayUpdates(20, Vector2.Zero);
        _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
        FailIf(!_player.IsGroundedForFloorButton || _currentRoom.IsSolid(_player.Position),
            "Bombchu side-view fixture must stand on room $6:$93's actual floor.");
        var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, direction,
            (int)_player.Position.X, (int)_player.Position.Y);
        rom.Word(0xd00a, (int)(_player.PrecisePosition.Y * 256)); rom.Word(0xd00c, (int)(_player.PrecisePosition.X * 256));
        rom.InitializeLinkGameplay();
        return rom;
    }
}
