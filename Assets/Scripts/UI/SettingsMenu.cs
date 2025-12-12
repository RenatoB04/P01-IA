using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InfimaGames.LowPolyShooterPack;


public class SettingsMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown resolutionsDropdown;
    [SerializeField] private Slider sensitivitySlider;

    [Header("Painel e Botão")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button closeSettingsButton;

    private Resolution[] resolutions;
    private const string PREF_RESOLUTION = "settings_resolution";
    private const string PREF_SENSITIVITY = "settings_sensitivity";

    private ICameraLook currentCameraLook;

    private void Awake()
    {
        
        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));

        if (closeSettingsButton != null)
            closeSettingsButton.onClick.AddListener(() => settingsPanel.SetActive(false));
    }

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        
        Character playerCharacter = FindObjectOfType<Character>();
        if (playerCharacter != null)
            currentCameraLook = playerCharacter as ICameraLook;

        CarregarResolucoes();
        CarregarOpcoesGuardadas();

        
        resolutionsDropdown.onValueChanged.AddListener(OnResolutionChanged);
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
    }

    private void CarregarResolucoes()
    {
        resolutions = Screen.resolutions;
        resolutionsDropdown.ClearOptions();

        List<string> options = new List<string>();
        int indiceActual = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string opcao = resolutions[i].width + " x " + resolutions[i].height;
            options.Add(opcao);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                indiceActual = i;
            }
        }

        resolutionsDropdown.AddOptions(options);

        int guardada = PlayerPrefs.GetInt(PREF_RESOLUTION, indiceActual);
        guardada = Mathf.Clamp(guardada, 0, resolutions.Length - 1);

        resolutionsDropdown.value = guardada;
        resolutionsDropdown.RefreshShownValue();

        AplicarResolucao(guardada);
    }

    private void CarregarOpcoesGuardadas()
    {
        float sens = PlayerPrefs.GetFloat(PREF_SENSITIVITY, 1.0f);
        sensitivitySlider.value = sens;
        AplicarSensibilidade(sens);
    }

    public void OnResolutionChanged(int index)
    {
        AplicarResolucao(index);
        PlayerPrefs.SetInt(PREF_RESOLUTION, index);
    }

    public void OnSensitivityChanged(float value)
    {
        AplicarSensibilidade(value);
        PlayerPrefs.SetFloat(PREF_SENSITIVITY, value);
    }

    private void AplicarResolucao(int index)
    {
        Resolution r = resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
    }

    private void AplicarSensibilidade(float value)
    {
        if (currentCameraLook != null)
            currentCameraLook.SetMouseSensitivity(value);
    }
}

public interface ICameraLook
{
    void SetMouseSensitivity(float value);
}
