using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionEquippedItemsRom()
    {
        int fixture = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int item in new[] { TreasureId.Shield, TreasureId.Boomerang })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            var (actor, mounted) = PrepareMountedCompanionRom(id);
            _inventory.GiveTreasure(item, 1);
            bool primary = id == 0x0c;
            _inventory.EquipA(primary ? item : 0); _inventory.EquipB(primary ? 0 : item);
            OracleRandomState seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, 72, 64)
                { CompanionDispatchEnabled = true };
            // Declared initialized mounted boundary from the native fixture:
            // species animation, rider ID $09 and post-object position copy.
            // Subsequent updates execute both original special-object callers,
            // ordinary Link/item dispatch after dismount, and rider copying.
            for (int address = 0xd000; address < 0xd040; address++) rom[address] = mounted[address];
            for (int address = 0xd100; address < 0xd140; address++) rom[address] = mounted[address];
            rom[0xcc2c] = 0xd1; rom[0xcc96] = 1; rom[0xccaa] = 0xff;
            rom[0xcc21] = rom[0xcc22] = 40; rom[0xcc23] = 2;
            var sounds = _sound.AttachPlayRequestAudit();
            int previous = 0, update = 0;
            int button = primary ? 1 : 2;
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~previous; previous = held;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter - 1); edge = 0;
                    string context = $"Companion ${id:x2} equipped ITEM${item:x2} A={primary} batch={batched} update={++update}";
                    FailIf(_player.CompanionRideActive != (rom[0xcc2c] == 0xd1) ||
                        _player.CompanionJumpActive != (rom[0xcc2c] == 0xd0 && rom[0xcc5c] != 0) ||
                        _player.Position != new Vector2(rom[0xd00d], rom[0xd00b]),
                        context + $": ride/air/Link runtime={_player.CompanionRideActive}/{_player.CompanionJumpActive}/{_player.Position}, native=${rom[0xcc2c]:x2}/${rom[0xcc5c]:x2}/{rom[0xd00d]},{rom[0xd00b]}, companion=${rom[0xd104]:x2}/${rom[0xd105]:x2} parameter=${rom[0xd121]:x2}.");
                    FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                        _entities.BoomerangParent.Active != (rom[0xd300] != 0),
                        context + $": equipped parent runtime={_player.IsUsingShield}/{_entities.BoomerangParent.Active}, native=${rom[0xcc6f]:x2}/${rom[0xd300]:x2}; Link=${rom[0xd004]:x2}, companion=${rom[0xd104]:x2}, air=${rom[0xcc5c]:x2}.");
                    FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": companion/equipped item sound order differs.");
                    OracleRandomState random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            Step();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _dialogue.ShowGameplayMessage("Companion input", 100); rom[0xcba0] = 1;
                Step(4, 3); _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, 3);
                FailIf(!_player.CompanionRideActive || rom[0xd104] != 5 || rom[0xd105] != 0 ||
                    _player.IsUsingShield || _player.IsUsingBoomerang,
                    "Riding Link must retain consumed dialogue edges and reject ordinary equipped parents.");
                Step(); Step(1, 1); Step();
                int remaining = 100;
                while ((rom[0xd104] != 5 || rom[0xd105] != 0) && remaining-- > 0) Step();
                FailIf(!_player.CompanionRideActive || rom[0xd104] != 5 || rom[0xd105] != 0,
                    $"Native companion ${id:x2} attack must finish before another fresh edge.");
            }
            Step(1, 2); Step(40, button);
            FailIf(_player.CompanionRideActive || _player.CompanionJumpActive,
                "Companion item fixture must complete native dismount and landing.");
            Step(); Step(1, button); Step(2, button);
            FailIf(item == TreasureId.Shield ? !_player.IsUsingShield : !_player.IsUsingBoomerang,
                "A fresh post-dismount edge must restore the equipped item.");
            Step(80); Step(1, button); Step(80);
            FailIf(_player.IsUsingShield || _player.IsUsingBoomerang,
                "Released repeated equipped item must finish after the companion handoff.");
        }
        GD.Print("Validated native Ricky/Dimitri/Moosh equipped Shield/Boomerang rejection, consumed dialogue edges, repeated species attacks, dismount/air/landing, fresh item reuse and cues/RNG through split/batched gameplay.");
    }
}
