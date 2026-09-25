using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogControllerAdapter()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
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
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
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

            // Empty native pages still receive the medium-cloud $47 write.
            // Allocation sets enabled only; puff initialization never clears it.
            _entities.Clear();
            _player.WarpTo(new(8,8));
            for (int i = 0; i < 3; i++)
                _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72 + i * 24,72),3));
            Step(5);
            var puffNode = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),0));
            var puff = System.Linq.Enumerable.Single(_entities.EntityAdapters<PuzzlePuffRoomEntity>());
            FailIf(_entities.InteractionSlot(puffNode) != 2 || puff.Counter2Alias != 60,
                "The first puff must inherit the disabled INTERACTION$d2 counter2 written by ENEMY$d2.");
            Step(19);
            FailIf(puffNode.Finished || puff.Counter2Alias != 60,
                "Puff initialization and animation must preserve the inherited unused counter2.");
            Step(1);
            FailIf(!puffNode.Finished, "Inherited counter2 must not change puff deletion on update20.");
            var nextPuff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),0));
            var nextAdapter = System.Linq.Enumerable.Single(_entities.EntityAdapters<PuzzlePuffRoomEntity>());
            FailIf(_entities.InteractionSlot(nextPuff) != 2 || nextAdapter.Counter2Alias != 0,
                "Deleting the first puff must clear the inherited byte before reuse.");
            foreach (bool killPuff in new[] { false,true })
            foreach (bool inherit in new[] { false,true })
            {
                _entities.Clear();
                for (int i = 0; i < 3; i++)
                    _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72 + i * 24,72),3));
                Step(inherit ? 5 : 4);
                Node2D SpawnEffect() => killPuff
                    ? _entities.Spawn<KillEnemyPuffEffect>(new KillEnemyPuffSpawn(new(24,24)))
                    : _entities.Spawn<ClinkEffect>(new EnemyClinkSpawn(new(24,24)));
                byte Counter() => killPuff
                    ? System.Linq.Enumerable.Single(_entities.EntityAdapters<KillPuffRoomEntity>()).Counter2Alias
                    : System.Linq.Enumerable.Single(_entities.EntityAdapters<SwordBeamClinkRoomEntity>()).Counter2Alias;
                var effect = SpawnEffect();
                FailIf(_entities.InteractionSlot(effect) != 2 || Counter() != (inherit ? 60 : 0),
                    "INTERAC$07/$08 must inherit inactive counter2 only when the medium cloud already wrote it.");
                Step(2);
                int elapsed = effect is KillEnemyPuffEffect kill ? kill.ElapsedFrames : ((ClinkEffect)effect).ElapsedFrames;
                FailIf(Counter() != 60 || elapsed != 2,
                    "Smog's same-page write must leave clink/kill-puff animation advancing and counter2 unchanged.");
                Step(30);
                FailIf(_entities.EntityAdapters<KillPuffRoomEntity>().Any() ||
                    _entities.EntityAdapters<SwordBeamClinkRoomEntity>().Any(),
                    "The unused counter2 must not delay either effect until its value reaches zero.");
                SpawnEffect();
                FailIf(Counter() != 0, "Deleted clink/kill-puff pages must not pass counter2 to their replacement.");
            }
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Smog controller adapter entry wait, player lift/lower and one-shot reset effects through single/batched gameplay updates.");
    }
}
