using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    public static CrosshairUI Instance { get; private set; }

    [Header("Refs")]
    public RectTransform dot;
    public RectTransform top;
    public RectTransform bottom;
    public RectTransform left;
    public RectTransform right;

    [Header("Look")]
    public bool useDotOnly = false;
    public float thickness = 3f;
    public float length = 12f;
    public float baseGap = 8f;

    [Header("Dynamics")]
    public float kickPerShot = 8f;     
    public float maxKick = 30f;
    public float relaxSpeed = 30f;     
    public float moveKick = 6f;        
    public float aimDownSightsScale = 0.7f; 

    [Header("Hitmarker")]
    public Image dotImage;
    public Color hitColor = Color.green;
    public float hitFlashTime = 0.07f;

    float currentKick = 0f;
    float hitTimer = 0f;
    Color dotBaseColor;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dotImage == null && dot != null) dotImage = dot.GetComponent<Image>();
        if (dotImage != null) dotBaseColor = dotImage.color;

        ApplyGeometry();
    }

    void Update()
    {
        
        if (currentKick > 0f)
        {
            currentKick = Mathf.Max(0f, currentKick - relaxSpeed * Time.unscaledDeltaTime);
            ApplyGeometry();
        }

        
        if (hitTimer > 0f)
        {
            hitTimer -= Time.unscaledDeltaTime;
            if (hitTimer <= 0f && dotImage != null) dotImage.color = dotBaseColor;
        }
    }

    void ApplyGeometry()
    {
        float gap = baseGap + currentKick;

        if (dot != null)
        {
            dot.sizeDelta = new Vector2(thickness, thickness);
            dot.anchoredPosition = Vector2.zero;
        }

        if (useDotOnly) { ToggleBars(false); return; }
        ToggleBars(true);

        if (top != null)
        {
            top.sizeDelta = new Vector2(thickness, length);
            top.anchoredPosition = new Vector2(0f, gap + length * 0.5f);
        }
        if (bottom != null)
        {
            bottom.sizeDelta = new Vector2(thickness, length);
            bottom.anchoredPosition = new Vector2(0f, -(gap + length * 0.5f));
        }
        if (left != null)
        {
            left.sizeDelta = new Vector2(length, thickness);
            left.anchoredPosition = new Vector2(-(gap + length * 0.5f), 0f);
        }
        if (right != null)
        {
            right.sizeDelta = new Vector2(length, thickness);
            right.anchoredPosition = new Vector2(gap + length * 0.5f, 0f);
        }
    }

    void ToggleBars(bool on)
    {
        if (top) top.gameObject.SetActive(on);
        if (bottom) bottom.gameObject.SetActive(on);
        if (left) left.gameObject.SetActive(on);
        if (right) right.gameObject.SetActive(on);
    }

    
    public void Kick(float amount = -1f)
    {
        float add = (amount > 0f) ? amount : kickPerShot;
        currentKick = Mathf.Min(maxKick, currentKick + add);
        ApplyGeometry();
    }

    public void SetADS(bool adsOn)
    {
        float scale = adsOn ? aimDownSightsScale : 1f;
        transform.localScale = Vector3.one * scale;
    }

    public void ShowHit()
    {
        if (dotImage == null) return;
        dotImage.color = hitColor;
        hitTimer = hitFlashTime;
    }
}
