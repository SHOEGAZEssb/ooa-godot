using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedGameKeysRom()
    {
        string[] actions = ["attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down"];
        int cases = 0;
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var memory = new OracleRuntimeState(game);
            var native = new LinkCollisionRom(game: game);
            // Clean-US specialObjects.s:updateGameKeysPressed, without replacing
            // getSimulatedInput. Terminal streams read their live value byte.
            int keysAddress = game == OracleGame.Ages ? 0xcc29 : 0xcc45;
            int deathAddress = game == OracleGame.Ages ? 0xcdd5 : 0xcc34;
            foreach (int previous in new[] { 0, 1, 0x0f, 0x50, 0xa0, 0xff })
            foreach (int mode in new[] { 0, 2, 0xff })
            foreach (bool dying in new[] { false, true })
            for (int held = 0; held < 256; held++)
            {
                var physicalHeld = new List<string>();
                var physicalPressed = new List<string>();
                for (int bit = 0; bit < actions.Length; bit++)
                {
                    if ((held & (1 << bit)) != 0) physicalHeld.Add(actions[bit]);
                    if ((held & 0x55 & (1 << bit)) != 0) physicalPressed.Add(actions[bit]);
                }
                native[0xc481] = (byte)held;
                native[keysAddress] = (byte)previous;
                native[0xcbc3] = (byte)mode;
                native[0xcbc9] = (byte)(held ^ 0xa5);
                native[deathAddress] = dying ? (byte)1 : (byte)0;
                native.Call(game == OracleGame.Ages ? 0x40b3 : 0x4097, bank: 5);
                memory.SetField("wGameKeysPressed", (byte)previous);
                memory.SetField("wUseSimulatedInput", (byte)mode);
                memory.SetField("wSimulatedInputValue", (byte)(held ^ 0xa5));
                Input.BeginOriginalUpdate(new ApplicationInputSnapshot(physicalHeld, physicalPressed, Vector2.Zero));
                try
                {
                    Input.PublishGameKeys(memory, mode == 0xff ? held ^ 0xa5 : null, dying);
                    FailIf(memory.ReadField("wGameKeysPressed") != native[keysAddress] ||
                        memory.ReadField("wGameKeysJustPressed") != native[keysAddress + 1] ||
                        memory.ReadField("wLinkAngle") != native[keysAddress + 2] ||
                        memory.ReadField("wUseSimulatedInput") != native[0xcbc3],
                        $"{game}: native key sample held=${held:x2}, previous=${previous:x2}, mode=${mode:x2}, dying={dying} differs from clean-US execution.");
                    for (int bit = 0; bit < actions.Length; bit++)
                        FailIf(Input.IsActionPressed(actions[bit]) != ((native[keysAddress] & (1 << bit)) != 0) ||
                            Input.IsActionJustPressed(actions[bit]) != ((native[keysAddress + 1] & (1 << bit)) != 0) ||
                            Input.IsPhysicalActionPressed(actions[bit]) != physicalHeld.Contains(actions[bit]) ||
                            Input.IsPhysicalActionJustPressed(actions[bit]) != physicalPressed.Contains(actions[bit]),
                            $"{game}: published native and physical input views disagree at held=${held:x2}, previous=${previous:x2}, mode=${mode:x2}, dying={dying}, button={actions[bit]}.");
                }
                finally { Input.EndOriginalUpdate(); }
                cases++;
            }

            foreach (int mode in new[] { 0, 1, 2, 0xff })
            foreach (byte palette in new byte[] { 1, 3, 0x80 })
            foreach (bool dying in new[] { false, true })
            {
                native[0xc481] = 0x11; native[keysAddress] = 0xa0;
                native[0xcbc3] = (byte)mode; native[0xcbc9] = 0xc0;
                native[0xc4ab] = palette; native.Word(0xcbc4, 0x1234);
                native[deathAddress] = dying ? (byte)1 : (byte)0;
                native.Call(game == OracleGame.Ages ? 0x40b3 : 0x4097, bank: 5);
                memory.SetField("wGameKeysPressed", 0xa0);
                memory.SetField("wUseSimulatedInput", (byte)mode);
                memory.SetField("wSimulatedInputValue", 0xc0);
                memory.SetField("wPaletteThread_mode", palette);
                memory.SetWramByte(0xcbc4, 0x34); memory.SetWramByte(0xcbc5, 0x12);
                Input.BeginOriginalUpdate(new ApplicationInputSnapshot(["attack", "move_right"], [], Vector2.Right));
                try
                {
                    Input.PublishGameKeys(memory, null, dying);
                    FailIf(memory.ReadField("wGameKeysPressed") != native[keysAddress] ||
                        memory.ReadField("wGameKeysJustPressed") != native[keysAddress + 1] ||
                        memory.ReadField("wLinkAngle") != native[keysAddress + 2] ||
                        memory.ReadField("wUseSimulatedInput") != native[0xcbc3] ||
                        native.Word(0xcbc4) != 0x1234 || memory.ReadWramByte(0xcbc4) != 0x34 || memory.ReadWramByte(0xcbc5) != 0x12,
                        $"{game}: palette mode=${palette:x2}, input mode=${mode:x2}, dying={dying} must preserve the native sample/counter gate without requesting a stream.");
                }
                finally { Input.EndOriginalUpdate(); }
                cases++;
            }

            memory.SetField("wPaletteThread_mode", 0);
            memory.SetField("wUseSimulatedInput", 1);
            Input.BeginOriginalUpdate(new ApplicationInputSnapshot([], [], Vector2.Zero));
            try
            {
                bool rejected = false;
                try { Input.PublishGameKeys(memory, null, dying: false); }
                catch (NotSupportedException error) when (error.Message.Contains("specialObjects.s:updateGameKeysPressed", StringComparison.Ordinal))
                { rejected = true; }
                FailIf(!rejected, $"{game}: missing simulated-input data must fail with source context.");
            }
            finally { Input.EndOriginalUpdate(); }
        }
        GD.Print($"Validated {cases} game-key samples against both clean-US ROMs, including physical/reversed/terminal input, fresh edges, raw angle bytes and death suppression.");
    }
}
