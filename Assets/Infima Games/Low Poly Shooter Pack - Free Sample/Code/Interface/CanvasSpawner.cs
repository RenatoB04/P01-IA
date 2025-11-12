// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace InfimaGames.LowPolyShooterPack.Interface
{
    /// <summary>
    /// Player Interface.
    /// </summary>
    public class CanvasSpawner : MonoBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Settings")]
        
        [Tooltip("Canvas prefab spawned at start. Displays the player's user interface.")]
        [SerializeField]
        private GameObject canvasPrefab;

        #endregion
        
        // ADIÇÃO: Campo para saber se o Canvas já foi spawnado
        private GameObject canvas;

        #region UNITY FUNCTIONS

        /// <summary>
        /// Awake.
        /// </summary>
        private void Awake()
        {
            // REMOÇÃO: Comentado para evitar que spawne para todos. A inicialização será manual.
            // Instantiate(canvasPrefab);
        }

        #endregion
        
        #region PUBLIC METHODS // ADIÇÃO

        /// <summary>
        /// Instancia o Canvas, mas só se ainda não tiver sido instanciado.
        /// </summary>
        public void SpawnCanvas()
        {
            if (canvas == null)
            {
                //Spawn Interface.
                canvas = Instantiate(canvasPrefab);
                // NOTA: O kit original não define parent, assume-se que é o root da cena.
            }
        }

        #endregion
    }
}