using Godot;
using System;

namespace oracleofages;

/// <summary>INTERAC_FARORE $10 script lane and its secret result handoff.</summary>
internal sealed class FaroreScriptHost : NpcInteractionCommandHost
{
    private readonly NpcCharacter _actor;
    private readonly FaroreScriptDatabase _data;
    private readonly LinkedGameNpcDatabase _secrets = new();
    private readonly OracleSaveData _save;
    private readonly InventoryState _inventory;
    private readonly TreasureDatabase _treasures;
    private readonly Action _redrawRoom;
    private readonly Func<Action<int?>, bool> _openSecret;
    private int _secretInputIndex = -1;
    private int _secretResult;
    private bool _secretPending;
    private bool _ownsDisabledMask;
    private bool _signalBit1;
    private int _generation;
    private int _rewardIndex = -1;
    private int _rewardPhase;
    private int _rewardTimer;
    private bool _chestCollected;
    private bool _makeChestDone;
    private GroundTreasurePickup? _upgradeTreasure;
    private string _transferSecret = string.Empty;

    internal FaroreScriptHost(NpcCharacter actor, FaroreScriptDatabase data,
        RoomSession rooms, RoomEntityManager entities, DialogueBox dialogue,
        InventoryState inventory, TreasureDatabase treasures,
        Func<Action<int?>, bool> openSecret, Action redrawRoom)
        : base("Farore", rooms, entities, dialogue, data.Script(rooms.SaveData.IsLinkedGame))
    {
        _actor = actor;
        _data = data;
        _save = rooms.SaveData;
        _inventory = inventory;
        _treasures = treasures;
        _openSecret = openSecret;
        _redrawRoom = redrawRoom;
        BindObject(actor);
    }

    internal void InitializeStateZero() =>
        _save.SetGlobalFlag(GlobalFlag.SecretChestWaiting, false);

    internal void Advance(Player player) => AdvanceObject(player);

    internal void ChestRewardCompleted() => _chestCollected = true;
    internal void MakeChestFinished() => _makeChestDone = true;
    internal void InstallChestTile()
    {
        Rooms.TrySetTile(0x75, ChestDatabase.ClosedTile);
        _redrawRoom();
    }
    internal bool HasPendingChest => _rewardIndex >= 0 && !_data.Reward(_rewardIndex).Upgrade;
    internal bool RewardFinished => _rewardIndex < 0;

    protected override bool MatchesAndPrepare(NpcCharacter npc) => ReferenceEquals(_actor, npc);
    public override bool TryInteract(NpcInteractionTarget target, Player player) =>
        MatchesAndPrepare(target.Npc) && QueueObjectInteraction(target, player);
    public override bool ScriptExecutionBlocked => base.ScriptExecutionBlocked ||
        _secretPending || Entities.ScreenTransitionActive;

