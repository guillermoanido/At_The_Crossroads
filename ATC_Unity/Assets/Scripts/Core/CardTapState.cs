using UnityEngine;

public class CardTapState : MonoBehaviour
{
    [SerializeField] private float tappedZRotation = -90f;

    private Quaternion untappedRotation = Quaternion.identity;

    public bool IsTapped { get; private set; }

    public void Toggle()
    {
        if (IsTapped) Untap();
        else Tap();
    }

    public void Tap()
    {
        if (IsTapped) return;
        untappedRotation = transform.localRotation;
        transform.localRotation = untappedRotation * Quaternion.Euler(0f, 0f, tappedZRotation);
        IsTapped = true;
    }

    public void Untap()
    {
        if (!IsTapped) return;
        transform.localRotation = untappedRotation;
        IsTapped = false;
    }
}
