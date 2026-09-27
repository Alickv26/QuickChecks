using QuickChecks.Input;

namespace QuickChecks.Core
{
    /// <summary>
    /// Concrete non-generic subclass of GameEventSO&lt;SwipeData&gt; used for
    /// ghost kart swipe events. Separate from SwipeEvent so ghost swipes
    /// don't drive the player's kart and vice versa.
    ///
    /// Same workaround as SwipeEvent — Unity can't instantiate generic
    /// ScriptableObjects via CreateInstance&lt;T&gt;().
    ///
    /// Created via menu: Assets > Create > QuickChecks > Events > GhostSwipeEvent
    /// </summary>
    [UnityEngine.CreateAssetMenu(menuName = "QuickChecks/Events/GhostSwipeEvent", fileName = "GhostSwipeEvent")]
    public class GhostSwipeEvent : GameEventSO<SwipeData> { }
}