    public override void SetDisabledObjects(int value)
    {
        if (value != 0x91) throw UnsupportedCommand($"set disabled objects ${value:x2}");
        Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0x91);
        _ownsDisabledMask = true;
    }

    public override void SetInputEnabled(bool enabled)
    {
        base.SetInputEnabled(enabled);
        if (enabled && _ownsDisabledMask)
        {
            Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
            _ownsDisabledMask = false;
        }
    }

    public override bool MemoryEquals(string binding, int value) => binding switch
    {
        "GLOBALFLAG_FINISHEDGAME" => (_save.HasGlobalFlag(GlobalFlag.FinishedGame) ? 1 : 0) == value,
        "GLOBALFLAG_SECRET_CHEST_WAITING" =>
            (_save.HasGlobalFlag(GlobalFlag.SecretChestWaiting) ? 1 : 0) == value,
        "cfc0bit1" => (_signalBit1 ? 1 : 0) == value,
        _ => throw UnsupportedCommand($"read {binding}")
    };

    public override int ReadMemory(string binding) => binding switch
    {
        "Interaction.var3f" => _secretResult,
        _ => throw UnsupportedCommand($"read {binding}")
    };

    public override void ShowText(int textId, string message) => ShowText(textId, message, 2);
    public override void ShowText(int textId, string message, int? textboxPosition)
    {
        if (textboxPosition != 2) throw UnsupportedCommand($"Farore textbox position {textboxPosition}");
        string resolved = message;
        if (textId == 0x551a)
        {
            string[] halves = _transferSecret.Split('|');
            if (halves.Length != 2) throw UnsupportedCommand("TX_551a before transfer secret generation");
            resolved = resolved.Replace("\\secret1", halves[0], StringComparison.Ordinal)
                .Replace("\\secret2", halves[1], StringComparison.Ordinal);
        }
        float linkY = Entities.WorldToScreen(ScriptPlayer.Position).Y;
        if (resolved.Contains("\\opt()", StringComparison.Ordinal))
            Dialogue.ShowGameplayChoiceMessage(resolved, linkY, textPosition: 2);
        else
            Dialogue.ShowGameplayMessageWithFlags(resolved, linkY, 0x02, 2);
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "AskAnySecret":
            {
                _secretPending = true;
                int generation = _generation;
                if (!_openSecret(index =>
                {
                    if (generation != _generation) return;
                    _secretInputIndex = index ?? -1;
                    _secretResult = ClassifySecret(_secretInputIndex);
                    _secretPending = false;
                })) throw UnsupportedCommand("acquire MENU_SECRET for askforsecret $ff");
                break;
            }
            case "scriptHelp.faroreCheckSecretValidity":
                _secretResult = ClassifySecret(_secretInputIndex);
                break;
            case "scriptHelp.faroreShowTextForSecretHint":
                ShowText(0x550f + (_secretInputIndex & 0x0f),
                    _data.Text(0x550f + (_secretInputIndex & 0x0f)));
                break;
            case "scriptHelp.faroreGenerateGameTransferSecret":
                GenerateTransferSecret();
                break;
            case "scriptHelp.faroreSpawnSecretChest":
                SpawnReward();
                break;
            case "ClearCfc0Bit1":
                _signalBit1 = false;
                break;
            default: throw UnsupportedCommand($"Farore native handler {handler}");
        }
    }

    private int ClassifySecret(int index)
    {
        if (index < 0) return 0;
        // scriptHelper.s: Farore accepts the Ages return secrets $30-$39;
        // the other short-secret game bands reach the wrong-game response.
        int game = (index >> 4) & 3;
        if (game == 0) return 3;
        if (game != 3) return 4;
        int quest = index & 0x0f;
        if (quest >= 10) return 3;
        if (!_save.IsLinkedGame) return 3;
        if (_save.HasGlobalFlag(0x5a + quest)) return 2;
        return _save.HasGlobalFlag(0x50 + quest) ? 1 : 5;
    }

    private void GenerateTransferSecret()
    {
        byte[] values = _secrets.GenerateGameTransferSecretValues(_save);
        string Glyphs(int start)
        {
            var builder = new System.Text.StringBuilder();
            for (int i = start; i < start + 10; i++)
            {
                if (i != start && (i - start) % 5 == 0) builder.Append(' ');
                builder.Append(_secrets.SecretSymbols[values[i]]);
            }
            return builder.ToString();
        }
        _transferSecret = Glyphs(0) + "|" + Glyphs(10);
    }

    private void SpawnReward()
    {
        int index = _secretInputIndex & 0x0f;
        if (index >= 10) throw UnsupportedCommand($"reward secret ${_secretInputIndex:x2}");
        if (!Entities.InteractionSlotAvailable) return; // getFreeInteractionSlot fails without a signal.
        _rewardIndex = index;
        _rewardPhase = 0;
        _rewardTimer = 0;
        _chestCollected = false;
        _makeChestDone = false;
        if (Entities.TryCreateInteraction(new FaroreRewardSpawn(this)) is null)
            _rewardIndex = -1;
    }

    internal void AdvanceReward(Player player)
    {
        if (_rewardIndex < 0) return;
        FaroreRewardRecord reward = _data.Reward(_rewardIndex);
        if (reward.Upgrade) AdvanceUpgrade(player, reward);
        else AdvanceChest(reward);
    }

    private void AdvanceChest(FaroreRewardRecord reward)
    {
        switch (_rewardPhase)
        {
            case 0:
                _rewardPhase = 1;
                break;
            case 1:
                TreasureObjectRecord objectRecord = _treasures.GetObject(
                    $"TREASURE_OBJECT_{reward.Symbol[9..]}_{reward.SubId:x2}");
                Entities.RuntimeState.SetWramByte(WramAddress.wChestContentsOverride,
                    (byte)objectRecord.TreasureId);
                Entities.RuntimeState.SetWramByte(WramAddress.wChestContentsOverride + 1,
                    (byte)objectRecord.SubId);
                // INTERAC_FARORE_MAKECHEST $11:$00 is another physical slot.
                // Failed allocation leaves $d9 waiting for a signal forever.
                Entities.TryCreateInteraction(new FaroreMakeChestSpawn(this));
                _rewardPhase = 2;
                break;
            case 2:
                if (!_makeChestDone) break;
                _rewardTimer = 0x3c;
                _rewardPhase = 3;
                break;
            case 3:
                if (--_rewardTimer > 0) break;
                _save.SetGlobalFlag(GlobalFlag.SecretChestWaiting);
                _signalBit1 = true;
                ShowText(0x5509, _data.Text(0x5509));
                _rewardPhase = 4;
                break;
            case 4:
                if (!_chestCollected) break;
                MarkSecretDone();
                _rewardTimer = 0x1e;
                _rewardPhase = 5;
                break;
            case 5:
                if (--_rewardTimer > 0) break;
                Rooms.TrySetTile(0x75, 0xac);
                _redrawRoom();
                _rewardIndex = -1;
                break;
        }
    }

    private void AdvanceUpgrade(Player player, FaroreRewardRecord reward)
    {
        switch (_rewardPhase)
        {
            case 0:
                _rewardPhase = 1;
                _rewardTimer = 30 + 60 + 20 + 20 + 20;
                break;
            case 1:
                if (--_rewardTimer > 0) break;
                int parameter = _rewardIndex switch
                {
                    0 => _inventory.SwordLevel >= 2 ? 2 : 1,
                    4 => _inventory.ShieldLevel >= 2 ? 2 : 1,
                    5 => 0,
                    7 => 0,
                    9 => _inventory.RingBoxLevel >= 2 ? 4 : 3,
                    _ => throw UnsupportedCommand($"upgrade reward ${_rewardIndex:x2}")
                };
                string name = _rewardIndex switch
                {
                    0 => $"TREASURE_OBJECT_SWORD_{parameter:x2}",
                    4 => $"TREASURE_OBJECT_SHIELD_{parameter:x2}",
                    5 => "TREASURE_OBJECT_BOMB_UPGRADE_00",
                    7 => "TREASURE_OBJECT_SEED_SATCHEL_UPGRADE",
                    9 => $"TREASURE_OBJECT_RING_BOX_{parameter:x2}",
                    _ => throw UnsupportedCommand("upgrade table")
                };
                GrantUpgradeTreasure(player, name, spin: false);
                if (_rewardIndex == 5) _inventory.ApplyTokayBombCapacityUpgrade();
                _rewardPhase = 2;
                _rewardTimer = 30;
                break;
            case 2:
                if (Dialogue.IsOpen || --_rewardTimer > 0) break;
                _upgradeTreasure?.Finish(player);
                _upgradeTreasure = null;
                if (_rewardIndex == 0)
                {
                    string swordSpin = $"TREASURE_OBJECT_SWORD_{_inventory.SwordLevel + 2:x2}";
                    GrantUpgradeTreasure(player, swordSpin, spin: true);
                    _rewardTimer = 0x5a;
                    _rewardPhase = 3;
                    break;
                }
                if (_rewardIndex == 7)
                    foreach (int seed in new[] { TreasureId.EmberSeeds, TreasureId.ScentSeeds,
                        TreasureId.PegasusSeeds, TreasureId.GaleSeeds, TreasureId.MysterySeeds })
                        if (_inventory.HasTreasure(seed)) _inventory.GiveTreasure(seed, 0x99);
                MarkSecretDone();
                _signalBit1 = true;
                ShowText(0x5509, _data.Text(0x5509));
                _rewardIndex = -1;
                break;
            case 3:
                if (--_rewardTimer > 0) break;
                _upgradeTreasure?.Finish(player);
                _upgradeTreasure = null;
                MarkSecretDone();
                _signalBit1 = true;
                ShowText(0x5509, _data.Text(0x5509));
                _rewardIndex = -1;
                break;
        }
    }

    private void GrantUpgradeTreasure(Player player, string objectName, bool spin)
    {
        if (!Entities.InteractionSlotAvailable) return;
        TreasureObjectRecord reward = _treasures.GetObject(objectName);
        _upgradeTreasure = Entities.GrantGroundTreasure(new GroundTreasureGrantRequest(
            Rooms.ActiveGroup, Rooms.CurrentRoom.Id, 0,
            Mathf.FloorToInt(player.Position.Y), Mathf.FloorToInt(player.Position.X),
            reward.Name, $"faroreGiveItem.s:INTERAC_FARORE_GIVEITEM ${_rewardIndex:x2}")
        {
            SpawnMode = TreasureSpawnMode.Instant,
            GrabMode = spin ? TreasureGrabMode.SpinSlash : TreasureGrabMode.TwoHands,
            DialogueTiming = GroundTreasureDialogueTiming.AfterGrab,
            CompletionOwner = GroundTreasureCompletionOwner.Caller,
            ExpectedTreasureId = reward.TreasureId,
            ExpectedSubId = reward.SubId,
            ExpectedObjectParameter = reward.Parameter
        }, player);
    }

    private void MarkSecretDone()
    {
        _save.SetGlobalFlag(0x5a + _rewardIndex);
        _save.SetGlobalFlag(GlobalFlag.SecretChestWaiting, false);
    }

    protected override void ResetHostState()
    {
        _generation++;
        _secretPending = false;
        _secretInputIndex = -1;
        _secretResult = 0;
        _transferSecret = string.Empty;
        _signalBit1 = false;
        _rewardIndex = -1;
        if (_upgradeTreasure is not null && GodotObject.IsInstanceValid(_upgradeTreasure))
            _upgradeTreasure.Finish(ScriptPlayer);
        _upgradeTreasure = null;
        if (_ownsDisabledMask)
        {
            Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
            _ownsDisabledMask = false;
        }
    }
}
