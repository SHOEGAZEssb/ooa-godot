using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBombScrolling()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x34);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Bombs, 0x10);
            _inventory.EquipA(TreasureId.Bombs);
            _inventory.EquipB(0);
            _player.WarpTo(new(20.75f, 88.25f));
            _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(_player.Position),
                "Held bomb must start on unchanged room0:$34 left-exit floor.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
            StepGameplayUpdates(19, Vector2.Zero, batched: batch);
            BombEffect bomb = _bomb.Bomb!;
            FailIf(_bomb.State != BombParentState.Holding || !_player.IsCarryingObject ||
                bomb.State != BombState.Held || _inventory.Bombs != 0x09,
                "ITEM$03 must complete its original lift before approaching the exit.");

            // Declare a second, unheld child to exercise the complementary
            // enabled=$01 -> $02 cleanup, independent of its creation parent.
            BombEffect unheld = _entities.Spawn<BombEffect>(new BombSpawn(
                _player, new BombDatabase().Data, 0, _ => { }));
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            unheld.Throw(_player, new(0, -8), Vector2I.Zero, 0, ObjectSpeed.Speed0);
            for (int update = 0; !_transitions.ScrollActive && update < 24; update++)
            {
                StepGameplayUpdates(1, Vector2.Left, batched: batch);
                FailIf(!_transitions.ScrollActive && _collision.Collides(_player.Position),
                    "Held ITEM$03 must reach room0:$33 through actual exit collision geometry.");
            }
            FailIf(!_transitions.ScrollActive || _currentRoom.Id != 0x33,
                "Carrying a bomb must admit the original room0:$34 -> $33 left exit.");
            ScrollWithBomb(bomb, batch);
            FailIf(!unheld.IsQueuedForDeletion() || _entities.ActiveBombCount != 1,
                "Scroll cleanup must retain held enabled=$03 and delete unheld enabled=$02 ITEM$03.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(bomb.Position != _player.Position || _bomb.Bomb != bomb ||
                !_player.IsCarryingObject || _inventory.Bombs != 0x09,
                "First arrival update must reattach the same live ITEM$03 without consuming ammo.");

            // Repeat in the other direction with the same child, through the
            // destination's real edge. No full room reset may replace it.
            for (int update = 0; !_transitions.ScrollActive && update < 32; update++)
                StepGameplayUpdates(1, Vector2.Right, batched: batch);
            FailIf(!_transitions.ScrollActive || _currentRoom.Id != 0x34,
                "Held ITEM$03 must survive a repeated room0:$33 -> $34 right exit.");
            ScrollWithBomb(bomb, batch);
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            if (cancel)
            {
                _player.DropHeldItemsForScript();
                StepGameplayUpdates(3, Vector2.Zero, batched: batch);
                FailIf(_bomb.Active || _player.IsCarryingObject || bomb.State == BombState.Held,
                    "Post-scroll cancellation must release the retained ITEM$03 and its parent.");
            }
            else
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
                StepGameplayUpdates(9, Vector2.Zero, batched: batch);
                FailIf(_bomb.Active || _player.IsCarryingObject || bomb.State == BombState.Held,
                    "Post-scroll drop must complete the original throw parent and release ITEM$03.");
            }
            int remaining = 0;
            while (_entities.Entities<BombEffect>().Any() && remaining++ < 200)
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(_entities.ActiveBombCount != 0 || remaining >= 200,
                "Released post-scroll ITEM$03 must finish its fuse/explosion and free its slot.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
            FailIf(_bomb.State != BombParentState.Lifting || _inventory.Bombs != 0x08,
                "A fresh bomb after scroll/release must consume exactly one further BCD bomb.");
        }
        GD.Print("Validated held ITEM$03 scroll retention, attachment, frozen fuse, repeated exits, unheld cleanup, drop/cancellation and fresh allocation with individual/batched updates.");
    }

    private void ScrollWithBomb(BombEffect bomb, bool batch)
    {
        int frame = bomb.AnimationFrame, counter = bomb.AnimationCounter;
        int elapsed = bomb.ElapsedFrames;
        Vector2 previousLink = _player.PrecisePosition;
        StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero,
            ["attack"], ["attack"], batch, afterUpdate: () =>
            {
                FailIf(!GodotObject.IsInstanceValid(bomb) || bomb.IsQueuedForDeletion() ||
                    bomb.State != BombState.Held || _bomb.Bomb != bomb ||
                    _bomb.State != BombParentState.Holding || !_player.IsCarryingObject ||
                    bomb.AnimationFrame != frame || bomb.AnimationCounter != counter ||
                    bomb.ElapsedFrames != elapsed,
                    "wScrollMode $08 must freeze the fuse/parent and preserve held enabled=$03 through the final scroll update.");
                if (_transitions.ScrollActive)
                    FailIf(bomb.Position != bomb.PrecisePosition.Floor() ||
                        bomb.Position != previousLink.Floor(),
                        "Held ITEM$03 must follow logical Link coordinates without applying the camera offset twice.");
                previousLink = _player.PrecisePosition;
            });
        FailIf(_transitions.ScrollActive || !_entities.Entities<BombEffect>().Contains(bomb) ||
            _entities.OutgoingEntities<BombEffect>().Count != 0 ||
            bomb.TransitionDrawOffset != Vector2.Zero,
            "Scroll completion must transfer held ITEM$03 to the active room without freeing its node.");
        StepGameplayUpdates(1, Vector2.Zero, batched: batch);
        FailIf(bomb.AnimationCounter != counter - 1 || bomb.ElapsedFrames != elapsed + 1,
            $"Held ITEM$03 fuse must resume exactly on the first update after scrolling: frame={frame}->{bomb.AnimationFrame}, counter={counter}->{bomb.AnimationCounter}, elapsed={elapsed}->{bomb.ElapsedFrames}, parent={_bomb.State}, state={bomb.State}.");
    }
}
