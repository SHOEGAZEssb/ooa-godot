using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private SwordRom PrepareSwordGameplayRom(int level, int ring = 0xff)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(4, 0x91);
        _entities.Clear();
        _inventory.GiveTreasure(TreasureId.Sword, level);
        _inventory.EquipA(TreasureId.Sword);
        if (ring != 0xff)
        {
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                $"Could not equip sword ROM ring ${ring:x2}.");
        }
        _player.WarpTo(new(120, 128));
        StepGameplayUpdates(16, Vector2.Up);
        FailIf(_player.Position != new Vector2(120, 112) || _collision.Collides(_player.Position),
            "Sword ROM fixture must walk through the actual $4:$91 entrance floor.");
        OracleRandomState seed = _random.CaptureState();
        var rom = new SwordRom(0, level, ring, seed.Rng1 | seed.Rng2 << 8);
        rom[0xd00b] = 112;
        rom[0xd00d] = 120;
        rom[0xc6aa] = (byte)_inventory.HealthQuarters;
        rom[0xc6ab] = (byte)_inventory.MaxHealthQuarters;
        rom[0xcc33] = (byte)_currentRoom.ActiveCollisions;
        for (int y = 0; y < 11; y++)
        for (int x = 0; x < 16; x++)
        {
            Vector2 point = new(x * 16 + 8, y * 16 + 8);
            if (x >= _currentRoom.WidthInTiles || y >= _currentRoom.HeightInTiles) continue;
            rom[0xcf00 + y * 16 + x] = _currentRoom.GetMetatile(point);
            rom[0xce00 + y * 16 + x] = (byte)_currentRoom.GetTerrainInfo(point).Collision;
        }
        return rom;
    }

    private void ValidateSwordGameplayRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int ring in new[] { 0xff, 0x16, 0x2f, 0x31 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            SwordRom rom = PrepareSwordGameplayRom(2, ring);
            if (!primary) { _inventory.EquipA(0); _inventory.EquipB(TreasureId.Sword); }
            string button = primary ? "attack" : "item";
            var audit = _sound.AttachPlayRequestAudit();
            int updates = 0;
            void Step(int count, bool held, bool pressed = false)
            {
                StepGameplayUpdates(count, Vector2.Zero, held ? [button] : [], pressed ? [button] : [], batched,
                    afterUpdate: () =>
                    {
                        rom[0xcc00] = (byte)_entities.FrameCounter;
                        Vector2 camera = _player.Position - _transitions.WorldToGameplayScreen(_player.Position);
                        rom[0xffaa] = (byte)camera.Y;
                        rom[0xffac] = (byte)camera.X;
                        rom.Update(held, pressed && updates == 0, primary);
                        CompareSwordRom(_player, rom, $"Gameplay ITEM $05 ring=${ring:x2}, A={primary}, batch={batched}, update={updates++}");
                        int beams = Enumerable.Range(0xd7, 5).Count(page => rom[page * 256] != 0 && rom[page * 256 + 1] == 0x27);
                        FailIf(_entities.Entities<SwordBeamEffect>().Count != beams,
                            $"Gameplay sword beam $27 allocation/lifetime differs at update={updates}, ring=${ring:x2}: native={beams}, runtime={_entities.Entities<SwordBeamEffect>().Count}, camera={camera}.");
                    });
            }
            Step(1, true, true);
            Step(19, true);
            // Link's text gate freezes parents; updateItems freezes initialized
            // children, while updateItemsPost still executes geometry/damage.
            _dialogue.ShowMessage("Sword pause.", _player.Position.Y);
            rom[0xcba0] = 1;
            Step(5, true);
            _dialogue.Close();
            rom[0xcba0] = 0;
            Step(50, true);
            Step(50, false);
            FailIf(_player.IsAttacking || rom[0xd200] != 0, "Sword gameplay fixture must complete before repeating.");
            updates = 0;
            Step(1, true, true);
            Step(4, false);
            // Full room replacement clears parents, then the orphaned weapon
            // is retired by its unconditional native post pass.
            LoadValidationRoom(4, 0x91);
            rom.Call(6, 0x4878); // clearAllParentItems_body
            rom.Call(7, 0x491a); // updateItemsPost
            CompareSwordRom(_player, rom, "Sword room replacement cancellation");
            FailIf(rom[0xd600] != 0 || _player.IsAttacking,
                "Room replacement must clear the parent and orphaned sword child.");
            // Beam creation is checked against executed ROM at each update;
            // dialogue sounds and room-load music are unrelated requests.
            FailIf(audit.Requests.Count(id => id == SoundId.SndSwordBeam) != rom.Sounds.Count(id => id == SoundId.SndSwordBeam),
                "Sword gameplay beam initialization sound count differs from the native child pass.");
        }
        GD.Print("Validated ROM sword A/B lifecycles, beam allocation/lifetime, dialogue pause/resume, repeat and room replacement through individual and batched gameplay updates on actual $4:$91 geometry.");
    }
}
