using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTargetGhiniStairs()
    {
        var source = new EnemyDatabase().GetRoomObjects(5, 0x9b).Single();
        // mainData.s: $13:$01 at $48/$68, then $20:$05 at $18/$88;
        // enemyData.s aliases rooms $99/$9b/$9d to $64/$17/$02 (three).
        FailIf(source is not { Kind: RoomObjectKind.RandomEnemy, Id: 0x17, SubId: 2, Count: 3, Flags: 0x64 },
            "5:9b must retain its three source-counted target Ghinis, not an empty enemy stream.");
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.SetScriptedEquippedItems(0, 0);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Enter()
            {
                LoadValidationRoom(5, 0x9b);
                typeof(Player).GetField("_enemyInvincibilityFrames", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_player, 100000f);
                FailIf(_entities.Entities<TargetGhiniCharacter>().Count != 3 || _entities.RoomEnemyCount != 3 ||
                    _currentRoom.GetPackedStorageMetatile(0x18) == 0x45,
                    "5:9b must enter with three Ghinis and no down stairs at tile$18.");
                _sound.ClearPlayRequestAudit();
            }
            Enter();
            byte initialTile = _currentRoom.GetPackedStorageMetatile(0x18);
            var script = _entities.Entities<DungeonTileScriptRoomEntity>().Single();
            Step(180);
            FailIf(script.Step != 1 || _entities.RoomEnemyCount != 4 || _saveData.HasRoomFlag(5, 0x9b, 0x80) ||
                _currentRoom.GetPackedStorageMetatile(0x18) != initialTile || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
                $"5:9b idle entry must never satisfy checknoenemies, reveal stairs or play SND_SOLVEPUZZLE: script={script.Step}, count={_entities.RoomEnemyCount}, flags={_saveData.GetRoomFlags(5, 0x9b):x2}, tile={_currentRoom.GetPackedStorageMetatile(0x18):x2}/{initialTile:x2}, cues={_sound.PlayRequestsFor(SoundId.SndSolvePuzzle)}.");
            // Cancelling the still-waiting script by leaving must not publish
            // a delayed completion, and returning must reconstruct the roster.
            LoadValidationRoom(5, 0x9a); Step(4); Enter(); Step(40);
            script = _entities.Entities<DungeonTileScriptRoomEntity>().Single();
            var enemies = _entities.EntityAdapters<TargetGhiniRoomEntity>().ToArray();
            for (int index = 0; index < enemies.Length; index++)
            {
                var enemy = enemies[index];
                var actor = (TargetGhiniCharacter)enemy.Node;
                FailIf(!enemy.ApplyItemCollision((RoomEntityItemCollision)ItemCollisionType.L1Sword,
                    actor.CollisionBounds, actor.Position.Floor() - Vector2.Right,
                    10, new List<RoomEntitySpawn>()), "5:9b ENEMY$17:$02 must accept a lethal source sword collision.");
                FailIf(_entities.RoomEnemyCount != 4 - index,
                    "A lethal Ghini hit must retain its room count through recoil and its death puff.");
                int guard = 0;
                while (_entities.RoomEnemyCount == 4 - index && guard++ < 100) Step();
                FailIf(_entities.RoomEnemyCount != 3 - index,
                    "ENEMY$17:$02 death puff must release exactly one counted enemy.");
                FailIf(_saveData.HasRoomFlag(5, 0x9b, 0x80) || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0 ||
                    _currentRoom.GetPackedStorageMetatile(0x18) != initialTile,
                    "5:9b stairs must wait through all Ghini recoil/death puffs and the following script commands.");
            }
            Step(40);
            FailIf(script.Step != 1 || _entities.RoomEnemyCount != 1 || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
                "5:9b $13:$01 must retain one synthetic enemy after the Ghinis die, until its block at $46 is pushed.");
            Vector2 block = new(0x68, 0x48);
            Vector2 approach = Vector2.Zero;
            foreach (Vector2 offset in new[] { Vector2.Down, Vector2.Right, Vector2.Left, Vector2.Up })
            {
                if (_collision.Collides(block + offset * 16) || _currentRoom.IsSolid(block - offset * 16)) continue;
                _player.WarpTo(block + offset * 16); approach = -offset; break;
            }
            FailIf(approach == Vector2.Zero, "5:9b block$46 needs a clear adjacent floor and push destination.");
            FailIf(_collision.Collides(_player.Position), "5:9b block approach must start on real clear floor.");
            for (int guard = 0; script.Step == 1 && guard < 150; guard++)
                StepGameplayUpdates(1, approach, batched: batched);
            FailIf(script.Step != 2 || _entities.RoomEnemyCount != 0,
                $"5:9b must clear its synthetic enemy only after a real block push and the $1e-update trigger delay: step={script.Step}, count={_entities.RoomEnemyCount}, player={_player.Position}, approach={approach}, block={_currentRoom.GetPackedStorageMetatile(0x46):x2}, pushing={_pushBlocks.Active}.");
            Step();
            FailIf(script.Step != 3 || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1 ||
                _saveData.HasRoomFlag(5, 0x9b, 0x80), "5:9b must play SND_SOLVEPUZZLE one command before publishing its stairs/flag.");
            Step();
            FailIf(_currentRoom.GetPackedStorageMetatile(0x18) != 0x45 || !_saveData.HasRoomFlag(5, 0x9b, 0x80),
                "5:9b must publish down stairs$45 at $18 and room flag$80 after all three Ghinis die.");
            Step(40);
            FailIf(_sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1, "5:9b must solve only once.");
            LoadValidationRoom(5, 0x9a); Step(4); LoadValidationRoom(5, 0x9b);
            _sound.ClearPlayRequestAudit(); Step(40);
            FailIf(_entities.Entities<DungeonTileScriptRoomEntity>().Any() ||
                _currentRoom.GetPackedStorageMetatile(0x18) != 0x45 || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
                "5:9b solved re-entry must preserve stairs and retire $20:$05 without replaying the jingle.");
        }
        GD.Print("Validated 5:9b target Ghini admission, idle/re-entry/cancellation, counted recoil/death-puff handoff, exact stair commands, cue and ROOMFLAG$80 persistence.");
    }
}
