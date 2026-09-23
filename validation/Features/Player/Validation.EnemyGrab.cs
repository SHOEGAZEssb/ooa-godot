using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateLikeLikePlayerGrab()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var precise = typeof(Player).GetField("_precisePosition", flags)!;
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 0);
        _inventory.EquipA(InventoryState.ItemSword);
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false)
            {
                input.CaptureForValidation(attack ? ["attack"] : [], attack ? ["attack"] : [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            byte Warps() => _entities.RuntimeState.ReadWramByte(OracleRuntimeState.WarpsDisabledAddress);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new(120,144));
            Step(32, Vector2.Up);
            FailIf(_player.Position != new Vector2(120,112), "Grab fixture must enter through the room's open passage.");
            Step(attack: true);
            FailIf(!_player.IsAttacking, "Grab cancellation fixture requires a live sword parent.");
            int health = _inventory.HealthQuarters;
            FailIf(!_player.RequestLikeLikeGrab() || !_player.EnemyGrabPending ||
                _player.EnemyGrabActive || !_player.IsAttacking || Warps() != 0 ||
                _player.InvincibilityFrames != -12 || _inventory.HealthQuarters != health,
                "LINKDMG_2c must defer grab initialization, preserve health/items, and write $f4.");
            Vector2 before = _player.Position;
            var frozen = new EnemyGrabValidationRestriction();
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [frozen]);
            Step(movement: Vector2.Right, attack: true);
            FailIf(!_player.EnemyGrabActive || _player.EnemyGrabSubstate != 1 ||
                _player.EnemyGrabPending || _player.IsAttacking || Warps() != 1 ||
                _player.Position != before || _player.InvincibilityFrames != -11 ||
                !_player.RejectsOrdinaryScreenTransition,
                "Linkstate$d must initialize immediately on the forced-state update and cancel parent input.");
            // Emulate only the enemy's later objectCopyPosition write. The
            // enemy capture/duration/shield-loss handler is tested separately
            // when registered; this scenario isolates Link's state machine.
            _player.SetScriptedPosition(before + new Vector2(0.75f,0.25f));
            _player.CopyLikeLikePosition(before + new Vector2(4,0));
            Vector2 held = before + new Vector2(4,0);
            FailIf((Vector2)precise.GetValue(_player)! != held + new Vector2(0.75f,0.25f) ||
                _player.PatchCollisionsEnabled || _player.AcceptsRoomEntityContact,
                "objectCopyPosition must retain fractional bytes and disable Link collision.");
            Step(20, Vector2.Right, attack: true);
            FailIf(_player.Position != held || _player.IsAttacking || _player.InvincibilityFrames != 0 ||
                _inventory.HealthQuarters != health || Warps() != 1 || _player.RequestLikeLikeGrab(),
                "Held Link must remain stationary, reject new items/grabs, and advance signed invincibility.");
            _player.ReleaseLikeLikeGrab();
            FailIf(!_player.PatchCollisionsEnabled || _player.EnemyGrabSubstate != 4 ||
                _player.InvincibilityFrames != 0 || Warps() != 1,
                "Enemy-pass release restores collision before the next Link update supplies invincibility.");
            Step(movement: Vector2.Right, attack: true);
            FailIf(_player.EnemyGrabActive || _player.Position != held || _player.IsAttacking ||
                Warps() != 0 || _player.InvincibilityFrames != -107 || !_player.RejectsOrdinaryScreenTransition,
                "Release update must write $94 then increment to $95, without moving or starting an item.");
            Step(107);
            FailIf(_player.InvincibilityFrames != 0 || _player.RejectsOrdinaryScreenTransition ||
                !_player.RequestLikeLikeGrab(), "Released Link must accept another grab after the exact grace boundary.");
            frozen.Frozen = false;
            Step();
            _player.CopyLikeLikePosition(held);
            _player.WarpTo(before);
            FailIf(_player.EnemyGrabActive || _player.EnemyGrabPending || Warps() != 0 ||
                !_player.PatchCollisionsEnabled, "Room placement must cancel capture and release its native warp lock.");
            FailIf(!_player.RequestLikeLikeGrab(), "Capture must work after cancellation.");
            _player.WarpTo(before);
            Step();
            FailIf(_player.EnemyGrabActive || _player.EnemyGrabPending || Warps() != 0,
                "A cancelled pending grab must not initialize on the following update.");
            _entities.RuntimeState.SetWramByte(OracleRuntimeState.WarpsDisabledAddress, 0x80);
            FailIf(_player.RequestLikeLikeGrab(), "collisionEffect3d must reject any nonzero wWarpsDisabled.");
            _player.WarpTo(before);
            FailIf(Warps() != 0x80, "Cancelling an idle capture must not clear another feature's warp lock.");
            _entities.RuntimeState.SetWramByte(OracleRuntimeState.WarpsDisabledAddress, 0);
        }
        GD.Print("Validated isolated Link grabbed-state request, item cancellation, hold, release, repeat and cancellation in single/batched gameplay updates.");
    }

    private sealed class EnemyGrabValidationRestriction : IRoomEntity, IPlayerRestriction
    {
        public Node2D Node { get; } = new();
        internal bool Frozen { get; set; } = true;
        public bool FreezesPlayerUpdates => Frozen;
        public bool DisablesSword => false;
        public void SetTransitionDrawOffset(Vector2 offset) { }
    }
}
