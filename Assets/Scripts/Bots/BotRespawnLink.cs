using UnityEngine;

[DisallowMultipleComponent]
public class BotRespawnLink : MonoBehaviour
{
    [HideInInspector] public BotSpawner_Proto spawner;     // definido pelo spawner ao instanciar
    [HideInInspector] public Transform[] patrolWaypoints;  // para reatribuir aos bots novos (opcional)

    BOTDeath death;

    void Awake()
    {
        // tenta obter o BOTDeath no mesmo GameObject
        death = GetComponent<BOTDeath>();
        if (!death)
        {
            Debug.LogWarning("[BotRespawnLink] BOTDeath não encontrado no bot.");
            return;
        }

        // subscreve ao evento de morte (evita duplicar subscrição)
        death.OnDied -= HandleDeath;
        death.OnDied += HandleDeath;
    }

    void OnDestroy()
    {
        // remove subscrição para evitar leaks ou callbacks inválidos
        if (death != null)
            death.OnDied -= HandleDeath;
    }

    void HandleDeath(BOTDeath d)
    {
        // notifica o spawner para criar um novo bot após delay
        if (spawner != null)
        {
            spawner.ScheduleRespawn(patrolWaypoints);
        }
        else
        {
            Debug.LogWarning("[BotRespawnLink] Spawner não definido; não consigo fazer respawn.");
        }
    }
}