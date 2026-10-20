using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateImpaFollowerScroll()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x6a);
            var owner = _roomEvents.Get<ImpaIntroEvent>();
            NpcRecord record = owner.Actor!.Record;
            owner.Cancel();
            _entities.Clear();
            _saveData.SetRoomFlag(0, 0x6a, 0x40);
            // Stage the completed encounter's native following handoff. All
            // movement and scrolling below use the actual collision/update loop.
            owner.Actor = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(record, "FollowingImpa"));
            owner.Actor.SetSpritePalette(owner.Database.PossessedPalette);
            _player.WarpTo(new Vector2(0x48, 0x48));
            _player.Face(Vector2I.Up);
            typeof(ImpaIntroEvent).GetMethod("BeginFollowing", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(owner, null);
            FailIf(_collision.Collides(_player.Position) || !owner.Actor.Visible,
                "Impa follower fixture must start in room $00:$6a's clear north/south corridor.");

            foreach (var (movement, destination) in new[] { (Vector2.Up, 0x5a), (Vector2.Down, 0x6a), (Vector2.Up, 0x5a) })
            {
                NpcCharacter outgoing = owner.Actor;
                bool transferred = false;
                int updates = 0;
                void Observe()
                {
                    updates++;
                    NpcCharacter actor = owner.Actor!;
                    FailIf(!actor.Visible || !actor.IsVisibleInTree(),
                        $"Impa $31:$00 vanished on update {updates} toward room $00:${destination:x2}.");
                    if (actor == outgoing || transferred) return;
                    transferred = true;
                    // objectSetReservedBit1 retains the original interaction,
                    // including animation bytes and its screen-space pose.
                    FailIf(outgoing.Visible || actor.Position + actor.TransitionDrawOffset !=
                        outgoing.Position + outgoing.TransitionDrawOffset,
                        "Impa $31:$00 scroll transfer duplicated or displaced the retained sprite.");
                    FailIf(actor.CurrentAnimationFrame != outgoing.CurrentAnimationFrame ||
                        actor.CurrentAnimationCounter != outgoing.CurrentAnimationCounter ||
                        actor.CurrentAnimationPixelHash != outgoing.CurrentAnimationPixelHash,
                        "Impa $31:$00 scroll transfer restarted or changed the retained animation.");
                }
                for (int limit = 0; !transferred && limit < 120; limit += 4)
                    StepGameplayUpdates(4, movement, batched: batched, afterUpdate: Observe);
                FailIf(!transferred || !_transitions.ScrollActive,
                    $"Impa follower did not reach room $00:${destination:x2} through ordinary movement: Link={_player.Position}, room=${_currentRoom.Id:x2}, scroll={_transitions.ScrollActive}, disabled=${_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}.");
                for (int limit = 0; _transitions.ScrollActive && limit < 80; limit += 4)
                    StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: Observe);
                FailIf(_transitions.IsTransitioning || _currentRoom.Id != destination ||
                    _entities.Entities<NpcCharacter>().Count(npc => npc.Active && npc.Record.Id == 0x31) != 1,
                    $"Impa follower did not finish scrolling into room $00:${destination:x2} with one active actor.");
            }
        }
        GD.Print("Validated Impa follower visibility, animation and draw-position continuity through repeated collision-driven scrolling with split/batched updates.");
    }
}
