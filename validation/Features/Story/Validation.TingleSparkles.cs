using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateTingleSparkleLifecycle()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int capacity in new[] { 0, 1, 3 })
        {
            LoadValidationRoom(0, 0x79);
            _player.WarpTo(new(0x18, 0x70), recordSafe: false);
            FailIf(_currentRoom.IsSolid(_player.Position), "Tingle sparkle fixture must place Link on room $0:$79's open floor.");
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step();
            var tingle = _entities.Entities<TingleRoomEntity>().Single();
            // Arrange only the balloon prerequisite; all subsequent actor and
            // child timing runs through the actual application update loop.
            FailIf(!_entities.ApplySwordHit(new Rect2(tingle.Npc.Position - new Vector2(4,4), new(8,8)),
                tingle.Npc.Position, 1, EnemyKnockbackStrength.Normal, itemZ: tingle.CollisionZ),
                "Tingle sparkle fixture failed to pop PART $44 at its live Z.");
            Step(80);
            FailIf(!tingle.Grounded, $"Tingle sparkle fixture must finish the source balloon fall: batch={batched}, capacity={capacity}, state={tingle.State}, z={tingle.ZFixed}, dying={_player.IsDying}, room=${_currentRoom.Id:x2}, text={_dialogue.IsOpen}.");
            tingle.StartKooloo();
            Step(59);
            FailIf(_entities.Entities<InteractionSparkleEffect>().Count != 0,
                "Tingle must wait for animation $03's update-60 cue.");

            var add = typeof(RoomEntityManager).GetMethod("AddEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<IRoomEntity, IRoomEntity>>(_entities);
            var occupants = new List<InteractionSlotValidationEntity>();
            while (_entities.InteractionSlotAvailable)
            {
                var puff = new PuzzlePuffEffect();
                puff.Initialize(new(24,24), 0);
                var occupant = new InteractionSlotValidationEntity(puff, _ => { });
                occupants.Add(occupant);
                add(occupant);
            }
            // Finished native owners release allocation before node cleanup.
            foreach (var occupant in occupants.TakeLast(capacity)) occupant.Finished = true;
            _runtimeState.SetWramByte(0xc049, 0);
            Step();
            var sparkles = _entities.Entities<InteractionSparkleEffect>().ToArray();
            Vector2[] offsets = [new(0,-24), new(8,-16), new(-8,-16)]; // tingle.s $e800/$f008/$f0f8
            FailIf(sparkles.Length != capacity ||
                _runtimeState.ReadWramByte(0xc049) != (capacity < 3 ? 0x10 : 0),
                "Tingle $84:$00 must allocate only available slots and preserve failed angle writes at $e049/$c049.");
            for (int i = 0; i < sparkles.Length; i++)
                FailIf(sparkles[i].Position != tingle.Npc.Position + offsets[i] ||
                    sparkles[i].ElapsedUpdates != 1 || !sparkles[i].Visible ||
                    sparkles[i].AnimationFrame != 0 || sparkles[i].SourceAngle != 0x10 ||
                    sparkles[i].RenderedTextureOrigin != sparkles[i].Position - new Vector2(16,16) ||
                    sparkles[i].TexturePixelHash != 0x9fab991cab9cedd0UL ||
                    _entities.InteractionSlot(sparkles[i]) <= _entities.InteractionSlot(tingle),
                    "Tingle $84:$00 children must initialize in later native slots on the cue update with source OAM.");

            _dialogue.ShowGameplayMessage("Sparkle lifecycle", 100);
            Step(8);
            FailIf(sparkles.Any(s => s.ElapsedUpdates != 9), "$84:$00 must advance during dialogue.");
            _dialogue.Close();
            var disabled = _entities.NonInteractionObjectsDisabledSource;
            try
            {
                _entities.NonInteractionObjectsDisabledSource = () => true;
                Step(8);
            }
            finally { _entities.NonInteractionObjectsDisabledSource = disabled; }
            FailIf(sparkles.Any(s => s.ElapsedUpdates != 17), "$84:$00 always-update must survive object freeze.");
            _entities.BeginScreenTransition(0, _world.LoadRoom(0, 0x89), new(0,128), _player);
            Step(19);
            FailIf(sparkles.Any(s => s.ElapsedUpdates != 36 || s.Finished || s.AnimationParameter != 0xff),
                "$84:$00 must retain the terminal $ff frame through update 36 during scrolling.");
            Step();
            FailIf(sparkles.Any(s => !s.Finished) || _entities.OutgoingEntities<InteractionSparkleEffect>().Count != 0,
                "$84:$00 must delete on update 37 before advancing animation.");
            _entities.FinishScreenTransition();
            LoadValidationRoom(0, 0x79);
            _player.WarpTo(new(0x18, 0x70), recordSafe: false);
            Step(2);
            FailIf(_entities.Entities<InteractionSparkleEffect>().Count != 0,
                "Room replacement must retire sparkle ownership before the next repeated fixture.");
            // Cancellation must release an active child as well as a finished
            // one, without leaking its native slot into the replacement room.
            _entities.Spawn<InteractionSparkleEffect>(new TingleKoolooSparkleSpawn(
                new(40,40), 0x10, new TingleDatabase().KoolooSparkleVisual));
            Step();
            FailIf(_entities.Entities<InteractionSparkleEffect>().Single().ElapsedUpdates != 1,
                "Cancellation fixture must contain an initialized active $84:$00 child.");
            LoadValidationRoom(0, 0x79);
            _player.WarpTo(new(0x18, 0x70), recordSafe: false);
            Step(2);
            FailIf(_entities.Entities<InteractionSparkleEffect>().Count != 0,
                "Room replacement must cancel an active $84:$00 child.");
        }
    }
}
