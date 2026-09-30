using QuickChecks.Input;

namespace QuickChecks.Core
{
    /// <summary>
    /// Concrete non-generic subclass of GameEventSO&lt;SwipeData&gt;.
    ///
    /// Unity's ScriptableObject.CreateInstance&lt;T&gt;() cannot instantiate
    /// generic types like GameEventSO&lt;SwipeData&gt; — it returns null,
    /// which then causes AssetDatabase.CreateAsset to throw ArgumentNullException.
    ///
    /// This concrete subclass works around that limitation. All code that
    /// references GameEventSO&lt;SwipeData&gt; as a field type still works
    /// because SwipeEvent inherits from it.
    ///
    /// Created via menu: Assets > Create > QuickChecks > Events > SwipeEvent
    /// </summary>
    [UnityEngine.CreateAssetMenu(menuName = "QuickChecks/Events/SwipeEvent", fileName = "SwipeEvent")]
    public class SwipeEvent : GameEventSO<SwipeData> { }
}
