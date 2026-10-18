using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private (WallmasterCharacter Hand, WallmasterRoomEntity Adapter, EnemyStatusRom Native)
        PrepareWallmasterIdentityRom(bool batched)
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x12);
        _inventory.EquipA(0); _inventory.EquipB(0);
        var hand = _entities.Entities<WallmasterCharacter>().Single();
        var adapter = _entities.EntityAdapters<WallmasterRoomEntity>().Single();
        foreach (var other in _entities.EntityAdapters<IRoomEntity>().Where(a => !ReferenceEquals(a.Node, hand)).ToArray())
            _entities.RetireEntity(other);
        var floor = Enumerable.Range(0, _currentRoom.WidthInTiles * _currentRoom.HeightInTiles)
            .Select(i => new Vector2(i % _currentRoom.WidthInTiles * 16 + 8, i / _currentRoom.WidthInTiles * 16 + 8))
            .Where(p => p.Y > 32 && p.X > 32 && p.Y < _currentRoom.Height - 32 && p.X < _currentRoom.Width - 32)
            .First(p => new[] { p, p + Vector2.Up * 16, p + Vector2.Down * 16 }
                .All(at => !_collision.Collides(at) && _currentRoom.GetTerrainInfo(at).Hazard == HazardType.None));
        _player.WarpTo(floor, recordSafe: false); _player.BeginCutsceneControl();
        var native = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
        native[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x28 * 4];
        native[0xd080] = 1; native[0xd081] = 0x28; native[0xd082] = 1;
        native.Update(_entities.FrameCounter, floor); // Original subid-$01 state zero.
        FailIf(native[0xd084] != 8, "Original Wallmaster hand state zero must enter state $08.");
        int wait = 0;
        while (hand.State != WallmasterState.Falling && wait++ < 512)
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
        FailIf(hand.State != WallmasterState.Falling, "Actual room $4:$12 spawner must start a falling hand.");
        // The port merges spawner and hand. Align at the first falling pose;
        // the original hand's state-$08 body owns the reference identity.
        native.Update(_entities.FrameCounter, floor);
        FailIf(native[0xd0a4] != 0xb5 || native[0xd0a5] != 0x25 ||
            native[0xd0a6] != 6 || native[0xd0a7] != 6,
            $"Original wallmaster_state8 must write collision $b5/mode $25/radii $06,$06; got ${native[0xd0a4]:x2}.");
        var away = Enumerable.Range(0, _currentRoom.WidthInTiles * _currentRoom.HeightInTiles)
            .Select(i => new Vector2(i % _currentRoom.WidthInTiles * 16 + 8, i / _currentRoom.WidthInTiles * 16 + 8))
            .First(p => p.DistanceTo(floor) > 48 && !_collision.Collides(p) && _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None);
        _player.WarpTo(away, recordSafe: false);
        return (hand, adapter, native);
    }

    private void ValidateWallmasterCollisionIdentityRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int repeat in new[] { 0, 1 })
        {
            var (hand, adapter, native) = PrepareWallmasterIdentityRom(batched);
            bool paused = false;
            var previousText = _entities.TextActiveSource;
            _entities.TextActiveSource = () => paused;
            void CompareIdentity()
            {
                FailIf(adapter.DimitriCollisionType != (native[0xd0a4] & 0x7f),
                    $"Wallmaster live collision identity: port=${adapter.DimitriCollisionType:x2}, ROM=${native[0xd0a4]:x2}, state={hand.State}, counter={hand.Counter}, batch={batched}, repeat={repeat}.");
            }
            void Step(int count, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, batched: batched, afterUpdate: () =>
                {
                    native[0xcba0] = paused ? (byte)1 : (byte)0;
                    native.Update(_entities.FrameCounter, _player.Position);
                    CompareIdentity();
                    // Link/item execution is outside this enemy fixture. The
                    // actual gameplay Link pose is the collision input here.
                    native[0xd024] = 0x80; native[0xd026] = native[0xd027] = 6;
                    native.ResolveLinkCollisions();
                });
            try
            {
                CompareIdentity();
                int wait = 0;
                while (hand.State != WallmasterState.Grounded && wait++ < 128) Step(1);
                FailIf(hand.State != WallmasterState.Grounded || hand.Counter != 30 || native[0xd086] != 30,
                    "Native and runtime falling hands must reach the 30-update ground window.");
                Step(28);
                FailIf(hand.Counter != 2 || native[0xd0a4] != 0xb5, "Ground counter $02 must still borrow collision identity $35.");
                paused = true; Step(2); paused = false;
                FailIf(hand.Counter != 2 || native[0xd086] != 2, "Text must preserve the identity-change boundary.");
                Vector2 approach = hand.Position + Vector2.Up * 13;
                FailIf(_collision.Collides(approach) || _collision.Collides(approach + Vector2.Down * 2),
                    "Late-grab control must approach through the room's actual open collision geometry.");
                _player.WarpTo(approach, recordSafe: false); _player.EndCutsceneControl();
                Step(1, Vector2.Down);
                FailIf(hand.Counter != 1 || native[0xd0a4] != 0xa8,
                    "The counter-$01 update must restore enabled collision identity $28.");
                FailIf(_player.CutsceneControlled || (native[0xd0aa] & 0x80) != 0,
                    "Wallmaster identity $28 must reject a normal Link approach on the counter-$01 update.");
                Step(2);
                FailIf(_player.CutsceneControlled || hand.State != WallmasterState.Rising,
                    "The weapon-vulnerable rising hand must not recapture Link.");
                // Retire an active hand, then repeat through a fresh actual
                // room load on the next row instead of retaining its identity.
                _entities.Clear(); native[0xd080] = 0; Step(2);
                FailIf(_entities.Entities<WallmasterCharacter>().Count != 0,
                    "Room cancellation must retire the hand and its identity owner.");
            }
            finally { _entities.TextActiveSource = previousText; }
        }
        ReinitializeGameplayForValidation();
    }

    private void ValidateWallmasterBiggoronMaskRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool late in new[] { false, true })
        {
            var (hand, _, native) = PrepareWallmasterIdentityRom(batched);
            int wait = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched,
                afterUpdate: () => native.Update(_entities.FrameCounter, _player.Position));
            while (hand.State != WallmasterState.Grounded && wait++ < 128) Step(1);
            if (late) Step(29);
            FailIf(native[0xd0a4] != (late ? 0xa8 : 0xb5),
                "Biggoron control must use the identity written by original Wallmaster AI, never supplied $a9.");
            // Declared open combat patch/health isolate controller dispatch;
            // the preceding identity transition executes independently in ROM.
            byte floor = _currentRoom.GetMetatile(hand.Position);
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), floor, null, 0);
            _player.EndCutsceneControl(); _player.WarpTo(new(80, 64), recordSafe: false); _player.Face(Vector2I.Up);
            hand.Position = new(99, 66); hand.Health = 40; hand.InvincibilityCounter = 0;
            native[0xd08b] = 66; native[0xd08d] = 99; native[0xd0a9] = 40;
            native.HitWithSword(7, 5);
            _inventory.GiveTreasure(TreasureId.BiggoronSword, 1); _inventory.EquipA(TreasureId.BiggoronSword);
            StepGameplayUpdates(3, Vector2.Zero, ["attack"], ["attack"], batched: batched);
            FailIf(hand.Health != 35 || native[0xd0a9] != 35 || _entities.Biggoron?.Weapon is null,
                $"Both original $35/$28 masks permit actual Biggoron damage: port={hand.Health}, ROM={native[0xd0a9]}, late={late}, batch={batched}.");
            _entities.ClearPhysicalPlayerItems(); _entities.Clear();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
        }
        ReinitializeGameplayForValidation();
    }
}
