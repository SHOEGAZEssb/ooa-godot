using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class KingZoraEvent(RoomEventContext context, OceanInteractionDatabase data)
    : InteractiveInfiniteScriptHost<NpcCharacter>(context, "KingZora"), IRoomEntryEvent,
      IUpdatesDuringDialogueRoomEvent
{
    private int _swordBranch, _secretResult, _generation;
    private bool _secretPending, _fading;
    private string _secret="";
    internal Func<int,Action<bool>,bool>? OpenSecretMenu {get;set;}
    public override bool DialogueOpen=>Context.DialogueOpen||_secretPending;
    public override bool ScriptExecutionBlocked => base.ScriptExecutionBlocked || _secretPending;
    public bool FreezesNonInteractionObjects=>BlocksGameplay;
    public bool MenusDisabled=>BlocksGameplay;
    public bool ScreenTransitionsDisabled=>BlocksGameplay;
    public bool Matches(int group, OracleRoomData room) => group == 5 && room.Id is 0xab or 0xad;

    public void Start(OracleRoomData room)
    {
        var save = Context.Rooms.SaveData;
        if (room.Id == 0xab && !save.HasGlobalFlag(AgesGlobalFlag.KingZoraCured)) return;
        var actor = Context.RequireNpc(5, room.Id, AgesInteractionId.KingZora, room.Id == 0xab ? 0 : 1, "INTERAC_KING_ZORA");
        actor.SetCollisionRadii(0x0a, 0x0a);
        actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        actor.ShowInitializedPresentation();
        _swordBranch=Context.Inventory.SwordLevel&1;
        var commands = room.Id == 0xab ? data.KingPresent : data.KingPast;
        StartInfiniteScript(actor, commands, firstCommand: OceanInteractionDatabase.Entry(commands, SelectEntry(room.Id)));
        SetInitialActorButtonSensitive();
    }

    private string SelectEntry(int room)
    {
        var save = Context.Rooms.SaveData;
        bool cured = save.HasGlobalFlag(AgesGlobalFlag.KingZoraCured);
        bool clean = save.HasGlobalFlag(AgesGlobalFlag.WaterPollutionFixed);
        bool d7 = (Context.Inventory.Essences & 0x40) != 0;
        if (room == 0xad)
            return "kingZoraScript_past_" + (!cured ? Context.Inventory.HasTreasure(TreasureId.Potion) ? "havePotion" : "dontHavePotion" :
                !clean ? "justCured" : d7 ? "afterD7" : "cleanedWater");
        if (!clean) return "kingZoraScript_present_" + (Context.Inventory.HasTreasure(TreasureId.LibraryKey) ? "acceptedTask" :
            save.HasRoomFlag(5, 0xab, 0x40) ? "giveKey" : "firstTime");
        if (!d7) return "kingZoraScript_present_" + (save.HasGlobalFlag(AgesGlobalFlag.GotPermissionToEnterJabu) ? "cleanedWater" : "justCleanedWater");
        if (save.HasGlobalFlag(AgesGlobalFlag.FinishedGame)) return "kingZoraScript_present_postGame";
        return "kingZoraScript_present_afterD7";
    }

    public override void UpdateFrame()
    {
        if(_fading)Context.Transitions.AdvanceNativeWhiteFade();
        AdvanceInfiniteScript();
        ScriptActor?.AdvanceAnimationUpdates(1);
    }
    public void UpdateDuringDialogueFrame() => UpdateFrame();
    public override int ReadMemory(string binding)=>binding=="SwordBranch"?_swordBranch:throw UnsupportedCommand(binding);
    public override bool MemoryEquals(string binding,int value)=>binding switch
    {
        "SecretResult"=>_secretResult==value,
        "PaletteBusy"=>(Context.Transitions.NativePaletteFadeActive?1:0)==value,
        _ when binding.StartsWith("Global:",StringComparison.Ordinal)=>(Context.Rooms.SaveData.HasGlobalFlag(int.Parse(binding.AsSpan(7)))?1:0)==value,
        _=>throw UnsupportedCommand(binding)
    };
    public override bool TextOptionEquals(int value) => EventResources.RequireDialogueChoice("King Zora has no completed choice.") == value;
    public override void ShowText(int id, string message, int? position)
    {
        message=message.Replace("\\secret1",_secret,StringComparison.Ordinal);
        if (id is 0x3401 or 0x3408 or 0x3409 or 0x3435)
            Context.ShowChoiceDialogue(message, textboxPosition: position, textboxFlags: id == 0x3401 ? 0 : 2);
        else Context.ShowDialogue(message, position, sourceTextId: id);
    }
    public override void GiveItem(int treasure, int parameter)
    {
        string reward=treasure==TreasureId.LibraryKey&&parameter==0?"TREASURE_OBJECT_LIBRARY_KEY_00":
            treasure==TreasureId.Sword&&parameter is 1 or 2 or 4 or 5?$"TREASURE_OBJECT_SWORD_{parameter:x2}":
            throw UnsupportedCommand($"give treasure ${treasure:x2}:${parameter:x2}");
        Context.GrantScriptTreasure(5, 0xab, treasure, parameter, reward, "scripts.s:kingZoraScript_present",
            objectParameter:Context.Treasures.GetObject(reward).Parameter,nativeDispatch:true);
        // scriptCmd_spawnItem ($de) arms the parent's counter1=$03 while
        // the new INTERAC_TREASURE initializes in its later object slot.
        WaitForNativeCounter(3);
    }
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(5, 0xab, (byte)flag);
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "AskSecret":
                _secretPending=true;int generation=_generation;
                if(OpenSecretMenu is null||!OpenSecretMenu(0,valid=>{if(generation!=_generation)return;_secretResult=valid?0:1;_secretPending=false;}))
                    throw UnsupportedCommand("acquire MENU_SECRET for KING_ZORA_SECRET $00");
                break;
            case "GenerateSecret":_secret=new LinkedGameNpcDatabase().GenerateSecret(0x10,Context.Rooms.SaveData);break;
            case "func_50e4":Context.Entities.CreateEnergySwirl(Context.Player.Position,0x6e);break;
            case "createSparkle":Context.Entities.TryCreateOwlSparkle(new(RequireScriptActor("KingZora").Position,new OwlStatueDatabase().Record(0).Sparkle));break;
            case "fadeoutToWhite":BeginFade(true,1);break;
            case "fadeinFromWhiteWithDelay, $04":BeginFade(false,4);break;
            case "loseTreasure, TREASURE_POTION": Context.Inventory.LoseTreasure(TreasureId.Potion); break;
            case "kingZora_createPotion":
                if (Context.Entities.InteractionSlotAvailable) Context.Entities.Spawn(new KingZoraPotionSpawn(data.Potion));
                break;
            default: throw UnsupportedCommand($"run {handler}");
        }
    }
    private void BeginFade(bool fadeOut,int delay)
    {
        EventResources.CaptureFullScreenFade();_fading=true;
        Context.Transitions.BeginNativeWhiteFade(fadeOut,delay);
    }
    protected override void ResetEventState()
    {
        _generation++;_secretPending=false;_secret="";
        if(_fading){Context.Transitions.CancelNativeWhiteFade();EventResources.ReleaseFullScreenFade();_fading=false;}
    }
}
