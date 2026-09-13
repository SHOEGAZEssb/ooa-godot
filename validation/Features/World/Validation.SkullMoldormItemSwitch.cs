using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullMoldormItemSwitchWrites()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var pending = (List<RoomEntitySpawn>)typeof(RoomEntityManager).GetField("_pendingSpawns", flags)!.GetValue(_entities)!;
        var process = typeof(RoomEntityManager).GetMethod("ProcessSpawns", flags)!;
        var record = new DungeonMechanicDatabase().GetRoomRecords(4, 0x89).Single(r => r.Id == 5);
        FailIf(record.SubId != 4 || record.PackedPosition != 0x62, "Skull room4:89 must retain its native PART_SWITCH mask4 at $62.");
        foreach (bool batch in new[] { false, true })
        foreach (bool initialized in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default)
            {
                input.CaptureForValidation([], [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
            }
            _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0x80);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 128));
            Step(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Moldorm item/switch fixture must approach through the actual entrance floor.");
            _player.SetBraceletLiftCollisionsDisabled(true);
            typeof(InventoryState).GetProperty(nameof(InventoryState.Rupees))!.SetValue(_inventory, 0);
            FailIf(!_entities.TrySpawnEnemy(0x4f, 0, new Vector2(120, 80), "US Moldorm item/switch PART write", out string error), error);
            Step(3);
            var head = _entities.Entities<MoldormCharacter>().Single();
            _entities.EntityAdapters<MoldormRoomEntity>().Single(owner => owner.Node == head)
                .ApplySwordHit(head.CollisionBounds, head.Position, 0x7f, EnemyKnockbackStrength.Low, pending);
            for (int i = 0; head.KnockbackCounter > (initialized ? 1 : 0) && i < 40; i++) Step();
            FailIf(head.IsDead || head.KnockbackCounter != (initialized ? 1 : 0), "Moldorm item/switch fixture missed its pre-death dispatch.");
            // Allocation fixture: exercise native PART handlers in the two
            // tail pages. The imported switch normally belongs to room4:89;
            // its ordinary geometry/hook interactions have a separate test.
            pending.Add(new KeeseFireSpawn(new Vector2(72, 80), 0));
            pending.Add(new KeeseFireSpawn(new Vector2(88, 80), 0));
            pending.Add(new ItemDropSpawn(3, new Vector2(120, 64)));
            pending.Add(new DungeonSwitchSpawn(record));
            process.Invoke(_entities, [null]);
            var drop = _entities.Entities<ItemDropEffect>().Single();
            Vector2 dropPosition = drop.Position;
            var crystal = _entities.Entities<DungeonSwitchRoomEntity>().Single();
            if (initialized) Step();
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(_entities.Entities<MoldormCharacter>().Count != 0 || _player.Position.DistanceTo(dropPosition) < 40,
                "Moldorm must die with Link outside the item's collision area.");
            if (!initialized)
            {
                FailIf(drop.Finished || drop.Collected || _inventory.Rupees != 0 || !crystal.CollisionEnabled ||
                    _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x80 || _sound.PlayRequestsFor(0x7e) != 0,
                    "State-zero part properties must overwrite earlier raw health/collision writes before item or switch handlers dispatch.");
                Step(3);
                FailIf(drop.Finished || _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x80,
                    "An overwritten state-zero write must not become a delayed item collection or switch toggle.");
                continue;
            }
            FailIf(!drop.Collected || _inventory.Rupees != 5 || _entities.Entities<ItemDropEffect>().Count != 0 ||
                crystal.CollisionEnabled || _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x84 ||
                _currentRoom.GetMetatile(crystal.Position) != 0x0b || _sound.PlayRequestsFor(0x7e) != 1,
                "Initialized PART_ITEM_DROP must grant five rupees remotely; PART_SWITCH must XOR mask4 and set tile$0b despite disabled collision.");
            FailIf(crystal.ApplySeedHit(crystal.CollisionBounds, _player.Position, 0x20, pending) != SeedHitResult.None,
                "The raw-cleared switch collision bit must reject further item contacts.");
            Step(3);
            FailIf(_inventory.Rupees != 5 || _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x80 ||
                _currentRoom.GetMetatile(crystal.Position) != 0x0a || _sound.PlayRequestsFor(0x7e) != 4,
                "Persistent switch health0 must toggle and sound once every PART dispatch without granting the item again.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Step(4);
                FailIf(_sound.PlayRequestsFor(0x7e) != 4 || _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x80,
                    "Dialogue must freeze the initialized switch's repeated DEAD-status toggles.");
            }
            finally { _entities.TextActiveSource = text; }
            Step();
            FailIf(_sound.PlayRequestsFor(0x7e) != 5 || _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x84,
                "The affected switch must resume toggling on its first unfrozen PART pass.");
            LoadValidationRoom(4, 0x91);
            FailIf(_entities.Entities<DungeonSwitchRoomEntity>().Count != 0 ||
                _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x84,
                "Room loading must remove the transient affected part while retaining the shared dungeon switch byte.");
        }

        // Isolate @linkCollectedItem's death-trigger guard: ordinary object
        // eligibility can freeze this part while Link dies, so explicitly
        // dispatch the receiving handler after publishing its raw status.
        LoadValidationRoom(4, 0x91);
        _player.WarpTo(new Vector2(120, 112));
        var dyingDrop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(3, new Vector2(120, 64)));
        var owner = _entities.EntityAdapters<ItemDropRoomEntity>().Single();
        owner.UpdateFrame(new RoomEntityFrame(_player, 0, false, null), pending);
        int rupees = _inventory.Rupees;
        typeof(Player).GetField("_deathPending", flags)!.SetValue(_player, true);
        try
        {
            owner.ClearHealthAndCollision();
            owner.UpdateFrame(new RoomEntityFrame(_player, 1, false, null), pending);
            FailIf(!dyingDrop.Finished || dyingDrop.Collected || _inventory.Rupees != rupees,
                "@linkCollectedItem must delete without granting treasure while wLinkDeathTrigger is set.");
        }
        finally { typeof(Player).GetField("_deathPending", flags)!.SetValue(_player, false); }
    }
}
