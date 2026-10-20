using Godot;
using System.Linq;

namespace oracleofages;

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
                _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0,
                "0:65 source state-zero linked/third-Essence/completed gate exposed an actor or locked control.");
        }
        foreach (bool batched in new[] { false, true })
        {
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
            FailIf(!_player.NativeForcedMovementActive || !_roomEvents.MenusDisabled || _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0x81 ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wScrollMode)!=0 || _saveData.HasGlobalFlag(0x38),
                "0:65 rescued initialization must own raw masks and scrolling before completion.");
            _roomEvents.Get<ZeldaRescueEvent>().Cancel();
            FailIf(_roomEvents.MenusDisabled || _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0 ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wScrollMode)!=1 || _saveData.HasGlobalFlag(0x38) ||
                _entities.Entities<NpcCharacter>().Any(n=>n.Active) || _player.NativeForcedMovementActive,
                "0:65 cancellation must release native actors/control without writing the completion flag.");
            LoadValidationRoom(0,0x65); StepGameplayUpdates(3,Vector2.Zero,batched:batched);
            FailIf(!_roomEvents.MenusDisabled || _entities.Entities<NpcCharacter>().Count(n=>n.Active)!=3,
                "0:65 cancellation must permit the source sequence to restart on re-entry.");
        }
        GD.Print("Validated room $0:$65 independent source gates, scrolling state-zero freeze, cancellation, raw control cleanup and rescued restart.");
    }
}
