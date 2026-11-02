
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedHudHealth(FrontendValidationRoot root, bool batched)
    {
        var engine = root._gameplaySession!;
        var rooms = engine.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var status = engine.StatusBar;
        var hud = root._scene.Hud;
        var game = rooms.SaveData.Game;
        bool ages = game == OracleGame.Ages;
        var app = new ApplicationValidationFixture(root);
        int originalA = inventory.EquippedA, originalB = inventory.EquippedB;
        int originalMoney = inventory.Rupees;
        int originalHealth = inventory.HealthQuarters;
        entities.Clear(); inventory.EquipA(0); inventory.EquipB(0);
        var floor = Enumerable.Range(0,80).Select(index => new Vector2(index % 10 * 16 + 8,index / 10 * 16 + 8))
            .First(point => point.X is >= 24 and <= 136 && point.Y is >= 24 and <= 104 &&
                !player.World.Collides(point) && player.World.GetActiveTerrain(point).Terrain.Hazard == HazardType.None);
        player.WarpTo(floor);
        inventory.SetScriptedHealth(inventory.MaxHealthQuarters);
        inventory.AddRupees(100-inventory.Rupees);
        hud.ShowStatusBar(); status.SynchronizeHealth(); status.SynchronizeRupees();
        var native = new BraceletRom(rooms.CurrentRoom,floor,0,0xff,game:game);
        native.LoadSave(rooms.SaveData); native.InitializeHud(inventory.HealthQuarters,inventory.Rupees);
        var sounds = root._sound.AttachPlayRequestAudit();
        int before = 7, observed = 0;
        entities.AddEntity(new ItemPhaseValidationEntity(() =>
        {
            observed++;
            FailIf(entities.ReadDisplayedHealth() != before,
                $"{game}: the object phase must read native displayed health ${before:x2} before the status tail.");
        }));
        void StageHealth(int value)
        {
            memory.SetField("wDisplayedHearts",(byte)value); native[0xcbe4] = (byte)value; before = value;
        }
        void Gate(byte value)
        {
            memory.SetField("wDontUpdateStatusBar",value); native[ages ? 0xcbe7 : 0xcbe8] = value;
        }
        void Step(int updates)
        {
            app.Step(updates,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                native.UpdateHud(memory.FrameCounter);
                FailIf(status.DisplayedHealth != native[0xcbe4] || memory.ReadWramByte(0xcbe4) != native[0xcbe4] ||
                    !sounds.Requests.SequenceEqual(native.Sounds),
                    $"{game}: health ${status.DisplayedHealth:x2} != native ${native[0xcbe4]:x2}, frame=${memory.FrameCounter:x2}, " +
                    $"cues {string.Join(',',sounds.Requests)} != {string.Join(',',native.Sounds)}.");
                FailIf((status.DisplayedHealth == inventory.HealthQuarters) != native.HeartDisplayUpdated(),
                    $"{game}: scriptCmd_checkHeartDisplayUpdated must observe the same authoritative byte as status and objects.");
                before = native[0xcbe4];
            });
        }
        try
        {
            StageHealth(7);
            FailIf(status.DisplayedHealth != 7 || entities.ReadDisplayedHealth() != 7,
                $"{game}: raw $cbe4 writes must be immediately visible without advancing status or copying the HUD view.");
            inventory.AddRupees(9); native.LoadSave(rooms.SaveData);
            Step(24); // Includes coincident money/full-heart cues and the exact global recovery phase.
            FailIf(hud.HealthQuarters != status.DisplayedHealth,
                $"{game}: the rendered heart model must publish the native status result.");
            StageHealth(inventory.HealthQuarters);
            hud.HealthQuarters = 0; // Stale presentation must not become the authoritative health value.
            Step(1);
            FailIf(hud.HealthQuarters != inventory.HealthQuarters,
                $"{game}: equal target/display bytes must still repair a stale rendered heart model.");
            StageHealth(7); Gate(0x77);
            int frozenImage = hud.HealthQuarters;
            Step(8);
            FailIf(hud.StatusBarHidden || hud.HealthQuarters != frozenImage || status.DisplayedHealth != 7,
                $"{game}: raw update-disable $77 must freeze status while retaining the visible HUD image.");
            Gate(0); Step(4);
            native.HideStatusBar(); hud.HideStatusBar();
            FailIf(memory.ReadField("wDontUpdateStatusBar") != 0xff || native[ages ? 0xcbe7 : 0xcbe8] != 0xff,
                $"{game}: hideStatusBar_body must publish native $ff, independently of its prior disable value.");
            StageHealth(7); Step(5);
            native.ShowStatusBar(); hud.ShowStatusBar();
            FailIf(memory.ReadField("wDontUpdateStatusBar") != 0 || hud.StatusBarHidden,
                $"{game}: showStatusBar_body must release the authoritative update gate and blanked presentation.");
            Step(21);
            FailIf(observed != 63 || status.DisplayedHealth != inventory.HealthQuarters,
                $"{game}: actual object/status phases must complete all 63 fixed updates, including disabled status updates.");
            entities.Clear();
            inventory.ApplyDamage(2); native.LoadSave(rooms.SaveData);
            StageHealth(inventory.HealthQuarters+2); Gate(0x77);
            int position = rooms.CurrentRoom.GetPackedPosition(floor);
            engine.Transitions.ApplyWarp(player,new Warp(rooms.ActiveGroup,rooms.CurrentRoom.Id,position,0,2,
                rooms.ActiveGroup,rooms.CurrentRoom.Id,position,0,0));
            app.Step(31,Vector2.Zero,batched:batched);
            FailIf(memory.ReadField("wDontUpdateStatusBar") != 0x77,
                $"{game}: outgoing fade must preserve the native status gate until destination initialization.");
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(memory.ReadField("wDontUpdateStatusBar") != (ages ? 0 : 0x77),
                $"{game}: full room loading must preserve the native cutscene03 difference in clearing wDontUpdateStatusBar.");
            native[ages ? 0xcbe7 : 0xcbe8] = ages ? (byte)0 : (byte)0x77;
            native.LoadCommonGraphics();
            FailIf(status.DisplayedHealth != native[0xcbe4] || status.DisplayedHealth != inventory.HealthQuarters + (ages ? 1 : 2),
                $"{game}: destination-load common graphics must run exactly one status body after the native gate write, before fade-in.");
            app.Step(33,Vector2.Zero,batched:batched);
        }
        finally
        {
            hud.ShowStatusBar(); entities.Clear();
            inventory.AddRupees(originalMoney-inventory.Rupees);
            inventory.SetScriptedHealth(originalHealth);
            inventory.EquipA(originalA); inventory.EquipB(originalB);
            status.SynchronizeHealth(); status.SynchronizeRupees();
        }
    }
}
