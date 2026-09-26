using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownOwlAllocation()
    {
        foreach(bool batch in new[]{false,true})
        {
            void Step(int count=1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch);
            LoadValidationRoom(4,0x9b);
            _player.WarpTo(new(120,152));
            Step(2);
            var owl=_entities.Entities<OwlStatueRoomEntity>().Single();
            // enemyData.s:group4Map9bEnemyObjectData, PART$13:$00 at$58.
            FailIf(owl.Position!=new Vector2(136,88),"Crown owl must occupy source tile$58.");
            while(_entities.InteractionSlotAvailable)
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),SoundId.MusNone));
            owl.ApplySeedHit(owl.CollisionBounds,owl.Position,ItemId.MysterySeed,new List<RoomEntitySpawn>());
            Step(18);
            FailIf(owl.Counter!=32 || _entities.Entities<InteractionSparkleEffect>().Count!=0,
                "Full INTERACTION pool must skip owl sparkles at counters$30/$28/$20 without stopping its countdown.");
            Step(8);
            var sparkle=_entities.Entities<InteractionSparkleEffect>().Single();
            FailIf(owl.Counter!=24 || sparkle.Position!=owl.Position+new Vector2(-6,-4) ||
                sparkle.ElapsedUpdates!=1 || sparkle.ZIndex!=9,
                $"Once slots become free, the next sparkle must use offset(-6,-4), same-update state0 and visible82 priority: counter={owl.Counter}, position={sparkle.Position}, updates={sparkle.ElapsedUpdates}, priority={sparkle.ZIndex}.");
            // The native sparkle survives scrolling and keeps enabled bit$80.
            // The outgoing owl itself freezes and cannot emit further children.
            _entities.BeginScreenTransition(4,_world.LoadRoom(4,0xbb),new(240,0),_player);
            Step(8);
            FailIf(sparkle.ElapsedUpdates!=9 || sparkle.Finished || owl.Counter!=24,
                "Outgoing owl sparkle must keep updating during scroll while its parent freezes.");
            Step(27);
            FailIf(sparkle.ElapsedUpdates!=36 || sparkle.Finished,
                "Owl sparkle must retain its terminal$ff frame through update36.");
            Step();
            FailIf(!sparkle.Finished || _entities.OutgoingEntities<InteractionSparkleEffect>().Count!=0,
                "Owl sparkle must delete and release its interaction slot on update37 during scroll.");
            _entities.FinishScreenTransition();
            LoadValidationRoom(0,0x60);
        }
    }
}
