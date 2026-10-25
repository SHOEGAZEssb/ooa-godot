using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void CompareFallingFireKeeseAliasRom()
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(7, 0x01);
        var keeseOwner = _entities.ResolveEnemySlot(0) as KeeseRoomEntity;
        FailIf(keeseOwner is null, "Room$7:$01 must retain the native Keese owner in ENEMY$d0.");
        var counter = (INativeEnemyCounter2RoomEntity)keeseOwner!;
        var original = new FrontendRom();
        // Execute func_553f with the real PART$d0 page for every possible
        // pre-existing ENEMY.counter2. Its returned timer uses the ROM table.
        byte[] caller = [0x16, 0xd0, 0xcd, 0x3f, 0x55, 0xc9];
        for (int index = 0; index < caller.Length; index++) original[0xc100 + index] = caller[index];
        for (int value = 0; value < 256; value++)
        {
            counter.Counter2 = value; original[0xd087] = (byte)value;
            int cycle = _entities.AdvanceFallingFireCycle(0);
            original.Call(0xc100, 0x11);
            FailIf(counter.Counter2 != original[0xd087] || cycle != counter.Counter2 ||
                new[] { 120, 120, 30, 30 }[cycle] != original[0xd0c6],
                "PART$23 must read/write the live Keese counter2 and select the original interval.");
        }
        foreach (var entity in _entities.EntityAdapters<IRoomEntity>().Where(entity => !ReferenceEquals(entity, keeseOwner)).ToArray())
            _entities.RetireEntity(entity);
        var keese = (KeeseCharacter)keeseOwner!.Node;
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        // Declare the approach-Keese turn-expiry boundary independently of
        // room placement. Foreign writes can leave counter2 zero while moving.
        typeof(KeeseCharacter).GetField("<Record>k__BackingField", hidden)!.SetValue(keese, keese.Record with { SubId = 1 });
        typeof(KeeseCharacter).GetField("_state", hidden)!.SetValue(keese, KeeseState.Moving);
        typeof(KeeseCharacter).GetField("_counter1", hidden)!.SetValue(keese, 1);
        typeof(KeeseCharacter).GetField("_speed", hidden)!.SetValue(keese, 0);
        typeof(KeeseCharacter).GetField("_angle", hidden)!.SetValue(keese, 0);
        typeof(KeeseCharacter).GetField("_turnAmount", hidden)!.SetValue(keese, 2);
        counter.Counter2 = 0;
        _player.WarpTo(new(8, 8)); _player.BeginCutsceneControl();
        var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 8, 8);
        native[0xd080] = 1; native[0xd081] = 0x32; native[0xd082] = 1;
        native[0xd084] = 9; native[0xd086] = 1; native[0xd0b0] = 2; native[0xd0a9] = 1;
        native[0xd08b] = (byte)keese.Position.Y; native[0xd08d] = (byte)keese.Position.X;
        native[0xd0a0] = 0x7f; // Declared held animation; no animation pointer fetch on this update.
        StepGameplayUpdates(1, Vector2.Zero, afterUpdate: () => native.AdvanceEnemies(_entities.FrameCounter));
        FailIf(keese.Counter2 != 0xff || keese.Counter2 != native[0xd087] ||
            keese.Counter1 != native[0xd086] || keese.State != KeeseState.Moving,
            "A zero Keese turn count written by PART$23 must wrap to $ff on the next native turn expiry.");
    }
}
