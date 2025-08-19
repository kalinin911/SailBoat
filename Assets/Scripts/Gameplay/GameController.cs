using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.HexGrid;
using Core.Pathfinding;
using Core.Camera;
using Gameplay.Map;
using Gameplay.Boat;
using Infrastructure.Events;
using Infrastructure.Services;
using Infrastructure.UI;
using Zenject;

namespace Gameplay
{
    public class GameController : MonoBehaviour, IGameController
    {
        [Header("Game Settings")]
        [SerializeField] private string _mapAssetKey = "DefaultMap";
        [SerializeField] private HexCoordinate _boatStartPosition = new HexCoordinate(0, 0);
        [SerializeField] private bool _useLoadingScreen = true;
        [SerializeField] private bool _preloadAssets = true;

        [Header("Performance Settings")]
        [SerializeField] private bool _enableAsyncLoading = true;
        [SerializeField] private int _maxTilesPerFrame = 10;

        [Inject] private IMapGenerator _mapGenerator;
        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IPathfinder _pathfinder;
        [Inject] private IBoatController _boatController;
        [Inject] private ICameraController _cameraController;
        [Inject] private IGameEventManager _eventManager;
        [Inject] private IAssetService _assetService;
        [Inject] private LoadingScreenManager _loadingScreenManager;
        [Inject] private PerformanceManager _performanceManager;

        private bool _isGameActive;
        private bool _isInitialized;

        private void Start()
        {
            InitializeGameAsync().Forget();
        }

        public async UniTask InitializeGameAsync()
        {
            if (_isInitialized)
                return;

            Debug.Log("Starting enhanced game initialization...");

            if (_useLoadingScreen && _loadingScreenManager != null)
            {
                await InitializeWithLoadingScreen();
            }
            else
            {
                await InitializeDirectly();
            }

            _isInitialized = true;
            Debug.Log("Enhanced game initialization completed!");
        }

        private async UniTask InitializeWithLoadingScreen()
        {
            var loadingPhases = new LoadingScreenManager.LoadingPhase[]
            {
                new LoadingScreenManager.LoadingPhase("Initializing Systems...", InitializeSystemsAsync),
                new LoadingScreenManager.LoadingPhase("Loading Assets...", PreloadAssetsAsync),
                new LoadingScreenManager.LoadingPhase("Generating Map...", GenerateMapAsync),
                new LoadingScreenManager.LoadingPhase("Setting up Gameplay...", SetupGameplayAsync),
                new LoadingScreenManager.LoadingPhase("Finalizing...", FinalizeInitializationAsync)
            };

            await _loadingScreenManager.LoadWithPhases(loadingPhases);
        }

        private async UniTask InitializeDirectly()
        {
            await InitializeSystemsAsync();
            
            if (_preloadAssets)
            {
                await PreloadAssetsAsync();
            }
            
            await GenerateMapAsync();
            await SetupGameplayAsync();
            await FinalizeInitializationAsync();
        }

        private async UniTask InitializeSystemsAsync()
        {
            Debug.Log("Initializing core systems...");

            // Initialize asset service
            if (_assetService is AddressableAssetService enhancedAssetService)
            {
                await enhancedAssetService.InitializeAsync();
            }

            // Initialize performance manager
            if (_performanceManager != null && Application.isMobilePlatform)
            {
                _performanceManager.EnableMobileMode(true);
            }

            await UniTask.Delay(100); // Small delay for system initialization
        }

        private async UniTask PreloadAssetsAsync()
        {
            if (!_preloadAssets)
                return;

            Debug.Log("Preloading game assets...");

            var essentialAssets = new[]
            {
                _mapAssetKey,
                "WaterTile",
                "TerrainTile",
                "Rock",
                "Vegetation"
            };

            if (_assetService is AddressableAssetService enhancedAssetService)
            {
                await enhancedAssetService.PreloadAssetsAsync(essentialAssets, progress =>
                {
                    if (_loadingScreenManager != null)
                    {
                        _loadingScreenManager.UpdateProgress(progress, $"Loading assets... {progress * 100:F0}%");
                    }
                });
            }
        }

