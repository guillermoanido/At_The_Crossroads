using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class PlayArea : MonoBehaviour
{
    [SerializeField] private Player owner;

    public Player Owner => owner;

    public bool ContainsScreenPoint(Vector2 screenPos, Camera cam)
        => RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, screenPos, cam);
}
