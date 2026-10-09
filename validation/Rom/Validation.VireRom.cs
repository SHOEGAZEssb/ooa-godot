using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVireRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked); LoadValidationRoom(5,0x12);
            var slots = (Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_enemySlots",flags)!.GetValue(_entities)!;
            var active = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!;
            var parent = _entities.Entities<VireCharacter>().Single();
            FailIf(parent.Position != new Vector2(0x78,0x58) || parent.Health != 0x12 || slots.Single().Value != 0 || _entities.RoomEnemyCount != 1,
                "Room $5:$12 must admit counted ENEMY$75:$00 with source HP$12 in slot$00 at Y$58/X$78.");
            // Bound the native enemy walk to the actual admitted miniboss and
            // its children/effects, keeping the room's collision geometry.
            foreach (var entity in active.Where(entity => entity.Node != parent && entity is not DungeonRewardRoomEntity).ToArray())
            { active.Remove(entity); typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!.Invoke(_entities,[entity]); }
            _player.WarpTo(new(0x78,0x88));
            FailIf(_collision.Collides(_player.Position),"Vire approach must start on the source room's reachable south floor.");
            typeof(Player).GetField("_enemyInvincibilityFrames",flags)!.SetValue(_player,100000f);
            var rom = new EnemyStatusRom(_currentRoom,_saveData,_random.Calls);
            rom[0xcc08] = 0x3c; rom[0xcc0a] = 0x8e;
            rom[0xd080] = 1; rom[0xd081] = 0x75; rom[0xd08b] = 0x58; rom[0xd08d] = 0x78;
            rom[0xd004] = 1; rom[0xd024] = 0x80; rom[0xd029] = 1; // live Link for native fairy collection
            rom[0xd026] = rom[0xd027] = 6;
            rom[0xcc39] = 6;
            rom[0xcc01] = (byte)(linked ? 1 : 0); // file load copies wFileIsLinkedGame to wIsLinkedGame
            rom[0xd240] = 1; rom[0xd241] = 0x20; rom[0xd24b] = 0x58; rom[0xd24d] = 0x78;
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            int update = 0;
            SomariaRom? itemRom = null;
            int itemPressed = 0;
            bool probingHiddenContact = false;
            SwordRom? swordRom = null;
            bool swordHeld = false, swordPressed = false;
            bool checkingHighSwing = false;
            int highSwingOverlaps = 0;
            int SwordDirection() => _player.FacingVector == Vector2I.Up ? 0 :
                _player.FacingVector == Vector2I.Right ? 1 : _player.FacingVector == Vector2I.Down ? 2 : 3;
            var states = new HashSet<int>();
            void Compare()
            {
                // Text presentation has its own ROM regressions. This fixture
                // executes the native showText publisher, freezes while open,
                // and closes that modal through the application's owner.
                if (itemRom is not null)
                {
                    itemRom[0xcba0] = rom[0xcba0];
                    itemRom.Update(itemPressed,itemPressed,_entities.FrameCounter); itemPressed = 0;
                    rom[0xcc5f] = itemRom[0xcc5f];
                    FailIf(_player.NativeItemUseActive != (itemRom[0xcc5f] != 0),
                        "Vire's item-use input must follow the executed native animation parent through completion.");
                }
                rom[0xffaa] = (byte)(-(int)_entities.ToScreen(Vector2.Zero).Y);
                rom[0xffac] = (byte)(-(int)_entities.ToScreen(Vector2.Zero).X);
                if (swordRom is not null)
                {
                    swordRom[0xff94] = rom[0xff94]; swordRom[0xff95] = rom[0xff95];
                    swordRom[0xcba0] = rom[0xcba0];
                    swordRom[0xcc00] = (byte)_entities.FrameCounter;
                    swordRom[0xd00b] = (byte)_player.Position.Y; swordRom[0xd00d] = (byte)_player.Position.X;
                    swordRom[0xd008] = (byte)SwordDirection();
                    swordRom.Update(swordHeld,swordPressed); swordPressed = false;
                    rom[0xff94] = swordRom[0xff94]; rom[0xff95] = swordRom[0xff95];
                    for (int address = 0xd600; address < 0xd640; address++) rom[address] = swordRom[address];
                }
                rom.Update(_entities.FrameCounter,_player.Position);
                if (swordRom is not null)
                {
                    // Link's body is protected in this combat fixture; the
                    // native scan still uses the actual sword and bat heights.
                    rom[0xd02b] = 1;
                    rom.ResolveLinkCollisions();
                    rom[0xd02b] = 0;
                    for (int address = 0xd600; address < 0xd640; address++) swordRom[address] = rom[address];
                    CompareSwordRom(_player,swordRom,$"Vire actual sword update{update} batch={batched}");
                    FailIf(_player.MeleeItemZ != unchecked((sbyte)swordRom[0xd60f]) && _player.IsAttacking,
                        "Vire combat must use the native sword height, including its Link.zh-$02 offset.");
                    if (checkingHighSwing)
                    {
                        foreach (var actor in _entities.Entities<VireCharacter>().Where(actor => !actor.MainForm))
                        {
                            FailIf(actor.State != 10 || (actor.ZFixed >> 8) > -15 || actor.Health != 1 || actor.NativeHitPending,
                                "Circling bats at source Zh=$ed..$f1 must survive a ground-level sword swing.");
                            if (_player.IsAttacking && RoomEntityManager.ObjectCollisionXYOverlaps(_player.GetSwordHitbox(),actor.CollisionBounds))
                                highSwingOverlaps++;
                        }
                    }
                }
                if (probingHiddenContact)
                {
                    rom.ResolveLinkCollisions();
                    FailIf(rom[0xd025] != 0 || rom[0xd02a] != 0 || rom[0xd02b] != 0 || rom[0xd02d] != 0 ||
                        _player.PendingContactDamageRaw != 0 || _player.NativeContactSignal ||
                        _player.InvincibilityFrames != 0 || _player.KnockbackFrames != 0,
                        "Vire's hidden main form after one bat death must publish no Link damage, contact, invincibility or recoil in the actual gameplay loop or clean US ROM.");
                }
                foreach (var pair in slots.Where(pair => pair.Key.Node is VireCharacter))
                {
                    var actor = (VireCharacter)pair.Key.Node;
                    int p = 0xd080 + pair.Value * 256;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter",flags)!.GetValue(actor.Animation)!;
                    var xy = new Vector2(rom.Word(p + 12)/256f,rom.Word(p + 10)/256f);
                    FailIf(rom[p] == 0 || actor.State != rom[p + 4] || actor.Substate != rom[p + 5] ||
                        actor.Counter1 != rom[p + 6] || actor.Counter2 != rom[p + 7] || actor.Direction != rom[p + 8] ||
                        actor.Angle != rom[p + 9] || actor.Position != xy || (actor.ZFixed & 0xffff) != rom.Word(p + 14) ||
                        actor.Speed != rom[p + 16] || actor.Health != rom[p + 41] ||
                        actor.CollisionEnabled != ((rom[p + 36] & 0x80) != 0) ||
                        actor.NativeHitPending != ((rom[p + 42] & 0x80) != 0) ||
                        (actor.InvincibilityCounter & 255) != rom[p + 43] || actor.KnockbackCounter != rom[p + 45] ||
                        actor.OrbitOffset != rom[p + 48] || actor.FireAnimationCounter != rom[p + 50] ||
                        actor.PreviousHealth != rom[p + 51] || actor.ChildrenAlive != rom[p + 52] || actor.HealthText != rom[p + 55] || actor.FairySpawned != rom[p+56] ||
                        actor.Visible != ((rom[p + 26] & 0x80) != 0) || actor.AnimationParameter != rom[p + 33] || timer != rom[p + 32],
                        $"Vire slot${pair.Value:x2} update{update} batch={batched}: runtime state/sub=${actor.State:x2}/${actor.Substate:x2}, c={actor.Counter1}/{actor.Counter2}, XY={actor.Position}, Z={actor.ZFixed}, speed/angle={actor.Speed}/{actor.Angle}, HP={actor.Health}, anim={timer}/{actor.AnimationParameter}, visible={actor.Visible}, collision={actor.CollisionEnabled}; " +
                        $"ROM=${rom[p+4]:x2}/${rom[p+5]:x2}, c={rom[p+6]}/{rom[p+7]}, XY={xy}, Z={rom.Word(p+14)}, speed/angle={rom[p+16]}/{rom[p+9]}, HP={rom[p+41]}, anim={rom[p+32]}/{rom[p+33]}, visible=${rom[p+26]:x2}, collision=${rom[p+36]:x2}, fairy={actor.FairySpawned}/{rom[p+56]}.");
                    if (actor.MainForm) states.Add(actor.State);
                }
                var rng = _random.CaptureState();
                int nativeRandomCalls = rom.RandomCalls + (swordRom?.RandomCalls ?? 0);
                var parts = (Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_partSlots",flags)!.GetValue(_entities)!;
                foreach (var pair in parts.Where(pair => pair.Key.Node is ItemDropEffect))
                {
                    var drop = (ItemDropEffect)pair.Key.Node;
                    int p = 0xd0c0+pair.Value*256;
                    FailIf((int)drop.State != rom[p+4] || drop.ZFixed != unchecked((short)rom.Word(p+14)) ||
                        drop.PrecisePosition != new Vector2(rom.Word(p+12)/256f,rom.Word(p+10)/256f) || drop.FairyMovementCounter != rom[p+7],
                        $"Vire fairy update{update}: runtime={drop.State}/{drop.PrecisePosition}/{drop.ZFixed}/c{drop.FairyMovementCounter}, native={rom[p+4]}/{rom.Word(p+12)/256f},{rom.Word(p+10)/256f}/{unchecked((short)rom.Word(p+14))}/c{rom[p+7]}.");
                }
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != nativeRandomCalls,
                    $"Vire update{update}: native RNG consumption differs: runtime=${rng.Rng2:x2}{rng.Rng1:x2}/{rng.Calls-seed.Calls}, ROM=${rom[0xff95]:x2}{rom[0xff94]:x2}/{nativeRandomCalls}; parent=${parent.State:x2}/${parent.Substate:x2} fairy={parent.FairySpawned}, dead={parent.IsDead}; nativeParts=" +
                    string.Join(",",Enumerable.Range(0,16).Where(slot => rom[0xd0c0+slot*256]!=0).Select(slot => $"{slot:x2}:{rom[0xd0c1+slot*256]:x2}/{rom[0xd0c2+slot*256]:x2}/{rom[0xd0c4+slot*256]:x2}")));
                update++;
                FailIf(_saveData.HasRoomFlag(5,0x12,OracleSaveData.RoomFlag80) != ((rom[0xca12]&0x80)!=0),
                    "Miniboss room flag$80 must be published on the same script update as the ROM.");
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,swordHeld ? ["attack"] : [],batched:batched,afterUpdate:Compare);
            Step(3);
            FailIf(parent.State != 8 || parent.Substate != 0,"Vire must wait outside the source approach rectangle.");
            StepGameplayUpdates(24,Vector2.Up,["move_up"],["move_up"],batched,Compare);
            FailIf(parent.Substate == 0,"Vire's intro must be reachable by walking north through the source room.");
            int guard = 0;
            while (!_dialogue.IsOpen && guard++ < 100) Step();
            FailIf(!_dialogue.IsOpen || rom[0xcba0] == 0 || rom.Word(0xcba2) != (linked ? 0x3313 : 0x3312),
                $"Vire intro must publish TX_2f12/2f13 through native showText according to linked-game state: linked={linked}/{_saveData.IsLinkedGame}, ROM=${rom.Word(0xcba2):x4}, file=${rom[0xc612]:x2}.");
            Step(4); _dialogue.Close(); rom[0xcba0] = 0; Step();
            FailIf(parent.State != 9 || parent.Counter1 != 90 || parent.PreviousHealth != 18,
                "Vire must resume from text, disappear for 90 updates and remember HP$12.");
            Step(700);
            void Await(Func<bool> condition,int limit,string boundary)
            {
                int remaining = limit;
                while (!condition() && remaining-- > 0) Step();
                FailIf(!condition(),$"Vire did not reach {boundary} within {limit} updates: parent=${parent.State:x2}/${parent.Substate:x2}, children={parent.ChildrenAlive}, counter2={parent.Counter2}, sword={_player.SwordState}; bats=" +
                    string.Join(";",_entities.Entities<VireCharacter>().Where(actor => !actor.MainForm).Select(actor => $"state${actor.State:x2} pos={actor.Position} Z={actor.ZFixed >> 8} HP={actor.Health}")));
            }
            void Sword(VireCharacter actor,int damage)
            {
                var pair = slots.Single(pair => pair.Key.Node == actor);
                rom.HitWithSword(ItemCollisionType.L1Sword,damage,pair.Value);
                ((ILinkSwordStateAwareRoomEntity)pair.Key).SetLinkSwordState(SwordActionState.Swing,1);
                FailIf(!((ISwordHittableRoomEntity)pair.Key).ApplySwordHit(actor.CollisionBounds,
                    actor.Position.Floor()-Vector2.Right,damage,EnemyKnockbackStrength.Low,new List<RoomEntitySpawn>()),
                    "Vire must accept the declared native sword contact.");
            }
            Await(() => states.Contains(10) && states.Contains(11),1400,"healthy charge and circle attacks");
            void CloseText(int id)
            {
                Await(() => _dialogue.IsOpen,150,$"TX_{id:x4}");
                FailIf(rom.Word(0xcba2) != id + 0x400,$"Vire must publish TX_{id:x4} at the same native boundary.");
                Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step();
            }
            foreach (var phase in new[] { (Damage:3,Health:15,Text:0x2f14), (Damage:6,Health:9,Text:0x2f15) })
            {
                Await(() => parent.CollisionEnabled && parent.InvincibilityCounter == 0,700,"vulnerable main form");
                Sword(parent,phase.Damage); CloseText(phase.Text);
                FailIf(parent.Health != phase.Health,"Vire health thresholds must use native HP bytes.");
                Step(700);
            }
            Await(() => states.Contains(12) && states.Contains(13),2400,"low-health creep and evasive circle attacks");
            Await(() => parent.State == 13 && parent.Substate == 2,1400,"evasive circle's charge");
            _inventory.GiveTreasure(TreasureId.Shovel,0); _inventory.EquipA(TreasureId.Shovel);
            itemRom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)_player.Position.X,(int)_player.Position.Y);
            itemRom.InitializeLinkGameplay(); itemPressed = 1;
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);
            FailIf(parent.Substate != 3 || parent.Counter1 != 12 || !_player.NativeItemUseActive,
                "Vire state$0d must turn away and start counter12 on the actual item parent's first signal.");
            Step(12);
            FailIf(parent.Substate != 4 || parent.Counter1 != 12,"Vire's evasive projectile must fire on the first counter12 boundary.");
            Step(12);
            FailIf(parent.Substate != 5 || parent.Speed != 70,"Vire must begin its SPEED_1c0 escape on the second counter12 boundary.");
            Await(() => parent.CollisionEnabled && parent.InvincibilityCounter == 0,700,"final vulnerable main form");
            Sword(parent,9); CloseText(0x2f16);
            FailIf(parent.State != 15 || parent.Substate != 2 || parent.Health != 1 || parent.ChildrenAlive != 2 ||
                _entities.Entities<VireCharacter>().Count != 3 || _entities.RoomEnemyCount != 1,
                "Vire must retain one counted main form and allocate two uncounted bat children in native slot order.");
            Step(240);
            itemRom = null; // The completed Shovel parent no longer owns inputs.
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword);
            var swordSeed = _random.CaptureState();
            swordRom = new SwordRom(SwordDirection(),1,seed:swordSeed.Rng1 | swordSeed.Rng2 << 8);
            swordRom[0xcc33] = (byte)_currentRoom.ActiveCollisions;
            for (int y = 0; y < _currentRoom.HeightInTiles; y++)
            for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                swordRom[0xcf00 + y * 16 + x] = _currentRoom.GetMetatile(new(x * 16 + 8,y * 16 + 8));
            void FaceBat(VireCharacter actor)
            {
                Vector2 delta = actor.Position - _player.Position;
                _player.Face(Mathf.Abs(delta.X) > Mathf.Abs(delta.Y)
                    ? new(Mathf.Sign(delta.X),0) : new(0,Mathf.Sign(delta.Y)));
            }
            Await(() => parent.Counter2 > 40 && _entities.Entities<VireCharacter>().Where(actor => !actor.MainForm).All(actor => actor.State == 10) &&
                _entities.Entities<VireCharacter>().Any(actor => !actor.MainForm && actor.Position.DistanceTo(_player.Position) < 32),800,"circling bat within the sword's planar reach");
            FaceBat(_entities.Entities<VireCharacter>().First(actor => !actor.MainForm && actor.Position.DistanceTo(_player.Position) < 32));
            checkingHighSwing = true;
            swordHeld = swordPressed = true;
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);
            swordHeld = false; Step(17);
            checkingHighSwing = false;
            FailIf(highSwingOverlaps == 0 || parent.ChildrenAlive != 2,
                "High bat swing must overlap XY and miss solely on native height, leaving both children alive.");
            for (int killed = 0; killed < 2; killed++)
            {
                if (killed == 1)
                {
                    Await(() => !_player.IsAttacking && parent.Counter2 > 80 &&
                        _entities.Entities<VireCharacter>().Single(actor => !actor.MainForm).State == 10,800,"safe high flight for sword charging");
                    swordHeld = swordPressed = true;
                    StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);
                    Step(58);
                    FailIf(_player.SwordState != SwordActionState.Charged || parent.ChildrenAlive != 1,
                        "Sword must charge through the actual held-button parent while the surviving bat remains overhead.");
                }
                Await(() => (killed == 0 ? !_player.IsAttacking : _player.SwordState == SwordActionState.Charged) &&
                    _entities.Entities<VireCharacter>().Any(actor => !actor.MainForm && actor.CollisionEnabled &&
                    actor.InvincibilityCounter == 0 && (actor.ZFixed >> 8) >= -9 && actor.Position.DistanceTo(_player.Position) < 22 &&
                    _entities.Entities<VireCharacter>().All(other => other.MainForm || other == actor ||
                        (other.ZFixed >> 8) < -9 || other.Position.DistanceTo(_player.Position) > 32)),1000,"one bat's reachable low charge");
                var aimedBat = _entities.Entities<VireCharacter>().First(actor => !actor.MainForm && actor.CollisionEnabled &&
                    actor.InvincibilityCounter == 0 && (actor.ZFixed >> 8) >= -9 && actor.Position.DistanceTo(_player.Position) < 22);
                var liveBats = _entities.Entities<VireCharacter>().Where(actor => !actor.MainForm).ToArray();
                FaceBat(aimedBat);
                if (killed == 0)
                {
                    swordHeld = swordPressed = true;
                    StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);
                }
                else
                {
                    swordHeld = false; Step();
                    FailIf(_player.SwordState != SwordActionState.Spin,"Releasing a charged sword must start the native spin attack.");
                }
                swordHeld = false;
                Await(() => liveBats.Any(actor => actor.Health == 0),16,killed == 0 ? "bat killed by the actual L1 sword swing" : "bat killed by the actual charged spin");
                var bat = liveBats.Single(actor => actor.Health == 0);
                int batSlot = slots.Single(pair => pair.Key.Node == bat).Value;
                FailIf(bat.CollisionEnabled || !bat.NativeHitPending || bat.IsDead ||
                    (rom[0xd0a4 + batSlot * 256] & 0x80) != 0,
                    "Bat lethal damage must clear collision immediately while preserving JUST_HIT before deletion.");
                if (killed == 0)
                {
                    // Cancel after the first accepted contact so the rest of
                    // this swing cannot also strike the surviving child.
                    _player.WarpTo(_player.Position,recordSafe:false);
                    typeof(Player).GetField("_enemyInvincibilityFrames",flags)!.SetValue(_player,100000f);
                    swordRom.Call(6,0x4878); swordRom.Call(7,0x491a);
                }
                Step();
                FailIf(bat.IsDead || bat.CollisionEnabled || parent.ChildrenAlive != 2-killed,
                    "Bat JUST_HIT must yield one update without collision or an early parent decrement.");
                Step();
                FailIf(parent.ChildrenAlive != 1-killed,"Each uncounted bat death must publish exactly one decrement to the live parent.");
                FailIf(!bat.IsDead || bat.CollisionEnabled || slots.Keys.Any(entity => entity.Node == bat) ||
                    rom[0xd080 + batSlot * 256] != 0 || parent.CollisionEnabled,
                    "Bat NO_HEALTH must delete its native slot, and Vire's HP$01 main form must retain the lethal collision clear.");
                if (killed == 0)
                {
                    // Exercise the hidden parent's stale hitbox with vulnerable
                    // Link on real floor, while the living bat is safely away.
                    Await(() => _entities.Entities<VireCharacter>().Single(actor => !actor.MainForm)
                        .Position.DistanceTo(parent.Position) > 32,500,"surviving bat away from hidden parent");
                    Vector2 previousPosition = _player.Position;
                    Vector2 contactPosition = parent.Position.Floor();
                    FailIf(_collision.Collides(contactPosition),"Hidden Vire contact probe must occupy the source room's real floor.");
                    _player.WarpTo(contactPosition); _player.ClearInteractionKnockback(clearInvincibility:true);
                    FailIf(!_player.NativeObjectVulnerable || !_player.OverlapsEnemyCollision(parent.CollisionBounds,parent.ZFixed >> 8),
                        "Hidden Vire contact probe must overlap its bounds with vulnerable Link at the native collision height.");
                    int health = _player.HealthQuarters;
                    probingHiddenContact = true;
                    Step(4);
                    FailIf(_player.HealthQuarters != health,"Hidden Vire must not hurt Link after the first bat death.");
                    probingHiddenContact = false;
                    _player.WarpTo(previousPosition);
                    typeof(Player).GetField("_enemyInvincibilityFrames",flags)!.SetValue(_player,100000f);
                }
            }
            CloseText(linked ? 0x2f18 : 0x2f17);
            Await(() => parent.IsDead,200,linked ? "linked boss teardown" : "unlinked departure");
            if (linked)
            {
                FailIf(_entities.RoomEnemyCount != 1 || !_entities.Entities<BossDeathExplosionEffect>().Any(),
                    "Linked Vire must transfer its counted room ownership into the boss explosion.");
                Await(() => _entities.RoomEnemyCount == 0,100,"linked explosion completion");
            }
            FailIf(_entities.Entities<VireCharacter>().Count != 0 || _entities.RoomEnemyCount != 0 || rom[0xcdd1] != 0,
                "Unlinked Vire must leave with its one-shot fairy, silently decrement room count, and delete both forms.");
            FailIf(_saveData.HasRoomFlag(5,0x12,OracleSaveData.RoomFlag80),"Zero-enemy check must yield before flag$80 publication.");
            Step();
            FailIf(!_saveData.HasRoomFlag(5,0x12,OracleSaveData.RoomFlag80),"The following reward update must publish flag$80.");
            Step(); Step(19);
            FailIf(_entities.Entities<MinibossPortal>().Any(),"Vire portal must wait the complete 20 updates after wait initialization.");
            Step();
            FailIf(!_entities.Entities<MinibossPortal>().Any(portal => portal.Visible),
                "Vire's reward must allocate and initialize its higher-slot portal at wait20 in group$5.");
            Step();
            FailIf(_entities.LinkCollisionsAndMenuDisabled,"Reward must unlock Link on the script update after portal creation.");
            _dialogue.Close(); _entities.LoadRoom(5,_currentRoom);
            FailIf(_entities.Entities<VireCharacter>().Count != 0,"Room $5:$12 flag$80 must suppress Vire on repeat entry.");
        }
        // Bound content verification to the long introduction and linked
        // farewell: native paging, positioning, colors and slow controls.
        var dialogue = new VireDialogueDatabase();
        RunDialogueRom(0x2f12,dialogue.Text(0x2f12),0,false,false);
        RunDialogueRom(0x2f18,dialogue.Text(0x2f18),0,true,false);
        GD.Print("Validated clean-US Vire $75 in Mermaid room $5:$12: real floor approach, intro/text freeze/resume, HP thresholds and main attacks, native item-parent avoidance, fixed movement/counters/animation/RNG/collision enablement, bat transformation/chase/charge/death boundaries, actual sword-button high-flight height rejection with XY overlap, low-flight swing/spin kills and cancellation/reuse, harmless hidden-parent contact after one bat death, unlinked departure, linked explosion/count transfer, flag$80/portal reward, repeat entry and two major dialogue variants through split/batched gameplay updates.");
    }
}
