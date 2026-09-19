using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGoronGallery()
    {
        LoadValidationRoom(3,0xe7); StepGameplayUpdates(4,Vector2.Zero);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        var keeper=cave.Actors.Single(a=>a.Actor.Record.Id==0x30);
        FailIf(cave.Actors.Any(a=>a.Actor.Record.Id==0x8b&&a.Actor.Active),"Goron Elder gallery attendant appeared before finished-game flag $14.");
        var data=new ShootingGalleryEventDatabase(1);
        int[] positions=[0x21,0x32,0x12,0x23,0x04,0x05,0x26,0x37,0x17,0x28];
        FailIf(!Enumerable.Range(0,10).Select(i=>data.Target(i).PackedPosition).SequenceEqual(positions),
            "Goron gallery target positions differ from shootingGallery_targetPositions_goron.");
        _inventory.AddRupees(100);
        int b=_inventory.EquippedB,a=_inventory.EquippedA;
        ApproachGoronFromFloor(keeper);
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int frame=0;frame<900&&keeper.Gallery!.Session is null;frame++)
        {
            if(_dialogue.IsOpen)
            { if(_dialogue.ChoiceActive) _dialogue.SubmitChoiceForValidation(0); else _dialogue.Close(); }
            StepGameplayUpdates(1,Vector2.Zero);
        }
        FailIf(keeper.Gallery!.Session is null||_inventory.Rupees!=80,"Goron gallery did not charge 20 rupees and spawn the native ball game.");
        for(int frame=0;frame<6500;frame++)
        {
            if(_dialogue.IsOpen)
            {
                if(_dialogue.ChoiceActive) { _dialogue.SubmitChoiceForValidation(1); break; }
                _dialogue.Close();
            }
            StepGameplayUpdates(1,Vector2.Zero);
        }
        StepGameplayUpdates(40,Vector2.Zero);
        if(_dialogue.IsOpen) _dialogue.Close();
        StepGameplayUpdates(40,Vector2.Zero);
        FailIf(keeper.Gallery.Session is not {GameComplete:true,Round:10}||_inventory.HasTreasure(0x5a)||
            _inventory.EquippedB!=b||_inventory.EquippedA!=a||cave.BlocksGameplay,
            "Goron gallery zero-hit game did not complete ten rounds, withhold Lava Juice, restore equipment, and release input.");
        ApproachGoronFromFloor(keeper); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        var previousSession=keeper.Gallery.Session;
        for(int i=0;i<900&&keeper.Gallery.Session==previousSession;i++) AdvanceGoronDialogue(1);
        StepGameplayUpdates(1,Vector2.Zero);
        // Independent source boundary: scriptHelp cpScore $07 means 100.
        // Ball scoring is covered separately; start here at its script handoff.
        keeper.Gallery.Session!.Score=100; keeper.Gallery.Session.GameComplete=true;
        AdvanceGoronDialogue(500,1);
        FailIf(!_inventory.HasTreasure(0x5a)||_inventory.EquippedA!=a||_inventory.EquippedB!=b,
            "Goron gallery score 100 did not award Lava Juice and restore equipment.");
        _saveData.SetGlobalFlag(0x14);
        LoadValidationRoom(3,0xe7); StepGameplayUpdates(4,Vector2.Zero);
        var elder=cave.Actors.Single(actor=>actor.Actor.Record.Id==0x8b);
        ApproachGoronFromFloor(elder);
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int frame=0;frame<8&&!_dialogue.IsOpen;frame++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(!_dialogue.ChoiceActive,"Postgame Goron Elder did not offer its secret conversation.");
        _dialogue.SubmitChoiceForValidation(1); StepGameplayUpdates(31,Vector2.Zero);
        if(_dialogue.IsOpen) _dialogue.Close(); StepGameplayUpdates(3,Vector2.Zero);
        FailIf(_saveData.HasGlobalFlag(0x6c)||cave.BlocksGameplay,"Declining the Elder secret committed a flag or retained input.");
        ApproachGoronFromFloor(elder); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<100&&!_secretEntry.IsActive;i++) AdvanceGoronDialogue(1);
        FailIf(!_secretEntry.IsActive,"Elder did not open the shared secret-entry screen.");
        for(int i=0;i<22;i++) _secretEntry.Update(1.0/60.0);
        _secretEntry.Submit(new LinkedGameNpcDatabase().GenerateSecretValues(0x08,_saveData));
        for(int i=0;i<22;i++) _secretEntry.Update(1.0/60.0);
        for(int i=0;i<1200&&elder.Gallery!.Session is null;i++) AdvanceGoronDialogue(1);
        FailIf(!_saveData.HasGlobalFlag(0x6c)||elder.Gallery!.Session is null||
            _inventory.EquippedA!=InventoryState.ItemBiggoronSword||_inventory.EquippedB!=InventoryState.ItemBiggoronSword,
            "Accepted Elder secret did not begin the Biggoron-sword gallery with both equipment slots reserved.");
        StepGameplayUpdates(1,Vector2.Zero);
        elder.Gallery.Session!.Score=300; elder.Gallery.Session.GameComplete=true;
        AdvanceGoronDialogue(500,1);
        FailIf(!_inventory.HasTreasure(0x0c)||!_saveData.HasGlobalFlag(0x76)||
            _inventory.EquippedA!=InventoryState.ItemBiggoronSword||_inventory.EquippedB!=InventoryState.ItemBiggoronSword,
            $"Elder gallery score 300: sword {_inventory.HasTreasure(0x0c)}, flag {_saveData.HasGlobalFlag(0x76)}, equips {_inventory.EquippedA:x2}/{_inventory.EquippedB:x2} versus {a:x2}/{b:x2}, score {elder.Gallery.Score}, blocked {cave.BlocksGameplay}.");
        ApproachGoronFromFloor(elder); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(8);
        cave.Cancel();
        FailIf(_inventory.EquippedA!=InventoryState.ItemBiggoronSword||_inventory.EquippedB!=InventoryState.ItemBiggoronSword||_player.CutsceneControlled,
            "Cancelling the Elder gallery failed to restore equipment and input.");
    }
}
