using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class DamageFeedItem : MonoBehaviour
{
    public TextMeshProUGUI label;
    public float life = 1.4f;
    public float fade = 0.4f;

    CanvasGroup cg;
    float t;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (!label) label = GetComponent<TextMeshProUGUI>();
    }

    public void Init(string text, Color color)
    {
        if (!label) label = GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.color = color;
        cg.alpha = 1f;
        t = 0f;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;

        if (t > life - fade)
        {
            float a = Mathf.InverseLerp(life, life - fade, t);
            cg.alpha = Mathf.Clamp01(1f - a);
        }

        if (t >= life)
            gameObject.SetActive(false);
    }
}
