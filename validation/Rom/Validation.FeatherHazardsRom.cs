using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareFeatherHazardsRom()
    {
        int hostCase1 = 0;
        foreach (byte tile in new byte[] { 0xf3, 0xfa })
        foreach (bool flippers in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            FeatherRom rom = PrepareFeatherRom(primary);
            var sounds = _sound.AttachPlayRequestAudit();
            if (flippers)
            {
                _inventory.GiveTreasure(TreasureId.Flippers, 0);
                rom[0xc69f] = 0x40;
            }
            // Link starts on floor outside the hazard and reaches it through
            // the actual jump, without teleporting onto the hazard tile.
            for (int y = 72; y <= 104; y += 16)
            for (int x = 136; x <= 168; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), tile, 0x10, 0);
            rom.CopyRoom(_currentRoom);
            StepFeatherRom(rom, 1, batched, 8, held: true, pressed: true, primary: primary);
            StepFeatherRom(rom, 29, batched, 8, primary: primary);
            FailIf(!rom.Airborne || _player.IsDrowning || _player.IsFallingInHole || _player.TopDownSwimming,
                "Feather must cross its hazard without entering a grounded terrain state before landing.");
            StepFeatherRom(rom, 1, batched, 8, primary: primary);
            FailIf(tile == 0xf3 && rom[0xcc9b] != 4,
                "Feather landing must establish the native hole standing counter $04 after its first terrain application.");
            FailIf(tile == 0xfa && flippers && (_player.TopDownSwimmingState != 2 || rom[0xcc5d] != 2),
                "Feather must enter swimming state2 on the landing update, before ground movement.");
            FailIf(!sounds.Requests.Where(id => id is SoundId.SndJump or SoundId.SndLand).SequenceEqual(rom.Sounds.Where(id => id is SoundId.SndJump or SoundId.SndLand)),
                "Feather hazard landing must preserve native jump/land sound order.");
            // B remains available to the native item dispatcher while
            // Flippers give A directly to swimming strokes. Probe Feather's
            // blocked-use gate through B in the grounded hazard phase.
            _inventory.EquipA(0);
            _inventory.EquipB(TreasureId.Feather);
            rom[0xc689] = 0;
            rom[0xc688] = 0x17;
            int update = 0;
            void Compare()
            {
                rom.Update(held: true, pressed: update == 0, primary: false);
                CompareFeatherRom(rom, $"Feather hazard ${tile:x2}, flippers={flippers}, update={update++}, batch={batched}");
                FailIf(_player.NativeNormalStateForInteraction != (rom[0xd004] == 1) ||
                    _player.HealthQuarters != rom[0xc6aa] || _player.TopDownSwimmingState != (rom[0xcc5d] & 0x0f) && !_player.IsDrowning,
                    $"Feather hazard ${tile:x2}: terrain ownership differs; native state=${rom[0xd004]:x2}, swim=${rom[0xcc5d]:x2}, counter=${rom[0xcc9b]:x2}, force=${rom[0xcc4f]:x2}; runtime normal={_player.NativeNormalStateForInteraction}, swim={_player.TopDownSwimmingState}, drowning={_player.IsDrowning}, falling={_player.IsFallingInHole}.");
            }
            StepGameplayUpdates(80, Vector2.Zero, ["item"], ["item"], batched, afterUpdate: Compare);
            if (tile == 0xfa && flippers)
                FailIf(!_player.TopDownSwimming || _player.HealthQuarters != 12,
                    "Feather water landing with Flippers must hand off to swimming without damage.");
            else
            {
                FailIf(_player.IsDrowning || _player.IsFallingInHole || _player.HealthQuarters != 10,
                    "Feather hazard landing must finish its native recovery and apply one half-heart of damage.");
                StepFeatherRom(rom, 1, batched, primary: false);
                StepFeatherRom(rom, 1, batched, held: true, pressed: true, primary: false);
                StepFeatherRom(rom, 30, batched, primary: false);
                FailIf(rom.Airborne, "Feather must work again after hazard recovery.");
            }
        }
        GD.Print("Validated executed-ROM Feather hole/water crossing, landing and recovery, Flippers swimming handoff and repeat jump after hazard damage through individual/batched gameplay updates.");
    }
}
