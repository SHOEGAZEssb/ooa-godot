using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot : GameRoot
{
    private int _neutralInputFrames;
    private ValidationCutsceneTrace? _enterPastCommandTrace;

    public override void _Ready()
    {
        base._Ready();
        ResetValidationInput();
        _scene.ProcessMode = ProcessModeEnum.Disabled;
    }

    public override void _Process(double delta)
    {
        // Scene entry can retain a just-pressed input edge for the remainder
        // of that real frame. Let it expire without advancing gameplay, since
        // the suite performs many original-engine updates synchronously.
        if (AnyValidationInputJustPressed())
        {
            _neutralInputFrames = 0;
            return;
        }
        if (++_neutralInputFrames < 2)
            return;

        SetProcess(false);
        _scene.ProcessMode = ProcessModeEnum.Inherit;
        _entities.GameButtonJustPressedSource = static () => false;
        RunValidation();
    }

    private void RunValidation()
    {
        try
        {
            ValidateAll();
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"Validation failed.\n{exception}");
            GetTree().Quit(1);
        }
    }

    private static void ResetValidationInput()
    {
        // The runner can be entered through a scene change while an editor or
        // joypad event is still marked just-pressed for the current frame.
        // Explicit frame simulations must start from neutral WRAM-style input.
        foreach (string action in new[]
        {
            "attack", "item", "move_up", "move_right", "move_down", "move_left",
            "map", "inventory"
        })
        {
            Input.ActionRelease(action);
        }
    }

    private static bool AnyValidationInputJustPressed() =>
        Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("item") ||
        Input.IsActionJustPressed("move_up") || Input.IsActionJustPressed("move_right") ||
        Input.IsActionJustPressed("move_down") || Input.IsActionJustPressed("move_left") ||
        Input.IsActionJustPressed("map") || Input.IsActionJustPressed("inventory");

    private void ValidateAll()
    {
        ValidateGameplaySceneGraph();
        ValidateGeneratedTableReader();
        ValidateMenuLifecycleFoundation();
        _world.ValidateRepresentativeRooms();
        ValidateOracleObjectMath();
        ValidateOracleRandom();
        ValidateRoomEventTimeline();
        ValidateSaveDataFoundation();
        ValidateSaveStore();
        ValidateTreasureInterpreter();
        ValidateDungeonCollectibles();
        ValidateRoomTileChanges();
        ValidateExplicitSavePersistence();
        ValidateMainMenu();
        ValidateNewGameIntro();
        ValidateSoundEngine();
        ValidateGraphicsCache();
        ValidateDebugFlagMenu();
        ValidateDebugCollision();
        ValidateDebugRoomWarp();
        ValidateDebugMapleShortcut();
        ValidateDeathRespawnCheckpoints();

        LoadValidationRoom(0, 0x11);
        ValidateStartupTransition();
        LoadValidationRoom(0, 0x22);
        ValidateSymmetryTransition();

        ValidateSigns();
        ValidateNpcs();
        ValidateRoom148NpcInteractions();
        ValidateRoom149FamilyInteractions();
        ValidateRoom157NpcInteractions();
        ValidateRoom158NpcInteractions();
        ValidateRoom175NpcInteractions();
        ValidateRoom176NpcInteractions();
        ValidateRoom186NpcInteractions();
        ValidateLowerBlackTowerInteractions();
        ValidateNpcFlagVisibility();
        ValidateGraveyardGhostKidsCutscene();
        ValidateBipinBlossomNaming();
        ValidateImpaIntroEncounter();
        ValidateMakuTreeDisappearanceCutscene();
        ValidateMakuSproutRescueCutscene();
        ValidateMakuTreeSavedCutscene();
        ValidateNayruIntroCutscene();
        ValidateRalphPortalDepartureEvent();
        ValidateAnimations();
        ValidateSwordBush();
        ValidateShield();
        ValidateShovel();
        ValidateSeedSatchel();
        ValidateGashaSpots();
        ValidateMapleEvents();
        ValidateEnemyPlacementRules();
        ValidateEnemyObjectPlacementOrder();
        ValidateKeese();
        ValidateGraveyardCrowsAndDropProducers();
        ValidateOctoroks();
        ValidateArrowMoblins();
        ValidateEnemySwordKnockback();
        ValidateEnemyDamageBlink();
        ValidateEnemyHazards();
        ValidateStalfos();
        ValidateZolsAndGels();
        ValidateItemDrops();
        ValidateTimePortals();
        ValidateEnterPastEvent();
        ValidateHouseWarp();
        ValidateCaveWarps();
        ValidateTerrain();
        ValidateHealth();
        ValidatePlayerDamageAndDeath();
        ValidateChests();
        ValidateInventoryFoundation();
        ValidateInventoryMenu();
        ValidateRingFunctionality();
        ValidateBraceletChestAndPushGate();
        ValidatePushBlocks();
        ValidateDungeonMechanics();
        ValidateSpiritsGraveEntranceInteractions();
        ValidateOverworldKeyholeAndGraveyardGate();
        ValidateDarkRoomInteractions();
        ValidateDungeonKeyDoors();
        ValidateSpiritsGrave();
        ValidateMapScreen();
        ValidateLynnaShopInteractions();
        ValidateVasuShopInteractions();
        ValidateRemoteMakuFirstEssenceCutscene();
        ValidateGameOverRestart();
        ValidateSaveAndQuitToTitle();

        GD.Print("Validated all gameplay and world-data scenarios.");
    }
}
