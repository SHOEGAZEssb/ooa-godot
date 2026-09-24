using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEnemyHighKnockback()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        foreach (string mode in new[] { "normal", "fast", "full", "wall" })
        {
            LoadValidationRoom(4, 0xab);
            _player.WarpTo(new(24, 24));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            Step();
            // Observe the final placed enemy before the post-object warp
            // dispatcher can overwrite $cec0 with its own scratch value.
            var moblin = _entities.Entities<ArrowMoblinCharacter>().Last();
            for (int y = 1; y < 10; y++)
            for (int x = 1; x < 15; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            moblin.Position = mode == "wall" ? new(236, 88) : new(64, 64);
            typeof(EnemyCharacter).GetProperty("KnockbackAngle", flags)!.SetValue(moblin, 8);
            typeof(EnemyCharacter).GetProperty("KnockbackCounter", flags)!.SetValue(moblin,
                mode == "normal" ? 9 : mode == "wall" ? 0x85 : 0x89);
            bool observeVelocity = true;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                if (!observeVelocity) return;
                FailIf(_runtimeState.ReadWramByte(0xcec0) != 0 ||
                    _runtimeState.ReadWramByte(0xcec1) != 0 ||
                    _runtimeState.ReadWramByte(0xcec2) != 0 ||
                    _runtimeState.ReadWramByte(0xcec3) != (mode == "normal" ? 2 : 3),
                    "Native recoil must publish SPEED_200/300 at angle $08 before wall rejection and post-object warp scratch.");
            });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 15]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            if (mode == "full")
                while (_entities.InteractionSlotAvailable)
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200, 104), 0));
            Step(mode == "wall" ? 1 : 4);
            observeVelocity = false;
            if (mode == "wall")
            {
                FailIf(moblin.Position != new Vector2(236, 88) || moblin.KnockbackCounter != 0x80 ||
                    _entities.Entities<KnockbackDustRoomEntity>().Count != 1,
                    "Wall-stopped high recoil must create dust before motion, then retain only bit7.");
                continue;
            }
            int speed = mode == "normal" ? 2 : 3;
            FailIf(moblin.Position != new Vector2(64 + speed * 4, 64) ||
                moblin.KnockbackCounter != (mode == "normal" ? 5 : 0x85),
                "Four source recoil ticks must move at SPEED_200 or SPEED_300 according to bit7.");
            Step(5);
            var dust = _entities.Entities<KnockbackDustRoomEntity>().ToArray();
            FailIf(moblin.Position != new Vector2(64 + speed * 9, 64) ||
                moblin.KnockbackCounter != (mode == "normal" ? 0 : 0x80) ||
                dust.Length != (mode == "fast" ? 3 : 0),
                "Nine recoil ticks must expire low bits; full INTERACTION capacity must drop dust without changing motion.");
            if (mode != "fast") continue;
            // Decremented counters88/84/80 request dust before movement;
            // source objectCopyPosition copies high bytes at X64/76/88.
            for (int i = 0; i < 3; i++)
                FailIf(dust[i].Position != new Vector2(64 + 12 * i, 64) || dust[i].ElapsedUpdates != 9 - 4 * i,
                    "Dust must use pre-move coordinates and initialize later in the same object update.");
            var freeze = _entities.NonInteractionObjectsDisabledSource;
            try
            {
                _entities.NonInteractionObjectsDisabledSource = () => true;
                Step(4);
                FailIf(dust[0].ElapsedUpdates != 13 || dust[0].AnimationParameter != 0xff ||
                    dust[0].Finished || !dust[0].Visible,
                    "Always-update dust must retain its 4/4/4 terminal frame on update13 after setup.");
                Step();
                FailIf(!dust[0].Finished,
                    "Dust must toggle and delete on the update after its terminal high-bit parameter.");
            }
            finally { _entities.NonInteractionObjectsDisabledSource = freeze; }
        }
        LoadValidationRoom(0, 0x60);
    }
}
