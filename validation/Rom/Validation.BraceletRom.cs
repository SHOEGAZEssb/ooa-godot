using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private BraceletRom PrepareBraceletRom(int direction = 0, int ring = 0xff, bool primary = true,
        byte tile = 0x10, byte underlying = 0xa0)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(4, 0xa8);
        _entities.Clear();
        _inventory.GiveTreasure(TreasureId.Bracelet, 1);
        _inventory.EquipA(primary ? TreasureId.Bracelet : 0);
        _inventory.EquipB(primary ? 0 : TreasureId.Bracelet);
        if (ring != 0xff)
        {
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                $"Could not equip Bracelet ROM ring ${ring:x2}.");
        }
        for (int y = 24; y <= 152; y += 16)
        for (int x = 56; x <= 184; x += 16)
            _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
        Vector2 center = new(120, 88);
        _currentRoom.SetPositionTileAndCollision(center, tile, 0x0f, 0);
        _currentRoom.SetUnderlyingMetatile(center, underlying);
        Vector2 facing = OracleObjectMath.StrictCardinalVector(direction * 8);
        _player.WarpTo(center - facing * 26);
        var rom = new BraceletRom(_currentRoom, _player.Position, direction, ring);
        StepGameplayUpdates(24, facing);
        for (int update = 0; update < 24; update++) rom.Walk(direction * 8);
        Vector2 expected = new(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f);
        FailIf(_player.Position != expected || _currentRoom.IsSolid(_player.Position),
            $"Bracelet direction={direction} must approach its tile through floor geometry, got {_player.Position}, expected {expected}.");
        rom[0xcc2d] = 4;
        rom[0xcc2e] = 1;
        var seed = _random.CaptureState();
        rom[0xff94] = seed.Rng1;
        rom[0xff95] = seed.Rng2;
        return rom;
    }

    private void CompareBraceletRom(BraceletRom rom, string context)
    {
        Vector2 tilePoint = new(120, 88);
        FailIf(_currentRoom.GetMetatile(tilePoint) != rom[0xcf57] ||
            _currentRoom.GetUnderlyingMetatile(tilePoint) != rom.Underlying(0x57) ||
            _currentRoom.GetTerrainInfo(tilePoint).Collision != rom[0xce57],
            $"{context}: tile $57 logical/underlying/collision bytes differ.");
        bool nativeParent = rom[0xd200] != 0;
        BraceletState expected = !nativeParent
            ? rom.ChildActive ? BraceletState.Projectile : BraceletState.Idle
            : rom[0xd204] switch
            {
                0 => BraceletState.SeekingWall,
                1 => BraceletState.GrabbingWall,
                2 => BraceletState.Lifting,
                3 => BraceletState.Holding,
                4 => BraceletState.Throwing,
                _ => throw new InvalidOperationException($"{context}: parent state ${rom[0xd204]:x2} unsupported.")
            };
        FailIf(_bracelet.State != expected,
            $"{context}: Bracelet runtime={_bracelet.State}, ROM={expected}, grab=${rom[0xcc5a]:x2}, direction={rom[0xd008]}, native child={rom[0xdc0d]},{rom[0xdc0b]}, angle=${rom[0xdc09]:x2}.");
        FailIf(_player.IsCarryingObject != (rom[0xcc5a] == 0x83) ||
            _player.BraceletLiftCollisionsDisabled != (rom[0xcc5a] == 0xc2),
            $"{context}: Bracelet Link ownership/collision mask differs from grab=${rom[0xcc5a]:x2}.");
        var child = _bracelet.LiftedObject;
        FailIf((child is not null) != rom.ChildActive,
            $"{context}: reserved ITEM $dc/$16 occupancy differs.");
        if (child is null) return;
        Vector2 ground = child.Thrown ? child.GroundPosition : _player.Position + new Vector2(child.Position.X, 0);
        int z = child.Thrown ? child.ZFixed : (int)child.Position.Y << 8;
        Vector2 nativeGround = new(rom.Word(0xdc0c) / 256f, rom.Word(0xdc0a) / 256f);
        int nativeZ = unchecked((short)rom.Word(0xdc0e));
        FailIf(ground != nativeGround || z != nativeZ || child.Thrown != (rom[0xdc04] == 3),
            $"{context}: reserved ITEM $16 runtime pos={ground}, z=${z & 0xffff:x4}, thrown={child.Thrown}; ROM pos={nativeGround}, z=${nativeZ & 0xffff:x4}, state=${rom[0xdc04]:x2}.");
        if (child.Thrown)
            FailIf(child.SpeedRaw != rom[0xdc10] || child.SpeedZ != unchecked((short)rom.Word(0xdc14)),
                $"{context}: ITEM $16 velocity differs: runtime=${child.SpeedRaw:x2}/${child.SpeedZ & 0xffff:x4}, ROM=${rom[0xdc10]:x2}/${rom.Word(0xdc14):x4}.");
    }

    private void StepBraceletRom(BraceletRom rom, int count, bool batched, int angle = 0xff,
        bool held = false, bool pressed = false, bool primary = true, bool moveLink = false,
        bool otherPressed = false)
    {
        int update = 0;
        string button = primary ? "attack" : "item";
        string other = primary ? "item" : "attack";
        string[] heldActions = held ? [button] : [];
        string[] pressedActions = pressed ? [button] : otherPressed ? [other] : [];
        StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
            heldActions, pressedActions, batched, afterUpdate: () =>
            {
                bool wasThrown = rom.ChildActive && rom[0xdc04] == 3;
                rom.Update(angle, held, pressed && update == 0, primary, moveLink, otherPressed && update == 0);
                CompareBraceletRom(rom, $"Bracelet batch={batched}, angle=${angle:x2}, update={update++}");
                if (wasThrown && !rom.ChildActive)
                    CompareBraceletImpactRom(rom, $"Bracelet impact angle=${angle:x2}, update={update}");
                if (moveLink)
                {
                    Vector2 nativeLink = new(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f);
                    FailIf(_player.PrecisePosition != nativeLink,
                        $"Bracelet movement handoff update={update}, batch={batched}: runtime={_player.PrecisePosition}, ROM={nativeLink}.");
                }
            });
    }

    private void ValidateBraceletGrabLiftRom()
    {
        int hostCase3 = 0;
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (bool batched in RomHostSchedules(hostCase3++))
        {
            BraceletRom rom = PrepareBraceletRom(direction);
            StepBraceletRom(rom, 1, batched, held: true, pressed: true);
            StepBraceletRom(rom, 10, batched, (direction * 8 + 16) & 0x1f, held: true);
            FailIf(_currentRoom.GetMetatile(new(120, 88)) != 0x10,
                "Bracelet must retain pot $10 before its eleventh pull update.");
            StepBraceletRom(rom, 1, batched, (direction * 8 + 16) & 0x1f, held: true);
            FailIf(_currentRoom.GetMetatile(new(120, 88)) != 0xa0 || rom[0xcf57] != 0xa0 ||
                rom.Underlying(0x57) != 0xa0,
                "Bracelet source $00 must restore pot $10's underlying dungeon floor $a0 on pull update11.");
            StepBraceletRom(rom, 13, batched);
            FailIf(!_bracelet.HoldingTile, "Bracelet must finish its 7/4/2 pickup lift.");
        }
        CompareBraceletGrabEdgesRom();
        CompareBraceletPullCancellationRom();
        GD.Print("Validated executed-ROM Bracelet grab, pull boundary, dungeon pot replacement and exact four-direction lift offsets through individual/batched gameplay updates.");
    }

    private void ValidateBraceletCarryThrowRom()
    {
        int hostCase2 = 0;
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int ring in new[] { 0xff, 0x12 })
        foreach (bool drop in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            BraceletRom rom = PrepareBraceletRom(direction, ring);
            var sounds = _sound.AttachPlayRequestAudit();
            StepBraceletRom(rom, 1, batched, held: true, pressed: true);
            StepBraceletRom(rom, 11, batched, (direction * 8 + 16) & 0x1f, held: true);
            StepBraceletRom(rom, 20, batched);
            StepBraceletRom(rom, 1, batched, drop ? 0xff : direction * 8, held: true, pressed: true);
            StepBraceletRom(rom, 45, batched);
            FailIf(_bracelet.LiftedObject is not null || rom.ChildActive,
                "A breakable Bracelet tile must retire its reserved child on impact.");
            FailIf(!sounds.Requests.Where(id => id is SoundId.SndPickup or SoundId.SndThrow).SequenceEqual(rom.Sounds),
                "Bracelet pickup/throw sound order must match the executed parent.");
        }
        CompareBraceletWallImpactRom();
        CompareBraceletObjectBounceRom();
        CompareBraceletHazardsRom();
        GD.Print("Validated executed-ROM Bracelet carry/drop/throw, ordinary/Toss velocities and impact lifetime across four directions through individual/batched gameplay updates.");
    }

    private void ValidateBraceletGameplayRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            BraceletRom rom = PrepareBraceletRom(primary: primary);
            StepBraceletRom(rom, 1, batched, held: true, pressed: true, primary: primary);
            StepBraceletRom(rom, 11, batched, 16, held: true, primary: primary);
            StepBraceletRom(rom, 13, batched, primary: primary);
            _dialogue.ShowMessage("Bracelet pause.", _player.Position.Y);
            rom[0xcba0] = 1;
            StepBraceletRom(rom, 7, batched, primary: primary);
            _dialogue.Close();
            rom[0xcba0] = 0;
            StepBraceletRom(rom, 1, batched, 0, primary: primary, otherPressed: true);
            StepBraceletRom(rom, 14, batched, 0, primary: primary, moveLink: true);
            StepBraceletRom(rom, 40, batched, primary: primary);
            FailIf(_bracelet.State != BraceletState.Idle, "Bracelet impact must release the parent for another pickup.");
            RepeatBraceletPickupRom(rom, batched, primary);
        }
        CompareBraceletRoomCancellationRom();
        GD.Print("Validated executed-ROM Bracelet A/B, other-button release, dialogue pause/resume and throw-to-Link movement handoff through individual/batched gameplay updates.");
    }
}
