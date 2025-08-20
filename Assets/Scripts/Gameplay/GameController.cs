using System.Collections.Generic;
using System.Linq;
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
        [Header("Game Settings")]
        [SerializeField] private string _mapAssetKey = "DefaultMap";

        [Header("Performance Settings")]
        [SerializeField] private bool _enableAsyncLoading = true;
        [SerializeField] private int _maxTilesPerFrame = 10;

        [Inject] private IMapGenerator _mapGenerator;
        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IPathfinder _pathfinder;
        [Inject] private IBoatController _boatController;
        [Inject] private IGameEventManager _eventManager;
        [Inject] private IAssetService _assetService;
        [Inject(Optional = true)] private PerformanceManager _performanceManager;
        
        private bool _isInitialized;
        private HexCoordinate _boatStartPosition;

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
            
            // Position boat early during map generation
            var mapGenerationTask = GenerateMapAsync();
            var boatPositioningTask = SetupGameplayAsync();
            
            await UniTask.WhenAll(mapGenerationTask, boatPositioningTask);
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
            var decorationKeys = new[] {
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
            // Wait for some tiles to be generated
            await WaitForInitialTiles();
            
            // Position boat as soon as possible
            await PositionBoatEarly();
            
            _eventManager.OnHexClicked += OnHexClicked;
        }

        /// <summary>
        /// Waits for initial tiles to be available
        /// </summary>
        private async UniTask WaitForInitialTiles()
        {
            int attempts = 0;
            while (attempts < 50) // Max 5 seconds
            {
                // Check if any tiles are walkable
                for (int q = -10; q <= 10; q++)
                {
                    for (int r = -10; r <= 10; r++)
                    {
                        var testHex = new HexCoordinate(q, r);
                        if (_hexGridManager.IsWalkable(testHex))
                        {
                            Debug.Log($"Initial tiles found after {attempts * 100}ms");
                            return; // Found walkable tiles
                        }
                    }
                }
                
                attempts++;
                await UniTask.Delay(100);
            }
            
            Debug.LogWarning("Timeout waiting for initial tiles");
        }

        /// <summary>
        /// Positions boat quickly using a fast initial scan
        /// </summary>
        private async UniTask PositionBoatEarly()
        {
            // Quick scan around center for immediate positioning
            var quickPosition = FindQuickStartPosition();
            
            if (_hexGridManager.IsWalkable(quickPosition))
            {
                _boatController.SetPosition(quickPosition);
                Debug.Log($"Quick boat position: {quickPosition}");
            }
            else
            {
                // Fallback to simple position
                var fallback = new HexCoordinate(0, 0);
                if (_hexGridManager.IsWalkable(fallback))
                {
                    _boatController.SetPosition(fallback);
                }
            }
            
            // Start refining position in background
            RefineBoatPositionAsync().Forget();
        }

        /// <summary>
        /// Quick scan for immediate boat positioning
        /// </summary>
        private HexCoordinate FindQuickStartPosition()
        {
            // Try center-ish positions first
            var quickCandidates = new HexCoordinate[]
            {
                new HexCoordinate(15, 20),
                new HexCoordinate(10, 15),
                new HexCoordinate(20, 25),
                new HexCoordinate(5, 10),
                new HexCoordinate(25, 30),
                new HexCoordinate(0, 0)
            };

            foreach (var candidate in quickCandidates)
            {
                if (_hexGridManager.IsWalkable(candidate))
                {
                    return candidate;
                }
            }

            return new HexCoordinate(0, 0);
        }

        /// <summary>
        /// Refines boat position to optimal location after map loads
        /// </summary>
        private async UniTaskVoid RefineBoatPositionAsync()
        {
            // Wait for map to be more complete
            await UniTask.Delay(2000);
            
            try
            {
                var optimalPosition = FindOptimalStartPosition();
                
                // Only move if we found a significantly better position
                if (_hexGridManager.IsWalkable(optimalPosition) && 
                    !_boatController.IsMoving)
                {
                    var currentPos = _boatController.CurrentHex;
                    var distance = currentPos.DistanceTo(optimalPosition);
                    
                    if (distance > 5) // Only move if significantly better
                    {
                        Debug.Log($"Refining boat position from {currentPos} to {optimalPosition}");
                        _boatController.SetPosition(optimalPosition);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to refine boat position: {ex.Message}");
            }
        }

        private async UniTask FinalizeInitializationAsync()
        {
            System.GC.Collect();
            await UniTask.Delay(100);
        }

        /// <summary>
        /// Finds the optimal boat starting position using multi-criteria analysis
        /// Considers: map center proximity, open water area, accessibility, distance from edges
        /// </summary>
        private HexCoordinate FindOptimalStartPosition()
        {
            var walkableTiles = ScanForWalkableTiles();
            
            if (walkableTiles.Count == 0)
            {
                Debug.LogError("No walkable tiles found on map!");
                return new HexCoordinate(0, 0);
            }

            var mapCenter = CalculateMapCenter(walkableTiles);
            var bestPosition = EvaluatePositions(walkableTiles, mapCenter);
            
            Debug.Log($"Optimal boat position: {bestPosition} (evaluated {walkableTiles.Count} positions)");
            return bestPosition;
        }

        /// <summary>
        /// Scans the map systematically to find all walkable tiles
        /// </summary>
        private List<HexCoordinate> ScanForWalkableTiles()
        {
            var walkableTiles = new List<HexCoordinate>();
            
            // Scan in expanding rings from origin to efficiently cover the map
            for (int radius = 0; radius <= 60; radius++)
            {
                var ringTiles = GetHexRing(new HexCoordinate(0, 0), radius);
                
                foreach (var hex in ringTiles)
                {
                    if (_hexGridManager.IsWalkable(hex))
                    {
                        walkableTiles.Add(hex);
                    }
                }
                
                // Early exit if we haven't found tiles in recent rings (reached map edge)
                if (radius > 10 && walkableTiles.Count > 0)
                {
                    var recentTiles = ringTiles.Count(hex => _hexGridManager.IsWalkable(hex));
                    if (recentTiles == 0 && radius > 30) break;
                }
            }
            
            return walkableTiles;
        }

        /// <summary>
        /// Calculates the geometric center of all walkable tiles
        /// </summary>
        private Vector2 CalculateMapCenter(List<HexCoordinate> walkableTiles)
        {
            if (walkableTiles.Count == 0) return Vector2.zero;
            
            float avgQ = walkableTiles.Average(h => (float)h.Q);
            float avgR = walkableTiles.Average(h => (float)h.R);
            
            return new Vector2(avgQ, avgR);
        }

        /// <summary>
        /// Evaluates all positions and returns the best one based on multiple criteria
        /// </summary>
        private HexCoordinate EvaluatePositions(List<HexCoordinate> candidates, Vector2 mapCenter)
        {
            var scoredPositions = new List<(HexCoordinate hex, float score)>();
            
            // Calculate bounds for edge distance scoring
            var minQ = candidates.Min(h => h.Q);
            var maxQ = candidates.Max(h => h.Q);
            var minR = candidates.Min(h => h.R);
            var maxR = candidates.Max(h => h.R);
            
            foreach (var candidate in candidates)
            {
                var score = CalculatePositionScore(candidate, mapCenter, minQ, maxQ, minR, maxR);
                scoredPositions.Add((candidate, score));
            }
            
            // Return position with highest score
            return scoredPositions.OrderByDescending(p => p.score).First().hex;
        }

        /// <summary>
        /// Calculates a comprehensive score for a position based on multiple factors
        /// </summary>
        private float CalculatePositionScore(HexCoordinate position, Vector2 mapCenter, int minQ, int maxQ, int minR, int maxR)
        {
            float score = 0f;
            
            // 1. Distance from map center (closer = better) - Weight: 40%
            var distanceFromCenter = Vector2.Distance(new Vector2(position.Q, position.R), mapCenter);
            var centerScore = Mathf.Max(0, 20f - distanceFromCenter) / 20f;
            score += centerScore * 0.4f;
            
            // 2. Distance from map edges (further = better) - Weight: 30%
            var edgeDistanceQ = Mathf.Min(position.Q - minQ, maxQ - position.Q);
            var edgeDistanceR = Mathf.Min(position.R - minR, maxR - position.R);
            var minEdgeDistance = Mathf.Min(edgeDistanceQ, edgeDistanceR);
            var edgeScore = Mathf.Clamp01(minEdgeDistance / 10f);
            score += edgeScore * 0.3f;
            
            // 3. Open water area around position (more open = better) - Weight: 20%
            var openAreaScore = CalculateOpenAreaScore(position);
            score += openAreaScore * 0.2f;
            
            // 4. Accessibility (can reach many other tiles) - Weight: 10%
            var accessibilityScore = CalculateAccessibilityScore(position);
            score += accessibilityScore * 0.1f;
            
            return score;
        }

        /// <summary>
        /// Calculates how much open water surrounds a position
        /// </summary>
        private float CalculateOpenAreaScore(HexCoordinate position)
        {
            int walkableCount = 0;
            int totalChecked = 0;
            
            // Check in 3-hex radius around position
            for (int radius = 1; radius <= 3; radius++)
            {
                var ring = GetHexRing(position, radius);
                foreach (var hex in ring)
                {
                    totalChecked++;
                    if (_hexGridManager.IsWalkable(hex))
                        walkableCount++;
                }
            }
            
            return totalChecked > 0 ? (float)walkableCount / totalChecked : 0f;
        }

        /// <summary>
        /// Calculates how many tiles can be reached from this position (basic connectivity)
        /// </summary>
        private float CalculateAccessibilityScore(HexCoordinate position)
        {
            int reachableCount = 0;
            var visited = new HashSet<HexCoordinate>();
            var queue = new Queue<HexCoordinate>();
            
            queue.Enqueue(position);
            visited.Add(position);
            
            // BFS to count reachable tiles (limited to prevent performance issues)
            while (queue.Count > 0 && reachableCount < 100)
            {
                var current = queue.Dequeue();
                reachableCount++;
                
                foreach (var neighbor in current.GetNeighbors())
                {
                    if (!visited.Contains(neighbor) && _hexGridManager.IsWalkable(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            // Normalize to 0-1 range (100 reachable tiles = perfect score)
            return Mathf.Clamp01(reachableCount / 100f);
        }

        /// <summary>
        /// Gets all hexes in a ring at specified radius from center
        /// </summary>
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

        private HexCoordinate FindValidStartPosition()
        {
            var searchRadius = 5;
            var originalPos = new HexCoordinate(0, 0);

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
        
        public bool IsInitialized => _isInitialized;
        public HexCoordinate BoatPosition => _boatController.CurrentHex;

        public void SetMapAsset(string mapAssetKey)
        {
            _mapAssetKey = mapAssetKey;
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