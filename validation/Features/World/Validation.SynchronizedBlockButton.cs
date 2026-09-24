using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockButton()
    {
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            LoadValidationRoom(4, 0x9e);
            // Stage the completed pair of leftward pushes: the source
            // statues $37/$57 now occupy $35/$55. Add an unpressed button
            // under either destination to isolate the shared $14 height rule.
            foreach (int p in new[] { 0x37, 0x57, 0x35, 0x55 })
                _currentRoom.SetPositionTileAndCollision(new((p & 15) * 16 + 8, (p >> 4) * 16 + 8),
                    p is 0x35 or 0x55 ? (byte)0x2a : (byte)0xa0, null, (long)_animationTicks);
            bool primaryButton = repeat == 0;
            _currentRoom.SetPositionTileAndCollision(new(88, primaryButton ? 72 : 40), 0x0c, null, (long)_animationTicks);
            _player.WarpTo(new(88, 104));
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.Layout[primaryButton ? 0x45 : 0x25] != 0x0c,
                "Statue button fixture must approach through actual floor below the staged button.");
            for (int i = 0; !_pushBlocks.Active && i < 80; i++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(!_pushBlocks.Active, "Actual northward input must begin the synchronized statue push.");
            var partner = _entities.Entities<PushBlockController>().Single();
            FailIf(_pushBlocks.BlockZHigh != 0 || partner.BlockZHigh != 0,
                "Both statues must start at zero height before reaching a button.");
            StepGameplayUpdates(12, Vector2.Zero, batched: batched);
            FailIf(_pushBlocks.BlockTopLeft != new Vector2(80, 73.5f) || _pushBlocks.BlockZHigh != 0,
                "Update13 crosses the button row after its pre-movement height sample.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_pushBlocks.BlockZHigh != (primaryButton ? -2 : 0) || partner.BlockZHigh != (primaryButton ? 0 : -2) ||
                _pushBlocks.BlockTopLeft != new Vector2(80, 73),
                "Update14 must raise only the statue over the unpressed button, preserving world XY.");
            StepGameplayUpdates(17, Vector2.Zero, batched: batched);
            FailIf(!_pushBlocks.Active || _pushBlocks.BlockZHigh != (primaryButton ? -2 : 0) ||
                partner.BlockZHigh != (primaryButton ? 0 : -2),
                "The button height must persist through movement update31.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 0 ||
                _currentRoom.Layout[0x45] != 0x2a || _currentRoom.Layout[0x25] != 0x2a,
                "Update32 must place both statues and release their moving representations.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
