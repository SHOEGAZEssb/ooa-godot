using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePickaxeInteractionAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int parentSlot in new[] { 2, 15 })
        foreach (int free in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(1, 0x48); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 112)); _player.BeginCutsceneControl();
            var record = new NpcDatabase().GetRoomNpcs(1, 0x48).Single(row => row.Id == 0x57 && row.SubId == 0);
            var worker = NpcCharacter.CreateFromRecord(record); var data = new Room148PickaxeDatabase().Record;
            worker.SetScriptAnimation(data.WorkAnimation);
            var parent = new Room148PickaxeWorkerRoomEntity(worker, data, _sound.PlaySound);
            _entities.AddEntity(parent);
            // Normal source placement is $d2. The declared $df case isolates
            // earlier-slot reuse without asserting a retail saturation route.
            InteractionAdmissionPool[parent] = parentSlot;
            StepGameplayUpdates(25, Vector2.Zero, batched: batched);
            _sound.ClearPlayRequestAudit();
            FailIf(_entities.Entities<Room148PickaxeDebris>().Count != 0, "INTERAC$57 first strike must wait for update 26.");
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 112);
            int parentAt = 0xd040 + parentSlot * 256;
            native[parentAt] = 0x81; native[parentAt + 1] = 0x57; native[parentAt + 4] = 1;
            native[parentAt + 11] = 0x58; native[parentAt + 13] = 0x38; native[parentAt + 0x1a] = 0x82;
            FillInteractionAdmissionPool(native, free);
            void SourceStrike()
            {
                native[parentAt + 0x21] = 1; native[0xffae] = 0x40; native[0xffaf] = (byte)(parentAt >> 8);
                byte[] caller = [0x16, (byte)(parentAt >> 8), 0x3e, 3, 0xcd, 0xa4, 0x6a, 0xc9];
                for (int index = 0; index < caller.Length; index++) native[0xc100 + index] = caller[index];
                SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 9);
            }
            SourceStrike();
            // Advance only source slots above the producer on its birth pass;
            // the low slots have already run. Older puffs are inert setup here.
            var source = NativeAdmissionChildren(native, 0x92, 6);
            if (parentSlot == 2)
            {
                native[parentAt] = 0;
                native.AdvanceInteractions();
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            var chips = _entities.Entities<Room148PickaxeDebris>().OrderBy(_entities.InteractionSlot).ToArray();
            FailIf(chips.Length != free || source.Length != free || _sound.PlayRequestsFor(SoundId.SndClink) != 1,
                $"INTERAC$57/$92 free={free}, parent=${parentSlot:x2}: chips={chips.Length}/{source.Length}, clinks={_sound.PlayRequestsFor(SoundId.SndClink)}, parameter={worker.CurrentAnimationParameter}; must create only the admitted prefix.");
            for (int index = 0; index < chips.Length; index++)
                FailIf(_entities.InteractionSlot(chips[index]) != (source[index] >> 8) - 0xd0 ||
                    chips[index].StateInitialized != (parentSlot == 2) || chips[index].Angle != (index == 0 ? 0x18 : 0x08),
                    "Pickaxe chips must own native slots, preserve helper order, and defer earlier-slot state 0 until the next pass.");
            // Existing movement regression covers the independently imported
            // fixed-point vectors. This check keeps the allocation/lifetime
            // handoff on the actual application loop and below the next strike.
            StepGameplayUpdates(parentSlot == 2 ? 17 : 18, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<Room148PickaxeDebris>().Count != 0, "Admitted chips must retire and release their native slots after landing.");
            _entities.Clear();
            FailIf(InteractionAdmissionPool.Count != 0, "Pickaxe cancellation must release all native interaction owners.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared original ordered pickaxe admission with partial capacity, both slot sides, application-loop lifetime and cancellation.");
    }
}
