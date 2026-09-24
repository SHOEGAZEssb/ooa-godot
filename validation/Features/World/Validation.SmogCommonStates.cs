using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmogCommonStates()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(SmogCharacter).GetProperty("State", flags)!;
        var counter2 = typeof(SmogCharacter).GetProperty("Counter2", flags)!;
        var subidProperty = typeof(SmogCharacter).GetProperty("SubId", flags)!;
        foreach (bool batched in new[] { false, true })
        foreach (int nativeState in new[] { 1, 2, 3, 4, 5, 6, 7 })
        foreach (int subid in new[] { 0, 1, 2, 0x82, 3, 0x83, 4, 5, 6 })
        {
            LoadValidationRoom(0, 0x60);
            _entities.Clear();
            _player.WarpTo(new(24, 24));
            var actor = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(120, 88), subid == 4 ? 3 : subid));
            // Stage the native common state directly, isolating its dispatch
            // from the source system that would write the state byte.
            state.SetValue(actor, nativeState);
            subidProperty.SetValue(actor, subid);
            actor.Counter1 = 37;
            counter2.SetValue(actor, 29);
            int animation = actor.AnimationFrame;
            int random = _entities.RandomCalls;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(actor.State != nativeState || actor.SubId != subid || actor.IsDead ||
                actor.Position != new Vector2(120, 88) || actor.Counter1 != 37 || actor.Counter2 != 29 ||
                actor.AnimationFrame != animation || _entities.RandomCalls != random || _entities.RoomEnemyCount != 1,
                $"Smog state${nativeState:x2}/subid${subid:x2} must return before subid handling, animation, timers or allocations.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
