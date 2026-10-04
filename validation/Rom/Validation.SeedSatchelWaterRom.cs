using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSeedSatchelWaterRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int terrain in new[] { 0, 1, 2 })
        foreach (int selected in Enumerable.Range(0, 5))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (terrain != 1) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            for (int seed = 0; seed < 5; seed++) _inventory.GiveTreasure(0x20 + seed, 0x20);
            _inventory.SelectSatchelSeeds(selected);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.SeedSatchel);
            LoadValidationRoom(terrain == 0 ? 2 : 7, terrain == 0 ? 0x90 : 0x05);
            _entities.Clear();
            Vector2 point = terrain == 0 ? new(88, 72) : new(40, 56);
            if (terrain == 0)
            {
                FailIf((_currentRoom.TilesetFlags & 0x40) == 0, "Room $2:$90 must retain TILESETFLAG_UNDERWATER.");
                // Isolate the room flag from the separate deep-water warp.
                _currentRoom.SetPositionTileAndCollision(point, 0xa0, 0, 0);
            }
            _player.WarpTo(point);
            FailIf(_collision.Collides(point), "Satchel water fixture must start in reachable room geometry.");
            var rom = new SideViewRom(_saveData, _random.CaptureState(), _currentRoom, point, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0;
            void Step(int count = 1, int held = 0, int pressed = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(pressed: edge, held: held); edge = 0;
                    string context = $"Satchel water terrain={terrain} ITEM${0x20 + selected:x2} A={primary} update={++update}";
                    bool parent = Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x19);
                    bool child = Enumerable.Range(0xd7, 5).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] is >= 0x20 and <= 0x24);
                    FailIf(_player.IsUsingSeedSatchel != parent || parent || child ||
                        _entities.Entities<EmberSeedEffect>().Count != 0 ||
                        _seedSatchel.Pegasus.RawCounter != (rom[0xcc6c] | rom[0xcc6d] << 8) || _seedSatchel.Pegasus.RawCounter != 0,
                        context + ": forbidden parent/projectile or Pegasus activation.");
                    for (int address = 0xc6b9; address <= 0xc6bd; address++)
                        FailIf(_saveData.ReadWramByte(address) != rom[address] || rom[address] != 0x20,
                            context + $": forbidden seed consumption at ${address:x4}.");
                    FailIf(sounds.Requests.Any(id => id is 0x52 or 0x72 or 0x85 or 0x7b or 0x90 or 0xa3) ||
                        rom.Sounds.Any(id => id is 0x52 or 0x72 or 0x85 or 0x7b or 0x90 or 0xa3),
                        context + ": rejected Satchel initialized a seed sound.");
                });
            }
            Step();
            if (terrain != 0)
                FailIf(!_player.SideScrollSwimming || rom[0xcc5d] == 0, "Room $7:$05 must enter swimming before Satchel input.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, button, button);
                Step(12, button);
                _dialogue.ShowMessage("Satchel water pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, button);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, button);
                Step();
            }
        }
        GD.Print("Validated clean-US all five Satchel seeds, A/B underwater-room and side-view Flippers/Mermaid swimming rejection, retained ammo, no children/Pegasus/sounds, dialogue and repeat through split/batched gameplay.");
    }
}
