using System.Collections.Generic;
using UnityEngine;

public class HandLayout : MonoBehaviour
{
    public float spacing = 200f;
    public float fanAngle = 15f;
    public float maxWidth = 1000f;

    private List<RectTransform> cards = new List<RectTransform>();

    void Start()
    {
        Refresh();
    }

    private void Update()
    {
        UpdateLayout();
    }
    public void AddCard(RectTransform card)
    {
        cards.Add(card);
        card.SetParent(transform, false);
        UpdateLayout();
    }

    public void RemoveCard(RectTransform card)
    {
        cards.Remove(card);
        UpdateLayout();
    }

    public void Refresh()
    {
        cards.Clear();

        foreach (Transform child in transform)
        {
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
                cards.Add(rect);
        }

        UpdateLayout();
    }

    void UpdateLayout()
    {
        int count = cards.Count;
        if (count == 0) return;

        float width = Mathf.Min(maxWidth, spacing * (count - 1));
        float startX = -width / 2f;

        bool useFan = count <= 6;   // only fan when few cards

        for (int i = 0; i < count; i++)
        {
            float x = count == 1 ? 0 : startX + i * (width / (count - 1));

            cards[i].anchoredPosition = new Vector2(x, 0);

            if (useFan)
            {
                float angle = Mathf.Lerp(-fanAngle, fanAngle, (float)i / (count - 1));
                cards[i].localRotation = Quaternion.Euler(0, 0, angle);
            }
            else
            {
                cards[i].localRotation = Quaternion.identity;
            }
        }
    }

}
