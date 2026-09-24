using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownExchangeStair()
    {
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9f);
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
            _inventory.EquipA(InventoryState.ItemSwitchHook);
            Vector2 stair = new(88, 88);
            FailIf(_currentRoom.GetMetatile(stair) != 0x44,
                "Exchange fixture requires Crown4:9f's unchanged stair $55.");
            Vector2 approach = Vector2.Zero;
            foreach (Vector2 offset in new[] { Vector2.Down, Vector2.Right, Vector2.Left, Vector2.Up })
            {
                bool open = true;
                for (int distance = 0; distance <= 32; distance++)
                    if (_collision.Collides(stair + offset * distance)) open = false;
                if (!open) continue;
                approach = offset;
                break;
            }
            FailIf(approach == Vector2.Zero, "Exchange must approach the stair through actual collision geometry.");
            _player.WarpTo(stair + approach * 32);
            _player.Face(new Vector2I((int)-approach.X, (int)-approach.Y));
            StepGameplayUpdates(2, Vector2.Zero);
            var target = _entities.Entities<LikeLikeCharacter>()[0];
            target.BeginPegasusHit();
            StepGameplayUpdates(16, Vector2.Zero, batched: batched);
            // Stage this room's mobile enemy on the stair, after its stun's
            // invincibility expires. No tile or collision geometry is changed.
            target.Position = stair;
            FailIf(!_entities.SwitchHook!.CanLiftEnemy(stair), "The real stair must permit enemy exchange.");
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            var hook = _entities.SwitchHook.Item!;
            for (int update = 0; hook.State != 3 && update < 40; update++)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(hook.State != 3 || !target.SwitchHookHeld,
                "Normal hook input must latch the room's Like Like on the stair.");
            StepGameplayUpdates(18 + 16 + 1, Vector2.Zero, batched: batched);
            FailIf(hook.Substate != 3 || _player.Position != stair || IsTransitioning,
                "The swap must put raised Link over the stair without activating it.");
            for (int update = 0; update < 16; update++)
            {
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(IsTransitioning || _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds,
                    "Neither lowering nor the final Z=0 write may bypass the retained Link air state.");
            }
            FailIf(!hook.Finished || !_player.TopDownAirborne || _player.TopDownAirZ != 0,
                "Hook completion must leave Z=0 with the source's retained air state.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_player.TopDownAirborne || !IsTransitioning ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds + 1,
                "The next Link dispatch clears air before func_60e9 and must activate the stair exactly once.");
        }
        ReinitializeGameplayForValidation();
    }
}
