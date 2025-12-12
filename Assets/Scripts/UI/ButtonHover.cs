using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(AudioSource))]
public class UIButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    Vector3 normalScale;
    Vector3 targetScale;

    [SerializeField] float scaleFactor = 1.05f;
    [SerializeField] float speed = 8f;

    [Header("Som de Hover")]
    [SerializeField] AudioClip hoverSound;

    AudioSource audioSource;
    static AudioSource currentHoverSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = 1f; 
    }

    void Start()
    {
        normalScale = transform.localScale;
        targetScale = normalScale;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        targetScale = normalScale * scaleFactor;

        if (hoverSound == null)
        {
            Debug.LogWarning($"[{name}] Nenhum som de hover atribuído!");
            return;
        }

        
        if (currentHoverSource != null && currentHoverSource.isPlaying)
            currentHoverSource.Stop();

        
        audioSource.Stop(); 
        audioSource.PlayOneShot(hoverSound);
        currentHoverSource = audioSource;
    }

    public void OnPointerExit(PointerEventData e)
    {
        targetScale = normalScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * speed);
    }
}