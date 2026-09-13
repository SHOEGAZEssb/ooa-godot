using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEyesoarFight()
    {
        RunEyesoarFight(false);
        RunEyesoarFight(true);
    }
    private void RunEyesoarFight(bool batch)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, Vector2 movement = default, bool fire = false)
        {
            input.CaptureForValidation(fire ? ["attack"] : [], fire ? ["attack"] : [], movement);
            if (batch) scheduler.Advance(count / 60.0, update);
            else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
        }
        while (_inventory.MaxHealthQuarters < 56) _inventory.GiveTreasure(TreasureDatabase.TreasureHeartContainer, 4);
        _inventory.RefillHealth();
        _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
        _inventory.EquipA(InventoryState.ItemSwitchHook);
        _saveData.SetRoomFlag(4, 0x6b, 0xff, false);
        LoadValidationRoom(4, 0x6c);
        _inventory.GiveTreasure(new TreasureDatabase().GetObject("TREASURE_OBJECT_BOSS_KEY_03"));
        _player.WarpTo(new Vector2(24, 88));
        FailIf(_currentRoom.IsSolid(_player.Position), "Eyesoar approach must start on real 4:6c west corridor floor.");
        for (int i = 0; !IsTransitioning && i < 100; i++) Step(movement: Vector2.Left);
        FailIf(!IsTransitioning, "Eyesoar's actual west approach did not start the room4:6b scroll.");
        var actors = _entities.Entities<EyesoarActor>();
        FailIf(actors.Count != 5 || !actors.Select(actor => (actor.Record.Id, actor.Record.SubId)).SequenceEqual(
            new[] { (0x7b, 1), (0x11, 3), (0x11, 2), (0x11, 1), (0x11, 0) }),
            "Eyesoar preload must initialize five linked actors in native allocation order and delete its spawner.");
        var body = actors.Single(actor => !actor.IsChild);
        var eyes = actors.Where(actor => actor.IsChild).ToArray();
        FailIf(_entities.RoomEnemyCount != 1 || body.State != 8 || body.Counter != 60 ||
            eyes.Any(eye => eye.State != 8 || eye.Counter != 90 || eye.Parent != body || eye.Visible),
            "Eyesoar preload lost its counted body, 60/90 counters, hidden children, or references.");
        var enemySlots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_enemySlots", flags)!.GetValue(_entities)!;
        FailIf(enemySlots.Values.Contains(0), "Eyesoar's deleted spawner must release ENEMY slot0 during preload.");
        for (int i = 0; IsTransitioning && i < 160; i++) Step();
        FailIf(IsTransitioning || _currentRoom.Id != 0x6b || body.Counter != 60 || eyes.Any(eye => eye.Counter != 90),
            "Eyesoar native counters advanced during scrolling.");
        while (eyes[0].Counter > 1) Step();
        FailIf(eyes.Any(eye => eye.State != 8 || eye.Visible) || _entities.Entities<EyesoarSpawnEffect>().Count != 0,
            "Eyesoar child spawned before its 90th active enemy update.");
        Step();
        var effects = _entities.Entities<EyesoarSpawnEffect>();
        FailIf(effects.Count != 4 || effects.Any(effect => effect.Parameter != 0) || eyes.Any(eye => eye.State != 9 || eye.Visible),
            "Four native blue ovals must initialize later in the child allocation update, without advancing their animations.");
        Step(5);
        FailIf(effects.Any(effect => effect.Parameter != 0) || eyes.Any(eye => eye.Visible), "Blue oval signal arrived before six animation updates.");
        Step();
        FailIf(effects.Any(effect => effect.Parameter != 255 || effect.Finished) || eyes.Any(eye => eye.Visible),
            "INTERAC_0b must publish its terminal parameter after ENEMY dispatch and remain allocated for the next enemy pass.");
        Step();
        FailIf(eyes.Any(eye => eye.State != 10 || !eye.Visible || eye.Distance != 24 || eye.ZFixed != -512) ||
            _entities.Entities<EyesoarSpawnEffect>().Count != 0,
            "Eyesoar children failed to observe the linked oval before its later INTERACTION deletion.");
        for (int i = 0; body.State < 10 && i < 80; i++) Step();
        FailIf(body.State != 10 || body.Counter != 1 || body.Health != 20 || body.ControlsDisabled,
            "Eyesoar's 60+60 intro did not begin the native fight.");

        int hooks = 0;
        for (int tick = 0; !body.IsDead && tick < 24000; tick++)
        {
            if (body.Health == 0) { Step(); continue; }
            bool vulnerable = body.State == 12;
            if (!_player.IsUsingSwitchHook) _inventory.EquipA(vulnerable ? InventoryState.ItemSword : InventoryState.ItemSwitchHook);
            Vector2 delta = body.Position - _player.Position;
            Vector2 move;
            if (vulnerable) move = delta.Length() > 13 ? delta.Normalized() : Vector2.Zero;
            else if (Math.Abs(delta.Y) > 2) move = new Vector2(0, Math.Sign(delta.Y));
            else move = Math.Abs(delta.X) > 60 ? new Vector2(Math.Sign(delta.X), 0) : Vector2.Zero;
            if (!_player.IsUsingSwitchHook)
                _player.Face(Math.Abs(delta.X) > Math.Abs(delta.Y) ? new Vector2I(Math.Sign(delta.X), 0) : new Vector2I(0, Math.Sign(delta.Y)));
            int oldState = body.State, oldHealth = body.Health;
            Step(movement: move, fire: (tick & 1) == 0 && (vulnerable || Math.Abs(delta.Y) <= 3));
            if (oldState != 3 && body.State == 3)
            {
                hooks++;
                FailIf(body.Substate != 0 || body.CollisionEnabled || _entities.SwitchHook!.Item is not { State: 1 },
                    "Eyesoar collision must write enemy state3/substate0 and disable collision, while the hook consumes its pending capture next update.");
                if (!batch && hooks == 1)
                {
                    Step();
                    FailIf(body.Substate != 1 || body.Counter != 150 || (body.Flags & 10) != 10 ||
                        eyes.Any(eye => eye.State != 15 || eye.TargetDistance != 24),
                        "Eyesoar hook setup must expose the body and release its four later-slot eyes in the same enemy pass.");
                    _player.DropHeldItemsForScript();
                    for (int i = 0; body.State == 3 && i < 12; i++) Step();
                    FailIf(body.State != 12 || body.Counter != 150 || body.ZFixed != 0 || !body.CollisionEnabled,
                        "Canceled Eyesoar capture must fall to the ground, preserve all150 vulnerable updates and restore collision.");
                }
            }
            if (oldState == 3 && body.State == 12)
                FailIf(body.Counter != 150 || body.ZFixed != 0 || !body.CollisionEnabled,
                    "Eyesoar's completed exchange must hand off to150 vulnerable updates only after native landing.");
            if (oldHealth != body.Health)
                FailIf(!vulnerable || body.InvincibilityCounter != 32 || !body.JustHit,
                    "Eyesoar sword damage must occur in the vulnerable phase after the enemy update.");
            if ((tick & 63) == 0) { _inventory.RefillHealth(); _inventory.ApplyDamage(4); }
        }
        FailIf(!body.IsDead || hooks == 0, $"Actual Eyesoar hook/sword fight did not finish: state={body.State}/{body.Substate}, hp={body.Health}, hooks={hooks}, Link={_player.Position}, boss={body.Position}.");
        for (int i = 0; !_saveData.HasRoomFlag(4, 0x6b, OracleSaveData.RoomFlag80) && i < 300; i++) Step();
        FailIf(!_saveData.HasRoomFlag(4, 0x6b, OracleSaveData.RoomFlag80) || _entities.RoomEnemyCount != 0 ||
            _entities.Entities<EyesoarActor>().Count != 0 || _entities.LinkCollisionsAndMenuDisabled,
            "Eyesoar death failed to remove its children, finish the explosion, release controls and set the boss-room flag.");
        if (batch)
        {
            LoadValidationRoom(4, 0x6b);
            FailIf(_entities.Entities<EyesoarActor>().Count != 0, "Defeated Eyesoar respawned on boss-room reentry.");
            Step();
        }
        FailIf(_entities.Entities<GroundTreasurePickup>() is not [{ Record: { TreasureObject: "TREASURE_OBJECT_HEART_CONTAINER_00" } }],
            "dungeonScript_bossDeath must create or restore an uncollected heart container.");
        var heart = _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(heart.Position != new Vector2(120, 88) || heart.Record.SpawnMode != 0 || heart.Record.GrabMode != 2 ||
            _saveData.HasRoomFlag(4, 0x6b, OracleSaveData.RoomFlagItem),
            "Boss reward must use source setcoords $58,$78, instant spawn and two-hand grab without setting ROOMFLAG_ITEM yet.");
        int maxHealthBefore = _inventory.MaxHealthQuarters;
        _inventory.ApplyDamage(8);
        _sound.ClearPlayRequestAudit();
        // Continue from the fight's actual floor position. The boss arena has
        // no interior obstacle between Link and its center reward.
        for (int i = 0; !_dialogue.IsOpen && i < 350; i++)
        {
            FailIf(_currentRoom.IsSolid(_player.Position), "Eyesoar reward approach left the actual boss-room floor.");
            Vector2 delta = new Vector2(120, 88) - _player.Position;
            Step(movement: delta.Normalized());
        }
        FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("Heart Container", StringComparison.Ordinal) ||
            !_saveData.HasRoomFlag(4, 0x6b, OracleSaveData.RoomFlagItem) ||
            _inventory.MaxHealthQuarters != maxHealthBefore + 4 || _inventory.HealthQuarters != _inventory.MaxHealthQuarters,
            "Walking to Eyesoar's Heart Container must show TX_0016, add four health quarters, refill health and set ROOMFLAG_ITEM.");
        Step();
        FailIf(!heart.Held || !_player.IsHoldingItemTwoHands || heart.Position != _player.Position + new Vector2(0, -14) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2,
            "Heart Container must enter the two-hand pose on the next interaction update after its treasure sound/text.");
        Vector2 heldPosition = _player.Position;
        Step(8, Vector2.Right);
        FailIf(_player.Position != heldPosition || heart.Finished || !heart.Held,
            "Heart Container pickup must hold Link and the reward until its dialogue ends.");
        _dialogue.Close(); Step(2);
        FailIf(!heart.Finished || _player.IsHoldingItemTwoHands || _player.CutsceneControlled ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Closing the Heart Container dialogue must delete the held reward and return control.");
        Step(8, Vector2.Right);
        FailIf(_player.Position.X <= heldPosition.X, "Link could not walk after collecting Eyesoar's Heart Container.");
        LoadValidationRoom(4, 0x6b); Step(5);
        FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0 || _entities.Entities<DungeonRewardRoomEntity>().Count != 0,
            "The heart container's item flag must suppress the completed reward on reentry.");
        if (batch)
        {
            _saveData.SetRoomFlag(4, 0x6b, 0xff, false);
            LoadValidationRoom(4, 0x6b); Step();
            var waiting = _entities.Entities<EyesoarActor>().Where(actor => actor.IsChild).ToArray();
            FailIf(waiting.Length != 4, "Direct-load Eyesoar fixture lost its four enemy children.");
            while (waiting[0].Counter > 1) Step();
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_interactionSlots", flags)!.GetValue(_entities)!;
            var reservations = new List<IRoomEntity>();
            try
            {
                for (int slot = 2; slot < 16; slot++) // getFreeInteractionSlot: $d2-$df.
                    if (!slots.Values.Contains(slot))
                    {
                        var reservation = new ArmosSlotReservation();
                        reservations.Add(reservation); slots.Add(reservation, slot);
                    }
                Step();
                FailIf(waiting.Any(eye => eye.State != 8 || eye.Counter != 0) || _entities.Entities<EyesoarSpawnEffect>().Count != 0,
                    "A full native INTERACTION pool must leave child counter0 and state8, with no substitute effect.");
            }
            finally
            {
                foreach (var reservation in reservations) { slots.Remove(reservation); reservation.Node.Free(); }
            }
            Step(255);
            FailIf(waiting.Any(eye => eye.State != 8 || eye.Counter != 1) || _entities.Entities<EyesoarSpawnEffect>().Count != 0,
                "Failed Eyesoar spawn must wrap its byte counter and wait255 further updates before the next zero update.");
            Step();
            FailIf(waiting.Any(eye => eye.State != 9) || _entities.Entities<EyesoarSpawnEffect>().Count != 4,
                "Eyesoar's retried child spawns did not acquire four freed native interaction slots after256 updates.");
        }
    }
}
