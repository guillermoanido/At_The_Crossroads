using System.Collections.Generic;
using UnityEngine;

public class HandLayout : MonoBehaviour
{
    public float cardSpacing = 200f;
    public float fanAngle = 15f;
    public float maxWidth = 1000f;

    private List<RectTransform> cards = new List<RectTransform>();

    void Start()
    {
        UpdateCardList();
        UpdateLayout();
    }

    void UpdateCardList()
    {
        cards.Clear();

        foreach (Transform child in transform)
        {
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
                cards.Add(rect);
        }
    }

    void UpdateLayout()
    {
        int count = cards.Count;
        if (count == 0) return;

        float width = Mathf.Min(maxWidth, cardSpacing * (count - 1));
        float startX = -width / 2f;

        for (int i = 0; i < count; i++)
        {
            float x = count == 1 ? 0 : startX + i * (width / (count - 1));
            float angle = count == 1 ? 0 : Mathf.Lerp(-fanAngle, fanAngle, (float)i / (count - 1));

            cards[i].anchoredPosition = new Vector2(x, 0);
            cards[i].localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
