using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBraceletObjectBounceRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool drop in new[] { false, true })
        foreach (int ring in new[] { 0xff, 0x12 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xb4);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Bracelet, 1);
            _inventory.EquipA(TreasureId.Bracelet);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    "Could not equip Toss Ring $12 for the Bracelet bounce fixture.");
            }
            var db = new EnemyDatabase();
            var random = new OracleRandom();
            var ball = new SmasherCharacter();
            var parent = new SmasherCharacter();
            parent.InitializePending(db.ImportedEnemy(EnemyId.Smasher, 1), _currentRoom, new(120, 88), random, 1);
            var world = new SmasherRoomEnvironment(_ => parent, () => { }, () => { },
                () => _entities.InteractionSlotAvailable, () => _entities.PartSlotAvailable,
                () => { }, () => { }, _ => { }, 1, true);
            var adapter = (SmasherRoomEntity)_entities.TryAllocateEnemy(slot =>
            {
                ball.InitializePending(db.ImportedEnemy(EnemyId.Smasher, 0), _currentRoom, new(120, 88), random, slot);
                return new SmasherRoomEntity(ball, world, true);
            })!;
            try
            {
                // State0 places the ball at parent.X-$20 ($58,$58).
                // Approach from clear floor to its right, outside its hitbox.
                _player.WarpTo(new(128, 88));
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                StepGameplayUpdates(40, Vector2.Left, batched: batched);
                FailIf(ball.State != 9 || _currentRoom.IsSolid(_player.Position),
                    "Bracelet bounce fixture must approach its ground ball through actual room geometry.");
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                StepGameplayUpdates(13, Vector2.Zero, batched: batched);
                FailIf(!_player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled,
                    $"Bracelet bounce fixture failed to lift the reachable Smasher ball: Link={_player.Position}, ball={ball.Position}, state={ball.State}:{ball.GrabSubstate}.");
                // Carry into open floor before throwing. The later native
                // enemy wall/rebound handler runs; boss contact is excluded
                // by the declared parent invincibility below.
                StepGameplayUpdates(24, Vector2.Right, batched: batched);
                FailIf(_player.Position != new Vector2(124, 88),
                    "The carried ball must reach the open arena through floor movement.");
                var rom = new BraceletRom(_currentRoom, _player.Position, 1, ring);
                rom[0xcc2d] = 4;
                rom[0xcc2e] = 1;
                rom.BeginSmasherBallThrow(ball.Position, ball.ZFixed >> 8, drop ? 0xff : 0x08);
                parent.InvincibilityCounter = 0x40;
                int update = 0;
                void Compare()
                {
                    // Execute the original later enemy pass as well: its
                    // dropped-angle/wall rebound can change reserved C.angle.
                    rom.Update(updateEnemies: true);
                    bool active = adapter.ReservedBraceletChildActive;
                    FailIf(active != rom.ChildActive,
                        $"Bracelet non-tile drop={drop}, ring=${ring:x2}, batch={batched}, update={update}: reserved child occupancy differs.");
                    if (active)
                    {
                        var actual = adapter.ReservedThrow!;
                        Vector2 position = new(rom.Word(0xdc0c) / 256f, rom.Word(0xdc0a) / 256f);
                        FailIf(actual.Position != position || actual.ZFixed != unchecked((short)rom.Word(0xdc0e)) ||
                            actual.Speed != rom[0xdc10] || actual.SpeedZ != unchecked((short)rom.Word(0xdc14)) ||
                            actual.Angle != rom[0xdc09],
                            $"Bracelet non-tile drop={drop}, ring=${ring:x2}, update={update}: runtime pos={actual.Position}, z=${actual.ZFixed & 0xffff:x4}, speed=${actual.Speed:x2}/${actual.SpeedZ & 0xffff:x4}, angle=${actual.Angle:x2}; ROM pos={position}, z=${rom.Word(0xdc0e):x4}, speed=${rom[0xdc10]:x2}/${rom.Word(0xdc14):x4}, angle=${rom[0xdc09]:x2}.");
                    }
                    else FailIf(ball.GrabSubstate != 3 || rom[0xd085] != 3,
                        "The final native Bracelet bounce must release relatedObj2 before retiring the reserved slot.");
                    update++;
                }
                StepGameplayUpdates(1, drop ? Vector2.Zero : Vector2.Right, ["attack"], ["attack"], batched,
                    afterUpdate: Compare);
                while (rom.ChildActive && update < 100)
                    StepGameplayUpdates(batched ? 4 : 1, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(rom.ChildActive || adapter.ReservedBraceletChildActive || !rom.Sounds.Contains(SoundId.SndBombLand),
                    "A non-tile Bracelet throw must execute native bouncing and release its reserved slot.");
            }
            finally { _entities.Clear(); parent.QueueFree(); }
        }
        GD.Print("Validated executed-ROM non-tile Bracelet weight-$20 drop/throw/Toss bouncing and final related-object/slot release after collision-reachable gameplay pickup.");
    }
}
