using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateWallSquish()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var record = new CrownDungeonDatabase().GetRoomRecords(4,0x9b).Single(row => row.Kind == DungeonObjectKind.WallSquish);
        FailIf(record.Order != 3 || record.Id != 0xdc || record.SubId != 0x17,
            "Crown requires INTERAC $dc:$17 at main-stream order $03.");
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1, Vector2 move = default) =>
                StepGameplayUpdates(count, move, [], [], batched: batch);
            LoadValidationRoom(4,0x9b);
            var detector = _entities.Entities<WallSquishRoomEntity>().Single();
            FailIf(detector.State != 0 || _entities.InteractionSlot(detector) != 5,
                "Crown detector must initialize in source slot $d5 after the reset interaction.");
            _player.WarpTo(new(136,136));
            Step();
            FailIf(detector.State != 1 || _player.SideScrollSquished,"Detector state0 must only initialize.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int i = 0; _player.Position.Y > 104 && i < 60; i++)
                {
                    Step(move:Vector2.Up);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Squish fixture approach entered solid room geometry.");
                }
                FailIf(_player.Position != new Vector2(136,104),"Cannot reach floor $68 from $88 below the owl.");
                _player.SetLocalRespawnPosition(new(136,136));
                byte original = _currentRoom.GetMetatile(_player.Position);
                void Collision(byte value) => _currentRoom.SetPositionTileAndCollision(new(136,104),0x2e,value,(long)_animationTicks);
                // Isolate the wall probe; these writes model a closing tile,
                // not a complete synchronized-block puzzle solution.
                foreach (byte open in new byte[] { 0x10,0x03,0x0c })
                {
                    Collision(open); Step();
                    FailIf(_player.SideScrollSquished,"Hole-permitted or one-sided collision must not squish Link.");
                }
                Collision(0x0f);
                object owner = new();
                _player.RequestState08Control(owner);
                detector.UpdateFrame(new(_player,0,false),new List<RoomEntitySpawn>());
                FailIf(_player.SideScrollSquished,"An existing forced state$08 request must prevent the detector from overwriting it.");
                _player.EndCutsceneControl(owner);
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(8);
                    FailIf(_player.SideScrollSquished,"Initialized detector must pause during text.");
                }
                finally { _entities.TextActiveSource = text; }
                _rooms.WriteBlockPushAngle(repeat == 0 ? 0 : 8);
                _sound.ClearPlayRequestAudit();
                Step();
                FailIf(!_player.SideScrollSquished || _player.SquishAnimation is not null,
                    "Detector must queue state$11 after Link's update, without initializing it.");
                Step();
                FailIf(_player.SquishAnimation is not { State:0 },"Next Link dispatch must consume the squish request.");
                Step();
                FailIf(_player.SquishAnimation is not { State:1 } animation ||
                    animation.Current.Graphic != (repeat == 0 ? 0x33 : 0x32),
                    "Detector orientation must be (wBlockPushAngle & $08) XOR $08.");
                for (int i = 0; _player.SquishAnimation is { Finished:false } && i < 90; i++) Step();
                FailIf(_player.SquishAnimation is not { Finished:true },"Detected squish did not complete its animation.");
                _currentRoom.SetPositionTileAndCollision(new(136,104),original,0,(long)_animationTicks);
                // Dungeon breakable table: tile$11 -> mode$1b; source$05
                // is allowed, replacement$a0, effect$06, no drop/room flag.
                // Stage at the local anchor only after the actual approach.
                _currentRoom.SetPositionTileAndCollision(new(136,136),0x11,0x0f,(long)_animationTicks);
                _transitions.DeactivateWarpAtPlayerPosition(_player); // old anchor$68
                Step();
                FailIf(_player.Position != new Vector2(136,136) || _player.Visible,
                    "Detected squish must initialize local respawn on the next dispatch.");
                FailIf(_currentRoom.GetMetatile(new(136,136)) != 0xa0,
                    "Instant respawn must execute source$05 tile break after copying local coordinates.");
                FailIf((int)typeof(RoomTransitionController).GetField("_deactivatedWarpPosition",flags)!.GetValue(_transitions)! != 0x88,
                    "Instant respawn must replace the old warp marker with local anchor$88 before its invisible wait.");
                _currentRoom.SetPositionTileAndCollision(new(136,136),0x44,0,(long)_animationTicks);
                FailIf(_entities.WarpTilesDisabled || _transitions.CheckTileWarp(_player) || IsTransitioning,
                    "The respawn anchor must suppress a stair warp through the actual transition owner.");
                _currentRoom.SetPositionTileAndCollision(new(136,136),0xa0,0,(long)_animationTicks);
                int health = _inventory.HealthQuarters;
                FailIf(_player.PatchCollisionsEnabled || _player.NativeNormalStateForInteraction,
                    "Squish must retain its collision lock through instant-respawn initialization.");
                Step();
                FailIf(_player.Visible || _inventory.HealthQuarters != health || _player.PatchCollisionsEnabled,
                    "Respawn counter$02 must remain hidden and undamaged on its first decrement.");
                Step();
                FailIf(!_player.Visible || _inventory.HealthQuarters != health-2 || _player.PatchCollisionsEnabled ||
                    _player.NativeNormalStateForInteraction,
                    "Second respawn decrement must reveal Link, apply raw $fc damage and begin locked recovery.");
                Step(15,Vector2.Left);
                FailIf(_player.Position != new Vector2(136,136) || _player.PatchCollisionsEnabled ||
                    _player.NativeNormalStateForInteraction,
                    "Recovery must suppress movement and retain collision lock through update15.");
                Step(move:Vector2.Left);
                FailIf(_player.Position != new Vector2(136,136) || !_player.PatchCollisionsEnabled ||
                    !_player.NativeNormalStateForInteraction,
                    "Recovery update16 must restore normal state and collisions without executing movement yet.");
                Step();
                FailIf(_player.SideScrollSquished || !_player.Visible ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy) != 1,
                    "Detected squish must release Link and sound only once before a repeated approach.");
            }
            LoadValidationRoom(4,0x9b);
            _player.WarpTo(new(136,136));
            _player.Face(Vector2I.Up);
            _currentRoom.SetPositionTileAndCollision(new(136,120),0x10,0x0f,(long)_animationTicks);
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,1);
            Step(8,Vector2.Up);
            FailIf(_currentRoom.IsSolid(_player.Position) || !_playerWorld.TryUseBracelet(_player,primaryButton:false),
                "Held-item squish fixture must begin a pot lift from adjacent floor.");
            for (int i=0;i<11;i++) _playerWorld.UpdateBracelet(_player,Vector2.Down,false,true,false);
            for (int i=0;i<13;i++) _playerWorld.UpdateBracelet(_player,Vector2.Zero,false,false,false);
            var held = _bracelet.LiftedObject;
            FailIf(!_bracelet.HoldingTile || held is null,"Squish fixture failed to complete the isolated pot lift.");
            // Held objects use a local drawing offset; release converts its
            // vertical component back to the child's native Z coordinate.
            int heldZ = Mathf.RoundToInt(held!.Position.Y) << 8;
            _player.ForceSideScrollSquish();
            Step();
            FailIf(!_bracelet.HoldingTile,"Forced-state consumption must precede the held-item drop.");
            Step();
            FailIf(_bracelet.LiftedObject != held || _bracelet.State != BraceletState.Projectile ||
                !held.Thrown || held.SpeedRaw != 0 || held.SpeedZ != 0x1c || held.ZFixed != heldZ ||
                held.ThrowDirection != Vector2I.Zero || _player.IsCarryingObject,
                "Squish must drop the held pot with angle$ff, preserve its ITEM child and run its first gravity update.");
            Step();
            FailIf(held.ZFixed != heldZ+0x1c || held.SpeedZ != 0x38,
                "Released pot must continue falling while squish owns Link's updates.");
            LoadValidationRoom(0,0x60);
            FailIf(_entities.Entities<WallSquishRoomEntity>().Count != 0,"Room departure must remove the detector.");
        }
    }
}
