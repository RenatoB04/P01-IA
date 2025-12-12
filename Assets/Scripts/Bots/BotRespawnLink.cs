using UnityEngine;







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
        
        
        if (spawner != null && patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            spawner.ScheduleRespawn(patrolWaypoints);
        }
    }
}
