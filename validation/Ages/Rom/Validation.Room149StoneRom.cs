using Godot;
using System.Linq;
using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private async Task ValidateRoom149StonePresentationRom()
    {
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.Essence, 6);
        LoadValidationRoom(1, 0x49);
        _player.WarpTo(new(0x50, 0x70), recordSafe: false);
        var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 2, 0x50, 0x70);
        // group1Map49ObjectData: original boy/father/observer stream.
        int[][] placements = [[0x3c, 0x0e, 0x48, 0x78], [0x3a, 0x0c, 0x48, 0x38], [0x43, 0x06, 0x28, 0x78]];
        for (int i = 0; i < placements.Length; i++)
        {
            int slot = 0xd240 + i * 256;
            rom[slot] = 1; rom[slot + 1] = (byte)placements[i][0]; rom[slot + 2] = (byte)placements[i][1];
            rom[slot + 11] = (byte)placements[i][2]; rom[slot + 13] = (byte)placements[i][3];
        }
        StepGameplayUpdates(1, Vector2.Zero);
        rom.AdvanceInteractions(_entities.FrameCounter);
        string captureDirectory = OS.GetEnvironment("OOA_POST_D7_SCREENSHOT_OUTPUT");
        if (!string.IsNullOrEmpty(captureDirectory))
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image capture = GetViewport().GetTexture().GetImage();
            capture.SavePng(System.IO.Path.Combine(captureDirectory, "room149-stone.png"));
        }
        var palette = rom.CreateMenuView();
        var colors = Enumerable.Range(1, 3).Select(shade =>
        {
            int rgb = palette.HudColor(true, 6, shade);
            return new Color((rgb & 31) / 31f, (rgb >> 5 & 31) / 31f, (rgb >> 10 & 31) / 31f);
        }).ToArray();
        void CheckStonePixels(NpcCharacter actor, string context)
            => CompareInteractionPalettePixelsRom(rom, actor, 6, context);
        var observer = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x43);
        var defaultActor = NpcCharacter.CreateFromRecord(observer.BaseRecord);
        try
        {
            // pastGuySubid3Script writes oamFlags=$06 while retaining the
            // graphics-default animation, without an explicit setanimation.
            defaultActor.SetBasePalette(6);
            defaultActor.AdvanceAnimationUpdates(5);
            defaultActor.SetScriptPaletteOverride(new Room149FamilyDatabase().StonePalette);
            CheckStonePixels(defaultActor, "Observer default-facing stone palette");
            FailIf(defaultActor.CurrentAnimationFrame != 0 ||
                SomariaPrivate<double>(defaultActor, "_animationTicks") != 5,
                "Recoloring default-facing stone frames must preserve the animation clock.");
            defaultActor.SetFacingDirection(Vector2I.Right);
            CheckStonePixels(defaultActor, "Observer other-facing stone palette");
            defaultActor.SetScriptPaletteOverride(null);
            defaultActor.SetBasePalette(observer.BaseRecord.Palette);
            FailIf(defaultActor.CurrentAnimationUsesColor(colors[1]),
                "Clearing the stone override must restore ordinary facing colors immediately.");
        }
        finally { defaultActor.Free(); }
        void Compare()
        {
            foreach (int id in new[] { 0x3a, 0x43 })
            {
                var actor = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == id);
                int slot = id == 0x3a ? 0xd340 : 0xd440;
                CheckStonePixels(actor, $"Room $1:$49 stone ${id:x2}");
                CompareInteractionOamRom(rom, actor, slot, $"Room $1:$49 stone ${id:x2}");
            }
        }
        Compare();
        StepGameplayUpdates(20, Vector2.Zero, batched: true, afterUpdate: () =>
        {
            rom.AdvanceInteractions(_entities.FrameCounter); Compare();
        });
    }
}
