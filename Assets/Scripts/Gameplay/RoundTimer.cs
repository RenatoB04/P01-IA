using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;
using System.Linq; 

public class RoundTimer : NetworkBehaviour
{
    [Header("Configuração")]
    public float roundDuration = 300f; 
    public bool startOnSpawn = true;

    [Header("UI Timer")]
    [SerializeField] private TMP_Text timerText;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        300f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> isRoundActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        timeRemaining.OnValueChanged += OnTimeChanged;
        if (IsServer && startOnSpawn) StartRound();
    }

    public override void OnNetworkDespawn()
    {
        timeRemaining.OnValueChanged -= OnTimeChanged;
    }

    void Update()
    {
        if (IsServer && isRoundActive.Value)
        {
            float newVal = timeRemaining.Value - Time.deltaTime;
            if (newVal <= 0f)
            {
                newVal = 0f;
                EndRound();
            }
            timeRemaining.Value = newVal;
        }
    }

    public void StartRound()
    {
        if (!IsServer) return;
        timeRemaining.Value = roundDuration;
        isRoundActive.Value = true;
    }

    public void EndRound()
    {
        if (!IsServer) return;
        isRoundActive.Value = false;
        
        
        string winnerName = "Ninguém";
        int highScore = -1;

        PlayerScore[] allScores = FindObjectsOfType<PlayerScore>();

        if (allScores.Length > 0)
        {
            var bestPlayer = allScores.OrderByDescending(p => p.Score.Value).First();
            
            if (bestPlayer != null)
            {
                highScore = bestPlayer.Score.Value;
                
                
                var nameScript = bestPlayer.GetComponent<PlayerName>();
                if (nameScript != null)
                {
                    winnerName = nameScript.Name;
                }
                else
                {
                    
                    winnerName = $"Player {bestPlayer.OwnerClientId}";
                }
            }
        }

        RoundEndedClientRpc(winnerName, highScore);
    }

    private void OnTimeChanged(float prev, float curr) => UpdateTimerUI(curr);

    [ClientRpc]
    private void RoundEndedClientRpc(string winner, int score)
    {
        UpdateTimerUI(0f);
        LockPlayerInputs();

        if (GameOverUI.Instance != null)
        {
            GameOverUI.Instance.ShowGameOver("FIM DA RONDA", winner, score);
        }
    }

    private void UpdateTimerUI(float seconds)
    {
        if (!timerText) return;
        int s = Mathf.CeilToInt(seconds);
        int mm = s / 60;
        int ss = s % 60;
        timerText.text = $"{mm:00}:{ss:00}";
        timerText.color = seconds <= 10f ? Color.red : Color.white;
    }

    private void LockPlayerInputs()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            GameplayCursor.Unlock();
            return;
        }

        var player = NetworkManager.Singleton.LocalClient.PlayerObject;
        
        var inputSystem = player.GetComponent<PlayerInput>();
        if (inputSystem != null) { inputSystem.DeactivateInput(); inputSystem.enabled = false; }

        var move = player.GetComponent<FP_Controller_IS>();
        if (move) move.enabled = false;

        var weapon = player.GetComponentInChildren<Weapon>();
        if (weapon) weapon.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}