using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateInteractionSlotOrder()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var add = (Func<IRoomEntity,IRoomEntity>)typeof(RoomEntityManager).GetMethod("AddEntity",flags)!
            .CreateDelegate(typeof(Func<IRoomEntity,IRoomEntity>),_entities);
        foreach (bool batch in new[] { false,true })
        {
            LoadValidationRoom(0,0x60);
            _entities.Clear();
            _player.BeginCutsceneControl();
            var trace = new List<string>();
            var actors = new Dictionary<string,InteractionSlotValidationEntity>();
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            void Spawn(string name,int expectedSlot,Action<InteractionSlotValidationEntity>? dispatch = null)
            {
                var puff = new PuzzlePuffEffect { Name = name };
                puff.Initialize(new(80,64),SoundId.MusNone);
                var actor = new InteractionSlotValidationEntity(puff,self => {
                    trace.Add(name);
                    dispatch?.Invoke(self);
                });
                actors.Add(name,actor);
                add(actor);
                FailIf(_entities.InteractionSlot(puff) != expectedSlot,
                    $"Native interaction {name} must allocate slot $d{expectedSlot:x1}.");
            }
            Spawn("A",2,self => {
                if (self.Updates == 1)
                    Spawn("C",4,child => { if (child.Updates == 1) Spawn("D",5); });
                if (self.Updates == 2) self.Finished = true;
            });
            Spawn("B",3,self => {
                if (self.Updates != 2) return;
                Spawn("E",2); // A deleted itself earlier in this pass.
                Spawn("F",6);
            });
            FailIf(trace.Count != 0,"Allocation must not itself dispatch native interaction state0.");
            Step(3);
            // bank0.s updateInteractions increments hActiveObject after every
            // call, reads enabled at each live slot, and never rewinds it.
            FailIf(string.Join(",",trace) != "A,B,C,D,A,B,C,D,F,E,B,C,D,F",
                "Native interaction traversal lost same-pass child chains, lower-slot deferral or slot reuse order.");
            FailIf(actors["E"].Updates != 1 || actors["F"].Updates != 2,
                "A lower-slot replacement waits one update; a higher-slot child starts immediately.");
            trace.Clear();
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Spawn("G",7);
                Step(2);
                FailIf(string.Join(",",trace) != "G",
                    "Text dispatch must initialize state0 once and freeze initialized native interactions.");
            }
            finally { _entities.TextActiveSource = text; }
            trace.Clear();
            Step(1);
            FailIf(string.Join(",",trace) != "E,B,C,D,F,G",
                "Text completion must resume native slot order, including the reused slot $d2.");
            _entities.Clear();
            trace.Clear();
            Step(2);
            FailIf(trace.Count != 0,"Clearing the room must remove all native slot dispatches.");
        }
        _player.EndCutsceneControl();
    }
}
