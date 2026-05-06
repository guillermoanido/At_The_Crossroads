using UnityEngine;

// Drop target. Place a UI Image with this script on each player's side of the
// table; CardMovement consults it on mouse-release to decide whether to play
// the card or snap it back to the hand.
[RequireComponent(typeof(RectTransform))]
public class PlayArea : MonoBehaviour
{
    [SerializeField] private Player owner;

    public Player Owner => owner;

    public bool ContainsScreenPoint(Vector2 screenPos, Camera cam)
    {
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, screenPos, cam);
    }
}
