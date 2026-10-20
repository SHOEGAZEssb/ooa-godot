using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEventNpcArrivalVisibility()
    {
        // Independent source state-zero writes: plen.s's AnimateAsNpc tail,
        // cheval.s/rafton.s/patch.s Visiblec2, symmetryNpc.s Visible82 and
        // mamamuYan.s's npcFaceLinkAndAnimate tail, and carpenter.s Visiblec2.
        // Native-adapter cases also guard the paths that already work. No
        // script update is needed to establish visibility before arrival fades.
        foreach (var (group, room, id) in new[]
        {
            (3, 0xf8, 0xcc), (0, 0x25, 0x9a), (2, 0x0f, 0x6a), (2, 0x1e, 0x69),
            (2, 0xe7, 0x53), (3, 0xbe, 0x94),
            (3, 0x6e, 0xbf), (3, 0x6f, 0xbf), (3, 0x7e, 0xbf),
            (3, 0x7f, 0xbf), (3, 0x8e, 0xbf), (3, 0xea, 0xbf),
            (3, 0xeb, 0xbf), (3, 0xec, 0xbf), (5, 0xf6, 0xbf)
        })
        for (int visit = 0; visit < 2; visit++)
        {
            if (visit == 0) ReinitializeGameplayForValidation();
            LoadValidationRoom(group, room);
            NpcCharacter[] actors = _entities.Entities<NpcCharacter>()
                .Where(actor => actor.Record.Id == id && actor.Active).ToArray();
            FailIf(actors.Length == 0 || actors.Any(actor => !actor.Visible || !actor.IsVisibleInTree()),
                $"Room ${group:x}:${room:x2} NPC ${id:x2} is hidden after source state-zero initialization, visit {visit}.");
            StepGameplayUpdates(2, Vector2.Zero, batched: visit != 0);
            FailIf(actors.Any(actor => actor.Active && !actor.Visible),
                $"Room ${group:x}:${room:x2} NPC ${id:x2} lost its initialized visibility.");
            LoadValidationRoom(0, 0x47);
        }

        foreach (bool present in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.TuniNutPlaced, present);
            LoadValidationRoom(1, 0x03);
            NpcCharacter resident = _entities.Entities<NpcCharacter>().Single(actor => actor.Record.Id == 0xbf);
            FailIf(resident.Active != present || resident.Visible != present,
                "Symmetry NPC $bf:$0c must preserve its state-zero flag-$29 deletion predicate.");

            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(0xcfd2, (byte)(present ? 1 : 0));
            LoadValidationRoom(5, 0xe8);
            NpcCharacter patch = _entities.Entities<NpcCharacter>().Single(actor => actor.Record is { Id: 0x94, SubId: 1 });
            FailIf(patch.Active != present || patch.Visible != present,
                "Patch $94:$01 must publish Visiblec2 only when the downstairs predicate retains him.");
        }

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
            LoadValidationRoom(0, 0x49);
            _player.WarpTo(new Vector2(0x48, 0x18));
            FailIf(_collision.Collides(_player.Position), "Nayru arrival fixture starts inside solid terrain.");
            int crowdUpdates = 0;
            var crowdPoses = new Dictionary<string, (Vector2 Position, int Frame, int Counter)>();
            void ObserveCrowd()
            {
                if (_currentRoom.Id != 0x39) return;
                var actors = _roomEvents.Get<NayruIntroEvent>().ActorRegistry;
                string[] names = ["Nayru", "Ralph", "Bear", "Monkey", "Rabbit", "Boy", "Bird"];
                FailIf(actors.Count != 7 || names.Any(name => !actors.ContainsKey(name) || !actors[name].Visible),
                    "Nayru's room $00:$39 gathering is hidden during the incoming scroll.");
                if (_transitions.ScrollActive)
                {
                    crowdUpdates++;
                    foreach (string name in names)
                    {
                        NpcCharacter actor = actors[name];
                        var pose = (actor.Position, actor.CurrentAnimationFrame, actor.CurrentAnimationCounter);
                        if (crowdPoses.TryGetValue(name, out var initial))
                            FailIf(pose != initial,
                                $"Nayru gathering actor {name} advanced beyond state zero during scrolling.");
                        else crowdPoses.Add(name, pose);
                    }
                }
            }
            for (int limit = 0; !_transitions.ScrollActive && limit < 48; limit += 4)
                StepGameplayUpdates(4, Vector2.Up, batched: batched, afterUpdate: ObserveCrowd);
            FailIf(!_transitions.ScrollActive, $"Nayru arrival did not reach the north exit: {_player.Position}.");
            for (int limit = 0; _transitions.ScrollActive && limit < 80; limit += 4)
                StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: ObserveCrowd);
            FailIf(crowdUpdates < 20 || _transitions.IsTransitioning,
                "Nayru arrival did not retain its visible gathering throughout the scroll.");

            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x57);
            _player.WarpTo(new Vector2(0x58, 0x48));
            int plenUpdates = 0;
            void ObservePlen()
            {
                if (_rooms.ActiveGroup != 3 || _currentRoom.Id != 0xf8) return;
                NpcCharacter plen = _entities.Entities<NpcCharacter>().Single(actor => actor.Record.Id == 0xcc);
                FailIf(!plen.Visible, "Plen $cc:$00 appeared only after the house-entry fade.");
                if (_transitions.IsTransitioning) plenUpdates++;
            }
            for (int limit = 0; !_transitions.IsTransitioning && limit < 64; limit += 4)
                StepGameplayUpdates(4, Vector2.Up, batched: batched, afterUpdate: ObservePlen);
            FailIf(!_transitions.IsTransitioning, $"Plen arrival did not enter the doorway: {_player.Position}.");
            for (int limit = 0; _transitions.IsTransitioning && limit < 180; limit += 4)
                StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: ObservePlen);
            FailIf(plenUpdates < 20 || _transitions.IsTransitioning || _currentRoom.Id != 0xf8,
                "Plen arrival did not remain visible throughout the destination fade.");
        }
        GD.Print("Validated source-initialized event NPC visibility, repeated room loads, Nayru gathering scrolling and Plen house-entry fades through split/batched gameplay.");
    }
}
