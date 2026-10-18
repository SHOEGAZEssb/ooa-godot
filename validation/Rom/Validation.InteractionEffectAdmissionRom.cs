using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private Dictionary<IRoomEntity, int> InteractionAdmissionPool =>
        SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");

    private void FillInteractionAdmissionPool(SomariaRom native, int free)
    {
        while (14 - InteractionAdmissionPool.Count > free)
        {
            var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(144, 104), SoundId.MusNone));
            int at = 0xd040 + _entities.InteractionSlot(puff) * 256;
            native[at] = 1; native[at + 1] = 5; native[at + 2] = 0x80;
        }
    }

    private static int[] NativeAdmissionChildren(SomariaRom native, int id, int sub) =>
        Enumerable.Range(2, 14).Select(slot => 0xd040 + slot * 256)
            .Where(at => native[at] != 0 && native[at + 1] == id && native[at + 2] == sub).ToArray();

    private static void CallInteractionAdmissionHelper(SomariaRom native, byte page, ushort address, byte bank)
    {
        native[0xffae] = 0x40; native[0xffaf] = page;
        byte[] caller = [0x16, page, 0xcd, (byte)address, (byte)(address >> 8), 0xc9];
        for (int index = 0; index < caller.Length; index++) native[0xc100 + index] = caller[index];
        SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, bank);
    }

    private void ValidateInteractionEffectAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            PrepareSwimmingRoom(); _entities.Clear(); _inventory.SetScriptedEquippedItems(0, 0);
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 40, 56);
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
            FillInteractionAdmissionPool(native, free);
            // Source interval/allocation boundary in link.s, selected by the
            // due counter. Native swimming motion is outside this comparison.
            native[0xffae] = 0; native[0xffaf] = 0xd0;
            byte[] caller = [0x16, 0xd0, 0x21, 7, 0xd0, 0xcd, 0xe0, 0x58, 0xc9];
            for (int index = 0; index < caller.Length; index++) native[0xc100 + index] = caller[index];
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 5);
            native.AdvanceInteractions();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            var bubbles = _entities.Entities<SideScrollBubbleRoomEntity>();
            var source = NativeAdmissionChildren(native, 0x91, 0);
            FailIf(bubbles.Count != free || source.Length != free || _entities.InteractionSlotAvailable ||
                _random.Calls - seed.Calls != 1 + free || native.RandomCalls != 1 + free ||
                SomariaPrivate<int>(_player, "_sideScrollBubbleCounter") != native[0xd007],
                "Room $7:$05 INTERAC$91 must retain Link's interval draw/reload but reject a full pool before child RNG.");
            if (free == 1)
                FailIf(_entities.InteractionSlot(bubbles.Single()) != (source.Single() >> 8) - 0xd0 ||
                    bubbles.Single().TurnCounter != 4 || bubbles.Single().TurnsRemaining != 5,
                    "Swimming bubble must own the source slot and initialize once in the later interaction pass.");
            _entities.RetireEntity(InteractionAdmissionPool.First(pair => pair.Key.Node is PuzzlePuffEffect).Key);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<SideScrollBubbleRoomEntity>().Count != free,
                "Released capacity must not retry a failed swimming bubble before Link's reloaded interval expires.");
            _entities.Clear();
            FailIf(InteractionAdmissionPool.Count != 0, "Bubble cancellation must release every interaction slot.");
        }

        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        foreach (bool cancel in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x79);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 112)); _player.BeginCutsceneControl();
            var actor = _entities.Entities<TingleRoomEntity>().Single();
            var script = _roomEvents.Get<TingleEvent>();
            // Declare the completed Kooloo script boundary. The existing
            // Tingle event regression covers the ordinary offer/animation path.
            F1Set(actor, "<KoolooComplete>k__BackingField", true);
            F1Set(script, "_stage", TingleEventStage.UpgradeAnimation);
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 112);
            foreach (int slot in InteractionAdmissionPool.Values)
            {
                int at = 0xd040 + slot * 256;
                native[at] = 1; native[at + 1] = 5; native[at + 2] = 0x80;
            }
            int parentAt = 0xd040 + _entities.InteractionSlot(actor) * 256;
            native[parentAt] = 1; native[parentAt + 1] = 0xc8;
            FillInteractionAdmissionPool(native, free);
            CallInteractionAdmissionHelper(native, (byte)(parentAt >> 8), 0x7990, 0x15);
            // The original source parent has completed its helper. Hide that
            // staged parent while executing the real child/filler slot walk.
            native[parentAt] = 0; native.AdvanceInteractions();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            var glows = _entities.Entities<TimedSparkleRoomEntity>();
            var source = NativeAdmissionChildren(native, 0x84, 4);
            FailIf(glows.Count != free || source.Length != free || script.Stage != TingleEventStage.UpgradeGlowWait || script.Counter != 120,
                $"Tingle INTERAC$84:$04 free={free}: runtime/source glows={glows.Count}/{source.Length}, stage={script.Stage}, counter={script.Counter}, complete={actor.KoolooComplete}, text={_dialogue.IsOpen}; checked attempt must preserve wait 120.");
            if (free == 1)
                FailIf(_entities.InteractionSlot(glows.Single()) != (source.Single() >> 8) - 0xd0 ||
                    glows.Single().RemainingUpdates != 120 || glows.Single().Position != _player.Position.Floor() ||
                    native[source.Single() + 6] != 120,
                    "Tingle glow must copy Link's high XY, initialize without a counter tick, and reserve the source slot.");
            if (cancel)
            {
                StepGameplayUpdates(2, Vector2.Zero, batched: batched); script.Cancel();
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(_entities.Entities<TimedSparkleRoomEntity>().Count != 0 || _inventory.SeedSatchelLevel != 1,
                    "Cancelling Tingle's admitted or rejected glow must retire it without granting the upgrade.");
            }
            else
            {
                StepGameplayUpdates(119, Vector2.Zero, batched: batched);
                FailIf(script.Counter != 1 || _inventory.SeedSatchelLevel != 1 || glows.Any(glow => glow.RemainingUpdates != 1),
                    "Failed glow admission must retain the full source wait; successful glow must retain its final counter update.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(script.Stage != TingleEventStage.UpgradeReward || _inventory.SeedSatchelLevel != 2 ||
                    _entities.Entities<TimedSparkleRoomEntity>().Count != 0,
                    "Tingle must grant the Satchel after wait 120, including when optional glow allocation failed.");
            }
            _dialogue.Close(); LoadValidationRoom(0, 0x89); LoadValidationRoom(0, 0x79);
            FailIf(_entities.Entities<TimedSparkleRoomEntity>().Count != 0, "Tingle glow must not survive cancellation/completion and room re-entry.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared swimming bubble and Tingle glow shared admission with original helpers, failure RNG, reward continuation and cancellation.");
    }
}
