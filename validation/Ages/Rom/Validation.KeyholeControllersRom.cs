using System.Reflection;

namespace Oracles;

public partial class ValidationRoot
{
    private void ValidateKeyholeControlCancellation()
    {
        foreach (var source in new[] {
            (Group:0,Room:0x5c,Key:0x42,X:72,Y:104),
            (Group:0,Room:0x0a,Key:0x43,X:120,Y:72),
            (Group:1,Room:0x0e,Key:0x45,X:104,Y:56),
            (Group:3,Room:0x0f,Key:0x44,X:104,Y:56),
            (Group:1,Room:0xa5,Key:0x46,X:72,Y:72) })
        foreach (bool batched in new[] { false,true })
        foreach (bool replace in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(source.Key,1);
            _inventory.SetScriptedEquippedItems(0,0);
            LoadValidationRoom(source.Group,source.Room);
            RetainKeyholeControllerRom(source.Room == 0x5c ? 3 : 2);
            _player.WarpTo(new(source.X,source.Y)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Keyhole cancellation approach must start on actual floor.");
            for (int step = 0; step < 120 && _runtimeState.ReadField("wDisabledObjects") == 0; step++)
                StepGameplayUpdates(batched ? 3 : 1,Vector2.Up,["move_up"],batched:batched);
            string context = $"Keyhole${source.Group:x}:${source.Room:x2}, batch={batched}, replace={replace}";
            FailIf(_runtimeState.ReadField("wDisabledObjects") != 0x81 ||
                _runtimeState.ReadField("wMenuDisabled") != 0x81 || _player.CutsceneControlled ||
                !_saveData.HasRoomFlag(source.Group,source.Room,0x80),
                context + ": actual floor approach did not publish native $81/$81 without private control.");
            var successor = new NativeScriptInputControl(_runtimeState);
            if (replace) { successor.SetDisabledObjects(0x81); successor.SetMenuDisabled(0x81); }
            Action cancel = source.Room switch
            {
                0x5c => _roomEvents.Get<GraveyardGateEvent>().Cancel,
                0x0a => _roomEvents.Get<CrownDungeonEntranceEvent>().Cancel,
                0xa5 => _roomEvents.Get<LibraryKeyholeEvent>().Cancel,
                _ => _roomEvents.Get<MermaidsCaveEntranceEvent>().Cancel
            };
            cancel();
            byte expected = replace ? (byte)0x81 : (byte)0;
            FailIf(_runtimeState.ReadField("wDisabledObjects") != expected ||
                _runtimeState.ReadField("wMenuDisabled") != expected ||
                !_saveData.HasRoomFlag(source.Group,source.Room,0x80) || !_inventory.HasTreasure(source.Key),
                context + ": cancellation lost newer control or changed the source room flag/retained key.");
            successor.Cancel();
            Vector2 before = _player.PrecisePosition;
            StepGameplayUpdates(2,Vector2.Down,["move_down"],batched:batched);
            FailIf(_runtimeState.ReadField("wDisabledObjects") != 0 || _runtimeState.ReadField("wMenuDisabled") != 0 ||
                _player.PrecisePosition.Y <= before.Y,
                context + ": released control did not resume Link along the actual approach floor.");
        }
    }

    private void CompareKeyholeControlBytesRom(SomariaRom rom, string context)
    {
        FailIf(_runtimeState.ReadField("wDisabledObjects") != rom[0xcc8a] ||
            _runtimeState.ReadField("wMenuDisabled") != rom[0xcc02],
            $"{context}: native control bytes differ: objects=${_runtimeState.ReadField("wDisabledObjects"):x2}/${rom[0xcc8a]:x2}, menu=${_runtimeState.ReadField("wMenuDisabled"):x2}/${rom[0xcc02]:x2}.");
    }

    private KeyholeControllerRoomEntity RetainKeyholeControllerRom(int expectedSlot = 2, bool retainCompanionPlacements = false)
    {
        // Keep the actual production allocation; remove unrelated actors from
        // this focused fixture through the manager's normal release operation.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var entities = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!;
        var release = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
        var controller = entities.OfType<KeyholeControllerRoomEntity>().Single();
        foreach (var entity in entities.Where(entity => !ReferenceEquals(entity,controller) &&
            (!retainCompanionPlacements || entity is not CompanionBarrierRoomEntity and not CompanionTutorialRoomEntity)).ToArray())
        {
            entities.Remove(entity);
            release.Invoke(_entities,[entity]);
        }
        FailIf(controller.PresentationNode is not null || _entities.NativeInteractionSlot(controller) != expectedSlot,
            $"mainData.s keyhole controller must retain original INTERACTION${0xd0+expectedSlot:x2}.");
        return controller;
    }
}