        private async UniTask GenerateMapAsync()
        {
            Debug.Log("Generating game map...");

            try
            {
                HexTile[,] tiles;
                
                if (_enableAsyncLoading)
                {
                    tiles = await GenerateMapWithProgressAsync();
                }
                else
                {
                    tiles = await _mapGenerator.GenerateMapAsync(_mapAssetKey);
                }

                // Add vegetation and rocks
                await UniTask.Run(() => _mapGenerator.AddRandomVegetationAndRocks(tiles));
                
                Debug.Log($"Map generated successfully with {tiles.GetLength(0)}x{tiles.GetLength(1)} tiles");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to generate map: {ex.Message}");
                throw;
            }
        }

        private async UniTask<HexTile[,]> GenerateMapWithProgressAsync()
        {
            // This would be implemented in the MapGenerator for async tile creation
            // For now, we'll simulate the async behavior
            var tiles = await _mapGenerator.GenerateMapAsync(_mapAssetKey);
            
            // Simulate processing tiles in batches for better performance
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var totalTiles = width * height;
            var processedTiles = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    processedTiles++;
                    
                    // Update progress every batch
                    if (processedTiles % _maxTilesPerFrame == 0)
                    {
                        var progress = (float)processedTiles / totalTiles;
                        if (_loadingScreenManager != null)
                        {
                            _loadingScreenManager.UpdateProgress(progress, 
                                $"Processing tiles... {processedTiles}/{totalTiles}");
                        }
                        await UniTask.NextFrame();
                    }
                }
            }

