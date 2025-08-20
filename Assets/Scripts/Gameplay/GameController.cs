using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.HexGrid;
using Core.Pathfinding;
using Core.Camera;
using Gameplay.Map;
using Gameplay.Boat;
using Infrastructure.Events;
using Infrastructure.Services;
using Zenject;

namespace Gameplay
{
    public class GameController : MonoBehaviour, IGameController
    {
        [Header("Game Settings")] [SerializeField]
        private string _mapAssetKey = "DefaultMap";

        [SerializeField] private HexCoordinate _boatStartPosition = new HexCoordinate(0, 0);

        [Header("Performance Settings")] [SerializeField]
        private bool _enableAsyncLoading = true;

        [SerializeField] private int _maxTilesPerFrame = 10;

        [Inject] private IMapGenerator _mapGenerator;
        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IPathfinder _pathfinder;
        [Inject] private IBoatController _boatController;
        [Inject] private ICameraController _cameraController;
        [Inject] private IGameEventManager _eventManager;
        [Inject] private IAssetService _assetService;
        [Inject(Optional = true)] private PerformanceManager _performanceManager;

        private bool _isInitialized;

        private void Start()
        {
            InitializeGameAsync().Forget();

            _eventManager.OnHexClicked += OnHexClicked;
        }

        public async UniTask InitializeGameAsync()
        {
            if (_isInitialized)
                return;

            await InitializeSystemsAsync();
            await PreloadAssetsAsync();
            await GenerateMapAsync();
            await SetupGameplayAsync();
            await FinalizeInitializationAsync();

            _isInitialized = true;
        }

        private async UniTask InitializeSystemsAsync()
        {
            if (_assetService is AddressableAssetService addressableAssetService)
            {
                await addressableAssetService.InitializeAsync();
            }

            if (_performanceManager != null && Application.isMobilePlatform)
            {
                _performanceManager.EnableMobileMode(true);
            }

            await UniTask.Delay(100);
        }

        private async UniTask PreloadAssetsAsync()
        {
            var essentialAssets = new List<string>
            {
                _mapAssetKey,
                "WaterTile",
                "WaterBackground"
            };

            // Add terrain tile variants
            for (int i = 1; i <= 7; i++)
            {
                essentialAssets.Add($"TerrainTile{i:00}");
            }

            // Add decoration prefabs
            var decorationKeys = new[]
            {
                "Grass01", "Grass02", "Hut", "Palm", "Plant01",
                "Rock01", "Rock02", "Rock03", "RockSet01", "RockSet02",
                "RockSet03", "Vegetation01", "Vegetation02"
            };
            essentialAssets.AddRange(decorationKeys);

            if (_assetService is AddressableAssetService addressableAssetService)
            {
                await addressableAssetService.PreloadAssetsAsync(essentialAssets.ToArray());
            }
        }

        private async UniTask GenerateMapAsync()
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

            // No need to call AddRandomVegetationAndRocks as it's now handled in GenerateMapAsync
        }

        private async UniTask<HexTile[,]> GenerateMapWithProgressAsync()
        {
            var tiles = await _mapGenerator.GenerateMapAsync(_mapAssetKey);

            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);
            var processedTiles = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    processedTiles++;

