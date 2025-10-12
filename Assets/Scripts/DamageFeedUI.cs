using System.Collections.Generic;
using UnityEngine;

public class DamageFeedUI : MonoBehaviour
{
    public static DamageFeedUI Instance { get; private set; }

    [Header("Refs")]
    public RectTransform content;          // arrasta o Content
    public DamageFeedItem itemPrefab;      // arrasta o prefab
    public int maxItems = 6;

    [Header("Estilo")]
    public Color normalColor = Color.white;
    public Color critColor = new Color(1f, 0.3f, 0.3f);

    readonly Queue<DamageFeedItem> pool = new();
    readonly List<DamageFeedItem> active = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    DamageFeedItem GetItem()
    {
        DamageFeedItem it = pool.Count > 0 ? pool.Dequeue() : Instantiate(itemPrefab, content);
        it.gameObject.SetActive(true);
        // garantir ordem: itens novos no topo → move para first sibling
        it.transform.SetAsFirstSibling();
        return it;
    }

    void RecycleInactive()
    {
        // remove os que já se auto-desativaram
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (!active[i].gameObject.activeSelf)
            {
                pool.Enqueue(active[i]);
                active.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Adiciona uma entrada, ex.: "-20" (dano) ou o que quiseres.
    /// </summary>
    public void Push(float amount, bool isCrit = false, string targetName = null)
    {
        RecycleInactive();

        // Limita nº de linhas
        while (active.Count >= maxItems)
        {
            var oldest = active[active.Count - 1];
            oldest.gameObject.SetActive(false);
            pool.Enqueue(oldest);
            active.RemoveAt(active.Count - 1);
        }

        string txt = $"-{Mathf.RoundToInt(amount)}";
        if (!string.IsNullOrEmpty(targetName))
            txt += $"  ({targetName})";

        var color = isCrit ? critColor : normalColor;

        var item = GetItem();
        item.Init(txt, color);
        active.Insert(0, item); // mantemos referência
    }
}
