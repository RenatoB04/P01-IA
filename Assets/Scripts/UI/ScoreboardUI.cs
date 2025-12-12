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

        
        var scores = FindObjectsOfType<PlayerScore>();
        if (scores == null || scores.Length == 0)
        {
            listText.text = "À espera de jogadores...";
            return;
        }

        
        var sorted = new List<(string name, int kills, int score)>(scores.Length);

        foreach (var ps in scores)
        {
            if (ps == null) continue;

            
            string pname = GetCorrectPlayerName(ps.gameObject);
            
            int kills = ps.Kills.Value;
            int score = ps.Score.Value;

            sorted.Add((pname, kills, score));
        }

        
        var ordered = sorted
            .OrderByDescending(e => e.score)
            .ThenByDescending(e => e.kills)
            .ToList();

        
        var sb = new StringBuilder();
        sb.AppendLine("JOGADOR               Kills   Score");
        sb.AppendLine("-----------------------------------");
        
        foreach (var e in ordered)
        {
            
            sb.AppendLine($"{e.name,-20}  {e.kills,5}   {e.score,5}");
        }

        listText.text = sb.ToString();
    }

    string GetCorrectPlayerName(GameObject playerObj)
    {
        
        var nameScript = playerObj.GetComponent<PlayerName>();
        if (nameScript != null)
        {
            return nameScript.Name;
        }

        
        if (playerObj.name.StartsWith("Bot"))
        {
            return playerObj.name;
        }

        
        var netObj = playerObj.GetComponent<NetworkObject>();
        if (netObj != null) 
        {
            return $"Player {netObj.OwnerClientId}";
        }

        return "Desconhecido";
    }
}