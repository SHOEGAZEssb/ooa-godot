using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombchuWaterGatesRom()
    {
        int fixture = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int terrain in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (terrain != 1) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(TreasureId.Bombchus, 0x10);
            _inventory.EquipA(primary ? TreasureId.Bombchus : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Bombchus);
            LoadValidationRoom(terrain == 0 ? 2 : 7, terrain == 0 ? 0x90 : 0x05);
            _entities.Clear();
            Vector2 point = terrain == 0 ? new(88, 72) : new(40, 56);
            if (terrain == 0)
            {
                FailIf((_currentRoom.TilesetFlags & 0x40) == 0, "Bombchu room $2:$90 must retain its underwater flag.");
                // Declare walkable terrain to isolate the room flag from its
                // separate deep-water warp. Side-view water remains unmodified.
                _currentRoom.SetPositionTileAndCollision(point, 0xa0, 0, 0);
            }
            _player.WarpTo(point);
            _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(point), "Bombchu water fixture starts inside solid room geometry.");
            var rom = new SideViewRom(_saveData, _random.CaptureState(), _currentRoom, point, _entities.FrameCounter);
            rom[0xd008] = 1;
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2;
            void Step(int count = 1, int held = 0, int pressed = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(pressed: edge, held: held); edge = 0;
                    bool parent = Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x0d);
                    bool child = Enumerable.Range(0xd7, 5).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x0d);
                    string context = $"Bombchu water terrain={terrain}, A={primary}, batch={batched}";
                    FailIf(parent || child || _entities.BombchuParent.Active || _entities.Entities<BombchuItem>().Count != 0 ||
                        _inventory.Bombchus != 0x10 || rom[0xc6b3] != 0x10,
                        context + ": source rejection must clear parent/child without spending packed-BCD ammo.");
                    CompareSideViewRom(rom, context);
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": rejected item changed sound order.");
                });
            }
            Step();
            if (terrain != 0)
                FailIf(!_player.SideScrollSwimming || rom[0xcc5d] == 0, "Room $7:$05 must enter swimming before Bombchu input.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, button, button); Step(8, button);
                _dialogue.ShowMessage("Bombchu water pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3, button);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(3, button); Step();
            }
        }
        GD.Print("Compared Bombchu A/B underwater-room and actual side-view Flippers/Mermaid swimming rejection: cleared parents/children, retained ammo, full Link, cues/RNG, text and repeat through split/batched gameplay.");
    }
}
