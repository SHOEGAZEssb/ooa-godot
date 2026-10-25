using Godot;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVeranFinalShieldRom()
    {
        foreach(var spec in new (int Form,int Mode,int Level)[]{(0,0x6a,1),(0,9,2),(1,0x6c,1),(2,0x6b,3)})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);
            var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();var actor=boss.Actor;
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(entity=>entity!=boss).ToArray())_entities.RetireEntity(other);
            _inventory.GiveTreasure(TreasureId.Shield,spec.Level);_inventory.EquipA(TreasureId.Shield);_inventory.EquipB(0);
            _player.WarpTo(new(120,100));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position),"Final-boss shield contact must be reachable on the source arena floor.");
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,100)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd7;rom[0xcc0a]=0xd8;rom[0xcc0c]=0xd9;rom[0xcc0e]=0xda;
            rom[0xd080]=1;rom[0xd081]=2;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            int update=0,pressed=1;var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom.UpdateGameplay(pressed,1,255,++update);pressed=0;
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    _player.InvincibilityFrames!=unchecked((sbyte)rom[0xd02b])||_player.HealthQuarters!=rom[0xc6aa]||
                    actor.JustHit!=((rom[0xd0aa]&128)!=0)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Veran mode${spec.Mode:x2}/shield{spec.Level} update{update}: source recoil/hit/sound differs: Link{_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, inv{_player.InvincibilityFrames}/{unchecked((sbyte)rom[0xd02b])}.");
                FailIf(_random.CaptureState().Calls!=seed.Calls+rom.RandomCalls,"Final-boss shield contact must preserve RNG consumption.");
            }
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],afterUpdate:Compare);
            void Set(string name,int value)=>typeof(VeranFinalCharacter).GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,value);
            Set("SubId",spec.Form);Set("CollisionMode",spec.Mode);actor.Position=new(120,88);rom[0xd082]=(byte)spec.Form;rom[0xd0a5]=(byte)spec.Mode;rom.Word(0xd08a,88*256);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,4);rom[AgesWramAddress.wDisabledObjects]=4;
            _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled,0);rom[AgesWramAddress.wMenuDisabled]=0;
            StepGameplayUpdates(1,Vector2.Zero,["attack"],afterUpdate:Compare);
            FailIf(_player.InvincibilityFrames!=(spec.Level==1?-15:-8)||!actor.JustHit,$"Veran mode${spec.Mode:x2}/shield{spec.Level}: expected native recoil inv{(spec.Level==1?-15:-8)} and pending hit, got {_player.InvincibilityFrames}/{actor.JustHit}.");
            StepGameplayUpdates(4,Vector2.Zero,["attack"],batched:spec.Level!=1,afterUpdate:Compare);
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranFinalRoomEntity>().Any(),"Room replacement must retire a shield-hit final boss.");
        }
    }
}
