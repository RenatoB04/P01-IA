using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DamageIndicatorUI : MonoBehaviour
{
    public static DamageIndicatorUI Instance { get; private set; }

    [Header("Refs")]
    public Canvas canvas;
    public CanvasGroup vignetteGroup;     
    public Image vignetteImage;           
    public RectTransform indicatorsRoot;  
    public RectTransform arrowPrefab;     

    [Header("Vignette Flash")]
    [Tooltip("Alpha do flash no pico do dano (0..1).")]
    public float flashMaxAlpha = 0.35f;
    [Tooltip("Tempo a subir o flash.")]
    public float flashInTime = 0.06f;
    [Tooltip("Tempo a desvanecer o flash.")]
    public float flashOutTime = 0.25f;

    [Header("Indicadores Direcionais")]
    [Tooltip("Distância do centro para posicionar a seta (em px).")]
    public float radius = 360f;
    [Tooltip("Vida útil da seta (segundos).")]
    public float arrowLifetime = 0.9f;
    [Tooltip("Escala do tamanho baseado no dano recebido.")]
    public Vector2 arrowScaleRange = new Vector2(0.9f, 1.3f); 

    Camera _cam;
    readonly List<Arrow> _arrows = new List<Arrow>();
    readonly Queue<RectTransform> _pool = new Queue<RectTransform>();
    Coroutine _flashRoutine;

    class Arrow
    {
        public RectTransform rt;
        public float deathTime;
        public float initialAlpha;
        public float damage;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!canvas) canvas = GetComponentInParent<Canvas>();
        _cam = Camera.main;

        if (vignetteGroup) vignetteGroup.alpha = 0f;
    }

    void Update()
    {
        
        float now = Time.unscaledTime;
        for (int i = _arrows.Count - 1; i >= 0; i--)
        {
            var a = _arrows[i];
            float t = Mathf.InverseLerp(a.deathTime - arrowLifetime, a.deathTime, now);
            float alpha = Mathf.Lerp(a.initialAlpha, 0f, t);

            var img = a.rt.GetComponent<Image>();
            if (img) img.color = new Color(img.color.r, img.color.g, img.color.b, alpha);

            if (now >= a.deathTime)
            {
                Recycle(a.rt);
                _arrows.RemoveAt(i);
            }
        }
    }

    RectTransform GetArrow()
    {
        RectTransform rt;
        if (_pool.Count > 0) rt = _pool.Dequeue();
        else rt = Instantiate(arrowPrefab, indicatorsRoot);
        rt.gameObject.SetActive(true);
        return rt;
    }

    void Recycle(RectTransform rt)
    {
        if (!rt) return;
        rt.gameObject.SetActive(false);
        _pool.Enqueue(rt);
    }

    
    
    
    
    
    public void RegisterHit(Vector3 worldFrom, float damage = 10f)
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        
        DoFlash(damage);

        
        Vector2 dir;
        float angle;
        GetScreenDirection(worldFrom, out dir, out angle);

        var rt = GetArrow();
        rt.anchoredPosition = dir * radius;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        float dmg01 = Mathf.Clamp01(damage / 50f); 
        float scale = Mathf.Lerp(arrowScaleRange.x, arrowScaleRange.y, dmg01);
        rt.localScale = Vector3.one * scale;

        var img = rt.GetComponent<Image>();
        if (img)
        {
            float baseAlpha = Mathf.Lerp(0.5f, 1f, dmg01);
            img.color = new Color(img.color.r, img.color.g, img.color.b, baseAlpha);
        }

        _arrows.Add(new Arrow
        {
            rt = rt,
            damage = damage,
            initialAlpha = rt.GetComponent<Image>() ? rt.GetComponent<Image>().color.a : 1f,
            deathTime = Time.unscaledTime + arrowLifetime
        });
    }

    void DoFlash(float damage)
    {
        if (vignetteGroup == null) return;

        float dmg01 = Mathf.Clamp01(damage / 35f);
        float peak = Mathf.Lerp(flashMaxAlpha * 0.4f, flashMaxAlpha, dmg01);

        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine(peak));
    }

    IEnumerator FlashRoutine(float peak)
    {
        
        float t = 0f;
        while (t < flashInTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(vignetteGroup.alpha, peak, t / flashInTime);
            vignetteGroup.alpha = a;
            yield return null;
        }
        vignetteGroup.alpha = peak;

        
        t = 0f;
        while (t < flashOutTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(peak, 0f, t / flashOutTime);
            vignetteGroup.alpha = a;
            yield return null;
        }
        vignetteGroup.alpha = 0f;
        _flashRoutine = null;
    }

    
    void GetScreenDirection(Vector3 worldFrom, out Vector2 dir, out float angleDeg)
    {
        Vector3 screen = _cam.WorldToScreenPoint(worldFrom);
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        
        if (screen.z < 0f)
        {
            Vector3 to = (worldFrom - _cam.transform.position).normalized;
            
            Vector3 forward = _cam.transform.forward;
            Vector3 reflect = Vector3.Reflect(to, forward);
            Vector3 fallback = _cam.transform.position + reflect * 5f;
            screen = _cam.WorldToScreenPoint(fallback);
        }

        Vector2 delta = (Vector2)screen - center;
        if (delta.sqrMagnitude < 0.001f) delta = Vector2.up; 

        dir = delta.normalized;

        
        angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
    }
}
