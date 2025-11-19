using UnityEngine;

public class PulseEffect : MonoBehaviour
{
    [Header("Configuração Visual")]
    public float maxRadius = 8f;       // Tamanho final da explosão
    public float expansionSpeed = 15f; // Velocidade de crescimento
    public float fadeSpeed = 2f;       // Velocidade de desaparecimento (opcional)

    private float currentRadius = 0.1f;
    private Material mat;
    private Color baseColor;

    void Start()
    {
        // Tenta pegar o material para fazer fade out (opcional)
        var renderer = GetComponent<Renderer>();
        if (renderer)
        {
            mat = renderer.material;
            baseColor = mat.color;
        }
    }

    void Update()
    {
        // 1. Aumenta o raio
        currentRadius += expansionSpeed * Time.deltaTime;

        // 2. Atualiza o tamanho (x2 porque escala 1 = raio 0.5)
        transform.localScale = Vector3.one * currentRadius * 2f;

        // 3. Se já chegou ao tamanho máximo, destroi-se
        if (currentRadius >= maxRadius)
        {
            Destroy(gameObject);
        }
        
        // (Opcional) Fade out à medida que cresce
        if (mat)
        {
            float alpha = Mathf.Clamp01(1f - (currentRadius / maxRadius));
            Color c = baseColor;
            c.a = alpha;
            mat.color = c;
        }
    }
}