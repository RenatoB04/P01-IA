using UnityEngine;

public class PulseEffect : MonoBehaviour
{
    [Header("Configuração Visual")]
    public float maxRadius = 8f;       
    public float expansionSpeed = 15f; 
    public float fadeSpeed = 2f;       

    private float currentRadius = 0.1f;
    private Material mat;
    private Color baseColor;

    void Start()
    {
        
        var renderer = GetComponent<Renderer>();
        if (renderer)
        {
            mat = renderer.material;
            baseColor = mat.color;
        }
    }

    void Update()
    {
        
        currentRadius += expansionSpeed * Time.deltaTime;

        
        transform.localScale = Vector3.one * currentRadius * 2f;

        
        if (currentRadius >= maxRadius)
        {
            Destroy(gameObject);
        }
        
        
        if (mat)
        {
            float alpha = Mathf.Clamp01(1f - (currentRadius / maxRadius));
            Color c = baseColor;
            c.a = alpha;
            mat.color = c;
        }
    }
}