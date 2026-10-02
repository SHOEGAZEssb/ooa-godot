using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDialoguePaging()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x47);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                _dialogue.MessageSpeed = 3;
                _dialogue.ShowMessage("A\nBB\nCCC\nDDDD\\stop()EE", _player.Position.Y);
                Vector2 start = _player.Position;
                int sounds = _sound.PlayRequestsFor(SoundId.SndText2);
                // Source states 0,1,2,3,4,5 -> 6,7,8,9,a,b,c,d,e,3,4.
                // Clean-US TX_0102 executes the same two-line continuation
                // at updates $0671-$067c. A single press starts both lines.
                // The stop press returns to state 0 while retaining the old
                // mapping; state 0 clears it on the following update.
                int[] glyphs = [0, 0, 1, 1, 3, 3, 3, 3, 2, 2, 5, 5, 5, 5, 3, 3, 7, 7, 0, 0, 2, 2, 2];
                int[] scroll = [0, 0, 0, 0, 0, 8, 8, 16, 0, 0, 0, 8, 8, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0];
                int update = 0;
                void Step(int count, string? button = null)
                {
                    StepGameplayUpdates(count, Vector2.Right,
                        button is null ? [] : [button], button is null ? [] : [button], batched,
                        afterUpdate: () =>
                        {
                            FailIf(_dialogue.VisibleGlyphCount != glyphs[update] ||
                                _dialogue.TextScrollOffset != scroll[update] ||
                                _dialogue.IsOpen != (update < 22) || _player.Position != start,
                                $"Standard dialogue update {update + 1}: glyphs={_dialogue.VisibleGlyphCount}, " +
                                $"scroll={_dialogue.TextScrollOffset}, open={_dialogue.IsOpen}.");
                            update++;
                        });
                }
                Step(2, "attack"); // Opening A cannot reveal or dismiss text.
                Step(1, "item");
                Step(1, "attack");
                Step(1, "item");
                Step(1, "attack"); // One SND_TEXT_2 for both new lines.
                Step(3);
                Step(1, "item");
                Step(1, "attack");
                Step(4); // Automatic shift, without an A/B edge or another cue.
                Step(1, "item");
                Step(1, "attack");
                Step(1, "item"); // Stop returns to state 0, then 1.
                Step(2, "attack");
                Step(1, "item"); // Revealing EE cannot close on the same press.
                Step(1, "attack");
                Step(1); // $10 releases text after the object pass.
                FailIf(_sound.PlayRequestsFor(SoundId.SndText2) != sounds + 2,
                    "Automatic second-line scrolling must not replay SND_TEXT_2.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(_dialogue.BlocksPlayerInput, "Dialogue retained modal input after release.");
                _dialogue.ShowMessage("Cancelled\ntext\nMore", _player.Position.Y);
                _dialogue.Close();
                StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                FailIf(_dialogue.IsOpen || _dialogue.IsScrollingText,
                    "Cancelling dialogue retained a pending scroll or reinitialization.");
            }
        }
        GD.Print("Validated standard two-line paging, stop initialization, input ownership, cancellation and repeat.");
    }
}