            return tiles;
        }

        private async UniTask SetupGameplayAsync()
        {
            Debug.Log("Setting up gameplay elements...");

            // Validate boat start position
            if (!_hexGridManager.IsWalkable(_boatStartPosition))
            {
                Debug.LogWarning("Boat start position is not walkable, finding alternative...");
                _boatStartPosition = FindValidStartPosition();
            }

            // Set boat position
            _boatController.SetPosition(_boatStartPosition);
            
            // Setup camera
            _cameraController.FollowTarget(_boatController.Transform);
            
            // Subscribe to events
            _eventManager.OnHexClicked += OnHexClicked;
            
            await UniTask.Delay(100); // Small delay for setup completion
        }

        private async UniTask FinalizeInitializationAsync()
        {
            Debug.Log("Finalizing game initialization...");

            // Start the game
            StartGame();
            
            // Force garbage collection after initialization
            System.GC.Collect();
            
            // Log performance statistics
            if (_performanceManager != null)
            {
                Debug.Log($"Game initialized with {_performanceManager.GetCurrentFPS():F1} FPS");
            }

            await UniTask.Delay(100);
        }

        private HexCoordinate FindValidStartPosition()
        {
            // Search for a valid water tile near the original start position
            var searchRadius = 5;
            var originalPos = _boatStartPosition;

            for (int radius = 1; radius <= searchRadius; radius++)
            {
                for (int q = -radius; q <= radius; q++)
                {
                    for (int r = -radius; r <= radius; r++)
                    {
                        var testPos = new HexCoordinate(originalPos.Q + q, originalPos.R + r);
                        if (_hexGridManager.IsWalkable(testPos))
                        {
                            Debug.Log($"Found valid start position: {testPos}");
                            return testPos;
                        }
                    }
                }
            }

            // Fallback to origin
            Debug.LogWarning("Could not find valid start position, using origin");
            return new HexCoordinate(0, 0);
        }

        public void StartGame()
        {
            _isGameActive = true;
            Time.timeScale = 1f;
            Debug.Log("Game started!");

            // Trigger game started event
            _eventManager?.TriggerBoatMovementCompleted(_boatController.Transform.position);
        }

        public void PauseGame()
        {
            _isGameActive = false;
            Time.timeScale = 0f;
            Debug.Log("Game paused");
        }

        public void ResumeGame()
        {
            _isGameActive = true;
            Time.timeScale = 1f;
            Debug.Log("Game resumed");
        }

        public async UniTask RestartGameAsync()
        {
            Debug.Log("Restarting game...");
            
            PauseGame();
            
            // Show loading screen during restart
            if (_loadingScreenManager != null)
            {
                _loadingScreenManager.ShowLoadingScreen("Restarting Game...");
            }

            // Reset game state
            _isInitialized = false;
            _isGameActive = false;

            // Clean up resources
            if (_assetService is AddressableAssetService enhancedAssetService)
            {
                enhancedAssetService.ClearCache();
            }

            // Force garbage collection
            System.GC.Collect();

            await UniTask.Delay(500); // Brief pause

            // Reinitialize
            await InitializeGameAsync();

            if (_loadingScreenManager != null)
            {
                _loadingScreenManager.HideLoadingScreen();
            }

            Debug.Log("Game restarted successfully!");
        }

        private void OnHexClicked(Vector2Int hexOffset, Vector3 worldPosition)
        {
            if (!_isGameActive || _boatController.IsMoving)
                return;

            var targetHex = HexCoordinate.FromOffset(hexOffset.x, hexOffset.y);
            
            if (!_hexGridManager.IsWalkable(targetHex))
            {
                Debug.Log("Cannot move to non-walkable hex");
                return;
            }

            var path = _pathfinder.FindPath(_boatController.CurrentHex, targetHex);
            
            if (path.Length > 0)
            {
                _eventManager.TriggerPathCalculated(path);
                _boatController.MoveToAsync(path).Forget();
                
                Debug.Log($"Moving boat to {targetHex} via path of {path.Length} hexes");
            }
            else
            {
                Debug.Log("No valid path found");
            }
        }

        // Public API for external control
        public bool IsGameActive => _isGameActive;
        public bool IsInitialized => _isInitialized;
        public HexCoordinate BoatPosition => _boatController.CurrentHex;

        public void SetMapAsset(string mapAssetKey)
        {
            _mapAssetKey = mapAssetKey;
        }

        public void SetBoatStartPosition(HexCoordinate position)
        {
            _boatStartPosition = position;
        }

        // Performance monitoring
        public void LogGameStatistics()
        {
            Debug.Log("=== Game Statistics ===");
            Debug.Log($"Game Active: {_isGameActive}");
            Debug.Log($"Initialized: {_isInitialized}");
            Debug.Log($"Boat Position: {_boatController.CurrentHex}");
            Debug.Log($"Boat Moving: {_boatController.IsMoving}");
            
            if (_performanceManager != null)
            {
                Debug.Log($"Current FPS: {_performanceManager.GetCurrentFPS():F1}");
            }

            if (_assetService is AddressableAssetService enhancedAssetService)
            {
                enhancedAssetService.LogCacheStatus();
            }
        }

        private void OnDestroy()
        {
            if (_eventManager != null)
            {
                _eventManager.OnHexClicked -= OnHexClicked;
            }

            // Clean up resources
            if (_assetService is AddressableAssetService enhancedAssetService)
            {
                enhancedAssetService.ReleaseAllAssets();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                PauseGame();
            }
            else if (_isInitialized)
            {
                ResumeGame();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _isGameActive)
            {
                PauseGame();
            }
        }

        // Debug and testing methods
        [ContextMenu("Log Game Statistics")]
        private void EditorLogGameStats()
        {
            LogGameStatistics();
        }

        [ContextMenu("Restart Game")]
        private void EditorRestartGame()
        {
            RestartGameAsync().Forget();
        }

        [ContextMenu("Force Memory Cleanup")]
        private void EditorForceMemoryCleanup()
        {
            if (_performanceManager != null)
            {
                _performanceManager.ForceMemoryCleanup();
            }
        }
    }
}