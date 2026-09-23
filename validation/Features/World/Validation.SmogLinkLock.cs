using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogLinkLock()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach (bool batch in new[] { false,true })
        {
            LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(24,24));
            for (int y = 8; y < 128; y += 16)
                for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x,y),0x0c,0,0);
            void Step(int count = 1)
            {
                input.CaptureForValidation([],[],Vector2.Right);
                if (batch) scheduler.Advance(count / 60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0,update);
            }
            var cloud = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(104,88),2));
            _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(136,104),2));
            var shot = _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(120,40),1));
            Step();
            Vector2 linkPosition = _player.Position, shotPosition = shot.Position;
            int timer = cloud.ProjectileCounter;
            _entities.LockSmogLinkAndMenu();
            FailIf(!_entities.PlayerUpdatesFrozen || !_entities.PlayerMenusDisabled || _entities.LinkCollisionsAndMenuDisabled,
                "INTERAC$33's mask$01/menu lock must freeze Link without setting wDisableLinkCollisionsAndMenu.");
            Step(4);
            FailIf(_player.Position != linkPosition || cloud.ProjectileCounter != timer - 4 || shot.Position == shotPosition,
                "Smog setup must hold Link's update while ENEMY and PART updates continue.");

            // Separate death/collision lock must survive enemyBoss_beginBoss.
            typeof(RoomEntityManager).GetMethod("DisableLinkCollisionsAndMenu",flags)!.Invoke(_entities,null);
            _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(104,104),2));
            Step();
            FailIf(_entities.PlayerUpdatesFrozen || !_entities.LinkCollisionsAndMenuDisabled ||
                !_entities.PlayerMenusDisabled || _player.Position != linkPosition,
                "Small-cloud initialization must release mask$01/menu in the enemy pass, retaining the separate collision/menu lock and earlier frozen Link update.");
            typeof(RoomEntityManager).GetMethod("EnableLinkCollisionsAndMenu",flags)!.Invoke(_entities,null);
            Step();
            FailIf(_entities.PlayerMenusDisabled || _player.Position.X <= linkPosition.X,
                "Link must resume movement on the update following enemyBoss_beginBoss.");

            _entities.LockSmogLinkAndMenu();
            _entities.Clear();
            FailIf(_entities.PlayerUpdatesFrozen || _entities.PlayerMenusDisabled,
                "Room teardown must release the Smog-owned Link/menu lock.");
        }
        GD.Print("Validated Smog Link-only lock, continuing enemy/part updates, boss-start release timing and teardown with single/batched gameplay updates.");
    }
}
