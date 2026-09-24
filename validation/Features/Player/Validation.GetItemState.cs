using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Older object-only fixtures deliberately leave Link's dispatch to the
    // fixture. Request consumption and state04 initialization are two updates.
    private void InitializeGetItemStateForValidation()
    {
        _player.AdvanceApplicationUpdate();
        _player.AdvanceApplicationUpdate();
    }

    private void ValidateGetItemState()
    {
        foreach (byte parameter in new byte[] { 0x00, 0x01, 0x80, 0x81 })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9b);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(136, 136));
            bool disabled = true;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Left, batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                disabled = true;
                _player.SetScriptedLinkAnimationMode(0x06);
                _dialogue.ShowMessage("State04 boundary.", _player.Position.Y);
                Vector2 before = _player.Position;
                _player.RequestGetItemState(parameter, () => disabled);
                FailIf(!_player.NativeNormalStateForInteraction || _player.IsHoldingItemOneHand || _player.IsHoldingItemTwoHands,
                    "A state04 request must retain normal state and the preceding animation.");
                Step();
                FailIf(_player.NativeNormalStateForInteraction || _player.IsHoldingItemOneHand || _player.IsHoldingItemTwoHands ||
                    _player.ScriptedLinkAnimationMode != 0x06,
                    "State04 consumption must precede animation initialization.");
                Step();
                FailIf(_player.IsHoldingItemTwoHands != ((parameter & 1) != 0) ||
                    _player.IsHoldingItemOneHand != ((parameter & 1) == 0) || _player.ScriptedLinkAnimationMode is not null,
                    "State04 must select animation (wcc50&$0f)+$0e on initialization.");
                Step(3);
                FailIf(_player.Position != before || _player.NativeNormalStateForInteraction,
                    "All state04 parameters must wait for text, without ordinary movement.");
                _dialogue.Close();
                Step();
                bool skipsMask = (parameter & 0x80) != 0;
                FailIf(_player.NativeNormalStateForInteraction != skipsMask,
                    "wcc50 bit7 must skip only the disabled-object gate after text closes.");
                disabled = false;
                if (!skipsMask) Step();
                FailIf(!_player.NativeNormalStateForInteraction || _player.IsHoldingItemOneHand || _player.IsHoldingItemTwoHands ||
                    _player.ScriptedLinkAnimationMode != 0x06 || _player.Position != before,
                    "State04 release must restore the saved animation and state01 without also moving Link.");
            }
            _player.RequestGetItemState(parameter, () => disabled);
            _player.WarpTo(new(136, 136));
            FailIf(!_player.NativeNormalStateForInteraction || _player.IsHoldingItemOneHand || _player.IsHoldingItemTwoHands,
                "WarpTo must cancel a pending state04 request.");
        }
    }
}
