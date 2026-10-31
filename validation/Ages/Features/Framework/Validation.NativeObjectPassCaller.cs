using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static readonly FieldInfo NativeObjectPass = typeof(GameRoot).GetField("_objectPass",BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo NativeToggleCaller = NativeObjectPass.FieldType.GetProperty("ToggleOwnedUpdate")!;

    private void StageDeferredNativeObjectPassCaller()
    {
        // These native references execute only category handlers. Declare
        // cutscene02's deferred room-selection caller for pending mode $04;
        // cutscene01 would consume the request after the measured object pass.
        _entities.AddEntity(new PartMovementScratchProbe(() =>
        {
            if ((_runtimeState.ReadField("wScrollMode") & 4) == 0) return;
            object pass = NativeObjectPass.GetValue(this)!;
            NativeToggleCaller.SetValue(pass,true);
            NativeObjectPass.SetValue(this,pass);
        }));
    }
}
