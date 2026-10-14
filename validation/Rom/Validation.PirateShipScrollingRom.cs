using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePirateShipScrollPreloadRom()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            int group = linked ? 0 : 1;
            _saveData.WriteWramByte(0xc6ec, 0xd7);
            _saveData.WriteWramByte(0xc6ed, 0x18);
            _saveData.WriteWramByte(0xc6ee, 0x48);
            _saveData.WriteWramByte(0xc6ef, 1);
            LoadValidationRoom(group, 0xd6);
            _player.WarpTo(new(0x88, 0x18));
            for (int update = 0; !_transitions.ScrollActive && update < 80; update++)
                StepGameplayUpdates(1, Vector2.Right, ["move_right"], ["move_right"], batched);
            FailIf(!_transitions.ScrollActive, "Pirate ROM fixture failed to enter $d7 through source sea geometry.");
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom,
                1, (int)_player.Position.X, (int)_player.Position.Y);
            rom[0xcc01] = linked ? (byte)1 : (byte)0;
            rom[0xcd00] = 8;
            rom.InitializeRoomInteractions();
            // Executed initializeRoom must allocate $c2:$00 in reserved $d1,
            // before any scrolling object updates or cutscene01 course ticks.
            const int slot = 0xd140;
            FailIf(rom[slot] != 1 || rom[slot + 1] != 0xc2 || rom[slot + 2] != 0 || rom[slot + 4] != 0,
                "Clean-US initializeRoom did not allocate uninitialized sailing ship in reserved $d1.");
            rom.AdvanceInteractions(_entities.FrameCounter);
            var ship = _entities.EntityAdapters<PirateShipRoomEntity>().SingleOrDefault();
            FailIf(ship is null, "Port failed clean-US initializeRoom's pre-scroll ship allocation.");
            var actor = (NpcCharacter)ship!.Node;
            void Compare()
            {
                FailIf(ship.State != rom[slot + 4] || actor.Visible != ((rom[slot + 0x1a] & 0x80) != 0) ||
                    actor.ZIndex != ObjectDrawPriority.FromVisible(rom[slot + 0x1a]) ||
                    actor.Position != new Vector2(rom[slot + 13], rom[slot + 11]) ||
                    actor.CurrentAnimationParameter != rom[slot + 0x21] ||
                    _saveData.ReadWramByte(0xc6ec) != rom[0xc6ec] ||
                    _saveData.ReadWramByte(0xc6ed) != rom[0xc6ed] ||
                    _saveData.ReadWramByte(0xc6ee) != rom[0xc6ee] ||
                    _saveData.ReadWramByte(0xc6ef) != rom[0xc6ef],
                    "Pirate ship scrolling visibility/state/pose or frozen global course differs from clean-US initialization/interaction dispatch.");
            }
            Compare();
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                // Destination initialized interactions stay frozen even on
                // cutscene00's terminal scroll update. Compare every update.
                rom.AdvanceInteractions(_entities.FrameCounter);
                Compare();
            });
            FailIf(_transitions.ScrollActive, "Pirate ROM scroll comparison did not reach completion.");
        }
        GD.Print("Compared executed clean-US initializeRoom's reserved pirate ship allocation, scrolling state-zero visibility/pose and frozen course with split/batched gameplay in both eras.");
    }
}
