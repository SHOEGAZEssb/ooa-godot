using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private async Task ValidateScriptActorRetirement()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x7c);
            var actor = _entities.Entities<PoeCharacter>().Single(npc => npc.Active);
            var binding = new ScriptActorBinding<PoeCharacter>("Poe");
            binding.Bind(actor);
            binding.EnableButton();
            FailIf(!binding.QueueButton(actor), "$0:$7c Poe retirement fixture did not queue its actor button.");
            int retirements = 0;
            bool notifiedBeforeDisposal = false;
            actor.Retired += retired =>
            {
                retirements++;
                notifiedBeforeDisposal = GodotObject.IsInstanceValid(retired);
            };

            _player.WarpTo(new(6, 64));
            _transitions.BeginScroll(_player, Vector2I.Left, 0x7b);
            FailIf(actor.IsRetired || binding.Actor != actor || !binding.ButtonPending || !actor.Visible,
                "$0:$7c -> $0:$7b scroll start retired the outgoing Poe or cleared its binding prematurely.");
            StepGameplayUpdates(_transitions.ScrollTotalFrames - 1, Vector2.Zero, batched: batched);
            FailIf(actor.IsRetired || binding.Actor != actor,
                "Outgoing Poe must retain its binding until clearObjectsWithEnabled2 at scroll completion.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!actor.IsRetired || retirements != 1 || !notifiedBeforeDisposal ||
                binding.Actor is not null || binding.ButtonPending || binding.ButtonSensitive,
                "Poe retirement must notify once before native disposal and detach its actor/button reference.");

            var mode = _scene.ProcessMode;
            _scene.ProcessMode = ProcessModeEnum.Disabled;
            try
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                FailIf(GodotObject.IsInstanceValid(actor), "Outgoing Poe survived the actual engine deletion boundary.");
                binding.Clear();
                binding.Clear();
                try
                {
                    binding.Bind(actor);
                    FailIf(true, "A retired INTERAC_POE $59 must not be rebound as a live script actor.");
                }
                catch (InvalidOperationException exception) when (exception.Message.Contains("Cannot bind retired actor", StringComparison.Ordinal)) { }
            }
            finally { _scene.ProcessMode = mode; }

            // Reusing a binding and clearing it before deletion must detach its
            // notification subscription, without releasing another owner's input.
            var next = _entities.Entities<NpcCharacter>().First(npc => npc.Active);
            var nextBinding = new ScriptActorBinding<NpcCharacter>("Child");
            nextBinding.Bind(next);
            nextBinding.Clear();
            object successor = new();
            _player.BeginCutsceneControl(interruptBracelet: false, owner: successor);
            try
            {
                var slot = _entities.EntityAdapters<IRoomEntity>().Single(entity => ReferenceEquals(entity.Node, next));
                _entities.RetireEntity(slot);
                _entities.RetireEntity(slot);
                FailIf(nextBinding.Actor is not null || !_player.IsCutsceneControlOwner(successor),
                    "Actor retirement must detach references without cancelling another owner's input.");
            }
            finally { _player.EndCutsceneControl(successor); }
        }
        GD.Print("Validated shared actor retirement notifications, outgoing scroll lifetime, actual native disposal, binding reuse and successor input ownership.");
    }
}
