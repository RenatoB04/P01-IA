using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class ScoreboardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text listText;

    [Header("Input")]
    [Tooltip("Ação para mostrar o scoreboard (ex.: Tab).")]
    [SerializeField] private InputActionReference showScoreboardAction;

    [Header("Opções")]
    [SerializeField] private float refreshRate = 10f;

    float nextRefreshTime;

    void OnEnable()
    {
        if (showScoreboardAction && !showScoreboardAction.action.enabled)
            showScoreboardAction.action.Enable();

        if (panel) panel.SetActive(false);
        nextRefreshTime = 0f;
    }

    void OnDisable()
    {
        if (showScoreboardAction && showScoreboardAction.action.enabled)
            showScoreboardAction.action.Disable();
    }

    void Update()
    {
        bool wantShow = showScoreboardAction != null && showScoreboardAction.action.IsPressed();

        if (panel && panel.activeSelf != wantShow)
        {
            panel.SetActive(wantShow);
            if (wantShow) RefreshNow();
        }

        if (wantShow && Time.unscaledTime >= nextRefreshTime)
        {
            RefreshNow();
            nextRefreshTime = Time.unscaledTime + (refreshRate > 0f ? 1f / refreshRate : 0.2f);
        }
    }

    void RefreshNow()
    {
        if (!listText) return;

        // Encontra todos os PlayerScore na cena
        var scores = FindObjectsOfType<PlayerScore>();
        if (scores == null || scores.Length == 0)
        {
            listText.text = "À espera de jogadores...";
            return;
        }

        // Lista temporária para ordenar
        var sorted = new List<(string name, int kills, int score)>(scores.Length);

        foreach (var ps in scores)
        {
            if (ps == null) continue;

            // --- CORREÇÃO AQUI: Ler do script PlayerName ---
            string pname = GetCorrectPlayerName(ps.gameObject);
            
            int kills = ps.Kills.Value;
            int score = ps.Score.Value;

            sorted.Add((pname, kills, score));
        }

        // Ordena por Score (maior primeiro)
        var ordered = sorted
            .OrderByDescending(e => e.score)
            .ThenByDescending(e => e.kills)
            .ToList();

        // Constrói o texto da tabela
        var sb = new StringBuilder();
        sb.AppendLine("JOGADOR               Kills   Score");
        sb.AppendLine("-----------------------------------");
        
        foreach (var e in ordered)
        {
            // Formatação: -20 caracteres para nome (alinhado esq), 5 para numeros (alinhado dir)
            sb.AppendLine($"{e.name,-20}  {e.kills,5}   {e.score,5}");
        }

        listText.text = sb.ToString();
    }

    string GetCorrectPlayerName(GameObject playerObj)
    {
        // 1. Tenta encontrar o nosso novo script PlayerName
        var nameScript = playerObj.GetComponent<PlayerName>();
        if (nameScript != null)
        {
            return nameScript.Name;
        }

        // 2. Fallback: Se for Bot, tentamos ver se tem nome no GameObject
        if (playerObj.name.StartsWith("Bot"))
        {
            return playerObj.name;
        }

        // 3. Último recurso: ID do Cliente
        var netObj = playerObj.GetComponent<NetworkObject>();
        if (netObj != null) 
        {
            return $"Player {netObj.OwnerClientId}";
        }

        return "Desconhecido";
    }
}