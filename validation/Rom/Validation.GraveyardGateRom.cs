using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOverworldKeyholeAndGraveyardGate()
    {
        CompareGraveyardKeyholeRom();
        ReinitializeGameplayForValidation();
        OverworldKeyholeDatabase database = _keyholes.Database;
        FailIf(database.Count != 6 || database.TileCount != 3 ||
            !database.TryGet(0,0x5c,out var keyhole) ||
            keyhole is not { Treasure:TreasureId.GraveyardKey,SubId:0,TileBase:0x0e,Palette:5 } ||
            !database.IsKeyholeTile(0,0xec) || !database.IsKeyholeTile(1,0xae) ||
            !database.IsKeyholeTile(4,0xec),
            "The original six-room Ages keyhole table or collision-set tiles are incomplete.");

        // Keep the distinct singleTileChanges.s reload golden. The native helper
        // covers actual contact, retained keys, script clocks, collapse and cues.
        _inventory.GiveTreasure(0x42,1);
        _saveData.SetRoomFlag(0,0x5c,0x80);
        LoadValidationRoom(0,0x5c);
        foreach (int packed in new[] { 0x34,0x43,0x44,0x45 })
            FailIf(_currentRoom.GetMetatile(new Vector2((packed & 15)*16+8,(packed >> 4)*16+8)) != 0x3a,
                $"Graveyard room$0:$5c lost persistent flag$80 tile substitution at ${packed:x2}.");
        FailIf(_roomEvents.Get<GraveyardGateEvent>().HasState || !_inventory.HasTreasure(0x42),
            "Opened Graveyard gate must remain retired on re-entry and retain key$42.");
        GD.Print("Validated clean-US Graveyard keyhole gameplay, retained key, event/key/puff lifetimes, shared shake/camera/RNG, text and repeat; independent import/reload goldens retained.");
    }
}
