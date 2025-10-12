using System;
using System.Collections;
using UnityEngine;

public class BOTDeath : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Componente que tem a bool isDead.")]
    public MonoBehaviour health;
    [Tooltip("Nome exato da bool no script de vida (case-sensitive).")]
    public string isDeadField = "isDead";

    [Header("Behavior")]
    [Tooltip("Atraso antes de desaparecer (segundos).")]
    public float delay = 0f;
    [Tooltip("Se true, Destroy(gameObject); se false, SetActive(false).")]
    public bool destroyInstead = false;

    [Header("Optional: parar IA/colisões antes de desaparecer")]
    public Behaviour[] toDisableOnDeath;
    public Collider[]  collidersToDisable;

    // >>> NOVO: evento para notificar listeners (ex.: Spawner) quando o bot morre
    public event Action<BOTDeath> OnDied;

    bool handled;

    void Reset()
    {
        collidersToDisable = GetComponentsInChildren<Collider>(true);
    }

    void OnEnable()
    {
        // Caso este objeto volte a ser ativado (pooling), volta a permitir detetar a próxima morte
        handled = false;
    }

    void Update()
    {
        if (handled || health == null) return;

        var t = health.GetType();
        var f = t.GetField(isDeadField);
        var p = t.GetProperty(isDeadField);

        bool isDeadNow = false;
        if (f != null && f.FieldType == typeof(bool))           isDeadNow = (bool)f.GetValue(health);
        else if (p != null && p.PropertyType == typeof(bool))   isDeadNow = (bool)p.GetValue(health);

        if (!isDeadNow) return;

        handled = true;

        // parar IA/colisões já
        if (toDisableOnDeath != null)
            foreach (var b in toDisableOnDeath) if (b) b.enabled = false;

        if (collidersToDisable != null)
            foreach (var c in collidersToDisable) if (c) c.enabled = false;

        // >>> NOVO: notifica quem estiver a ouvir que este bot morreu (antes de desaparecer)
        try { OnDied?.Invoke(this); } catch { /* protege contra listeners com exceções */ }

        StartCoroutine(Disappear());
    }

    IEnumerator Disappear()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (destroyInstead) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}