using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShovelWaterRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int terrain in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (terrain != 1) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(TreasureId.Shovel, 0);
            _inventory.EquipA(primary ? TreasureId.Shovel : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Shovel);
            LoadValidationRoom(terrain == 0 ? 2 : 7, terrain == 0 ? 0x90 : 0x05);
            _entities.Clear();
            Vector2 point = terrain == 0 ? new(88, 72) : new(40, 56);
            if (terrain == 0)
            {
                FailIf((_currentRoom.TilesetFlags & 0x40) == 0, "Shovel room $2:$90 must retain its underwater flag.");
                // Isolate checkLinkOnGround from the separate deep-water warp.
                _currentRoom.SetPositionTileAndCollision(point, 0xa0, 0, 0);
            }
            _player.WarpTo(point);
            FailIf(_collision.Collides(point), "Shovel water fixture starts inside solid room geometry.");
            var initialRandom = _random.CaptureState();
            var rom = new SideViewRom(_saveData, initialRandom, _currentRoom, point, _entities.FrameCounter);
            int maturity = _saveData.GashaMaturity;
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0;
            void Step(int count = 1, int held = 0, int pressed = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(pressed: edge, held: held); edge = 0;
                    string context = $"Shovel water terrain={terrain} A={primary} update={++update}";
                    bool parent = Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x15);
                    bool child = Enumerable.Range(0xd6, 10).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x15);
                    // shovelParent state0 calls checkLinkOnGround: swimming,
                    // underwater and mounted/airborne Link must clear the parent.
                    FailIf(_player.IsUsingShovel != parent || parent || child || _shovel.ChildActive ||
                        _entities.Entities<ShovelDebrisEffect>().Count != 0,
                        context + ": rejected shovel retained its parent, child or debris.");
                    FailIf(_saveData.GashaMaturity != rom.Word(0xc65f) || _saveData.GashaMaturity != maturity,
                        context + ": rejected shovel matured Gasha state.");
                    FailIf(sounds.Requests.Any(id => id is SoundId.SndDig or SoundId.SndClink) ||
                        rom.Sounds.Any(id => id is SoundId.SndDig or SoundId.SndClink),
                        context + ": rejected shovel emitted dig/clink sound.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls,
                        context + ": rejected shovel changed shared RNG.");
                });
            }
            Step();
            if (terrain != 0)
                FailIf(!_player.SideScrollSwimming || rom[0xcc5d] == 0, "Room $7:$05 must enter swimming before shovel input.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, button, button);
                Step(12, button);
                _dialogue.ShowMessage("Shovel water pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, button);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, button);
                Step();
            }
        }
        GD.Print("Validated clean-US Shovel A/B underwater-room and side-view Flippers/Mermaid swimming rejection, silent parent/child/debris gates, maturity/RNG, dialogue, held input and repeat through split/batched gameplay.");
    }
}
