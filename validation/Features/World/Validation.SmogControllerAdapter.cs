using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogControllerAdapter()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach (bool batch in new[] { false,true })
        {
            LoadValidationRoom(0,0x60); _entities.Clear();
            _player.SetSwitchHookPosition(new(120.25f,88.75f),0x5a);
            bool busy = true, reset = false;
            int spawned = 0, puffs = 0, sounds = 0;
            var services = new SmogEncounterServices(
                () => 0, () => busy, () => 3,
                _entities.LockSmogLinkAndMenu, () => {}, value => reset = value,
                _ => spawned++, _ => puffs++, _ => 0, _ => 0x11,
                (_,_) => true, _ => {}, () => sounds++, () => {});
            var entity = new SmogEncounterRoomEntity(new(),new(120,88),services);
            typeof(RoomEntityManager).GetMethod("AddEntity",flags)!.Invoke(_entities,[entity]);
            int slot = _entities.InteractionSlot(entity.Node);
            FailIf(slot != 2,"INTERAC$33 must occupy the first available native dynamic interaction slot, $d2.");
            typeof(RoomEntityManager).GetMethod("WriteSmogInteractionCounter",flags)!.Invoke(_entities,[slot,60]);
            FailIf(entity.Counter2Alias != 60 || entity.Controller.Counter != 0,
                "Smog's $47 alias must reach the live interaction counter2 without changing controller counter1.");
            void Step(int count)
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0,update);
            }
            Step(2);
            FailIf(entity.Controller.State != 0 || spawned != 0 || !_entities.PlayerUpdatesFrozen,
                "INTERAC$33 must retain its live Link lock and wait while boss entry is busy.");
            busy = false; Step(1);
            FailIf(entity.Controller.State != 1 || spawned != 1 || puffs != 1,
                "INTERAC$33 must issue its single intro spawn/puff when entry becomes ready.");
            typeof(SmogEncounterController).GetProperty("State",flags)!.SetValue(entity.Controller,2);
            Step(7);
            FailIf(entity.Controller.State != 3 || _player.SwitchHookZFixed != (-7 * 256 | 0x5a),
                "Smog adapter must lift the actual frozen player by seven high-Z bytes, retaining low Z.");
            Step(1); // Already at phase-zero destination: advance without rewriting fractions.
            Step(7);
            FailIf(entity.Controller.State != 5 || _player.SwitchHookZFixed != 0x5a,
                "Smog adapter must lower the actual player to zh=$00 through eligible interaction updates.");
            typeof(InventoryState).GetProperty(nameof(InventoryState.MaxHealthQuarters))!.SetValue(_inventory,20);
            typeof(InventoryState).GetProperty(nameof(InventoryState.HealthQuarters))!.SetValue(_inventory,16);
            typeof(SmogEncounterController).GetProperty("State",flags)!.SetValue(entity.Controller,8);
            // This ambiguous control owner deliberately diagnoses on a native
            // state query; a changed button must bypass both Link gates.
            typeof(Player).GetField("_cutsceneControlled",flags)!.SetValue(_player,true);
            Step(1);
            typeof(Player).GetField("_cutsceneControlled",flags)!.SetValue(_player,false);
            FailIf(entity.Controller.State != 10 || _inventory.HealthQuarters != 12 || !reset || sounds != 1,
                "Changed Smog button must apply the inventory-owned four-quarter penalty and begin cleanup once.");
            Step(2);
            FailIf(_inventory.HealthQuarters != 12 || sounds != 1,
                "Cleanup updates must not repeat the reset penalty or sound.");
            FailIf(entity.Counter2Alias != 60,
                "INTERAC$33 must retain its unused counter2 byte across phase and reset updates.");
            entity.WriteCounter2Alias(0);
            for (int i = 0; i < 3; i++)
                _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72 + i * 32,72),3));
            Step(4);
            FailIf(entity.Counter2Alias != 0,
                "Merged Smog clouds must not write the interaction alias before their fifth initialization update.");
            Step(1);
            FailIf(entity.Counter2Alias != 60,
                "ENEMY slot$02's medium-cloud initialization must write60 to live INTERACTION slot$02 counter2.");
            bool rejected = false;
            try { _ = ((ISmogEncounterWorld)entity).LinkZHigh; }
            catch (InvalidOperationException) { rejected = true; }
            FailIf(!rejected,"Smog adapter must not retain a player reference outside its active update.");
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Smog controller adapter entry wait, player lift/lower and one-shot reset effects through single/batched gameplay updates.");
    }
}
