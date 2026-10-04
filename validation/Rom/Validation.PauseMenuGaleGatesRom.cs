using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuGaleGatesRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int group in new[] { 0, 1 })
        foreach (bool introDone in new[] { false, true })
        foreach (int keys in new[] { 4, 8, 12 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            if (introDone) _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _saveData.SetRoomFlag(group, group == 0 ? 0x13 : 0x08, 0x10);
            LoadValidationRoom(group, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1); _inventory.GiveTreasure(0x23, 0x20);
            _inventory.SelectSatchelSeeds(3);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.SeedSatchel);
            _inventory.ApplyDamage(_inventory.HealthQuarters - 1); _statusBar.SynchronizeHealth();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            _player.WarpTo(new(80.25f, 64.5f)); _player.Face(Vector2I.Down);
            var randomSeed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, randomSeed, _currentRoom, 2, 80, 64);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40; rom.InitializeLinkGameplay();
            var menus = rom.CreateMenuView();
            rom[0xcbe4] = rom[0xc6aa]; rom[0xcbe5] = rom[0xc6ad]; rom[0xcbe6] = rom[0xc6ae];
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            static bool Relevant(int sound) => sound is 0x60 or 0x5a or 0x54 or 0x55;
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    menus.AdvancePalette(); menus.Update(edge, held, _saveData.ReadWramByte(0xc622));
                    if (rom[0xcbcb] == 0) rom.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter);
                    edge = 0;
                    string context = $"Gale menu gates era={group} A={primary} intro={introDone} keys=${keys:x2} batch={batched} update={++update}";
                    FailIf(_menuLifecycle.IsActive != (rom[0xcbcb] != 0) ||
                        _player.GaleActive != (rom[0xcc4f] == 7 || rom[0xd004] == 7),
                        context + ": menu/capture ownership differs.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        _player.ObjectZHigh != rom[0xd00f], context + ": captured Link position/height differs.");
                    FailIf(!sounds.Requests.Where(Relevant).SequenceEqual(rom.Sounds.Where(Relevant)),
                        context + $": native error/warning/menu cue order differs: runtime={string.Join(',', sounds.Requests.Where(Relevant))}, native={string.Join(',', rom.Sounds.Where(Relevant))}.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - randomSeed.Calls != rom.RandomCalls, context + ": shared seed/capture RNG differs.");
                });
            }
            int button = primary ? 1 : 2;
            Step(1, button, button);
            while (!_player.GaleActive && update < 60) Step();
            FailIf(!_player.GaleActive || rom[0xcc02] != 1 || rom[0xcc4f] != 7,
                "Actual outdoor Gale collision must publish wMenuDisabled=$01 and pending Link state $07.");
            _saveData.WriteWramByte(0xc622, 63);
            Step(1, keys, keys); // Global $40: IntroDone precedes the native Gale mask.
            Step(3, held: keys);
            Step(); Step(1, keys, keys);
            Step(57, held: keys); Step(); Step(1, keys, keys); // Global $80, during ascent.
            FailIf(_menuLifecycle.IsActive || !_player.GaleActive || rom[0xcc02] != 1 ||
                sounds.RequestsFor(0x60) != 0 || sounds.RequestsFor(0x5a) != (introDone ? 0 : 3),
                "Pending/initialized/ascent Gale must suppress warnings and menus after the native IntroDone error gate.");
        }
        GD.Print("Validated clean-US outdoor Gale collision-published menu mask, pending/initialized/ascent Link states, Start/Select/chord IntroDone cue precedence, held/fresh edges, two low-health warning boundaries, fixed Link motion and shared RNG through split/batched gameplay. Destination-menu and return flows remain covered separately.");
    }
}
