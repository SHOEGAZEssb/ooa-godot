using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownBoomerangTransformation()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        int FreeSlot(string method) => (int)typeof(RoomEntityManager).GetMethod(method, flags)!.Invoke(_entities, null)!;
        foreach (bool batched in new[] { false, true })
        foreach (bool spark in new[] { true, false })
        foreach (string mode in new[] { "normal", "text", "scroll", "full-interactions", "last-interaction",
            "full-parts", "last-part", "lost-puff", "lost-puff-hole", "lost-puff-owl",
            "lost-puff-small-key", "lost-puff-boss-key" })
        {
            LoadValidationRoom(4, spark ? 0xa8 : 0x9f);
            _player.WarpTo(new(24, 24));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step();
            EnemyCharacter actor = spark ? _entities.Entities<SparkCharacter>().First()
                : _entities.Entities<WhispCharacter>().First();
            IItemCollisionHittableRoomEntity target = spark ? _entities.EntityAdapters<SparkRoomEntity>().First()
                : _entities.EntityAdapters<WhispRoomEntity>().First();
            int State() => actor is SparkCharacter s ? s.TransformationState : ((WhispCharacter)actor).TransformationState;
            int roomCount = _entities.RoomEnemyCount;
            int defeats = 0;
            void Defeated() => defeats++;
            _entities.EnemyDefeated += Defeated;
            var text = _entities.TextActiveSource;
            var freeze = _entities.NonInteractionObjectsDisabledSource;
            var partFillers = new List<ItemDropEffect>();
            bool scrolling = false;
            try
            {
                // These are collision-handler checks, followed by the real
                // application update loop. The boomerang parent/item path is
                // deliberately not claimed by this regression.
                var mask = spark ? EnemyBehaviorTables.Shared.SparkActiveCollisions : EnemyBehaviorTables.Shared.WhispActiveCollisions;
                var effects = spark ? EnemyBehaviorTables.Shared.SparkCollisionEffects : EnemyBehaviorTables.Shared.WhispCollisionEffects;
                FailIf(mask[0x17].Value != 1 || effects[0x17].Value != 0x35,
                    "Clean-US Spark/Whisp boomerang column$17 must select effect$35.");
                bool Hit() => target.ApplyItemCollision(RoomEntityItemCollision.Boomerang,
                    actor.CollisionBounds, actor.Position, 2, new List<RoomEntitySpawn>());
                actor.InvincibilityCounter = 1;
                FailIf(Hit(), "Nonzero invincibility must reject the boomerang collision.");
                actor.InvincibilityCounter = 0;
                FailIf(!Hit() || State() != 8 || actor.Health != 0 || actor.CollisionEnabled || actor.InvincibilityCounter != 0,
                    "Effect$35 must set health=0 and disable collision, deferring JUST_HIT without invincibility or immediate state9.");
                Vector2 stopped = actor.Position;
                if (mode is "full-interactions" or "last-interaction")
                {
                    while (_entities.InteractionSlotAvailable &&
                        (mode == "full-interactions" || FreeSlot("FindFreeInteractionSlot") != 15))
                        _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200, 104), 0));
                }
                if (mode == "text")
                {
                    _entities.TextActiveSource = () => true;
                    Step(3);
                    FailIf(State() != 8 || actor.Position != stopped,
                        "Text must freeze the enemy's pending boomerang hit.");
                    _entities.TextActiveSource = text;
                }
                _sound.ClearPlayRequestAudit();
                Step();
                if (mode == "full-interactions")
                {
                    FailIf(State() != 9 || !actor.Visible || actor.Position != stopped,
                        "Full INTERACTION pool must retain visible state9 without movement.");
                    Step(19);
                    FailIf(State() != 9 || _entities.Entities<PuzzlePuffEffect>().Any(),
                        "Enemy phase must still fail allocation before terminal puffs delete in the interaction phase.");
                    Step();
                }
                var puff = _entities.Entities<PuzzlePuffEffect>().Single(p => !p.AlwaysUpdates);
                int puffSlot = _entities.InteractionSlot(puff);
                FailIf(State() != 0x0a || actor.Visible || actor.CollisionEnabled || actor.Health != 0 ||
                    puff.ElapsedUpdates != 1 || puff.Position != OracleObjectMath.ToPixelPosition(stopped) ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndPoof) != 1,
                    $"{mode}: state9 must hide the defeated enemy and initialize PUFF$02 later that update.");
                if (mode == "last-interaction")
                    FailIf(puffSlot != 15, "The transformation must use final INTERACTION slot$df.");
                FailIf(Hit(), "The transformation must reject repeated hits after effect$35 clears collisionType bit7.");
                if (mode == "text")
                {
                    _entities.TextActiveSource = () => true;
                    Step(3);
                    FailIf(puff.ElapsedUpdates != 1 || State() != 0x0a,
                        "PUFF subid$02 must pause with the enemy during text.");
                    _entities.TextActiveSource = text;
                }
                if (mode == "scroll")
                {
                    _entities.BeginScreenTransition(4, _currentRoom, new(240, 0), _player);
                    scrolling = true;
                    Step(3);
                    FailIf(puff.ElapsedUpdates != 1 || State() != 0x0a || actor.IsDead,
                        "Initialized PUFF$02 and its related enemy must remain frozen during scrolling.");
                    continue;
                }
                if (mode is "full-parts" or "last-part")
                {
                    _currentRoom.SetPositionTileAndCollision(new(200, 104), 0xa0, 0, 0);
                    while (_entities.PartSlotAvailable &&
                        (mode == "full-parts" || FreeSlot("FindFreePartSlot") != 15))
                        partFillers.Add(_entities.Spawn<ItemDropEffect>(new ItemDropSpawn(ItemDropDatabase.OneRupee, new(200, 104))));
                }
                if (mode is "lost-puff" or "lost-puff-hole" or "lost-puff-owl" or
                    "lost-puff-small-key" or "lost-puff-boss-key")
                {
                    _entities.NonInteractionObjectsDisabledSource = () => true;
                    Step(19);
                    _entities.NonInteractionObjectsDisabledSource = freeze;
                    Step(2);
                    FailIf(actor.IsDead || State() != 0x0a || _entities.Entities<PuzzlePuffEffect>().Contains(puff),
                        "A puff deleted while enemies are disabled must leave a zero page, not a stale $ff reference.");
                    if (mode is "lost-puff-small-key" or "lost-puff-boss-key")
                    {
                        int graphic = mode == "lost-puff-small-key" ? 0x42 : 0x43;
                        var replacement = _entities.Spawn<DungeonKeyUseEffect>(
                            new DungeonKeyUseSpawn(stopped, _treasures.GetObjectVisual(graphic)));
                        FailIf(_entities.InteractionSlot(replacement) != puffSlot || replacement.AnimationParameter != 0,
                            "Key sprite must reuse the cleared page with animParameter00 before initialization.");
                        // interaction17 animations0/3 start with parameter00;
                        // states1/2 only age counters8/20, never animate.
                        Step(28);
                        FailIf(actor.IsDead || replacement.AnimationParameter != 0 || replacement.Finished,
                            "A key sprite's stationary animation byte must keep Spark/Whisp waiting through update28.");
                        Step();
                        FailIf(actor.IsDead || !replacement.Finished,
                            "Deleting the key on update29 must not complete the related enemy.");
                        Step(2);
                        FailIf(actor.IsDead || State() != 0x0a,
                            "The key's deleted page must read00, not a synthetic terminal animation byte.");
                        var clink = _entities.Spawn<ClinkEffect>(new SwordBeamClinkSpawn(stopped));
                        FailIf(_entities.InteractionSlot(clink) != puffSlot,
                            "A second replacement must reuse the key's released slot.");
                        Step(9);
                        FailIf(actor.IsDead || clink.AnimationParameter != 0xff,
                            "Only the subsequent clink's actual terminal byte may release the enemy.");
                    }
                    else if (mode == "lost-puff-owl")
                    {
                        var replacement = _entities.Spawn<OwlStatueSparkleEffect>(
                            new OwlStatueSparkleSpawn(stopped, new OwlStatueDatabase().Record(0).Sparkle));
                        FailIf(_entities.InteractionSlot(replacement) != puffSlot || replacement.AnimationParameter != 0,
                            "An uninitialized owl sparkle must reuse the page with animParameter00.");
                        // interactionAnimation5a6f1: durations9/9/9/8, followed
                        // by parameterff. State0 initializes without advancing.
                        Step(35);
                        FailIf(actor.IsDead || replacement.AnimationParameter != 0 || replacement.Finished,
                            "The replacement owl sparkle must not release Spark/Whisp before update36.");
                        Step();
                        FailIf(actor.IsDead || replacement.AnimationParameter != 0xff || replacement.Finished,
                            "The owl terminal byte must remain available for the following enemy phase.");
                    }
                    else if (mode == "lost-puff-hole")
                    {
                        var replacement = _entities.Spawn<FallingDownHoleEffect>(new FallingDownHoleSpawn(stopped));
                        FailIf(_entities.InteractionSlot(replacement) != puffSlot,
                            "Hole effect must reuse the transformation's native interaction page.");
                        Step(31);
                        FailIf(actor.IsDead || replacement.CurrentParameter != 0 || replacement.Finished,
                            "Hole animation's literal 8/12/12 durations must not release Spark/Whisp before update32.");
                        Step();
                        FailIf(actor.IsDead || replacement.CurrentParameter != 0xff || replacement.Finished,
                            "Hole terminal parameter$ff must be observed by the following enemy pass.");
                    }
                    else
                    {
                        var replacement = _entities.Spawn<ClinkEffect>(new SwordBeamClinkSpawn(stopped));
                        FailIf(_entities.InteractionSlot(replacement) != puffSlot,
                            "Alias fixture must reuse the transformation's native interaction page.");
                        Step(9);
                        FailIf(actor.IsDead || replacement.AnimationParameter != 0xff,
                            "Enemy phase must observe the replacement clink's terminal byte on the following update.");
                    }
                }
                else
                {
                    Step(18);
                    FailIf(actor.IsDead || State() != 0x0a || puff.CurrentParameter != 0xff || puff.Finished ||
                        _entities.Entities<ItemDropEffect>().Any(p => p.SubId == ItemDropDatabase.Fairy),
                        "Update19 reaches puff parameter$ff after the enemy pass; neither enemy deletion nor fairy happens early.");
                }
                Step();
                var fairies = _entities.Entities<ItemDropEffect>().Where(p => p.SubId == ItemDropDatabase.Fairy).ToArray();
                int expectedFairies = spark && mode != "full-parts" ? 1 : 0;
                FailIf(!actor.IsDead || fairies.Length != expectedFairies || defeats != 0 || _entities.RoomEnemyCount != roomCount,
                    $"{mode}/spark={spark}: spark_stateA must silently delete, then create only Spark's fairy if PART allocation succeeds " +
                    $"(dead={actor.IsDead}, fairies={fairies.Length}/{expectedFairies}, defeats={defeats}, count={_entities.RoomEnemyCount}/{roomCount}).");
                if (expectedFairies != 0)
                    FailIf(fairies[0].Position != OracleObjectMath.ToPixelPosition(stopped) || !fairies[0].Visible,
                        "The fairy must copy enemy high-byte coordinates and run state0 in the same PART pass.");
                foreach (ItemDropEffect filler in partFillers) filler.ClearHealthAndCollision();
                Step(3);
                FailIf(_entities.Entities<ItemDropEffect>().Count(p => p.SubId == ItemDropDatabase.Fairy) != expectedFairies || defeats != 0,
                    "Freeing PART slots after deletion must not retry a failed fairy allocation or count an enemy defeat.");
            }
            finally
            {
                _entities.EnemyDefeated -= Defeated;
                _entities.TextActiveSource = text;
                _entities.NonInteractionObjectsDisabledSource = freeze;
                if (scrolling) _entities.FinishScreenTransition();
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