                    if (processedTiles % _maxTilesPerFrame == 0)
                    {
                        await UniTask.NextFrame();
                    }
                }
            }

            return tiles;
        }

        private async UniTask SetupGameplayAsync()
        {
            if (!_hexGridManager.IsWalkable(_boatStartPosition))
            {
                _boatStartPosition = FindValidStartPosition();
            }

            _boatController.SetPosition(_boatStartPosition);
            _cameraController.FollowTarget(_boatController.Transform);
            _eventManager.OnHexClicked += OnHexClicked;

            await UniTask.Delay(100);
        }

        private async UniTask FinalizeInitializationAsync()
        {
            System.GC.Collect();
            await UniTask.Delay(100);
        }

        private HexCoordinate FindValidStartPosition()
        {
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
                            return testPos;
                        }
                    }
                }
            }

            return new HexCoordinate(0, 0);
        }

        private void OnHexClicked(Vector2Int hexOffset, Vector3 worldPosition)
        {
            var targetHex = HexCoordinate.FromOffset(hexOffset.x, hexOffset.y);
    
            if (!_hexGridManager.IsWalkable(targetHex))
                return;

            // Cancel current movement
            _boatController.CancelCurrentMovement();
    
            // Calculate path from current hex
            var startHex = _boatController.GetCurrentHexFromPosition();
            var path = _pathfinder.FindPath(startHex, targetHex);
    
            if (path.Length > 0)
            {
                // Show path
                _eventManager.TriggerPathCalculated(path);
        
                // Start movement
                _boatController.MoveToAsync(path).Forget();
            }
        }

        private async UniTaskVoid ProcessNewPathRequest(HexCoordinate targetHex)
        {
            try
            {
                // Wait for movement cancellation to complete
                await UniTask.Delay(100);

                // Ensure boat is on a valid hex
                await EnsureBoatOnValidHex();

                // Calculate new path
                CalculateAndExecutePath(targetHex);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Path request error: {ex.Message}");
            }
        }

        private async UniTask EnsureBoatOnValidHex()
        {
            try
            {
                if (_boatController?.Transform == null) return;

                var currentWorldPos = _boatController.Transform.position;
                var detectedHex = _hexGridManager.WorldToHex(currentWorldPos);

                // If current detected hex is not walkable, find nearest walkable one
                if (!_hexGridManager.IsWalkable(detectedHex))
                {
                    var nearestWalkable = FindNearestWalkableHex(currentWorldPos);
                    if (nearestWalkable.HasValue)
                    {
                        detectedHex = nearestWalkable.Value;
                    }
                    else
                    {
                        Debug.LogError("Cannot find walkable hex near boat position!");
                        return;
                    }
                }

                // Update boat's current hex using proper method
                _boatController.UpdateCurrentHex(detectedHex);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"EnsureBoatOnValidHex error: {ex.Message}");
            }
        }

        private HexCoordinate? FindNearestWalkableHex(Vector3 worldPosition)
        {
            var centerHex = _hexGridManager.WorldToHex(worldPosition);

            // Check center first
            if (_hexGridManager.IsWalkable(centerHex))
                return centerHex;

            // Spiral search
            for (int radius = 1; radius <= 5; radius++)
            {
                var neighbors = GetHexRing(centerHex, radius);
                foreach (var hex in neighbors)
                {
                    if (_hexGridManager.IsWalkable(hex))
                        return hex;
                }
            }

            return null;
        }

        private HexCoordinate[] GetHexRing(HexCoordinate center, int radius)
        {
            if (radius == 0) return new[] { center };

            var results = new List<HexCoordinate>();
            var hex = new HexCoordinate(center.Q - radius, center.R + radius);

            var directions = new[]
            {
                new HexCoordinate(1, 0), new HexCoordinate(1, -1), new HexCoordinate(0, -1),
                new HexCoordinate(-1, 0), new HexCoordinate(-1, 1), new HexCoordinate(0, 1)
            };

            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < radius; j++)
                {
                    results.Add(hex);
                    hex = new HexCoordinate(hex.Q + directions[i].Q, hex.R + directions[i].R);
                }
            }

            return results.ToArray();
        }

        private void CalculateAndExecutePath(HexCoordinate targetHex)
        {
            var startHex = _boatController.CurrentHex;
            var path = _pathfinder.FindPath(startHex, targetHex);

            Debug.Log($"Path calculated: {path.Length} steps from {startHex} to {targetHex}");

            if (path.Length > 0)
            {
                // Trigger path calculated event for PathRenderer
                _eventManager.TriggerPathCalculated(path);

                // Start movement
                _boatController.MoveToAsync(path).Forget();
            }
            else
            {
                Debug.LogWarning($"No valid path found from {startHex} to {targetHex}");
            }
        }

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

        private void OnDestroy()
        {
            if (_eventManager != null)
            {
                _eventManager.OnHexClicked -= OnHexClicked;
            }

            if (_assetService is AddressableAssetService addressableAssetService)
            {
                addressableAssetService.ClearCache();
            }
        }
    }
}