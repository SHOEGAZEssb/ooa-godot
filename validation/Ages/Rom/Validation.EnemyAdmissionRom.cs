using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private Dictionary<IRoomEntity, int> F8Enemies => SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_enemySlots");

    private void F8Setup()
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xa1); _entities.Clear();
        _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(216, 144)); _player.BeginCutsceneControl();
        // Declared clear geometry isolates capacity, rather than claiming a
        // natural retail route to a saturated object pool.
        FillValidationRoom(0x0c, width: 15, height: 11);
    }

    private void F8Fill(SomariaRom native, int free)
    {
        foreach (int slot in Enumerable.Range(0, 16).Where(slot => !F8Enemies.ContainsValue(slot)).Skip(free).ToArray())
        {
            var held = new EnemyAiSlotReservation(); _entities.RegisterEnemySlot(held, slot); _entities.AddEntity(held);
            int at = 0xd080 + slot * 256; native[at] = 1; native[at + 4] = 8;
        }
    }

    private SomariaRom F8Native(int id, int sub, int parentSlot)
    {
        var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 216, 144);
        int at = 0xd080 + parentSlot * 256;
        native[at] = 0x11; native[at + 1] = (byte)id; native[at + 2] = (byte)sub;
        native[at + 11] = 72; native[at + 13] = 80; native[0xcdd1] = 1;
        return native;
    }

    private void F8Parent(SomariaRom native, int parentSlot)
    {
        native[0xffae] = 0x80; native[0xffaf] = (byte)(0xd0 + parentSlot);
        native[0xcc00] = (byte)_entities.FrameCounter;
        byte[] caller = [0x16, (byte)(0xd0 + parentSlot), 0xcd, 0x07, 0x2f, 0xc9];
        for (int i = 0; i < caller.Length; i++) native[0xc100 + i] = caller[i];
        SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 0x0d);
    }

    private void ValidateEnemyAdmissionRom()
    {
        var enemies = EnemyDatabase.Shared;
        var redSource = Enumerable.Range(0, 8).SelectMany(group => Enumerable.Range(0, 256)
            .SelectMany(room => enemies.GetRoomObjects(group, room))).First(row => row.Id == 0x34 && row.SubId == 1);
        foreach (bool batched in new[] { false, true })
        foreach (int parentSlot in new[] { 0, 15 })
        foreach (int id in new[] { 0x34, 0x70, 0x7a, 0x52 })
        foreach (int free in id == 0x70 ? new[] { 0, 1, 2, 3 } : id == 0x34 ? new[] { 0, 1, 2 } : new[] { 0, 1 })
        {
            F8Setup(); IRoomEntity parent;
            if (id == 0x34)
            {
                var source = redSource;
                FailIf(!enemies.TryGetZolDefinition(source, out var definition), "Missing source red Zol placement.");
                var zol = new ZolCharacter(); zol.Initialize(definition, _currentRoom, new(80, 72), _random, _entities.OnSoundRequested);
                parent = new ZolRoomEntity(zol, enemies.EnemyHandlers.ResolveHandler(source).CombatSource(source, 1), _entities.OnSoundRequested);
                F1Set(zol, "_state", ZolState.RedSplitDelay); F1Set(zol, "_counter2", 1);
            }
            else if (id == 0x70)
            {
                var boss = new GiantGhiniBoss();
                boss.Initialize(new DungeonBossDatabase().Enemy(id), _currentRoom, new(80, 72), _random,
                    _entities.OnSoundRequested, () => false, () => { }, () => { },_entities.ToScreen);
                parent = new GiantGhiniBossRoomEntity(boss, Vector2I.Up);
            }
            else if (id == 0x7a)
            {
                var boss = new ShadowHagBoss();
                boss.Initialize(new DungeonBossDatabase().Enemy(id), _currentRoom, new(80, 72), _random,
                    _entities.OnSoundRequested, () => false, () => { }, () => { }, () => { }, (_, _, _) => { }, () => false, "");
                F1Set(boss, "_initialized", true); F1Set(boss, "_introActive", false);
                F1Set(boss, "_state", ShadowHagState.SpawningBugs); F1Set(boss, "_counter1", 0x31);
                parent = new ShadowHagBossRoomEntity(boss, Vector2I.Up);
            }
            else
            {
                var scanner = new FlyingTileSpawnerRoomEntity(0, enemies.ImportedEnemy(id), true, _random);
                F1Set(scanner, "_state", FlyingTileSpawnerState.Waiting); F1Set(scanner, "_counter", 1);
                parent = scanner;
            }
            _entities.RegisterEnemySlot(parent, parentSlot); _entities.AddEntity(parent);
            var native = F8Native(id, id == 0x34 ? 1 : 0, parentSlot);
            int at = 0xd080 + parentSlot * 256;
            if (id != 0x70)
            {
                F8Parent(native, parentSlot);
                if (id == 0x34) { native[at + 4] = 0x0d; native[at + 7] = 1; }
                else if (id == 0x7a) { native[at + 4] = 0x0d; native[at + 6] = 0x31; native[at + 0x30] = 0; }
                else { F8Parent(native, parentSlot); native[at + 6] = 1; }
            }
            F8Fill(native, free);
            int[] freeSlots = Enumerable.Range(0, 16).Where(slot => !F8Enemies.ContainsValue(slot)).ToArray();
            var seed=_random.CaptureState();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); F8Parent(native, parentSlot);
            int childId = id switch { 0x34 => 0x43, 0x70 => 0x3f, 0x7a => 0x42, _ => 0x52 };
            Type childType = id switch { 0x34 => typeof(GelCharacter), 0x70 => typeof(GiantGhiniChild), 0x7a => typeof(ShadowHagBug), _ => typeof(FlyingTileCharacter) };
            var children = _entities.EntityAdapters<IRoomEntity>().Where(actor => childType.IsInstanceOfType(actor.Node)).ToArray();
            int[] nativeChildren = Enumerable.Range(0, 16).Where(slot => slot != parentSlot && native[0xd080 + slot * 256] != 0 && native[0xd081 + slot * 256] == childId).ToArray();
            int expected = Math.Min(free, id == 0x70 ? 3 : id == 0x34 ? 2 : 1);
            FailIf(children.Length != expected || nativeChildren.Length != expected ||
                children.Any(child => !F8Enemies.ContainsKey(child)) || !nativeChildren.SequenceEqual(freeSlots.Take(expected)) ||
                !children.Select(child => F8Enemies[child]).Order().SequenceEqual(nativeChildren),
                $"ENEMY${id:x2} must preserve checked ordered allocation before parent deletion: free={free}, parentSlot=${parentSlot:x2}, runtime={children.Length}, native={nativeChildren.Length}.");
            if (id == 0x70)
            {
                // The real enemy walk also visits newly allocated higher slots.
                // Lower slots retain state0 until the next walk.
                foreach(int childSlot in nativeChildren.Where(slot=>slot>parentSlot))F8Parent(native,childSlot);
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Giant Ghini's state0 and admitted higher-slot children must consume common RNG in original order: parent${parentSlot:x2}, free{free}, calls{_random.CaptureState().Calls-seed.Calls}/{native.RandomCalls}.");
                FailIf(((GiantGhiniBoss)parent.Node).ChildrenAlive != native[at + 0x30], "Giant Ghini must count only successfully admitted children.");
                for (int i = 0; i < expected; i++)
                    FailIf(children[i].Node.Name != $"GiantGhiniChild_{i}" || (native[0xd082 + nativeChildren[i] * 256] & 3) != 3 - i,
                        "Giant Ghini's partial batch must retain source subids $03,$02,$01 in physical-slot order.");
            }
            if (id == 0x7a)
                FailIf(SomariaPrivate<int>(parent.Node, "_bugsAlive") != native[at + 0x30], "Shadow Hag must count only admitted uncounted bugs.");
            if (id == 0x52)
                FailIf(((FlyingTileSpawnerRoomEntity)parent).LayoutIndex != 1 || ((FlyingTileSpawnerRoomEntity)parent).Counter != native[at + 6],
                    "Flying Tile failure must consume the layout entry and reload the original cadence.");
            FailIf(_entities.RoomEnemyCount != native[0xcdd1], "Checked enemy failures must not invent room-count contributions.");
            if (free == 0)
            {
                var held = F8Enemies.First(pair => pair.Key is EnemyAiSlotReservation); _entities.RetireEntity(held.Key);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.EntityAdapters<IRoomEntity>().Any(actor => childType.IsInstanceOfType(actor.Node)),
                    "Released capacity must not replay a failed split, initial child batch, bug gate, or consumed tile entry.");
            }
            _entities.Clear(); FailIf(F8Enemies.Count != 0, "Cancellation must release admitted enemies and fixture reservations.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared F8 checked child admission, partial batches, parent counts, occupied-parent boundaries and cancellation against original handlers.");
    }
}
