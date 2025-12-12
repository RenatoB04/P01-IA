using UnityEngine;

[DisallowMultipleComponent] // Garante que apenas um componente deste tipo existe por GameObject
public class BotRespawnLink : MonoBehaviour
{
    [Header("Ligação ao Spawner (opcional)")]
    public BotSpawner_Proto spawner; // Referência opcional ao spawner que vai gerenciar respawns

    [Tooltip("Waypoints preferidos para este bot em respawns futuros (opcional).")]
    public Transform[] patrolWaypoints; // Waypoints preferenciais para patrulha do bot após respawn

    private BOTDeath death; // Referência ao script de morte do bot

    void Awake()
    {
        // Obtém referência ao script de morte
        death = GetComponent<BOTDeath>();
        if (death != null)
        {
            // Remove e adiciona novamente para evitar duplicação de eventos
            death.OnDied -= OnBotDied;
            death.OnDied += OnBotDied;
        }
    }

    void OnDestroy()
    {
        // Remove listener para evitar chamadas após destruição do GameObject
        if (death != null)
            death.OnDied -= OnBotDied;
    }

    // Função chamada quando o bot morre
    void OnBotDied(BOTDeath d)
    {
        // Se houver um spawner e waypoints definidos, agenda respawn com esses waypoints
        if (spawner != null && patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            spawner.ScheduleRespawn(patrolWaypoints);
        }
    }
}