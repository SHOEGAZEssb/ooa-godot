using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;

/// <summary>One interactionRunScript lane for a spawned household actor.</summary>
internal sealed class FamilyNpcScriptHost : NpcInteractionCommandHost
{
    private readonly NpcCharacter _npc;
    private readonly FamilyNpcScriptDatabase _data;
    private readonly InventoryState _inventory;
    private readonly TreasureDatabase _treasures;
    private readonly OracleSaveData _save;
    private GroundTreasurePickup? _treasure;
    private int _var3a;
    private int _var3b;
    private int _var3c;
    private int _loadedTextId;
    private bool _ownsDisabledMask;

    internal FamilyNpcScriptHost(NpcCharacter npc, FamilyNpcScriptDatabase data,
        RoomSession rooms, RoomEntityManager entities, DialogueBox dialogue,
        InventoryState inventory, TreasureDatabase treasures)
        : base("Family", rooms, entities, dialogue, data.Script(npc.Record))
    {
        _npc = npc;
        _data = data;
        _inventory = inventory;
        _treasures = treasures;
        _save = rooms.SaveData;
        BindObject(npc);
    }

    internal void Advance(Player player) => AdvanceObject(player);

    protected override bool MatchesAndPrepare(NpcCharacter npc) =>
        ReferenceEquals(npc, _npc);

    public override bool TryInteract(NpcInteractionTarget target, Player player) =>
        MatchesAndPrepare(target.Npc) && QueueObjectInteraction(target, player);

    public override bool ScriptExecutionBlocked => base.ScriptExecutionBlocked ||
        Entities.ScreenTransitionActive;

