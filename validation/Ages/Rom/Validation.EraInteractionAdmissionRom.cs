using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateEraInteractionAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int era in new[] { 0, 1 })
        foreach (int free in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(era, era == 0 ? 0x47 : 0x48); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _saveData.SetGlobalFlag(0x16, false);
            _runtimeState.SetWramByte(AgesWramAddress.wSentBackByStrangeForce, 0);
            Vector2 floor = Enumerable.Range(0, 80).Select(index => new Vector2(index % 10 * 16 + 8, index / 10 * 16 + 8))
                .First(position => !_collision.Collides(position) && _currentRoom.GetTerrainInfo(position).Hazard == HazardType.None);
            _player.WarpTo(floor); _player.BeginCutsceneControl();
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)floor.X, (int)floor.Y);
            native[0xcdde] = 0; FillInteractionAdmissionPool(native, free);
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0x5e7d, 1);
            bool created = _transitions.CheckDisplayEraInfoAfterFullRoomLoad();
            var banner = _entities.Entities<EraInfoDisplay>().SingleOrDefault();
            var source = NativeAdmissionChildren(native, 0xe0, 0);
            FailIf(created != (free == 1) || (banner is null) != (free == 0) || source.Length != free || _entities.InteractionSlotAvailable,
                "INTERAC$e0 must check the post-placement $d2-$df pool before constructing an optional era banner.");
            if (banner is not null)
                FailIf(_entities.InteractionSlot(banner) != (source.Single() >> 8) - 0xd0,
                    "Present/Past banner must reserve the same source interaction slot.");
            int updates = 0;
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 2); native[0xcc8a] = 2;
            StepGameplayUpdates(87, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                updates++; native.AdvanceInteractions(_entities.FrameCounter);
                if (updates == 2) { _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); native[0xcc8a] = 0; }
                var active = Enumerable.Range(2, 14).Select(slot => 0xd040 + slot * 256)
                    .Where(at => native[at] != 0 && native[at + 1] == 0xe0).ToArray();
                if (banner is not null && active.Length == 1)
                {
                    int at = active.Single();
                    FailIf(banner.SubId != native[at + 2] || (int)banner.Stage != native[at + 4] ||
                        unchecked((byte)(int)banner.Position.X) != native[at + 13] ||
                        unchecked((byte)(int)banner.Position.Y) != native[at + 11],
                        $"INTERAC$e0 era={era} update={updates}: native stage/XY must match throughout the ordinary 87-update lifetime.");
                }
            });
            FailIf(_entities.Entities<EraInfoDisplay>().Count != 0 || banner is { Finished: false } ||
                Enumerable.Range(2, 14).Any(slot => native[0xd040 + slot * 256] != 0 && native[0xd041 + slot * 256] == 0xe0),
                "Era banner must finish at update 87; a failed request must never retry after puffs free capacity.");
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0x5e7d, 1);
            FailIf(!_transitions.CheckDisplayEraInfoAfterFullRoomLoad() || _entities.InteractionSlot(_entities.Entities<EraInfoDisplay>().Single()) != 2,
                "A later room-load request must reuse lowest dynamic slot $d2 after ordinary completion.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched); _entities.Clear();
            FailIf(InteractionAdmissionPool.Count != 0, "Era cancellation must release the physical interaction owner.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared Present/Past era admission, native slot order, complete ordinary lifetime, repeat and cancellation.");
    }
}
