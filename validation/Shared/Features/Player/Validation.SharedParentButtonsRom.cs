
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedParentButtonsRom()
    {
        int cases = 0;
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool ages = game == OracleGame.Ages;
            var memory = new OracleRuntimeState(game);
            var native = new LinkCollisionRom(game: game);
            var parents = new ParentItemSlotState[4];
            for (int profile = 0; profile < 4; profile++)
            for (int primary = 0; primary < 256; primary++)
            for (int secondary = 0; secondary < 256; secondary++)
            {
                for (int index = 0; index < 4; index++)
                {
                    byte id = profile == 3 ? unchecked((byte)(primary + index * 37)) : (byte)(index switch { 0 => 5, 1 => 6, 2 => 15, _ => 1 });
                    byte enabled = profile == 0 ? (byte)0 : profile == 2 ? (byte)(index switch { 0 => 0x80, 1 => 0xff, 2 => 0, _ => 2 }) : (byte)1;
                    parents[index] = new(enabled, id);
                    int at = (0xd2 + index) << 8;
                    native[at] = enabled; native[at + 1] = id; native[at + 3] = 0xff; native[at + 0x20] = 0x57;
                }
                native[ages ? 0xc689 : 0xc681] = (byte)primary;
                native[ages ? 0xc688 : 0xc680] = (byte)secondary;
                native.Call(ages ? 0x4890 : 0x4842, bank: 6);
                ParentItemButtons.Reassign(memory, parents, primary, secondary);
                for (int index = 0; index < 4; index++)
                {
                    int at = (0xd2 + index) << 8;
                    FailIf(ParentItemButtons.Read(memory, index + 2) != native[at + 3] || native[at] != parents[index].Enabled ||
                        native[at + 1] != parents[index].Id || native[at + 0x20] != 0x57,
                        $"{game}: parent button reassignment profile{profile}, A${primary:x2}, B${secondary:x2}, slot${index+2:x2} differs or changes parent lifetime.");
                }
                cases++;
            }
            FailIf(native.RandomUpdates != 0, "Native menu parent reassignment must not consume RNG.");
        }
        GD.Print($"Validated {cases} both-ROM menu parent assignments, all A/B bytes and IDs, disabled/nonzero-enabled slots, B precedence, unrelated-state retention and no RNG.");
    }
}
