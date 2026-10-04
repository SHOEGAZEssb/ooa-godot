using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShieldUnderwaterRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int level in new[] { 1, 2, 3 })
        foreach (int terrain in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (terrain != 1) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(TreasureId.Shield, level);
            _inventory.EquipA(primary ? TreasureId.Shield : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Shield);
            LoadValidationRoom(terrain == 0 ? 2 : 7, terrain == 0 ? 0x90 : 0x05);
            _entities.Clear();
            Vector2 point = terrain == 0 ? new(88, 72) : new(40, 56);
            if (terrain == 0)
            {
                FailIf((_currentRoom.TilesetFlags & 0x40) == 0, "Room $2:$90 must retain its source underwater flag.");
                // Isolate the tileset eligibility gate from deep-water travel,
                // which has its own remaining movement/transition audit.
                _currentRoom.SetPositionTileAndCollision(point, 0xa0, 0, 0);
            }
            _player.WarpTo(point);
            FailIf(_collision.Collides(point), "Shield terrain fixture must begin in walkable room geometry.");
            var rom = new SideViewRom(_saveData, _random.CaptureState(), _currentRoom, point, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            int button = primary ? 1 : 2;
            void Step(int count = 1, int held = 0, int pressed = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(pressed: edge, held: held);
                    edge = 0;
                    FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                        (int)typeof(Player).GetField("_shieldParentButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)! != (rom[0xd500] == 0 ? 0 : rom[0xd503]) ||
                        (bool)typeof(Player).GetField("_shieldParentInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)! != (rom[0xd504] != 0) ||
                        !sounds.Requests.Where(id => id == SoundId.SndShield).SequenceEqual(rom.Sounds.Where(id => id == SoundId.SndShield)),
                        $"Shield underwater/swim terrain={terrain} L{level} A={primary} update={++update}: raised={_player.IsUsingShield}/${rom[0xcc6f]:x2}, sound runtime={string.Join(',', sounds.Requests)} ROM={string.Join(',', rom.Sounds)}, group/flags=${rom[0xcc2d]:x2}/${rom[0xcc34]:x2}.");
                });
            }
            Step(); // Enter the actual side-view water before pressing Shield.
            if (terrain != 0)
                FailIf(!_player.SideScrollSwimming || rom[0xcc5d] == 0,
                    "Room $7:$05 did not enter native swimming before the shield gate.");
            Step(1, button, button);
            Step(12, button);
            Step();
            Step(1, button, button);
            Step(12, button);
            Step();
            FailIf(_player.IsUsingShield || rom[0xd500] != 0 ||
                sounds.Requests.Contains(SoundId.SndShield),
                "Underwater/swimming shield retained its parent or initialized a shield cue.");
        }
        GD.Print("Validated clean-US Shield underwater-room and side-view Flippers/Mermaid Suit swimming gates, A/B and every level, held input, release/repeat and silent rejection through split/batched application updates.");
    }
}
