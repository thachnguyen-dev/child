using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChildGame
{
    /// <summary>
    /// A world object (with a Collider2D) that reports touches. Needs a Physics2DRaycaster on the camera
    /// (<see cref="MiniGameSession.EnsureRaycaster"/>). UI screens on top block it automatically.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Tappable : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        /// <summary>Fires on touch down (responsive, for gameplay).</summary>
        public event Action<PointerEventData> Pressed;
        /// <summary>Fires on a completed tap (down and up on the object, for menus and the hub).</summary>
        public event Action<PointerEventData> Clicked;

        public void OnPointerDown(PointerEventData e) => Pressed?.Invoke(e);
        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(e);
    }
}
