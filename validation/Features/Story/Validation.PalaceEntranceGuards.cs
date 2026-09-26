using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceEntranceGuardCollision()
    {
        // mainData.s places $40:$02/$09 at ($48,$28)/($58,$28).
        // soldier.s calls objectPreventLinkFromPassing, except while
        // GLOBALFLAG_10 is set and GLOBALFLAG_0b is clear. The generic
        // scripts use radius $06; Link's $06 radius stops him at Y=$34.
        foreach (bool batched in new[] { false, true })
        foreach (bool completed in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetGlobalFlag(GlobalFlag.Flag0b, completed);
            _saveData.SetGlobalFlag(GlobalFlag.Flag10, completed);
            foreach (int x in new[] { 0x48, 0x58 })
            {
                LoadValidationRoom(1, 0x46);
                _player.WarpTo(new Vector2(x, 0x58));
                FailIf(_rooms.CurrentRoom.IsSolid(_player.Position) ||
                    _entities.BlocksLink(_player.Position),
                    "1:46 guard approach must begin on clear ground.");
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    StepGameplayUpdates(40, Vector2.Up, ["move_up"], batched: batched);
                    FailIf(_player.Position != new Vector2(x, 0x34),
                        $"1:46 guard at X=${x:x2} failed to stop Link at Y=$34: {_player.Position}.");
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                    FailIf(!_dialogue.IsOpen,
                        $"1:46 guard at X=${x:x2} is not reachable for conversation.");
                    _dialogue.Close();
                    StepGameplayUpdates(1, Vector2.Zero);
                    StepGameplayUpdates(8, Vector2.Down, ["move_down"], batched: batched);
                }
            }

            // Exercise the source gate on the real entrance actors, then
            // cancellation/re-entry so stale non-solid actors cannot survive.
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MysterySeeds, 0x20);
            LoadValidationRoom(1, 0x46);
            var palace = _roomEvents.Get<DekuForestPalaceEvent>();
            var guards = _entities.Entities<NpcCharacter>()
                .Where(npc => npc.Record.Id == 0x40 && npc.Record.SubId is 0x02 or 0x09).ToArray();
            FailIf(guards.Length != 2 || guards.Any(npc => !_entities.BlocksLink(npc.Position)),
                "1:46 entrance guards must block before GLOBALFLAG_10.");
            palace.SetGlobalFlag(GlobalFlag.Flag10);
            FailIf(guards.Any(npc => _entities.BlocksLink(npc.Position)),
                "1:46 escort guards must allow passage after GLOBALFLAG_10 and before GLOBALFLAG_0b.");
            palace.Cancel();
            FailIf(guards.Any(npc => npc.Active), "1:46 cancellation retained active guards.");
            _saveData.SetGlobalFlag(GlobalFlag.Flag0b);
            LoadValidationRoom(1, 0x46);
            FailIf(!_entities.BlocksLink(new Vector2(0x48, 0x28)) ||
                !_entities.BlocksLink(new Vector2(0x58, 0x28)),
                "1:46 completed re-entry failed to restore guard collision.");
        }
    }
}
