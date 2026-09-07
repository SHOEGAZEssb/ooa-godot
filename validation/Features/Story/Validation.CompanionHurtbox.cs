using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMountedCompanionHurtbox()
    {
        var database = new EnemyDatabase();
        RoomObjectRecord source = RoomEnemyPlacements(database, 0, 0x74, 0x09, 0x00)[0];
        var texture = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        for (int direction = 0; direction < 4; direction++)
        {
            PrepareCompanionFidelityRoom();
            _entities.Clear();
            _player.RefillHealth();
            Vector2 point = new(72, 64);
            IRoomEntity companion = id switch
            {
                0x0b => _entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(point, direction, 0, 0x2a, Riding: true)),
                0x0c => _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(point, direction, 0, 0x2a, Riding: true)),
                _ => _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(point, direction, 0, 0x2a, Riding: true))
            };
            CompanionRuntimeState.Begin(_runtimeState, id, 0x2a, point, direction);
            _entities.Update(1.0 / 60, _player);
            FailIf(!_player.CompanionRideActive || _player.EnemyContactPosition != companion.Node.Position,
                $"Companion ${id:x2}, direction ${direction:x2}: collision did not follow wLinkObjectIndex=$d1.");

            // A radius-$02 part against the companion's radius-$06 accepts
            // companion-minus-part deltas -$08..+$07 on each axis.
            foreach (Vector2 axis in new[] { Vector2.Right, Vector2.Down })
            foreach (int distance in new[] { -9, -8, -7, 7, 8, 9 })
            {
                Rect2 bounds = new(point + axis * distance - Vector2.One * 2, Vector2.One * 4);
                bool expected = distance >= -7 && distance <= 8;
                FailIf(_player.OverlapsEnemyCollision(bounds) != expected,
                    $"Companion ${id:x2}, direction ${direction:x2}: hurtbox edge {axis} * {distance} differed from checkObjectsCollidedFromVariables.");
            }

            var enemy = new OctorokCharacter();
            AddChild(enemy);
            enemy.Initialize(ResolveOctorok(database, source), _rooms.CurrentRoom,
                point + Vector2.Down * 8, new OracleRandom());
            var adapter = new OctorokRoomEntity(enemy,
                database.EnemyHandlers.ResolveHandler(source).CombatSource(source, killableEnemyIndex: 1), _ => { });
            int health = _player.HealthQuarters;
            adapter.HandleLinkContact(_player);
            FailIf(_player.HealthQuarters >= health,
                $"Companion ${id:x2}, direction ${direction:x2}: enemy below companion missed (health={health}->{_player.HealthQuarters}, invincibility={_player.InvincibilityFrames}, knockback={_player.KnockbackFrames}, contact={_player.AcceptsRoomEntityContact}, z={_player.CompanionRideZFixed}, dying={_player.IsDying}, point={_player.EnemyContactPosition}, enemy={enemy.Position}).");
            _player.ResetEnemyInvincibility();

            // Exercise exact Z boundaries without depending on a particular
            // companion's jump speed or animation cadence.
            Vector2 offset = _player.Position - point;
            _player.WarpTo(_player.Position);
            _player.SetCompanionRidePosition(point, offset, direction, -7 * 256,
                texture, texture, Vector2.Zero, Vector2.Zero);
            health = _player.HealthQuarters;
            adapter.HandleLinkContact(_player);
            FailIf(_player.HealthQuarters != health,
                $"Companion ${id:x2}: ground enemy hit at relative Z +$07.");
            Rect2 centered = new(point - Vector2.One * 2, Vector2.One * 4);
            foreach (int difference in new[] { -8, -7, 6, 7 })
                FailIf(_player.OverlapsEnemyCollision(centered, -7 + difference) != (difference >= -7 && difference < 7),
                    $"Companion ${id:x2}: relative Z {difference} lost the source $07/$0e window.");

            var rock = _entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(point, 0));
            rock.UpdateFrame(_player);
            rock.UpdateFrame(_player);
            FailIf(rock.Finished || _player.HealthQuarters != health,
                $"Companion ${id:x2}: ground projectile collided below airborne rider.");
            _player.SetCompanionRidePosition(point, offset, direction, -6 * 256,
                texture, texture, Vector2.Zero, Vector2.Zero);
            rock.Position = point;
            rock.UpdateFrame(_player);
            FailIf(!rock.Finished || _player.HealthQuarters >= health,
                $"Companion ${id:x2}: projectile failed to hit at relative Z +$06.");
            enemy.QueueFree();

            _player.BeginCompanionDismount(point, direction);
            FailIf(_player.CompanionRideActive || _player.EnemyContactPosition != _player.Position,
                $"Companion ${id:x2}: dismount retained companion collision ownership.");
        }
        GD.Print("Validated mounted Ricky/Dimitri/Moosh hurtbox ownership in four directions, exact XY/Z edges, enemy/projectile damage and dismount.");
    }
}
