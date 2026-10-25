using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void CompareMaplePartAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1, 10 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
            _saveData.SetMapleKillCounter(30); LoadValidationRoom(1, 2);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(80, 80)); _player.BeginCutsceneControl();
            var maple = _entities.Entities<MapleEncounter>().Single(); F1Retain(_entities.EntityAdapters<MapleEncounterRoomEntity>().Single());
            for (int update = 0; update < 1600 &&
                !(maple.Stage == MapleEncounterStage.Flying && maple.MainFlight &&
                  SomariaPrivate<int>(maple, "_counter") == 0 && maple.Position.X is > 24 and < 136 && maple.Position.Y is > 24 and < 104); update++)
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(maple.Stage != MapleEncounterStage.Flying || !maple.MainFlight || SomariaPrivate<int>(maple, "_counter") != 0,
                $"The real Maple flight must reach its collision-ready path: stage={maple.Stage}, main={maple.MainFlight}, counter={SomariaPrivate<int>(maple, "_counter")}, position={maple.Position}, room={_rooms.ActiveGroup}:{_currentRoom.Id:x2}.");
            _player.WarpTo(maple.Position);
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            F1Fill(free, native);
            native[0xd100] = 1; native[0xd101] = 0x0e; native[0xd103] = (byte)maple.DropPattern;
            native[0xd10b] = (byte)maple.Position.Y; native[0xd10d] = (byte)maple.Position.X; native[0xd10f] = 0xf8;
            native[0xffae] = 0; native[0xffaf] = 0xd1;
            byte[] caller = [0x16, 0xd1, 0xcd, 0x93, 0x69, 0xc9]; // mapleSpawnItemDrops, clean US $05:$6993.
            for (int i = 0; i < caller.Length; i++) native[0xc100 + i] = caller[i];
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 5);
            int selectionCalls = native.RandomCalls;
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(maple.Stage != MapleEncounterStage.Recoiling, "Maple must collide through the real application update.");
            F1Birth(native);
            int birthCalls = native.RandomCalls - selectionCalls;
            native[0xcd19] = 15; native.AdvanceScreenShake();
            var drops = _entities.Entities<MapleDroppedItem>();
            int count = F1Count(native, 0x14) + F1Count(native, 0x15);
            FailIf(drops.Count != count || count > free || F1Parts.Count != 16 - free + count,
                "Maple must skip checked PART$14/$15 allocations without constructing encounter-private drops.");
            foreach (var item in drops)
                FailIf(item.Slot != F1Parts.Single(pair => ReferenceEquals(pair.Key.Node, item)).Value ||
                    item.ItemIndex != native[0xd0c3 + item.Slot * 256],
                    "Maple item identity and target order must use the actual shared PART slots.");
            FailIf(SomariaPrivate<int>(maple, "_uniqueItemMask") != native[0xd12c] ||
                SomariaPrivate<bool>(maple, "_heartPieceSpawned") != (native[0xd12b] != 0),
                "Failed Maple drops must retain the source unique-item writes made before allocation.");
            foreach (int address in new[] { 0xc6aa, 0xc6ad, 0xc6ae, 0xc6b0, 0xc6b9, 0xc6ba, 0xc6bb, 0xc6bc, 0xc6bd })
                FailIf(_saveData.ReadWramByte(address) != native[address], $"Maple allocation failure must preserve source inventory deduction at ${address:x4}.");
            FailIf(_random.Calls - seed.Calls != native.RandomCalls,
                $"Maple RNG quantities: free={free}, runtime={_random.Calls - seed.Calls}, native={native.RandomCalls}, selection={selectionCalls}, birth={birthCalls}, items={string.Join(',', drops.Select(drop => drop.ItemIndex))}.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            _entities.Clear(); FailIf(F1Parts.Count != 0, "Maple cancellation must release every shared allocation.");
        }
    }
}
