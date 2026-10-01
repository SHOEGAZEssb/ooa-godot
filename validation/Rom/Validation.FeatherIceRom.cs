using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareFeatherIceVelocityRom(FeatherRom rom, string context)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        int Read(string name) => (int)typeof(Player).GetField(name, flags)!.GetValue(_player)!;
        FailIf(Read("_topDownMovementAngle") != rom[0xd009] ||
            Read("_topDownMovementSpeedRaw") != rom[0xd010] ||
            Read("_topDownMovementTargetSpeedRaw") != rom[0xd011],
            $"{context}: ice object velocity differs; native angle/speed/target=${rom[0xd009]:x2}/${rom[0xd010]:x2}/${rom[0xd011]:x2}.");
        if (!rom.Airborne)
            FailIf(Read("_topDownMovementTerrainMode") != rom[0xd036] ||
                Read("_topDownMovementVelocityCounter") != rom[0xd012] ||
                Read("_topDownMovementVelocityInterval") != rom[0xd013],
                $"{context}: ice ground timing differs; native mode/counter/interval=${rom[0xd036]:x2}/${rom[0xd012]:x2}/${rom[0xd013]:x2}.");
    }

    private void ValidateFeatherIceMomentumRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int takeoff in new[] { 0, 8, 16, 24, 0xff })
        foreach (int steering in new[] { 0, 8, 16, 24, 0xff })
        foreach (int groundUpdates in new[] { 1, 13 })
        {
            FeatherRom rom = PrepareFeatherRom(primary, 0.5f);
            // The fractional warp in PrepareFeatherRom clears velocity.
            // Both loops enter ice from rest and generate their own momentum.
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x8a, 0, 0);
            rom.CopyRoom(_currentRoom);
            StepFeatherRom(rom, groundUpdates, batched, takeoff, primary: primary);
            int launchAngle = rom[0xd009];
            int launchSpeed = rom[0xd010];
            StepFeatherRom(rom, 1, batched, takeoff, held: true, pressed: true, primary: primary);
            FailIf(rom[0xd009] != launchAngle || rom[0xd010] != launchSpeed || rom[0xd013] != 0,
                $"Ice Feather takeoff=${takeoff:x2}, steering=${steering:x2}, warmup={groundUpdates}: expected inherited angle/speed=${launchAngle:x2}/${launchSpeed:x2}, var13=$00; native=${rom[0xd009]:x2}/${rom[0xd010]:x2}, var13=${rom[0xd013]:x2}.");
            StepFeatherRom(rom, 13, batched, steering, held: true, primary: primary);
            FailIf(rom[0xd009] != launchAngle || rom[0xd010] != launchSpeed,
                "Ice Feather must retain its inherited momentum through rising update14.");
            StepFeatherRom(rom, 17, batched, steering, held: true, primary: primary);
            StepFeatherRom(rom, 12, batched, steering, held: true, primary: primary);
            StepFeatherRom(rom, 1, batched, primary: primary);
            StepFeatherRom(rom, 31, batched, held: true, pressed: true, primary: primary);
        }
        foreach (bool batched in new[] { false, true })
        foreach (bool snowshoe in new[] { false, true })
        foreach (int pegasus in new[] { 0, 0x80, 20 })
        {
            FeatherRom rom = PrepareFeatherRom();
            if (snowshoe)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug((int)RingId.Snowshoe);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, (int)RingId.Snowshoe) || !_inventory.EquipRingAt(0),
                    "Could not equip SNOWSHOE_RING $21 for Feather ice comparison.");
                rom[0xc6cb] = 0x21;
            }
            _runtimeState.SetWramByte(0xcc6c, (byte)pegasus);
            rom.Word(0xcc6c, pegasus);
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), x >= 136 ? (byte)0x8a : (byte)0xa0, 0, 0);
            rom.CopyRoom(_currentRoom);
            // Enter ice from normal floor, retaining the previous full speed.
            StepFeatherRom(rom, 18, batched, 8);
            StepFeatherRom(rom, 1, batched, 24, held: true, pressed: true);
            StepFeatherRom(rom, 30, batched, 24);
            StepFeatherRom(rom, 16, batched, 24);
            StepFeatherRom(rom, 12, batched);
            StepFeatherRom(rom, 1, batched);
            StepFeatherRom(rom, 31, batched, held: true, pressed: true);
        }
        GD.Print("Validated executed-ROM ice entry/exit, inherited Feather angle/speed, six-update ground convergence, descending steering, landing, Snowshoe/Pegasus and repeat A/B jumps through split/batched gameplay updates.");
    }
}
