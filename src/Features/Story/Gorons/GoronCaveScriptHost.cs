using Godot;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class GoronCaveScriptHost : InteractiveCutsceneCommandHost
{
    private readonly GoronCaveEvent _owner;
    private readonly CutsceneCommandRunner _runner;
    internal NpcCharacter Actor { get; }
    internal int Slot { get; }
    public override RoomEventContext Context => _owner.Context;
    private GoronCaveDatabase Data => _owner.Database;
    internal GoronCaveDatabase Database => Data;
    internal GoronDanceController? Dance { get; private set; }
    internal GoronGalleryController? Gallery { get; private set; }
    internal GoronBigBangController? BigBang {get;private set;}
    internal GoronTargetCartsController? Carts {get;private set;}
    private bool _secretPending;
    private int _secretResult, _generation;
    private string _secret="";
    public override bool ScriptExecutionBlocked => base.ScriptExecutionBlocked||_secretPending;
    internal bool MenusDisabled => Gallery?.MenusDisabled==true;
    private OracleRuntimeState Wram => Context.Entities.RuntimeState;
    private bool _pending, _sensitive, _fixedAnimation, _initialized;
    private bool _zero, _carry;
    private int _angle, _speed, _movement, _walkingAnimation;
    private int _loadedText;
    private int _var3c, _var3d, _var3e;
    private int _jumpZ, _jumpSpeedZ;
    private bool _dancing;
    internal int JumpZ => _jumpZ;
    private bool Past => (Context.Rooms.CurrentRoom.TilesetFlags & 0x80) != 0;
    private int _explosionCounter, _explosionIndex, _soundCounter;
    private Vector2 _position;
    internal bool InputLocked => InputControlHeld;
    internal int CommandIndex => _runner.Instruction;
    internal int Counter => _runner.Counter;
    internal int MovementCounter => _movement;
    internal int LoadedTextId => _loadedText;
    internal bool PendingButton => _pending;

    internal GoronCaveScriptHost(GoronCaveEvent owner, NpcCharacter actor)
    {
        _owner = owner; Actor = actor; _runner = new(this);
        Slot = Context.Entities.InteractionSlot(actor);
        _position = actor.Position;
        Context.Entities.EntityAdapters<GoronCaveRoomEntity>().Single(entity=>entity.Npc==actor).Host=this;
    }
    internal void Initialize()
    {
        if(_initialized || !Actor.Active) return;
        _initialized = true;
        Actor.SetAnimationRate(0);
        if(Actor.Record is {Id:0x66,SubId:0x0b}) BigBang=new(this);
        if(Actor.Record is {Id:0x66,SubId:9}) { Carts=new(this); Carts.Initialize(); }
        if(Actor.Record.Id==0x30||Actor.Record is {Id:0x8b,SubId:2})
        {
            Gallery=new(this); Wram.SetWramByte(0xcfdc,0);
            StartScript(Actor.Record.SubId==1?"shootingGalleryScript_goronNpc":"shootingGalleryScript_goronElderNpc");
            AdvanceScript(); Animate(); return;
        }
        if(Actor.Record.SubId==2&&Actor.Record.Id==0x66)
        {
            Actor.SetScriptVisible(false); _jumpSpeedZ=-0x200;
            Context.Player.Face(Vector2I.Up); Context.Player.SetScriptedLinkAnimationMode(null);
            Wram.SetWramByte(0xcfd2,0); Context.Sound.PlaySound(0xcd);
            UpdateDanceJump(); return;
        }
        if(Actor.Record.SubId==0&&Actor.Record.Id==0x66)
        {
            Dance=new(this);
            Actor.SetBasePalette(Past?2:1);
            _owner.SpawnDancers(IsLinkedGame&&Past);
            Dance.Initialize();
        }
        string entry = Actor.Record.Id == 0x8b ? $"goronElderScript_subid{Actor.Record.SubId:x2}_body" :
            Actor.Record.SubId switch
            {
                4 => "goron_subid04Script",
                5 => Actor.Record.Var03 < 3 ? "goron_subid05Script_A" : "goron_subid05Script_B",
                6 => Actor.Record.Var03 == 0 ? "goron_subid06Script_A" : "goron_subid06Script_B",
                9 => Actor.Record.Var03==0?"goron_subid09Script_A":"goron_subid09Script_B",
                0 or 1 or 3 or 7 or 8 or 0x0a or 0x0b or 0x0c or 0x0d or 0x0e or 0x10 => $"goron_subid{Actor.Record.SubId:x2}Script",
                _ => throw new InvalidOperationException($"Unsupported Goron ${Actor.Record.SubId:x2}.")
            };
        if (Actor.Record.SubId == 6 && Actor.Record.Id == 0x66)
        {
            if (Actor.Record.Var03 == 0) Wram.SetWramByte(0xcfdd, 0);
            else for (int address = 0xcfc0; address < 0xcfe0; address++) Wram.SetWramByte(address, 0);
        }
        Animation(Actor.Record.DefaultAnimation, false);
        _runner.Start(Data.Commands, Data.Entry(entry));
        // $66 state 0 explicitly calls the script before falling through state 1.
        _runner.AdvanceFrame();
        if (Actor.Record.Id == 0x66 && Actor.Record.SubId>=3 && Actor.Active) _runner.AdvanceFrame();
        Animate();
    }
    internal void Update()
    {
        if (!Actor.Active) return;
        if (!_initialized) { Initialize(); return; }
        if(Gallery is not null) { Gallery.Update(); Animate(); return; }
        if(Actor.Record.SubId==2&&Actor.Record.Id==0x66) { UpdateDanceJump(); return; }
        if(Dance is not null) { Dance.Update(); if(Dance.State==1) Animate(); return; }
        if(Actor.Record.SubId==1)
        {
            if(Wram.ReadWramByte(0xcfd4)!=0)
            {
                _dancing=true; SetNativeAnimation(Wram.ReadWramByte(0xcfd2));
                Actor.SetScriptDrawOffset(new Vector2(0,_owner.DanceJumpZ>>8)); return;
            }
            if(_dancing) { _dancing=false; Animation(2,false); Actor.SetScriptDrawOffset(Vector2.Zero); return; }
        }
        _runner.AdvanceFrame();
        Animate();
    }
    internal void StartScript(string entry) => _runner.Start(Data.Commands,Data.Entry(entry));
    internal void AdvanceScript() => _runner.AdvanceFrame();
    internal void StartCommands(IReadOnlyList<CutsceneCommand> commands) => _runner.Start(commands);
    internal void SetNativeAnimation(int animation) => Animation(animation,true);
    internal bool SpawnDanceJump() => _owner.SpawnDanceJump();
    private void UpdateDanceJump()
    {
        if(OracleObjectMath.UpdateSpeedZ(ref _jumpZ,ref _jumpSpeedZ,0x40))
        {
            Context.Player.SetCutsceneDrawZFixed(0); Wram.SetWramByte(0xcfd3,0); Actor.SetActive(false); return;
        }
        Context.Player.SetCutsceneDrawZFixed(_jumpZ);
        if(_jumpSpeedZ==0) { Context.Player.Face(Vector2I.Down); Wram.SetWramByte(0xcfd2,2); }
    }
    public override void ScriptEnded()
    {
        if(Gallery is not null) Gallery.ScriptEnded();
        else if(Dance is not null) Dance.ScriptEnded();
        else Actor.SetActive(false);
    }
    private void Animate()
    {
        if (!Actor.Active) return;
        if(BigBang is not null&&_var3e!=0) return;
        if(Dance is not null) { if(Dance.State==1) Actor.FaceLinkAndAnimateOneUpdate(Context.Player); return; }
        if (_fixedAnimation || Actor.Record.SubId is 3 or 4 && Actor.Record.Id == 0x66)
            Actor.AnimateAsNpcOneUpdate(Context.Player);
        else Actor.FaceLinkAndAnimateOneUpdate(Context.Player);
    }
    internal bool TryInteract(NpcCharacter actor)
    {
        if (actor != Actor || !Actor.Active || !_sensitive || _owner.BlocksGameplay) return false;
        _pending = true;
        return true;
    }
    internal void Cancel(bool deactivateActor = true)
    {
        ReleaseInputControl(); _runner.Clear(); _pending = false;
        Gallery?.Cancel(); BigBang?.Cancel(); Carts?.Cancel(); _generation++; _secretPending=false;
        if(Dance is not null || Actor.Record is {Id:0x66,SubId:2})
        { Context.Player.SetCutsceneDrawZFixed(0); Context.Player.SetScriptedLinkAnimationMode(null); }
        if (GodotObject.IsInstanceValid(Actor))
        {
            Actor.SetScriptButtonSensitive(false);
            if (deactivateActor) Actor.SetActive(false);
        }
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Goron";
    public override void SetInputEnabled(bool enabled)
    { base.SetInputEnabled(enabled); if(enabled) Gallery?.EnableInput(); }
    public override void SetMusic(int music) => Context.Sound.PlayMusicIfChanged(music);
    public override void InitializeActorCollisionRadii(string actor) => Actor.InitializeCollisionRadii();
    public override void SetActorCollisionRadii(string actor,int radiusY,int radiusX) => Actor.SetCollisionRadii(radiusY,radiusX);
    public override void SetActorButtonSensitive(string actor)
    {
        _sensitive = true; Actor.SetScriptButtonSensitive(true);
    }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool value = _pending; _pending = false; return value;
    }
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) =>
        Actor.SetScriptAnimation(encodedAnimation);
    public override void SetActorCoordinates(string actor, int y, int x)
    {
        _position = new(x, y); Actor.Position = _position;
    }
    private void Animation(int index, bool fixedAnimation)
    {
        _fixedAnimation = fixedAnimation;
        if (fixedAnimation) Actor.SetScriptAnimation(Data.Animation(Actor.Record.Id, index));
        else Actor.SetFacingDirection(index switch
        { 0 => Vector2I.Up, 1 => Vector2I.Right, 3 => Vector2I.Left, _ => Vector2I.Down });
    }
    private void FaceDown()
    {
        _angle = 0x10;
        _fixedAnimation = false;
        Actor.FaceDownAndResetNativeNpcCooldown();
    }
    private void Move() => Actor.Position = OracleObjectMovement.Shared.ApplySpeed(ref _position, _speed, _angle);
    private bool HasEssence => (Context.Inventory.Essences & 0x10) != 0;
    private bool SavedElder => Context.Rooms.SaveData.HasGlobalFlag(0x2f);
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding) => binding switch
    {
        "wcddb:CPU_ZFLAG" => _zero ? 1 : 0,
        "wcddb:$80" => _zero ? 1 : 0,
        "wcddb:CPU_CFLAG" => _carry ? 1 : 0,
        "Interaction.pressedAButton" => _pending ? 1 : 0,
        "Interaction.subid" => Actor.Record.SubId,
        "Interaction.var03" => Actor.Record.Var03,
        "Interaction.textID" => _loadedText & 0xff,
        "Interaction.var3c" => _var3c,
        "Interaction.var3d" => _var3d,
        "Interaction.var3e" => _var3e,
        "Interaction.animParameter" => Actor.CurrentAnimationParameter,
        "GLOBALFLAG_SAVED_GORON_ELDER" => SavedElder ? 1 : 0,
        "GLOBALFLAG_FINISHEDGAME" => Context.Rooms.SaveData.HasGlobalFlag(0x14) ? 1 : 0,
        "GLOBALFLAG_BEGAN_ELDER_SECRET" => Context.Rooms.SaveData.HasGlobalFlag(0x6c) ? 1 : 0,
        "GLOBALFLAG_DONE_ELDER_SECRET" => Context.Rooms.SaveData.HasGlobalFlag(0x76) ? 1 : 0,
        "wTmpcfc0.shootingGallery.disableGoronNpcs" => Wram.ReadWramByte(0xcfdc),
        "FinalRound" => Gallery?.FinalRound==true?1:0,
        "wTextInputResult" => _secretResult,
        "wTmpcfc0.bigBangGame.gameStatus" => Wram.ReadWramByte(0xcfc0),
        "wTmpcfc0.bigBangGame.prizeIndex" => Wram.ReadWramByte(0xcfd6),
        "wTmpcfc0.targetCarts.prizeIndex" => Wram.ReadWramByte(0xcfd6),
        "wTmpcfc0.targetCarts.beginGameTrigger" => Wram.ReadWramByte(0xcfdb),
        "wTmpcfc0.goronCutscenes.elderVar_cfdd" => Wram.ReadWramByte(0xcfdd),
        "wTmpcfc0.genericCutscene.state" => Wram.ReadWramByte(0xcfc0),
        "wTmpcfc0.goronCutscenes.goronGuardMovedAside" => Wram.ReadWramByte(0xcfc0),
        "wPaletteThread_mode" => _owner.PaletteBusy ? 1 : 0,
        "w1Link.id" => _owner.MovingLink ? 1 : 0,
        "wSelectedTextOption" => EventResources.RequireDialogueChoice("Goron dance difficulty has no choice."),
        _ when binding.StartsWith("wTmpcfc0.goronDance.",StringComparison.Ordinal) => Wram.ReadWramByte(DanceAddress(binding)),
        _ when binding.StartsWith("Treasure:",StringComparison.Ordinal) =>
            Context.Inventory.HasTreasure(Convert.ToInt32(binding[9..],16)) ? 1 : 0,
        _ => throw UnsupportedCommand($"read Goron binding {binding}")
    };
    public override void WriteMemory(string binding, int value)
    {
        if(binding=="wcc50") { Context.Player.SetScriptedLinkAnimationMode(value); return; }
        if(binding.StartsWith("wTmpcfc0.goronDance.",StringComparison.Ordinal))
        { Wram.SetWramByte(DanceAddress(binding),(byte)value); return; }
        int address = binding switch
        {
            "wTmpcfc0.goronCutscenes.elderVar_cfdd" => 0xcfdd,
            "wTmpcfc0.genericCutscene.state" => 0xcfc0,
            "wTmpcfc0.goronCutscenes.goronGuardMovedAside" => 0xcfc0,
            "wTmpcfc0.genericCutscene.cfde" => 0xcfde,
            "wTmpcfc0.genericCutscene.cfdf" => 0xcfdf,
            "wTmpcfc0.bigBangGame.gameStatus" => 0xcfc0,
            "wTmpcfc0.targetCarts.beginGameTrigger" => 0xcfdb,
            "wShopHaveEnoughRupees" => 0xccd5,
            _ => throw UnsupportedCommand($"write Goron binding {binding}")
        };
        Wram.SetWramByte(address, checked((byte)value));
    }
    public override bool GateOpen(string gate) => gate == "PaletteDone" ? !_owner.PaletteBusy :
        throw UnsupportedCommand($"read Goron gate {gate}");
    public override bool RoomFlagSet(int flag) => Context.Rooms.SaveData.HasRoomFlag(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, (byte)flag);
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, (byte)flag);
    public override void ShowLoadedText() => Text(_loadedText);
    public override bool TextOptionEquals(int value) =>
        EventResources.RequireDialogueChoice("Goron bomb-flower prompt has no choice.") == value;
    public override void ShowText(int id, string message) => ShowText(id, message, null);
    public override void ShowText(int id, string message, int? position)
    {
        message=message.Replace("\\secret1",_secret,StringComparison.Ordinal);
        if(Gallery is not null) message=message.Replace("\\num1",Gallery.Score.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal);
        else if(Carts is not null) message=message.Replace("\\num1",Wram.ReadWramByte(0xcfde).ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal);
        else if(Dance is not null) message=message.Replace("\\num1",(8-Wram.ReadWramByte(0xcfdb)).ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal);
        if (message.Contains("\\opt(",StringComparison.Ordinal))
            Context.ShowChoiceDialogue(message, textboxPosition: position);
        else Context.ShowDialogue(message, position);
    }
    private void Text(int id)
    {
        var text = Data.Text(id); ShowText(id, text.Message, text.Position == 0 ? null : text.Position);
    }
    public override void GiveItem(int id, int parameter) => Context.GrantScriptTreasure(
        Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, id, parameter,
        id switch { 0x43 => "TREASURE_OBJECT_CROWN_KEY_00", 0x59 => "TREASURE_OBJECT_GORON_LETTER_00",
            0x5c => "TREASURE_OBJECT_GORON_VASE_00", 0x5d => "TREASURE_OBJECT_GORONADE_00",
            _ => Data.Reward(id,parameter) }, "goron.s");
    public override bool UpdateNativeHandler(string handler, CutsceneActorId? actor, int commandUpdate, int frames, string payload)
    {
        if(handler is "MoveLeft" or "MoveRight")
        { if(commandUpdate==0) { _angle=handler=="MoveLeft"?0x18:8; Animation(handler=="MoveLeft"?3:1,true); } }
        else if (handler != "ApplySpeed") throw UnsupportedCommand(handler);
        if (commandUpdate >= frames) return true;
        if (commandUpdate != 0) Move();
        return false;
    }
    public override void RunNativeHandler(string handler)
    {
        string[] parts = handler.Split(", ", StringSplitOptions.None);
        string op = parts[0];
        int arg = parts.Length > 1 && parts[1].StartsWith('$') ?
            int.Parse(parts[1][1..], NumberStyles.HexNumber) : 0;
        switch (op)
        {
            case "Delete": Actor.SetActive(false); _runner.Clear(); return;
            case "goron_targetCarts_spawnPrize":
                int cartPrize=RoomFlagSet(0x20)?new[]{1,1,1,1,1,1,1,1,2,2,2,3,3,4,4,4}[Context.Entities.NextRandomValue()&15]:0;
                if(cartPrize==4&&Context.Inventory.HasTreasure(6)) cartPrize=3;
                Wram.SetWramByte(0xcfd6,(byte)cartPrize);
                _owner.DisplayPrize(new[]{"TREASURE_OBJECT_ROCK_BRISKET_01","TREASURE_OBJECT_RUPEES_11","TREASURE_OBJECT_RUPEES_12","TREASURE_OBJECT_GASHA_SEED_06","TREASURE_OBJECT_BOOMERANG_01"}[cartPrize],new(0x78,0x78),0); return;
            case "goron_targetCarts_deleteCrystals": Carts!.DeleteCrystals(); return;
            case "goron_targetCarts_loadCrystals": Carts!.LoadCrystals(false); return;
            case "goron_targetCarts_configureInventory": Carts!.ConfigureInventory(); return;
            case "goron_targetCarts_restoreInventory": Carts!.RestoreInventory(); return;
            case "goron_targetCarts_deleteMinecartAndClearStaticObjects": Carts!.DeleteCart(); return;
            case "Spawn:INTERAC_MINECART": Carts!.SpawnCart(); return;
            case "goron_targetCarts_beginGame": Carts!.Begin(); return;
            case "goron_targetCarts_endGame": Carts!.End(); return;
            case "goron_targetCarts_setLinkPositionToCartPlatform": Context.Player.SetScriptedPosition(new(0x38,0x88)); Context.Player.Face(Vector2I.Up); return;
            case "goron_targetCarts_setLinkPositionAfterGame": Context.Player.SetScriptedPosition(new(0xa8,0x78)); Context.Player.Face(Vector2I.Right); return;
            case "goron_checkLinkNotInAir": _zero=!Context.Player.MinecartJumpActive; return;
            case "goron_targetCarts_setupNumTargetsHitText":
                int hits=Wram.ReadWramByte(0xcfde); Wram.SetWramByte(0xcba8,(byte)((hits/10)*16+hits%10)); Wram.SetWramByte(0xcba9,0); return;
            case "goron_targetCarts_checkHitAllTargets": _zero=Wram.ReadWramByte(0xcfde)==12; return;
            case "goron_targetCarts_checkHit9OrMoreTargets": _carry=Wram.ReadWramByte(0xcfde)>=9; return;
            case "Random:Interaction.var3d":
                if(arg!=1) throw UnsupportedCommand(handler);
                _var3d=Context.Entities.NextRandomValue()&arg; return;
            case "goron_bigBang_spawnPrize":
                int prize=RoomFlagSet(0x20)?new[]{1,1,1,2,2,2,2,2,2,2,2,2,2,3,3,4}[Context.Entities.NextRandomValue()&15]:0;
                if(prize==4) prize+=Context.Entities.NextRandomValue()&1;
                Wram.SetWramByte(0xcfd6,(byte)prize);
                _owner.DisplayPrize(new[]{"TREASURE_OBJECT_OLD_MERMAID_KEY_01","TREASURE_OBJECT_RUPEES_12","TREASURE_OBJECT_RUPEES_13","TREASURE_OBJECT_GASHA_SEED_06","TREASURE_OBJECT_RING_12","TREASURE_OBJECT_RING_13"}[prize],new(0x50,0x38),-16); return;
            case "goron_bigBang_hideSelf": _var3e=1; Actor.SetCollisionRadii(0,0); Actor.SetScriptVisible(false); return;
            case "goron_bigBang_unhideSelf": _var3e=0; Actor.SetCollisionRadii(6,6); Actor.SetScriptVisible(true); return;
            case "goron_bigBang_initLinkPosition": Context.Player.SetScriptedPosition(new(0x50,0x48)); Context.Player.Face(Vector2I.Up); Context.Player.SetScriptedLinkAnimationMode(null); return;
            case "goron_bigBang_blockOrRestoreExit": BigBang!.Exit(arg); return;
            case "goron_bigBang_loadMinigameLayout1_topHalf": BigBang!.Layout("minigameLayout1_topHalf",false); return;
            case "goron_bigBang_loadMinigameLayout1_bottomHalf": BigBang!.Layout("minigameLayout1_bottomHalf",true); return;
            case "goron_bigBang_loadMinigameLayout2_topHalf": BigBang!.Layout("minigameLayout2_topHalf",false); return;
            case "goron_bigBang_loadMinigameLayout2_bottomHalf": BigBang!.Layout("minigameLayout2_bottomHalf",true); return;
            case "goron_bigBang_loadNormalRoomLayout_topHalf": BigBang!.Layout("normalRoomLayout",false); return;
            case "goron_bigBang_loadNormalRoomLayout_bottomHalf": BigBang!.Layout("normalRoomLayout",true); return;
            case "goron_bigBang_createBombSpawner": BigBang!.Begin(); return;
            case "goron_bigBang_checkLinkHitByBomb": _zero=Context.Player.InvincibilityFrames!=0; return;
            case "goron_checkLinkInAir": _zero=Context.Player.TopDownAirZ==0; return;
            case "clearParts": BigBang!.Clear(); return;
            case "setLinkToState08AndSetDirection":
                Context.Player.BeginCutsceneControl(owner:this);
                Context.Player.Face(parts[1] switch {"DIR_LEFT"=>Vector2I.Left,"DIR_DOWN"=>Vector2I.Down,_=>throw UnsupportedCommand(handler)}); return;
            case "AskElderSecret":
                _secretPending=true; int generation=_generation;
                if(_owner.OpenSecretMenu is null||!_owner.OpenSecretMenu(8,valid=>
                    { if(generation!=_generation) return; _secretResult=valid?0:1; _secretPending=false; }))
                    throw UnsupportedCommand("open ELDER_SECRET $08 menu");
                return;
            case "GenerateElderSecret": _secret=new LinkedGameNpcDatabase().GenerateSecret(0x18,Context.Rooms.SaveData); return;
            case "shootingGallery_equipSword": Gallery!.Equip(false); return;
            case "shootingGallery_equipBiggoronSword": Gallery!.Equip(true); return;
            case "shootingGallery_restoreEquips": Gallery!.RestoreEquips(); return;
            case "shootingGallery_setEntranceTiles": Gallery!.Entrance(arg); return;
            case "shootingGallery_removeAllTargets": Gallery!.RemoveTargets(); return;
            case "shootingGallery_beginGame": Gallery!.Begin(); return;
            case "EnableAllObjects": if(Gallery is not null) Gallery.EnableObjects(); else SetInputEnabled(true); return;
            case "shootingGallery_initLinkPosition": Context.Player.SetScriptedPosition(new Vector2(0x50,0x60)); Context.Player.Face(Vector2I.Up); return;
            case "shootingGallery_initLinkPositionAfterGame": Context.Player.SetScriptedPosition(new Vector2(0x68,0x68)); Context.Player.Face(Vector2I.Right); return;
            case "shootingGallery_initLinkPositionAfterBiggoronGame": Context.Player.SetScriptedPosition(new Vector2(0x38,0x68)); Context.Player.Face(Vector2I.Left); return;
            case "shootingGallery_cpScore": _zero=Gallery!.Score>=new[]{350,250,150,50,400,300,200,100,300}[arg]; return;
            case "Write:Interaction.var31": _pending=arg!=0; return;
            case "ResetMusic": Context.Sound.PlayRoomMusic(Context.Rooms.ActiveGroup,Context.Rooms.CurrentRoom.Id,Context.Rooms.SaveData); return;
            case "restartSound": Context.Sound.RestartSound(); return;
            case "fadeoutToWhite": _owner.BeginFade(true,1); return;
            case "setScreenShakeCounter": Context.Entities.BeginScreenShake(arg); return;
            case "clearAllItemsAndPutLinkOnGround": Context.Entities.ClearPhysicalPlayerItems(); Context.Player.PutOnGroundForScript(); return;
            case "goronDance_initLinkPosition": Context.Player.WarpTo(new Vector2(0x50,0x5c)); Context.Player.Face(Vector2I.Down); return;
            case "goronDance_clearVariables": Dance!.Clear(); return;
            case "goronDance_restartGame": Dance!.Restart(); return;
            case "goronDance_checkNumFailedRounds":
                _zero=Wram.ReadWramByte(0xcfdb)==0;
                Wram.SetWramByte(0xcba8,(byte)(8-Wram.ReadWramByte(0xcfdb)));
                Wram.SetWramByte(0xcba9,0); return;
            case "goronDance_giveRandomRingPrize":
                int ringIndex=(Context.Entities.NextRandomValue()&1)+(Past&&Wram.ReadWramByte(0xcfdd)==0?0:2);
                if(!Context.Entities.InteractionSlotAvailable) return;
                Context.Entities.GrantGroundTreasure(new GroundTreasureGrantRequest(Context.Rooms.ActiveGroup,Context.Rooms.CurrentRoom.Id,
                    0,(int)Context.Player.Position.Y,(int)Context.Player.Position.X,"TREASURE_OBJECT_RING_00","scriptHelper.s:goronDance_giveRandomRingPrize")
                    {SpawnMode=0,GrabMode=2,InventoryWrite=GroundTreasureInventoryWrite.UnappraisedRing,
                        InventoryParameter=new[]{0x19,0x3f,0x30,0x1e}[ringIndex],RoomFlagTiming=GroundTreasureRoomFlagTiming.Never},Context.Player); return;
            case "goron_showText_differentForPresent": Text(Convert.ToInt32(parts[1][4..],16)+(Past?0:0x20)); return;
            case "goron_decideTextToShow_differentForLinkedInPast": Text(Convert.ToInt32(parts[1][4..],16)+(Past?(IsLinkedGame?0x20:0):0x10)); return;
            case "shootingGallery_checkLinkHasRupees": _zero=Context.Inventory.Rupees>=int.Parse(parts[1][9..],CultureInfo.InvariantCulture); return;
            case "removeRupeeValue": Context.Inventory.AddRupees(-int.Parse(parts[1][9..],CultureInfo.InvariantCulture)); return;
            case "giveRupees": Context.Inventory.AddRupees(int.Parse(parts[1][9..],CultureInfo.InvariantCulture)); return;
            case "goron_determineTextForGenericNpc":
                int state = Context.Rooms.SaveData.HasGlobalFlag(0x14) ? (Past ? 2 : 3) :
                    Past ? (SavedElder ? 1 : 0) : Context.Rooms.SaveData.HasGlobalFlag(0x1a) ? 2 :
                    (Context.Inventory.Essences & 8) != 0 ? 1 : 0;
                int low = Data.Bytes($"generic-{Actor.Record.SubId:x2}")[Actor.Record.Var03 * 4 + state];
                if (low == 0x27 && !IsLinkedGame) low = 0xff;
                _loadedText = 0x3100 | low; return;
            case "goron_showTextForClairvoyantGoron":
                int tip = 0;
                if ((Context.Inventory.Essences & 0x20) == 0)
                {
                    int[] treasures = Data.Bytes("hint-treasures");
                    do { tip++; } while (tip < 8 && !Context.Inventory.HasTreasure(treasures[tip - 1]));
                }
                if (tip == 3)
                { if (Context.Inventory.HasTreasure(0x5a)) tip = 9; }
                else if (tip >= 5 && Context.Rooms.SaveData.HasRoomFlag(3,0x3e,0x40)) tip = 10;
                Text((Past ? 0x3143 : 0x314f) + tip); return;
            case "Write:Interaction.oamFlags": Actor.SetBasePalette(arg); return;
            case "Write:Interaction.var3c": _var3c=arg; return;
            case "goron_checkGracefulGoronQuestStatus":
                _var3e=!Context.Inventory.HasTreasure(0x5a) ? 2 : !Context.Inventory.HasTreasure(0x45) ? 1 : 0; return;
            case "goron_checkInPresent": _zero=!Past; return;
            case "goron_showText_differentForPast":
                Text(Convert.ToInt32(parts[1][4..],16)+(Past?0:12)); return;
            case "loseTreasure":
                int treasure=parts[1] switch { "TREASURE_GORON_VASE"=>0x5c,"TREASURE_ROCK_BRISKET"=>0x5e,
                    "TREASURE_GORONADE"=>0x5d,
                    _ when parts[1].StartsWith('$')=>arg, _=>throw UnsupportedCommand(handler) };
                Context.Inventory.LoseTreasure(treasure); return;
            case "goron_clearRefillBit": Wram.SetWramByte(0xcc4d,(byte)(Wram.ReadWramByte(0xcc4d)&0xfe)); return;
            case "goron_checkEnoughTimePassed": _zero=(Wram.ReadWramByte(0xcc4d)&1)!=0; return;
            case "goron_tryTakeEmberSeedsAndBombs":
                _zero=Context.Inventory.HasTreasure(0x19)&&Context.Inventory.HasTreasure(0x20)&&
                    Context.Inventory.HasTreasure(3)&&Context.Inventory.EmberSeeds>=0x20&&Context.Inventory.Bombs>=0x20;
                if (_zero)
                {
                    for(int i=0;i<20;i++) Context.Inventory.TryConsumeBomb();
                    Context.Inventory.TryConsumeSeedsFromScript(0x20,20);
                }
                return;
            case "checkEssenceObtained": _zero = HasEssence; return;
            case "checkEssenceNotObtained": _zero = !HasEssence; return;
            case "goron_beginWalkingLeft":
                _speed = 0x14; _angle = 0x18; _movement = 0x40; _walkingAnimation = 3;
                Animation(3, true); return;
            case "goron_decMovementCounter": _movement = Math.Max(0, _movement - 1); _zero = _movement == 0; return;
            case "goron_reverseWalkingDirection":
                _movement = 0x80; _angle ^= 0x10; _walkingAnimation ^= 2;
                Animation(_walkingAnimation, true); return;
            case "goron_refreshWalkingAnimation": Animation(_walkingAnimation, true); return;
            case "objectApplySpeed": Move(); return;
            case "turnToFaceLink":
                Actor.SetFacingDirection(RoomEventResources.DirectionToward(Actor.Position, Context.Player.Position)); return;
            case "goron_showTextForGoronWorriedAboutElder": Text(SavedElder ? 0x247a : 0x2479); return;
            case "goron_showTextForSubid05":
                int[] texts = Data.Bytes("dialogue-table");
                int variant = Actor.Record.Var03;
                Text(texts[variant < 3 ? variant * 2 + (SavedElder ? 1 : 0) : variant + 3]); return;
            case "goron_checkShouldBeNapping":
                // objectCheckCollidedWithLink_ignoreZ adds Link's $06 radius.
                Vector2 delta = OracleObjectMath.ToPixelPosition(Context.Player.Position) - Actor.Position;
                _carry = delta.X < -0x1e || delta.X >= 0x1e || delta.Y < -0x1e || delta.Y >= 0x1e; return;
            case "goron_faceDown": FaceDown(); return;
            case "goron_setAnimation": Animation(arg, true); return;
            case "goron_createRockDebrisToLeft": _owner.CreateDebris(Actor.Position + new Vector2(-6, -10), true); return;
            case "goron_createRockDebrisToRight": _owner.CreateDebris(Actor.Position + new Vector2(6, -10), true); return;
            case "goron_checkLinkApproachedWithBombFlower":
                Vector2 offset = OracleObjectMath.ToPixelPosition(Context.Player.Position) - new Vector2(0x58, 0x88);
                _carry = Context.Inventory.HasTreasure(0x49) && offset.X >= -14 && offset.X < 14 && offset.Y >= -30 && offset.Y < 30; return;
            case "goron_putLinkInState08": Context.Player.BeginCutsceneControl(owner: this); return;
            case "forceLinkDirection": Context.Player.Face(Vector2I.Left); return;
            case "goron_setSpeedToMoveDown": _speed=0x28; _angle=0x10; Animation(2,true); return;
            case "goron_cpLinkY": _zero=(int)Actor.Position.Y == (int)Context.Player.Position.Y; return;
            case "goron_checkReachedLinkHorizontally": _zero=(int)Actor.Position.X == (int)Context.Player.Position.X-14; return;
            case "goron_cpXTo48": _zero=(int)Actor.Position.X==0x48; return;
            case "goron_cpYTo60": _zero=(int)Actor.Position.Y==0x60; return;
            case "createExclamationMark": _owner.CreateExclamation(Actor.Position, int.Parse(parts[1], CultureInfo.InvariantCulture)); return;
            case "goron_createBombFlowerSprite": _owner.CreateBombFlower(); return;
            case "goron_deleteTreasure": _owner.DeleteBombFlower(); return;
            case "goron_createExplosionIndex": _owner.CreateExplosion(arg); return;
            case "goron_initCountersForBombFlowerExplosion": _movement=90; _soundCounter=1; return;
            case "goron_countdownToNextExplosionGroup":
                if (--_explosionCounter != 0) return;
                _explosionIndex=(_explosionIndex+1)&7;
                _explosionCounter=Data.Bytes("explosion-counters")[_explosionIndex];
                int[] groups=Data.Bytes("explosion-groups");
                for (int i=0;i<4 && groups[_explosionIndex*4+i]!=0xff;i++)
                    _owner.CreateExplosion(groups[_explosionIndex*4+i]);
                return;
            case "goron_countdownToPlayRockSoundAndShakeScreen":
                if (--_soundCounter != 0) return;
                _soundCounter=5; Context.Sound.PlaySound(0xa5); Context.Entities.BeginScreenShake(4); return;
            case "fadeoutToWhiteWithDelay": _owner.BeginFade(true, arg); return;
            case "fadeinFromWhite": _owner.BeginFade(false, 1); return;
            case "goron_clearRockBarrier": _owner.ClearBarrier(); return;
            case "goron_createFallingRockSpawner": _owner.StartFallingRocks(); return;
            case "goronElder_lookingUpAnimation": Animation(4,true); return;
            case "goronElder_normalAnimation": Animation(2,false); return;
            case "moveLinkToPosition": _owner.BeginMoveLink(); return;
            case "Spawn:INTERAC_GORON_ELDER": _owner.SpawnElder(); return;
            case "Write:Interaction.pressedAButton": _pending=arg != 0; return;
            case "Write:Interaction.speed":
                if (parts[1] != "SPEED_100") throw UnsupportedCommand(handler);
                _speed=0x28; return;
            case "Write:Interaction.var3a": _explosionCounter=arg; return;
            case "Write:Interaction.var3b": _explosionIndex=arg; return;
            default:
                if (op.StartsWith("Angle:", StringComparison.Ordinal))
                { _angle=int.Parse(op[6..], NumberStyles.HexNumber); return; }
                if (op.StartsWith("Speed:", StringComparison.Ordinal))
                { _speed=int.Parse(op[6..], NumberStyles.HexNumber); return; }
                throw UnsupportedCommand($"execute Goron helper {handler}");
        }
    }
    private static int DanceAddress(string binding) => binding[20..] switch
    {
        "failureType"=>0xcfd1,"danceAnimation"=>0xcfd2,"linkJumping"=>0xcfd3,"linkStartedDance"=>0xcfd4,
        "frameCounter"=>0xcfd5,"currentMove"=>0xcfd7,"consecutiveBPressCounter"=>0xcfd8,"cfd9"=>0xcfd9,
        "roundIndex"=>0xcfda,"numFailedRounds"=>0xcfdb,"beat"=>0xcfdc,"danceLevel"=>0xcfdd,
        "remainingRounds"=>0xcfde,"dancePattern"=>0xcfdf,
        _=>throw new InvalidOperationException($"Unknown Goron dance WRAM binding {binding}.")
    };
}
