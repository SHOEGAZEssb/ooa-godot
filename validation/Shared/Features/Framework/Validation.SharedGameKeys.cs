
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedGameKeys(GameRoot root, bool batched)
    {
        var session = root._gameplaySession!;
        var memory = session.Rooms.RuntimeState;
        var game = session.Rooms.SaveData.Game;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var inventory = entities.Inventory!;
        int equippedA = inventory.EquippedA, equippedB = inventory.EquippedB;
        inventory.EquipA(0); inventory.EquipB(0);
        var app = new ApplicationValidationFixture(root);
        var samples = new List<(bool Held, bool Pressed, bool PhysicalHeld, bool PhysicalPressed)>();
        entities.AddEntity(new ItemPhaseValidationEntity(() => samples.Add((
            Input.IsActionPressed("attack"), Input.IsActionJustPressed("attack"),
            Input.IsPhysicalActionPressed("attack"), Input.IsPhysicalActionJustPressed("attack")))));
        app.Step(1, Vector2.Zero, batched: batched);
        app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
        Require(1, 1, 0xff, "first native A sample");

        app.Step(1, Vector2.Zero, held: ["attack", "debug_flags"], pressed: ["debug_flags"], batched: batched);
        int pausedSamples = samples.Count;
        app.Step(3, Vector2.Zero, batched: batched);
        FailIf(!session.Menus.Flags.IsActive || samples.Count != pausedSamples,
            $"{game}: the input-history fixture requires actual paused object dispatch.");
        Require(1, 1, 0xff, "menu updates preserve the preceding native game sample");
        app.Step(1, Vector2.Zero, held: ["attack", "debug_flags"], pressed: ["attack", "debug_flags"], batched: batched);
        Require(1, 0, 0xff, "closing a menu compares with the last object pass");
        FailIf(samples[^1] != (true, false, true, true),
            $"{game}: resumed gameplay and physical/menu input must retain distinct fresh-key bytes.");

        app.Step(1, Vector2.Zero, batched: batched);
        Require(0, 0, 0xff, "release");
        app.Step(3, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
        Require(1, 0, 0xff, "held catch-up updates");
        FailIf(!samples[^3].Pressed || samples[^2].Pressed || samples[^1].Pressed,
            $"{game}: native fresh A must occur once after release in either host schedule.");

        memory.SetField("wGameKeysPressed", 0);
        app.Step(1, Vector2.Zero, held: ["attack"], batched: batched);
        Require(1, 1, 0xff, "raw native predecessor write");
        FailIf(samples[^1] != (true, true, true, false),
            $"{game}: gameplay fresh A must derive from WRAM rather than the physical edge.");
        var saved = memory.CaptureState();
        memory.SetField("wGameKeysPressed", 0);
        memory.RestoreState(saved);
        app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
        Require(1, 0, 0xff, "restored native predecessor");

        memory.SetField("wUseSimulatedInput", 2);
        app.Step(1, Vector2.Right, held: ["attack", "move_right"], batched: batched);
        Require(0x21, 0x20, 0x18, "native one-update reversal");
        FailIf(memory.ReadField("wUseSimulatedInput") != 0,
            $"{game}: special-object input must consume the native reversal request.");
        app.Step(1, Vector2.Right, held: ["attack", "move_right"], batched: batched);
        Require(0x11, 0x10, 8, "physical direction after reversal");
        app.Step(1, Vector2.Zero, batched: batched);
        inventory.EquipA(equippedA); inventory.EquipB(equippedB);

        void Require(int held, int pressed, int angle, string boundary)
        {
            // specialObjects.s:updateGameKeysPressed owns these three adjacent
            // bytes and computes current & ~previous only on an object pass.
            FailIf(memory.ReadField("wGameKeysPressed") != held ||
                memory.ReadField("wGameKeysJustPressed") != pressed || memory.ReadField("wLinkAngle") != angle,
                $"{game}, batch={batched}, {boundary}: native keys=${memory.ReadField("wGameKeysPressed"):x2}/" +
                $"${memory.ReadField("wGameKeysJustPressed"):x2}, angle=${memory.ReadField("wLinkAngle"):x2}; " +
                $"expected ${held:x2}/${pressed:x2}/${angle:x2}.");
        }
    }
}
