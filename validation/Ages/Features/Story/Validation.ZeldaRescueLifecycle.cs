using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateZeldaRescueLifecycle()
    {
        // Independent state-zero gates; the saved bit alone does not create Zelda
        // without Impa's preceding moved flag (native @init7 branch precedence).
        foreach (var gate in new[] { (false, true, false), (true, false, false), (true, true, true) })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(gate.Item1);
            if (gate.Item2) _inventory.GiveTreasure(TreasureId.Essence, 2);
            _saveData.SetGlobalFlag(0x38, gate.Item3);
            LoadValidationRoom(0, 0x65); StepGameplayUpdates(3, Vector2.Zero);
            FailIf(_entities.Entities<NpcCharacter>().Any(n=>n.Active) || _roomEvents.MenusDisabled ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=0,
                "0:65 source state-zero linked/third-Essence/completed gate exposed an actor or locked control.");
        }
        foreach (bool batched in new[] { false, true })
        {
            // impaInCutscene.s:@init7 and bird.s:@initSubid04 delete both
            // slots in an unlinked game. Flush their queued frees explicitly:
            // synchronous validation otherwise never reaches Godot's end-frame
            // deletion before the next room load cancels the retained hosts.
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x65);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            DrainRetiredValidationNodes();
            _player.WarpTo(new(0x9a, 0x70));
            _transitions.BeginScroll(_player, Vector2I.Right, 0x66);
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched);
            FailIf(_activeGroup != 0 || _currentRoom.Id != 0x66 || _transitions.IsTransitioning ||
                _roomEvents.Get<ZeldaRescueEvent>().HasState || _roomEvents.MenusDisabled,
                "Entering $0:$66 from the left retained deleted $0:$65 rescue actors or event control.");

            // Approach the actual house doorway from the walkable tile below.
            int door = Enumerable.Range(0, 80).Select(index => (index / 10) * 16 + index % 10).Single(position =>
                WarpDatabase.IsWarpTile(_currentRoom.ActiveCollisions,
                    _currentRoom.GetMetatile(new Vector2((position & 15) * 16 + 8, (position >> 4) * 16 + 8))));
            _player.WarpTo(new((door & 15) * 16 + 8, (door >> 4) * 16 + 24));
            StepGameplayUpdates(24, Vector2.Up, batched: batched);
            StepGameplayUpdates(160, Vector2.Zero, batched: batched);
            FailIf(_activeGroup != 2 || _currentRoom.Id != 0xe7 || _transitions.IsTransitioning ||
                _roomEvents.MenusDisabled || _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0,
                $"$0:$66 house warp must reach $2:$e7 and release arrival control after rescue cancellation; actual ${_activeGroup:x}:${_currentRoom.Id:x2}, Link={_player.Position}, door=${door:x2}.");
            Vector2 arrival = _player.Position;
            StepGameplayUpdates(8, Vector2.Up, batched: batched);
            FailIf(_player.Position == arrival,
                "$2:$e7 house arrival left Link unable to move after entering $0:$66 from the left.");

            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(true);
            _inventory.GiveTreasure(TreasureId.Essence, 2);
            LoadValidationRoom(0, 0x66);
            _player.WarpTo(new(0,0x70));
            _transitions.BeginScroll(_player, Vector2I.Left, 0x65);
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched:batched);
            var actors=_entities.EntityAdapters<ZeldaRescueActor>().ToArray();
            FailIf(actors.Length!=2 || actors.Any(a=>a.State!=1||!a.Node.Visible),
                "0:65 scrolling preload must resolve both native state-zero actors.");
            FailIf(_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x31).Position!=new Vector2(0x48,0x68) ||
                _entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x4c).ScriptDrawOffset!=Vector2.Zero ||
                _entities.Entities<NpcCharacter>().Any(n=>n.ScriptButtonSensitive),
                "0:65 incoming native actors advanced scripts or gravity during scrolling.");
            _saveData.SetGlobalFlag(0x39); _saveData.SetGlobalFlag(0x3c);
            LoadValidationRoom(0,0x65); _player.WarpTo(new(0x50,0x78));
            StepGameplayUpdates(75,Vector2.Zero,batched:batched);
            FailIf(!_player.NativeForcedMovementActive || !_roomEvents.MenusDisabled || _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=0x81 ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wScrollMode)!=0 || _saveData.HasGlobalFlag(0x38),
                "0:65 rescued initialization must own raw masks and scrolling before completion.");
            _roomEvents.Get<ZeldaRescueEvent>().Cancel();
            FailIf(_roomEvents.MenusDisabled || _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=0 ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wScrollMode)!=1 || _saveData.HasGlobalFlag(0x38) ||
                _entities.Entities<NpcCharacter>().Any(n=>n.Active) || _player.NativeForcedMovementActive,
                "0:65 cancellation must release native actors/control without writing the completion flag.");
            LoadValidationRoom(0,0x65); StepGameplayUpdates(3,Vector2.Zero,batched:batched);
            FailIf(!_roomEvents.MenusDisabled || _entities.Entities<NpcCharacter>().Count(n=>n.Active)!=3,
                "0:65 cancellation must permit the source sequence to restart on re-entry.");
        }
        GD.Print("Validated room $0:$65 independent source gates, scrolling state-zero freeze, cancellation, raw control cleanup and rescued restart.");
    }
}
