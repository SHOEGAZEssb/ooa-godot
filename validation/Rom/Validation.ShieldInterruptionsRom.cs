using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShieldInterruptionsRom()
    {
        int fixture = 0;
        foreach (int item in new[] { TreasureId.Bombs, TreasureId.Boomerang,
            TreasureId.CaneOfSomaria, TreasureId.Shovel, TreasureId.SeedSatchel,
            TreasureId.Shooter, TreasureId.Bracelet })
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x91); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Shield, 1);
            _inventory.GiveTreasure(item, item == TreasureId.Bombs ? 0x10 : 1);
            _inventory.GiveTreasure(TreasureId.EmberSeeds, 0x10);
            _inventory.SelectSatchelSeeds(0); _inventory.SelectShooterSeeds(0);
            _inventory.EquipA(primary ? TreasureId.Shield : item);
            _inventory.EquipB(primary ? item : TreasureId.Shield);
            _player.WarpTo(new(120, 128)); StepGameplayUpdates(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112) || _collision.Collides(_player.Position),
                "Shield interruption must approach through room $4:$91's actual entrance.");
            _player.Face(Vector2I.Up);
            OracleRandomState seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 112);
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int shield = primary ? 1 : 2, opposite = primary ? 2 : 1;
            int previous = 0, update = 0;
            T Field<T>(string name) => (T)typeof(Player).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~previous; previous = held;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter); edge = 0;
                    string context = $"Shield interruption ITEM${item:x2} A={primary} batch={batched} update={++update}";
                    FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                        Field<int>("_shieldParentButton") != (rom[0xd500] == 0 ? 0 : rom[0xd503]) ||
                        Field<bool>("_shieldParentInitialized") != (rom[0xd504] != 0),
                        context + $": raised/parent/state runtime={_player.IsUsingShield}/{Field<int>("_shieldParentButton")}/{Field<bool>("_shieldParentInitialized")}, native=${rom[0xcc6f]:x2}/${rom[0xd500]:x2}/${rom[0xd504]:x2}; lower slots=${rom[0xd200]:x2}/${rom[0xd300]:x2}/${rom[0xd400]:x2}.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        context + ": Link position differs.");
                    FailIf(!sounds.Requests.Where(id => id == SoundId.SndShield).SequenceEqual(rom.Sounds.Where(id => id == SoundId.SndShield)),
                        context + ": Shield initialization cues differ.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": item sound order differs.");
                    OracleRandomState random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(2, shield);
                FailIf(!_player.IsUsingShield, "Shield must raise before the opposite item interruption.");
                Step(3, shield | opposite);
                _dialogue.ShowMessage("Shield interruption pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3, shield | opposite);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(16, shield | opposite);
                // A released Shooter shoots; Bracelet releases its wall pull.
                // Physical Bombs/Boomerangs/blocks may outlive their parent.
                Step(1, shield);
                if (item == TreasureId.Bombs) { Step(1, shield | opposite); Step(1, shield); }
                int remaining = 100;
                while (!_player.IsUsingShield && remaining-- > 0) Step(1, shield);
                FailIf(!_player.IsUsingShield, $"ITEM${item:x2} did not release its parent back to held Shield.");
                Step(2, shield); Step();
                FailIf(_player.IsUsingShield, "Released Shield retained its active parent.");
            }
            if (item == TreasureId.Bombs)
            {
                // With no retained Shield parent, $c2/$83 grab state prevents
                // allocation. Throwing clears the grab byte later in the
                // parent pass; allocation resumes on the following update.
                Step(3, opposite); Step(3, shield | opposite);
                FailIf(rom[0xd500] != 0 || _player.IsUsingShield,
                    "Lifting Bomb must reject a newly pressed Shield parent.");
                Step(16, shield | opposite); Step(1, shield);
                Step(1, shield | opposite); Step(12, shield);
                FailIf(!_player.IsUsingShield, "New Shield did not initialize after the Bomb grab ended.");
                Step();
            }
        }
        GD.Print("Validated ROM Shield interruptions by Bombs, Boomerang, Cane, Shovel, Satchel, Shooter and Bracelet: A/B, retained parent/state/cue, pause, terminal resumption and repeat through split/batched gameplay.");
    }
}
