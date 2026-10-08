using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateFireballCounterRoomEntry()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _player.ApplicationUpdateOwned = true;
            LoadValidationRoom(5, 0x22);
            _player.WarpTo(new Vector2(120, 88), recordSafe: false);
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            Step(3);
            var shooters = _entities.Entities<FireballShooterRoomEntity>().ToArray();
            FailIf(shooters.Length != 4 || shooters.Any(s => s.State != 9),
                "$5:$22 must initialize four ENEMY$50:$81 children before slot reuse.");
            // Isolate room-clear bookkeeping from combat. Run deletion and
            // the shooter fallthrough through the ordinary gameplay pass.
            foreach (var rope in _entities.Entities<RopeCharacter>()) rope.FinishGale();
            Step(2);
            FailIf(_entities.RoomEnemyCount != 0 || shooters.Any(s => !s.Finished || s.Counter1 != 0xff),
                "$50 state9 must dirty freed slots $05-$08 with $ff after the four Ropes leave.");

            _transitions.BeginScroll(_player, Vector2I.Right, 0x23);
            FinishScroll();
            // group5Map23 reserves slots $00-$04: three Water Tektites,
            // then two Whisps. Their live slots force $5:$24 into $05-$07.
            FailIf(_entities.Entities<WaterTektiteCharacter>().Count != 3 ||
                _entities.Entities<WhispCharacter>().Count != 2,
                "$5:$23 must preserve source enemy order before the $5:$24 handoff.");
            var beforeEntryRandom = _random.CaptureState();
            _transitions.BeginScroll(_player, Vector2I.Right, 0x24);
            var whisp = _entities.Entities<WhispCharacter>().Single();
            var swimmers = _entities.Entities<WaterTektiteCharacter>().ToArray();
            var producer = _entities.ResolveEnemySlot(8)?.Node as ItemDropProducer;
            FailIf(_entities.ResolveEnemySlot(5)?.Node != whisp || whisp.Counter1 != 0xff ||
                !whisp.Initialized || !whisp.Visible || whisp.Position != new Vector2(0x78, 0x58) ||
                swimmers.Length != 2 || swimmers.Any(s => s.State != 8 || s.Counter != 0x40) ||
                !swimmers.Select(s => s.Position).SequenceEqual(new Vector2[] { new(0x48, 0x48), new(0x98, 0x58) }) ||
                producer is null || !producer.Initialized || producer.Counter1 != 0xff,
                "$5:$24 Whisp/producer slots $05/$08 must retain $ff; Water Tektites must overwrite slots $06-$07 with $40 at state0.");
            FailIf(_entities.Entities<ItemDropProducer>().Count != 5,
                "$5:$24 must retain all five source item-drop producers.");
            // bank0 room parsing: 256 buffer draws; enemyStandardUpdate and
            // native handlers: 2 Whisp + 4 Water Tektite + 5 producer draws.
            FailIf(_random.CaptureState().Calls - beforeEntryRandom.Calls != 267,
                $"$5:$24 initialization RNG: expected 267 draws, got {_random.CaptureState().Calls - beforeEntryRandom.Calls}.");
            var positions = swimmers.Select(s => s.Position).ToArray();
            var whispPosition = whisp.Position;
            var random = _random.CaptureState();
            Step(8);
            FailIf(!IsTransitioning || whisp.Position != whispPosition || whisp.Counter1 != 0xff ||
                swimmers.Where((s, i) => s.Position != positions[i] || s.Counter != 0x40).Any() ||
                _random.CaptureState().Calls != random.Calls || producer!.Counter1 != 0xff || producer.Finished,
                "$5:$24 initialized destination enemies and RNG must freeze during scrolling with inherited counter1.");
            FinishScroll();
            whispPosition = whisp.Position;
            Step(1);
            FailIf(whisp.Position == whispPosition || whisp.Counter1 != 0xff ||
                swimmers.Any(s => s.Counter >= 0x40),
                "$5:$24 movement must resume after scrolling without consuming Whisp's unused $ff counter.");

            _transitions.BeginScroll(_player, Vector2I.Left, 0x23);
            FinishScroll();
            _transitions.BeginScroll(_player, Vector2I.Right, 0x24);
            var repeated = _entities.Entities<WhispCharacter>().Single();
            FailIf(repeated.Counter1 != 0 || !repeated.Initialized,
                "$5:$24 repeat entry must see cleared slots after outgoing object cleanup.");
            // Session replacement cancels the pending scroll. Ordinary room
            // loading must then start with clear native pages and residue.
            ReinitializeGameplayForValidation();
            _player.ApplicationUpdateOwned = true;
            LoadValidationRoom(5, 0x24);
            var reloaded = _entities.Entities<WhispCharacter>().Single();
            FailIf(IsTransitioning || reloaded.Counter1 != 0 || reloaded.Initialized,
                "$5:$24 ordinary reload must cancel scrolling and clear native counter residue.");
            Step(1);
            FailIf(!reloaded.Initialized || reloaded.Counter1 != 0,
                "$5:$24 clean state0 must preserve its zero counter on reload.");

            void FinishScroll()
            {
                for (int i = 0; IsTransitioning && i < 40; i++) Step(4);
                FailIf(IsTransitioning, "$5:$22-$24 slot-reuse scroll did not complete within 160 updates.");
            }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Validated $5:$22 -> $23 -> $24 dirty enemy-slot reuse, destination freeze/resume, repeat entry and reload cancellation in split/batched gameplay updates.");
    }
}
