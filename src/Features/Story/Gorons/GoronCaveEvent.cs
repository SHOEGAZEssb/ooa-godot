using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// Each host retains one original interaction slot. The shared bytes live in
// OracleRuntimeState; room cancellation must not clear the WRAM union.
internal sealed class GoronCaveEvent(RoomEventContext context) : IRoomEvent, IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent
{
    internal RoomEventContext Context => context;
    internal GoronCaveDatabase Database { get; } = new();
    internal System.Func<int,System.Action<bool>,bool>? OpenSecretMenu { get; set; }
    private readonly List<GoronCaveScriptHost> _actors = new();
    private readonly List<GoronRock> _rocks = new();
    private readonly List<GoronExplosion> _explosions = new();
    private NpcCharacter? _bombFlower, _exclamation, _rockSpawner;
    private int _exclamationSlot, _spawnerSlot;
    private bool _exclamationFresh;
    private int _exclamationCounter, _fallingCounter;
    private bool _falling;
    private int _fadeDirection, _fadeCounter, _fadeDelay, _fadeOffset;
    private RoomEventResources? _resources;
    internal bool PaletteBusy => _fadeDirection != 0;
    internal bool MovingLink { get; private set; }
    internal IReadOnlyList<GoronCaveScriptHost> Actors => _actors;
    internal int DanceJumpZ => _actors.FirstOrDefault(a=>a.Actor.Record is {Id:0x66,SubId:2}&&a.Actor.Active)?.JumpZ??0;
    public bool HasState => _actors.Count != 0;
    public bool BlocksGameplay => _actors.Any(actor => actor.InputLocked);
    public bool MenusDisabled => BlocksGameplay||_actors.Any(actor=>actor.MenusDisabled);
    public bool ScreenTransitionsDisabled => BlocksGameplay;
    public bool Matches(int group, OracleRoomData room) => context.Entities.Entities<NpcCharacter>().Any(n =>
        n.Record.Implementation == NpcImplementationClassification.EventOwned && (n.Record.Id is 0x66 or 0x8b||n.Record is {Id:0x30,SubId:1}));
    public void Start(OracleRoomData room)
    {
        _resources = new(context, this);
        foreach (var actor in context.Entities.Entities<NpcCharacter>().Where(n =>
            n.Record.Implementation == NpcImplementationClassification.EventOwned && (n.Record.Id is 0x66 or 0x8b||n.Record is {Id:0x30,SubId:1})).ToArray())
        {
            var host = new GoronCaveScriptHost(this, actor);
            _actors.Add(host);
        }
    }
    public void UpdateFrame()
    {
        UpdatePresentation();
        UpdateLink();
        UpdateSlots(false);
    }
    internal void InitializeDuringTransition()
    {
        // State-zero interactions remain eligible while ordinary updates are
        // frozen. Visit native slots so later-slot children initialize once in
        // this pass too; no initialized script or animation advances here.
        for(int slot=2;slot<16;slot++)
            _actors.FirstOrDefault(actor=>actor.Slot==slot&&actor.Actor.Active)?.Initialize();
    }
    public void UpdateDuringDialogueFrame() => UpdateSlots(true);
    private void UpdateSlots(bool dialogue)
    {
        // getFreeInteractionSlot reuses the first vacant slot. Children in
        // earlier slots wait for the next pass; later slots run this pass.
        for (int slot=2;slot<16;slot++)
        {
            if (!dialogue)
            {
                var host=_actors.FirstOrDefault(a=>a.Slot==slot&&a.Actor.Active);
                host?.Update();
                if (_exclamation is not null && _exclamationSlot==slot)
                    RoomEventResources.UpdateExclamation(ref _exclamation,ref _exclamationFresh,ref _exclamationCounter);
                if (_falling && _spawnerSlot==slot) UpdateRockSpawner();
                var explosion=_explosions.FirstOrDefault(e=>e.Slot==slot&&e.Actor.Active);
                explosion?.Update(context.Sound);
            }
            var rock=_rocks.FirstOrDefault(r=>r.Slot==slot&&r.Actor.Active);
            if (rock is null || dialogue&&rock.Falling) continue;
            if (!rock.Falling && context.Entities.RuntimeState.ReadWramByte(0xcfde)!=0)
            { rock.Actor.SetActive(false); continue; }
            if (rock.Update(context.Entities.NextRandomValue, Database.Bytes("falling-positions")))
            {
                if (rock.Falling)
                { CreateDebris(rock.Position,false); context.Entities.BeginScreenShake(4); }
                rock.Actor.SetActive(false);
            }
        }
        _rocks.RemoveAll(rock=>!rock.Actor.Active);
        _explosions.RemoveAll(effect=>!effect.Actor.Active);
    }
    public bool TryInteractNpc(NpcCharacter npc) =>
        _actors.Any(actor => actor.TryInteract(npc));
    public void Cancel()
    {
        foreach (var actor in _actors) actor.Cancel();
        _actors.Clear();
        foreach (var rock in _rocks) if (GodotObject.IsInstanceValid(rock.Actor)) rock.Actor.SetActive(false);
        _rocks.Clear();
        foreach (var effect in _explosions) if (GodotObject.IsInstanceValid(effect.Actor)) effect.Actor.SetActive(false);
        _explosions.Clear();
        if (_rockSpawner is not null && GodotObject.IsInstanceValid(_rockSpawner)) _rockSpawner.SetActive(false);
        _rockSpawner=null;
        if (_bombFlower is not null && GodotObject.IsInstanceValid(_bombFlower)) _bombFlower.SetActive(false);
        _bombFlower = null;
        RoomEventResources.RetireExclamation(ref _exclamation, ref _exclamationFresh, ref _exclamationCounter);
        _resources?.ReleaseFullScreenFade();
        _fadeDirection=0; MovingLink=false; _falling=false;
    }
    internal void SpawnElder()
    {
        if (!context.Entities.InteractionSlotAvailable) return;
        NpcCharacter actor = context.Entities.Spawn<NpcCharacter>(new GoronCaveNpcSpawn(
            Database.Actor(0x8b, 0, 0x38, 0x50)));
        var host = new GoronCaveScriptHost(this, actor);
        _actors.Add(host);
    }
    internal void SpawnTunnelGoron()
    {
        if(!context.Entities.InteractionSlotAvailable) return;
        _resources=new(context,this);
        var record=Database.Actor(0x66,3,0xa8,0x58) with {Group=context.Rooms.ActiveGroup,Room=context.Rooms.CurrentRoom.Id};
        var actor=context.Entities.Spawn<NpcCharacter>(new GoronCaveNpcSpawn(record));
        var host=new GoronCaveScriptHost(this,actor); _actors.Add(host); host.Initialize();
    }
    internal void SpawnDancers(bool subrosians)
    {
        int[] objects=Database.Bytes(subrosians?"dancers-subrosian":"dancers-goron");
        for(int i=0;i<7;i++)
        {
            if(!context.Entities.InteractionSlotAvailable) return;
            var record=Database.Actor(objects[i*5],objects[i*5+1],objects[i*5+3],objects[i*5+2]) with
            {Group=context.Rooms.ActiveGroup,Room=context.Rooms.CurrentRoom.Id,Var03=objects[i*5+4]};
            var actor=context.Entities.Spawn<NpcCharacter>(new GoronCaveNpcSpawn(record));
            _actors.Add(new(this,actor));
        }
    }
    internal bool SpawnDanceJump()
    {
        if(!context.Entities.InteractionSlotAvailable) return false;
        var record=Database.Actor(0x66,2,(int)context.Player.Position.X,(int)context.Player.Position.Y);
        var actor=context.Entities.Spawn<NpcCharacter>(new GoronCaveNpcSpawn(record,false));
        _actors.Add(new(this,actor)); return true;
    }
    internal void BeginMoveLink() => MovingLink = true;
    private void UpdateLink()
    {
        if (!MovingLink) return;
        // linkCutscene5, path $01: X=$38, then Y=$60 at SPEED_100.
        Vector2 pos = context.Player.Position;
        Vector2I direction = (int)pos.X != 0x38 ?
            ((int)pos.X > 0x38 ? Vector2I.Left : Vector2I.Right) :
            ((int)pos.Y > 0x60 ? Vector2I.Up : Vector2I.Down);
        if ((int)pos.X == 0x38 && (int)pos.Y == 0x60)
        { MovingLink=false; context.Player.Face(Vector2I.Up); return; }
        context.Player.AdvanceCutsceneMovement(direction, direction);
    }
    internal void BeginFade(bool toWhite, int delay)
    {
        _resources!.CaptureFullScreenFade();
        _fadeDirection=toWhite ? 1 : -1; _fadeOffset=toWhite ? 0 : 32;
        _fadeDelay=delay; _fadeCounter=1;
    }
    private void UpdatePresentation()
    {
        if (_fadeDirection != 0 && --_fadeCounter == 0)
        {
            _fadeCounter=_fadeDelay; _fadeOffset+=_fadeDirection;
            if (_fadeOffset is >=32 or <0) _fadeDirection=0;
            context.Fade.Color=new Color(1,1,1,System.Math.Clamp(_fadeOffset,0,31)/31f);
        }
    }
    internal void ClearBarrier()
    {
        int[] tiles=Database.Bytes("barrier");
        for (int y=3;y<=5;y++) for (int x=1;x<=5;x++)
            context.Rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(x*16+8,y*16+8),
                (byte)tiles[(y-3)*5+x-1], null, context.AnimationTick());
    }
    internal void CreateExclamation(Vector2 position, int frames)
    {
        if (!context.Entities.InteractionSlotAvailable) return;
        _exclamation=SpawnEffect(Database.Effect(0x9f,0), position+new Vector2(0,-13));
        _exclamationSlot=context.Entities.InteractionSlot(_exclamation);
        _exclamationFresh=true; _exclamationCounter=frames;
    }
    internal void CreateBombFlower()
    {
        if (!context.Entities.InteractionSlotAvailable) return;
        var item=context.Treasures.GetObject("TREASURE_OBJECT_BOMB_FLOWER_01");
        var visual=context.Treasures.GetObjectVisual(item.Graphic);
        var record=new NpcRecord(5,0xc3,0x60,0x49,0x60,0x38,1,0,visual.Sprite,visual.TileBase,
            visual.Palette,visual.DefaultAnimation,false,visual.Animation,visual.Animation,
            visual.Animation,visual.Animation,"",NpcImplementationClassification.EventOwned);
        _bombFlower=SpawnEffect(record,new Vector2(0x38,0x60));
    }
    internal void DeleteBombFlower() { _bombFlower?.SetActive(false); _bombFlower=null; }
    internal void DisplayPrize(string name,Vector2 position,int z)
    {
        if(!context.Entities.InteractionSlotAvailable) return;
        var item=context.Treasures.GetObject(name);
        var visual=context.Treasures.GetObjectVisual(item.Graphic);
        var record=new NpcRecord(context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,0x60,0,0,0,0,0,
            visual.Sprite,visual.TileBase,visual.Palette,visual.DefaultAnimation,false,
            visual.Animation,visual.Animation,visual.Animation,visual.Animation,"",NpcImplementationClassification.EventOwned);
        _bombFlower=SpawnEffect(record,position); _bombFlower.SetScriptDrawOffset(new(0,z));
    }
    internal void CreateExplosion(int index)
    {
        if (!context.Entities.InteractionSlotAvailable) return;
        int[] positions=Database.Bytes("explosion-positions");
        var visual=Database.Effect(0x56,0);
        var actor=SpawnEffect(visual,new Vector2(positions[index*2+1],positions[index*2]));
        actor.SetFixedDrawPriority(NpcCharacter.InFrontOfLinkZIndex);
        _explosions.Add(new(actor,context.Entities.InteractionSlot(actor)));
    }
    private NpcCharacter SpawnEffect(NpcRecord record, Vector2 position)
    {
        var actor=context.Entities.Spawn<NpcCharacter>(new GoronCaveNpcSpawn(
            record with { X=(int)position.X,Y=(int)position.Y },false));
        actor.SetAnimationRate(0); return actor;
    }
    internal void CreateDebris(Vector2 position, bool punching)
    {
        if (!punching) context.Sound.PlaySound(0xa5);
        int pattern=context.Entities.NextRandomValue() & (punching ? 1 : 3);
        int[] angles=Database.Bytes("angles");
        for (int i=0;i<4;i++)
        {
            if (!context.Entities.InteractionSlotAvailable) break;
            var actor=SpawnEffect(Database.Effect(0x92,2),position);
            _rocks.Add(new(actor,position,angles[(pattern+(punching?4:0))*4+i],
                punching?0x28:0x3c,punching?-228:-232,0,false,context.Entities.InteractionSlot(actor)));
        }
        if (punching) context.Sound.PlaySound(0xa5);
    }
    internal void StartFallingRocks()
    {
        if (!context.Entities.InteractionSlotAvailable) return;
        _rockSpawner=SpawnEffect(Database.Effect(0x92,1),Vector2.Zero);
        _rockSpawner.SetScriptVisible(false);
        _spawnerSlot=context.Entities.InteractionSlot(_rockSpawner);
        _falling=true; _fallingCounter=1;
    }
    private void UpdateRockSpawner()
    {
        if (_falling)
        {
            if (context.Entities.RuntimeState.ReadWramByte(0xcfdf)!=0)
            { _falling=false; _rockSpawner!.SetActive(false); }
            else if (--_fallingCounter==0)
            {
                _fallingCounter=20;
                if (!context.Entities.InteractionSlotAvailable) return;
                Vector2 pos=Vector2.Zero;
                var actor=SpawnEffect(Database.Effect(0x92,1),pos);
                _rocks.Add(new(actor,pos,0,0,0,
                    0,true,context.Entities.InteractionSlot(actor)));
            }
        }
    }
}
