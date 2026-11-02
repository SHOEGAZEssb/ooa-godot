
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedMainThreadClock()
    {
        foreach (bool batched in new[] { false, true })
        {
            InitializeCompletedIntroRoom(0, 0x60);
            _player.WarpTo(new Vector2(72, 40));
            _saveData.WriteField("wPlaytimeCounter", 0xfe);
            _saveData.WriteField("wPlaytimeCounter", 0x17, 1);
            _runtimeState.FrameCounter = 0x50;
            var observed = new List<int>();
            _entities.AddEntity(new ItemPhaseValidationEntity(() => observed.Add(_runtimeState.FrameCounter)));

            int expected = 0xfe;
            void Advance(int updates, string[]? pressed = null)
            {
                Application.Step(updates, Vector2.Zero, held: pressed, pressed: pressed, batched: batched,
                    afterUpdate: () =>
                    {
                        expected = (expected + 1) & 0xff;
                        FailIf(_runtimeState.FrameCounter != expected ||
                            _saveData.ReadField("wPlaytimeCounter") != expected,
                            "bank0.s:mainThread must publish the incremented playtime byte once before every foreground update.");
                    });
            }

            Advance(2);
            FailIf(!observed.SequenceEqual(new[] { 0xff, 0x00 }) || _saveData.ReadField("wPlaytimeCounter", 1) != 0x18,
                "Objects must consume the source main-thread clock and its byte carry, independently of the previous frame byte.");
            Advance(1, ["inventory"]);
            Advance(22);
            FailIf(!InventoryMenuOpen || observed.Count != 2,
                "An inventory menu must freeze objects while the main-thread clock continues through both fades.");
            Advance(1, ["inventory"]);
            Advance(23);
            FailIf(InventoryMenuOpen || observed.Count <= 2,
                "Closing inventory must resume the existing object dispatcher without a second frame advance.");

            int stoppedFrame = _runtimeState.FrameCounter;
            byte stoppedPlaytime = _saveData.ReadField("wPlaytimeCounter");
            BeginGameOver();
            Application.Step(4, Vector2.Zero, batched: batched);
            FailIf(_runtimeState.FrameCounter != stoppedFrame || _saveData.ReadField("wPlaytimeCounter") != stoppedPlaytime,
                "saveQuitMenu_state0 stops THREAD_1 during Game Over; UI updates must not run its main-thread clock.");
            _inventoryMenu.CloseImmediatelyForValidation();
        }
    }
}
