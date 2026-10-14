using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePirateShipScrollPreload()
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
            FailIf(_currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.Water,
                "Pirate scrolling fixture starts outside swimmable $d6 sea geometry.");
            FailIf(_entities.EntityAdapters<PirateShipRoomEntity>().Any(), "Offscreen $d7 ship spawned in source room $d6.");
            for (int update = 0; !_transitions.ScrollActive && update < 80; update++)
                StepGameplayUpdates(1, Vector2.Right, ["move_right"], ["move_right"], batched);
            FailIf(!_transitions.ScrollActive || _currentRoom.Id != 0xd7,
                "Swimming through $d6's east boundary did not start the actual $d7 scroll.");
            var ship = _entities.EntityAdapters<PirateShipRoomEntity>().SingleOrDefault();
            FailIf(ship is null, "initializeRoom omitted the reserved pirate ship before scrolling preload.");
            var actor = (NpcCharacter)ship!.Node;
            Vector2 position = new(_saveData.ReadWramByte(0xc6ee), _saveData.ReadWramByte(0xc6ed));
            byte direction = _saveData.ReadWramByte(0xc6ef);
            FailIf(_entities.InteractionSlot(actor) != 1 || ship.State != 1 || !actor.Visible ||
                actor.Position != position || actor.TransitionDrawOffset != new Vector2(160, 0) ||
                actor.CurrentAnimationOpaquePixels == 0,
                "Destination $c2:$00 lacks source state-0 visibility, position, graphics, or reserved $d1 allocation before scrolling.");
            int animationFrame = actor.CurrentAnimationFrame;
            bool enteredViewport = false;
            void CheckFrozen()
            {
                FailIf(!actor.Visible || ship.State != 1 || actor.Position != position ||
                    actor.CurrentAnimationFrame != animationFrame ||
                    _saveData.ReadWramByte(0xc6ec) != 0xd7 ||
                    _saveData.ReadWramByte(0xc6ee) != (int)position.X ||
                    _saveData.ReadWramByte(0xc6ed) != (int)position.Y ||
                    _saveData.ReadWramByte(0xc6ef) != direction,
                    "Pirate ship/course advanced or disappeared during destination scrolling.");
                if (_transitions.ScrollActive && actor.Position.X + actor.TransitionDrawOffset.X < 152)
                    enteredViewport = true;
            }
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Right, ["move_right"],
                batched: batched, afterUpdate: CheckFrozen);
            FailIf(_transitions.ScrollActive || !enteredViewport || actor.TransitionDrawOffset != Vector2.Zero,
                "Pirate ship did not enter the visible gameplay field before scroll completion.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!ReferenceEquals(ship, _entities.EntityAdapters<PirateShipRoomEntity>().Single()) ||
                actor.Position != position + Vector2.Right || _saveData.ReadWramByte(0xc6ee) != (int)position.X + 1,
                "Finishing the scroll duplicated the ship or lost the shared even-update course phase.");

            // Return through the other edge with the outgoing $d1 slot retained,
            // then enter again. Only room initialization recreates the ship.
            _transitions.BeginScroll(_player, Vector2I.Left, 0xd6);
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched);
            FailIf(_entities.EntityAdapters<PirateShipRoomEntity>().Any(), "Outgoing pirate ship survived scroll cleanup in $d6.");
            _transitions.BeginScroll(_player, Vector2I.Right, 0xd7);
            ship = _entities.EntityAdapters<PirateShipRoomEntity>().SingleOrDefault();
            FailIf(ship is null || !ship.Node.Visible || ship.State != 1,
                "Repeating the sea scroll omitted initialized pirate ship presentation.");
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched);
        }
        GD.Print("Validated pirate ship initialization before both eras' real sea scrolling, visible/frozen destination presentation, course resumption, outgoing cleanup and repeated entry with split/batched updates.");
    }
}
