using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class RoomEntityManager
{
    private AgesRoomEntityFactory _agesFactory => _factory as AgesRoomEntityFactory ??
        throw new NotSupportedException($"{AssetRoot}: this operation requires Ages entity content.");

    internal IRoomEntityFactory BindAgesFactory(NpcDatabase npcs, EnemyDatabase enemies,
        TimePortalDatabase timePortals, RoomSession? rooms)
    {
        _familyState = new BipinBlossomFamilyStateResolver(npcs);
        return new AgesRoomEntityFactory(this, enemies, timePortals, rooms);
    }

    internal void SpawnDiggingEnemy(int roll, int subId, Vector2 position)
    {
        if (_runtimeState.ReadField("wDiggingUpEnemiesForbidden") != 0)
            return;
        TryAllocateEnemy(_ => _agesFactory.CreateDiggingEnemy(
            _diggingEnemies.Enemy(roll, subId), position, _roomForActiveEntities,
            _diggingEnemies.BeetleCounters));
    }

    internal TwinrovaRoomEntity? TryCreateTwinrova(Vector2 position, int var03) =>
        (TwinrovaRoomEntity?)TryAllocateEnemy(_ => _agesFactory.EnemyFactory.CreateTwinrova(position, var03, _roomForActiveEntities));
    internal void AddPalaceRescueActor() => AddEntity(new PalacePossessionRoomEntity(this,_agesFactory.Resources.PalacePossession,0x6e,0,Vector2.Zero));
    internal PossessedNayruRoomEntity? TryCreatePalacePossessionEnemy(Vector2 position,int subid,PossessedNayruCharacter? parent=null) =>
        (PossessedNayruRoomEntity?)TryAllocateEnemy(_ =>
        {
            var actor=new PossessedNayruCharacter();
            actor.Initialize(this,_agesFactory.Resources.PalacePossession,subid,position,parent);
            return new PossessedNayruRoomEntity(actor);
        });
    internal void TryCreateVeranProjectile(EnemyCharacter parent,int subid,Vector2 position,int z,int angle)
    {
        if (PartSlotAvailable) AddEntity(new VeranProjectileRoomEntity(this,_agesFactory.Resources.PalacePossession,parent,subid,position.Floor(),(z>>8)<<8,angle));
    }
    internal IRoomEntity? TryCreateVeranSpider(Vector2 position) => TryAllocateEnemy(_ =>
    {
        var data=_agesFactory.Resources.PalacePossession;
        return new VeranSpiderRoomEntity(new VeranSpiderCharacter(this,data,_roomForActiveEntities,position.Floor()),data,this);
    });
    private BipinBlossomFamilyStateResolver _familyState = null!;
    private NpcVisibilityRuleDatabase? _npcVisibilitySourceCache;
    private NpcVisibilityRuleDatabase _npcVisibility => _npcVisibilitySourceCache ??= new(AssetRoot);
    private NpcDialogueRuleDatabase? _npcDialogueSourceCache;
    private NpcDialogueRuleDatabase _npcDialogue => _npcDialogueSourceCache ??= new(AssetRoot);
    private NpcPositionRuleDatabase? _npcPositionsSourceCache;
    private NpcPositionRuleDatabase _npcPositions => _npcPositionsSourceCache ??= new(AssetRoot);
    internal BipinBlossomFamilyStateResolver FamilyStateResolver =>
        _familyState;
    internal KeyholeControllerDatabase KeyholeControllers => _agesFactory.Resources.KeyholeControllers;
    internal JabuExteriorDatabase JabuExterior => _agesFactory.Resources.JabuExterior;

    internal bool TrySpawnEnemy(int id, int subId, Vector2 position, string source, out string error)
    {
        // Native interaction state zero can create enemies during scrolling
        // (goron_targetCarts_loadCrystals). The preload pass initializes these
        // later children; the debug UI's transition restriction does not apply.
        if (!CanSpawnRoomObject(position, out error))
            return false;
        int slot = Enumerable.Range(0, 16).FirstOrDefault(
            candidate => !_reservedEnemySlots.Contains(candidate), -1);
        if (slot < 0)
        {
            error = $"Enemy ${id:x2}:${subId:x2}: all $10 enemy slots are occupied.";
            return false;
        }
        IRoomEntity? entity = _agesFactory.EnemyFactory.CreateStandaloneEnemy(
            id, subId, _roomForActiveEntities, position, source, out error);
        if (entity is null)
            return false;
        RegisterEnemySlot(entity, slot);
        AddEntity(entity);
        return true;
    }

    internal TimePortal SpawnTemporaryTimePortal(Vector2 position)
    {
        IRoomEntity entity = AddEntity(
            _agesFactory.CreateTemporaryTimePortal(_roomForActiveEntities, position));
        return (TimePortal)entity.Node;
    }

    internal void RefreshAgesNpcState(IEnumerable<IRoomEntity> entities)
    {
        if (_saveData is null)
            return;
        foreach (IRoomEntity entity in entities)
        {
            if (entity is IRoomSaveStateEntity stateEntity)
                stateEntity.RefreshSaveState();
            if (entity is IOrdinaryNpcEntity { RefreshStoryState: true } ordinary)
            {
                NpcCharacter npc = ordinary.Npc;
                if (ordinary.RefreshVisibility)
                    npc.SetFlagVisible(_npcVisibility.ShouldShow(
                        npc.BaseRecord, _saveData, _runtimeState));
                if (_familyState.TryResolveDialogue(
                    npc.BaseRecord, _saveData, out Dialogue familyDialogue))
                {
                    npc.SetDialogue(
                        familyDialogue.TextId,
                        familyDialogue.Message,
                        npc.BaseRecord.CanFace);
                }
                else if (_npcDialogue.TryResolve(
                    npc.BaseRecord, _saveData,
                    out NpcDialogueRuleDatabaseDialogue dialogue))
                {
                    npc.SetDialogue(
                        dialogue.TextId, dialogue.Message, dialogue.CanFace);
                }
                if (ordinary.RefreshPosition && _npcPositions.TryResolve(
                    npc.BaseRecord, _saveData, out Vector2 position))
                {
                    npc.SetStatePosition(position);
                }
                // Ordinary state-zero behavior is fully represented by these
                // imported visibility, dialogue and position rules. Publish
                // only after all of them have selected the source pose.
                if (npc.Record.Implementation == NpcImplementationClassification.OrdinaryGeneric)
                    npc.ShowInitializedPresentation();
                ordinary.CompleteStoryInitialization();
            }
        }
    }

    internal void BindAgesEntity(IRoomEntity entity)
    {
        if (entity is TokayNpcRoomEntity tokaySlot)
        {
            tokaySlot.CreateShieldAccessory = parent =>
            {
                var record = parent.Record;
                // tokay.s @initSubid1d returns on flag $40 or failed
                // getFreeInteractionSlot before constructing accessory $63.
                if (_saveData is null || _inventory is null ||
                    _saveData.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag40) ||
                    !InteractionSlotAvailable) return;
                var accessory = new TokayAttachedVisualRoomEntity(parent,
                    _agesFactory.Resources.TokayNative.Visual(_inventory.ShieldLevel < 2 ? "shield1" : "shield2", record.Group, record.Room),
                    new Vector2(0, -12));
                parent.Accessory = accessory;
                parent.SetScriptAnimation(_agesFactory.Resources.TokayInteractions.Animation(6));
                AddEntity(accessory);
            };
            tokaySlot.CreateHeldAccessory = parent =>
            {
                if (!InteractionSlotAvailable) return false;
                AddEntity(new TokayHeldAccessoryRoomEntity(parent) { Visible=false });
                return true;
            };
        }
    }

    private void ReplaceEnemy(IRoomEntity original, RoomEnemyReplacement replacement)
    {
        if (!_enemySlots.TryGetValue(original, out int slot))
            throw new InvalidOperationException($"{replacement.Source.Source}: enemy replacement has no original slot.");
        int index = _activeEntities.IndexOf(original);
        var next = _agesFactory.EnemyFactory.CreateEnemyReplacement(replacement, _roomForActiveEntities);
        _activeEntities.RemoveAt(index);
        FreeEntity(original);
        RegisterEnemySlot(next, slot);
        AddEntity(next);
        _activeEntities.Remove(next);
        _activeEntities.Insert(index, next);
        // The live enemy walk has consumed this physical slot's dispatch.
        // Its replacement starts state0 next update, without a death outcome.
    }

    internal bool TryCreateMermaidChangingFloorWorker(int subid)
    {
        if (!InteractionSlotAvailable) return false;
        AddEntity(_agesFactory.CreateMermaidChangingFloorWorker(subid));
        return true;
    }

    internal bool TryCreateFloorColorWorker(Vector2 position,byte tile)
    {
        if (!InteractionSlotAvailable) return false;
        AddEntity(_agesFactory.CreateFloorColorWorker(position,tile));
        return true;
    }

    internal bool TryCreateFloorToggleTile(byte position,byte takeoff)
    {
        if (!InteractionSlotAvailable) return false;
        AddEntity(_agesFactory.CreateFloorToggleTile(position,takeoff));
        return true;
    }
    internal void TryCreatePortalDecoration(DungeonObjectRecord producer)
    {
        if (InteractionSlotAvailable) AddEntity(_agesFactory.CreatePortalDecoration(producer));
    }
    internal bool TryCreateSavedMonkey(DungeonObjectRecord record)
    {
        if (!InteractionSlotAvailable) return false;
        AddEntity(_agesFactory.CreateSavedMonkey(record)); return true;
    }
    internal bool TryCreateFallingFire(Vector2 position)
    {
        if (!PartSlotAvailable) return false;
        AddEntity(new FallingFireRoomEntity(this, _agesFactory.Resources.PlacedParts, 2, position));
        return true;
    }

    internal bool TryMergeSmogClouds(int phase) => SmogCloudMerge.TryMerge(
        _enemySlots.OrderBy(pair => pair.Value).Select(pair => pair.Key.PresentationNode).OfType<SmogCharacter>(),
        phase, spawn =>
        {
            if (TryAllocateEnemy(_ => _agesFactory.Create(spawn,_roomForActiveEntities)) is null)
                _agesFactory.ApplyFailedSmogControllerAllocation(spawn, merged: true);
        });
    internal int TryCreateInteractionExplosion(Vector2 position,int zHigh=0,int var03=1)
    {
        if (!InteractionSlotAvailable) return -1;
        var entity = AddEntity(_agesFactory.Create(new InteractionExplosionSpawn(position, zHigh,
            _agesFactory.Resources.Patch.ExplosionVisual, Var03: var03), _roomForActiveEntities));
        return _interactionSlots[entity];
    }
    internal bool TryCreateFallingRock(int subid,int var03,Vector2 position,int counter=0,int angle=0)
    {
        if(!InteractionSlotAvailable)return false;
        AddEntity(new FallingRockRoomEntity(this,_agesFactory.Resources.FallingRocks,subid,var03,position,counter,angle));return true;
    }

    internal void RecreateLightableTorches(LightableTorchState state)
    {
        // Native $90:$07 clears all PART$06 slots before its checked C7
        // interaction allocation. It does not clear wNumTorchesLit.
        foreach (var entity in _activeEntities.OfType<LightableTorchRoomEntity>().ToArray())
        { _activeEntities.Remove(entity); FreeEntity(entity); }
        if (!InteractionSlotAvailable) return;
        AddEntity(_agesFactory.CreateLightableTorchScanner(state));
    }
    public void LoadCutsceneRoom(int group, OracleRoomData room, bool includeTimePortals)
    {
        Clear();
        _roomForActiveEntities = room;
        if (!includeTimePortals)
            return;
        foreach (IRoomEntity portal in _agesFactory.CreateTimePortals(group, room))
            AddEntity(portal);
    }
}
