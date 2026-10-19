using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFairyFlightPublicationRom()
    {
        // Source entry presets: ordinary hide/reveal, companion rescue/guide,
        // and the three reward fairies. Controller scripts are isolated here.
        foreach (var (subId, preset) in new[] { (0, 0), (0, 3), (3, 0x0f), (3, 0x14), (4, 0x11), (4, 0x12), (4, 0x13) })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x82);
            var owner = _roomEvents.Get<FairiesWoodsEvent>(); owner.Cancel();
            var flight = (ForestFairyFlight)typeof(FairiesWoodsEvent).GetMethod("SpawnFlight", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(owner, [preset])!;
            if (subId != 0)
            {
                flight = new ForestFairyFlight(owner.Database, _runtimeState, _sound,
                    SomariaPrivate<FairiesWoodsSparkleLayer>(owner, "_sparkles"), flight.Actor,
                    preset, _ => { }, subId);
                var flights = SomariaPrivate<List<ForestFairyFlight>>(owner, "_flights");
                flights.Clear(); flights.Add(flight);
            }
            SomariaPrivate<CutsceneCommandRunner>(owner, "_runner").Clear();
            F1Set(owner, "_stage", FairiesWoodsStage.IntroScript);
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(entity => ReferenceEquals(entity.Node, flight.Actor)));
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1);
            _runtimeState.SetWramByte(0xcfd2, subId == 4 ? (byte)1 : (byte)0);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcfd2] = _runtimeState.ReadWramByte(0xcfd2);
            int at = 0xd040 + _entities.InteractionSlot(flight.Actor) * 256;
            native[at] = 1; native[at + 1] = 0x49; native[at + 2] = (byte)subId; native[at + 3] = (byte)preset;
            int updates = 0, motions = 0, holds = 0, circleVisits = 0;
            byte[] expected = new byte[4];
            void Step(int count, bool text = false) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int beforeY = native.Word(at + 10), beforeX = native.Word(at + 12);
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; });
                updates++;
                if (native[at] != 0 && (beforeY != native.Word(at + 10) || beforeX != native.Word(at + 12)) && !text) motions++;
                if (flight.Stage == (int)FairyFlightStage.Circle) circleVisits++;
                FailIf(flight.Active != (native[at] != 0) ||
                    _runtimeState.ReadWramByte(0xcfd2) != native[0xcfd2] ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]) ||
                    flight.Active && (unchecked((ushort)flight.YFixed) != native.Word(at + 10) ||
                        unchecked((ushort)flight.XFixed) != native.Word(at + 12) || flight.Angle != native[at + 9]),
                    $"Fairy $49:${subId:x2} preset${preset:x2} visit={updates}: native XY/angle/signal/lifetime/publication differs.");
                if (text) holds++;
            });
            Step(4);
            _dialogue.ShowMessage("Fairy flight hold.", _player.Position.Y); native[0xcba0] = 1;
            Step(3, true); _dialogue.Close(); native[0xcba0] = 0;
            int waits = 0;
            for (int limit = 0; flight.Active && limit < 1200; limit += 2)
            {
                Step(2);
                if (flight.Stage == (int)FairyFlightStage.WaitForSignal && ++waits >= 3)
                { _runtimeState.SetWramByte(0xcfd2, 0); native[0xcfd2] = 0; waits = 0; }
            }
            FailIf(flight.Active || motions < 8 || holds != 3 || subId == 3 && circleVisits < 60,
                $"Fairy fixture must exercise movement, text hold, source signal handoff and retirement; subid=${subId:x2}, motions={motions}, circle={circleVisits}.");
            owner.Cancel(); StepGameplayUpdates(2, Vector2.Zero, batched: batched); _entities.Clear();
        }
    }
}
