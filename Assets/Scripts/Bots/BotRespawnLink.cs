using UnityEngine;

/// <summary>
/// Ligação entre um bot individual e o spawner.
/// Neste momento o respawn principal é feito via BOTDeath.OnAnyBotKilled
/// (ver BotSpawner_Proto), por isso isto serve sobretudo para manter
/// compatibilidade e, no futuro, poderes forçar respawn com path específico.
/// </summary>
[DisallowMultipleComponent]
public class BotRespawnLink : MonoBehaviour
{
    [Header("Ligação ao Spawner (opcional)")]
    public BotSpawner_Proto spawner;

    [Tooltip("Waypoints preferidos para este bot em respawns futuros (opcional).")]
    public Transform[] patrolWaypoints;

    BOTDeath death;

    void Awake()
    {
        death = GetComponent<BOTDeath>();
        if (death != null)
        {
            death.OnDied -= OnBotDied;
            death.OnDied += OnBotDied;
        }
    }

    void OnDestroy()
    {
        if (death != null)
            death.OnDied -= OnBotDied;
    }

    void OnBotDied(BOTDeath d)
    {
        // O respawn real é tratado pelo BotSpawner_Proto através de BOTDeath.OnAnyBotKilled.
        // Aqui apenas chamamos ScheduleRespawn caso no futuro queiras usar paths específicos.
        if (spawner != null && patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            spawner.ScheduleRespawn(patrolWaypoints);
        }
    }
}