    public override void SetDisabledObjects(int value)
    {
        if (value is not (0 or 0x91))
            throw UnsupportedCommand($"set disabled objects ${value:x2}");
        Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,
            (byte)value);
        _ownsDisabledMask = value != 0;
    }

    public override void ShowText(int textId, string message)
    {
        _ = _data.Text(textId);
        string resolved = BipinBlossomFamilyInteractionDatabase.SubstituteChildName(
            message, _save);
        ShowDialogue(resolved, resolved.Contains("\\opt()", StringComparison.Ordinal));
    }

    public override void ShowLoadedText() =>
        ShowText(_loadedTextId, _data.Text(_loadedTextId));

    public override void SetActorAnimation(string actor, int animation,
        string encodedAnimation)
    {
        _ = RequireActor(actor);
        if (_npc.Record.Id != InteractionId.Bipin || animation is not (2 or 3))
            throw UnsupportedCommand($"setanimation ${animation:x2}");
        _npc.SetScriptAnimation(encodedAnimation);
    }

    public override int ReadMemory(string binding) => binding switch
    {
        "Interaction.var03" => _npc.Record.Var03,
        "Interaction.var3a" => _var3a,
        "Interaction.var3b" => _var3b,
        "Interaction.var3c" => _var3c,
        "wChildStatus" => _save.ReadWramByte(WramAddress.wChildStatus),
        "wChildStage8Response" => _save.ReadWramByte(WramAddress.wChildStage8Response),
        "wSelectedTextOption" => TakeChoice(),
        _ => throw UnsupportedCommand($"read {binding}")
    };

    public override bool MemoryEquals(string binding, int value) =>
        ReadMemory(binding) == value;

    public override void RunNativeHandler(string handler)
    {
        string[] words = handler.Split(' ', 2);
        if (words.Length != 2)
            throw UnsupportedCommand(handler);
        string command = words[0];
        string[] args = words[1].Split(',', StringSplitOptions.TrimEntries);
        switch (command)
        {
            case "asm15": RunAsm(args); break;
            case "writeobjectbyte":
                if (args.Length != 2) throw UnsupportedCommand(handler);
                if (args[0] == "Interaction.var3a") _var3a = Hex(args[1]);
                else if (args[0] == "Interaction.textID+1")
                    _loadedTextId = (_loadedTextId & 0xff) | (TextByte(args[1]) << 8);
                else throw UnsupportedCommand(handler);
                break;
            case "getrandombits":
                if (args.Length != 2 || args[0] != "Interaction.textID")
                    throw UnsupportedCommand(handler);
                _loadedTextId = (_loadedTextId & 0xff00) |
                    (Entities.NextRandomValue() & Hex(args[1]));
                break;
            case "addobjectbyte":
                if (args.Length != 2 || args[0] != "Interaction.textID")
                    throw UnsupportedCommand(handler);
                _loadedTextId = (_loadedTextId & 0xff00) |
                    ((_loadedTextId + TextByte(args[1])) & 0xff);
                break;
            default: throw UnsupportedCommand(handler);
        }
    }

    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId is not (TreasureId.GashaSeed or TreasureId.Bombs) ||
            parameter != (treasureId == TreasureId.GashaSeed ? 3 : 2))
            throw UnsupportedCommand($"giveitem ${treasureId:x2}:${parameter:x2}");
        TreasureObjectRecord item = _treasures.GetObject(treasureId, parameter);
        Vector2 position = ScriptPlayer.Position;
        _treasure = Entities.GrantGroundTreasure(new GroundTreasureGrantRequest(
            Rooms.ActiveGroup, Rooms.CurrentRoom.Id, 0,
            Mathf.FloorToInt(position.Y), Mathf.FloorToInt(position.X),
            item.Name,
            $"family scripts.s:{CurrentCommandIndex} giveitem ${treasureId:x2}:${parameter:x2}")
        {
            SpawnMode = TreasureSpawnMode.Instant,
            GrabMode = TreasureGrabMode.TwoHands,
            DialogueTiming = GroundTreasureDialogueTiming.AfterGrab,
            CompletionOwner = GroundTreasureCompletionOwner.Caller,
            ExpectedTreasureId = treasureId,
            ExpectedSubId = parameter,
            ExpectedObjectParameter = item.Parameter
        }, ScriptPlayer);
    }

    protected override void BeforeAdvanceFrame()
    {
        if (_treasure is null) return;
        _treasure.Finish(ScriptPlayer);
        _treasure = null;
    }

    protected override void ResetHostState()
    {
        if (_ownsDisabledMask)
        {
            Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
            _ownsDisabledMask = false;
        }
        if (_treasure is null) return;
        _treasure.Finish(ScriptPlayer);
        _treasure = null;
    }

    private void RunAsm(string[] args)
    {
        string name = args[0];
        int value = args.Length > 1 ? RupeeOrHex(args[1]) : 0;
        switch (name)
        {
            case "scriptHelp.bipin_showText_subid1To9":
                int subid = _npc.Record.SubId;
                int textId = subid switch
                {
                    1 => 0x4302, 2 or 3 => 0x4303, 4 => 0x4304,
                    5 => 0x4305, 6 => 0x4306, 7 => 0x4307,
                    8 or 9 => 0x4308,
                    _ => throw UnsupportedCommand($"Bipin subid ${subid:x2}")
                };
                ShowText(textId, _data.Text(textId));
                break;
            case "scriptHelp.checkc6e2BitSet":
                _var3b = (_save.ReadWramByte(WramAddress.wc6e2) & (1 << value)) != 0 ? 1 : 0;
                break;
            case "scriptHelp.setc6e2Bit":
                WriteSave(WramAddress.wc6e2,
                    _save.ReadWramByte(WramAddress.wc6e2) | (1 << value));
                break;
            case "scriptHelp.setNextChildStage":
                WriteSave(WramAddress.wNextChildStage, value);
                break;
            case "scriptHelp.blossom_addValueToChildStatus":
            case "scriptHelp.child_addValueToChildStatus":
                WriteSave(WramAddress.wChildStatus,
                    (_save.ReadWramByte(WramAddress.wChildStatus) + value) & 0xff);
                break;
            case "scriptHelp.blossom_checkHasRupees":
            case "scriptHelp.child_checkHasRupees":
                _var3c = _inventory.Rupees < RupeeValue(value) ? 1 : 0;
                break;
            case "removeRupeeValue":
                _inventory.AddRupees(-RupeeValue(value));
                break;
            case "scriptHelp.child_setStage8Response":
                WriteSave(WramAddress.wChildStage8Response, value);
                break;
            case "scriptHelp.child_setStage8ResponseToSelectedTextOption":
                WriteSave(WramAddress.wChildStage8Response, value + TakeChoice());
                break;
            case "scriptHelp.child_giveRupees":
                _inventory.GiveTreasure(TreasureId.Rupees, value);
                break;
            case "scriptHelp.child_giveOneHeart":
                _inventory.Heal(4);
                break;
            case "scriptHelp.child_giveHeartRefill":
                _inventory.Heal(0x40);
                break;
            case "refillSeedSatchel":
                foreach (int seed in new[] { TreasureId.EmberSeeds, TreasureId.ScentSeeds,
                             TreasureId.PegasusSeeds, TreasureId.GaleSeeds, TreasureId.MysterySeeds })
                    if (_inventory.HasTreasure(seed)) _inventory.GiveTreasure(seed, 0x99);
                break;
            case "scriptHelp.child_playMusic":
                // constants/common/music.s: MUS_ZELDA_SAVED $38, MUS_PRECREDITS $4a.
                Entities.RequestSound(_save.ReadWramByte(WramAddress.wChildStage8Response) == 0
                    ? 0x38 : 0x4a);
                break;
            default: throw UnsupportedCommand($"asm15 {name}");
        }
    }

    private void WriteSave(int address, int value)
    {
        if (_save.WriteWramByte(address, (byte)value))
            _save.CommitInventoryChange();
    }

    private int TakeChoice() => Dialogue.TryTakeChoiceResult(out int choice)
        ? choice : throw UnsupportedCommand("read missing wSelectedTextOption");

    private int RupeeOrHex(string token) => token.StartsWith("RUPEEVAL_", StringComparison.Ordinal)
        ? token switch
        {
            "RUPEEVAL_001" => 0x01, "RUPEEVAL_010" => 0x04,
            "RUPEEVAL_050" => 0x0b, "RUPEEVAL_100" => 0x0c,
            "RUPEEVAL_150" => 0x0f, "RUPEEVAL_200" => 0x0d,
            _ => throw UnsupportedCommand($"rupee value {token}")
        }
        : Hex(token);

    private int RupeeValue(int parameter) => _treasures.RupeeValue(parameter);

    private static int Hex(string token) => token.StartsWith('$') &&
        int.TryParse(token.AsSpan(1), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out int value)
            ? value : throw new InvalidOperationException($"Invalid family hex scalar {token}.");

    private static int TextByte(string token)
    {
        string raw = token.TrimStart('<', '>');
        if (!raw.StartsWith("TX_", StringComparison.Ordinal) ||
            !int.TryParse(raw.AsSpan(3), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out int id))
            throw new InvalidOperationException($"Invalid family text byte {token}.");
        return token.StartsWith('>') ? id >> 8 : id & 0xff;
    }
}
