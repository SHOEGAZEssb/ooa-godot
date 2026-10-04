using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAnimationGameplayRom()
    {
        var ticks = typeof(NpcCharacter).GetField("_animationTicks", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int hostCase1 = 0;
        foreach (bool text in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x66);
            _player.WarpTo(new(24, 112), recordSafe: false);
            var npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3b);
            // Establish the ordinary NPC's distant-Link facing before isolating
            // its animation clock. femaleVillager's subid1 script calls the
            // shared facing/animation path; Link stays beyond its $28 range.
            StepGameplayUpdates(1, Vector2.Zero);
            string encoded = npc.Record.DownAnimation;
            var definition = OracleGraphicsCache.GetAnimationDefinition(encoded);
            var rom = new ObjectAnimationRom(0, 0x3b);
            bool frozen = false;
            _entities.TextActiveSource = () => text && frozen;
            _entities.InitializedObjectsDisabledSource = () => !text && frozen;
            int observed = 0;
            void Compare()
            {
                observed++;
                if (frozen) rom.FrozenInteractionUpdate(text);
                else rom.Advance();
                var frame = definition.Frames[npc.CurrentAnimationFrame];
                int counter = frame.Duration - (int)(double)ticks.GetValue(npc)!;
                FailIf(counter != rom.Counter || npc.CurrentAnimationParameter != rom.Parameter || frame.EncodedOam != rom.Oam,
                    $"ROM gameplay animation update={observed}, frozen={frozen}, text={text}, batch={batched}: counter={counter}/${rom.Counter:x2}.");
            }
            npc.SetScriptAnimation(encoded); rom.Set(2);
            StepGameplayUpdates(15, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = true;
            StepGameplayUpdates(40, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = false;
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            StepGameplayUpdates(31, Vector2.Zero, batched: batched, afterUpdate: Compare);
            // Explicit same-animation requests restart even at a frame edge.
            npc.SetScriptAnimation(encoded); rom.Set(2);
            StepGameplayUpdates(33, Vector2.Zero, batched: batched, afterUpdate: Compare);
            npc.SetAnimationCounter(0); rom.Counter = 0;
            // Publish the first decrement ($ff), freeze, then cross the exact
            // wrap boundary after resumption through the real application loop.
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = true;
            StepGameplayUpdates(8, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = false;
            StepGameplayUpdates(256, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(observed != 385, "Animation gameplay fixture missed an application update.");
        }
        GD.Print("Validated ROM animation clocks through gameplay updates: text/initialized-object freezes, exact resume boundary, repeated resets and zero-counter wrapping in individual/batched host frames.");
    }
}
