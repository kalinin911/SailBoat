using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.HexGrid;
using Data;
using Infrastructure.Services;
using Zenject;

namespace Gameplay.Map
{
    public class MapGenerator : IMapGenerator
    {
        private const string BACKGROUND_ASSET_KEY = "WaterBackground";
        private const float DECORATION_SPAWN_CHANCE = 0.3f;
        private const int TILES_PER_BATCH = 50;

        private readonly IHexGridManager _hexGridManager;
        private readonly DiContainer _container;
        private readonly IAssetService _assetService;

        private readonly string[] _decorationKeys = {
            "Grass01", "Grass02", "Hut", "Palm", "Plant01",
            "Rock01", "Rock02", "Rock03", "RockSet01", "RockSet02",
            "RockSet03", "Vegetation01", "Vegetation02"
        };

        private readonly Dictionary<string, GameObject> _decorationCache = new Dictionary<string, GameObject>();
        private bool _decorationsPreloaded;

        public MapGenerator(IHexGridManager hexGridManager, DiContainer container, IAssetService assetService)
        {
            _hexGridManager = hexGridManager ?? throw new ArgumentNullException(nameof(hexGridManager));
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
        }

        public async UniTask<HexTile[,]> GenerateMapAsync(string mapAssetKey)
        {
            try
            {
                if (!_decorationsPreloaded)
                {
                    await PreloadDecorationsAsync();
                    _decorationsPreloaded = true;
                }

                var mapTextAsset = await _assetService.LoadAssetAsync<TextAsset>(mapAssetKey);
                ValidateMapAsset(mapTextAsset, mapAssetKey);

                var mapData = ParseMapData(mapTextAsset.text);
                await CreateMapBackgroundAsync(mapData);
                return await CreateTilesFromDataAsync(mapData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Map generation failed: {ex.Message}");
                throw;
            }
        }

        private void ValidateMapAsset(TextAsset mapTextAsset, string mapAssetKey)
        {
            if (mapTextAsset == null)
                throw new InvalidOperationException($"Could not load map asset: {mapAssetKey}");
            
            if (string.IsNullOrEmpty(mapTextAsset.text))
                throw new InvalidOperationException($"Map asset {mapAssetKey} contains no data");
        }

        private async UniTask PreloadDecorationsAsync()
        {
            var preloadTasks = new List<UniTask<GameObject>>();

            foreach (var key in _decorationKeys)
            {
                preloadTasks.Add(_assetService.LoadAssetAsync<GameObject>(key));
            }

            var loadedPrefabs = await UniTask.WhenAll(preloadTasks);

            for (int i = 0; i < _decorationKeys.Length; i++)
            {
                var key = _decorationKeys[i];
                var prefab = loadedPrefabs[i];
                
                if (prefab != null)
                {
                    _decorationCache[key] = prefab;
                }
                else
                {
                    Debug.LogWarning($"Failed to preload decoration prefab: {key}");
                }
            }

            Debug.Log($"Preloaded {_decorationCache.Count}/{_decorationKeys.Length} decoration prefabs");
        }

        private async UniTask CreateMapBackgroundAsync(int[,] mapData)
        {
            try
            {
                var backgroundPrefab = await _assetService.LoadAssetAsync<GameObject>(BACKGROUND_ASSET_KEY);
                
                if (backgroundPrefab == null)
                {
                    Debug.LogWarning($"Could not load background prefab: {BACKGROUND_ASSET_KEY}");
                    return;
                }

                var background = _container.InstantiatePrefab(backgroundPrefab);
                background.name = $"MapBackground_{DateTime.Now.Ticks}";

                var mapBounds = CalculateMapBounds(mapData);
                PositionAndScaleBackground(background, mapBounds);

                Debug.Log($"Background created for map bounds: {mapBounds}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create map background: {ex.Message}");
            }
        }

        private Bounds CalculateMapBounds(int[,] mapData)
        {
            var width = mapData.GetLength(0);
            var height = mapData.GetLength(1);
            var hexSize = _hexGridManager.GetHexSize();

            var samplePoints = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(width - 1, 0),
                new Vector2Int(0, height - 1),
                new Vector2Int(width - 1, height - 1),
                new Vector2Int(width / 2, height / 2)
            };

            var bounds = new Bounds();
            bool boundsInitialized = false;

            foreach (var point in samplePoints)
            {
                var worldPos = HexCoordinate.FromOffset(point.x, point.y).ToWorldPosition(hexSize);
                
                if (!boundsInitialized)
                {
                    bounds = new Bounds(worldPos, Vector3.zero);
                    boundsInitialized = true;
                }
                else
                {
                    bounds.Encapsulate(worldPos);
                }
            }

            var padding = hexSize * 1.5f;
            bounds.Expand(padding * 2f);

            return bounds;
        }

        private void PositionAndScaleBackground(GameObject background, Bounds mapBounds)
        {
            var backgroundPosition = mapBounds.center;
            backgroundPosition.y = -0.1f;
            background.transform.position = backgroundPosition;

            var scale = new Vector3(mapBounds.size.x, mapBounds.size.z, 1f);
            background.transform.localScale = scale;

            Debug.Log($"Background positioned at {backgroundPosition} with scale {scale}");
        }

        private async UniTask<HexTile[,]> CreateTilesFromDataAsync(int[,] mapData)
        {
            var width = mapData.GetLength(0);
            var height = mapData.GetLength(1);
            var totalTiles = width * height;
            var tiles = new HexTile[width, height];

            Debug.Log($"Creating {totalTiles} tiles in batches of {TILES_PER_BATCH}");

            var processedTiles = 0;
            var batchTasks = new List<UniTask>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    batchTasks.Add(CreateTileAsync(x, y, mapData[x, y], tiles));
                    
                    if (batchTasks.Count >= TILES_PER_BATCH)
                    {
                        await UniTask.WhenAll(batchTasks);
                        processedTiles += batchTasks.Count;
                        
                        LogProgress(processedTiles, totalTiles);
                        batchTasks.Clear();
                        await UniTask.NextFrame();
                    }
                }
            }

            if (batchTasks.Count > 0)
            {
                await UniTask.WhenAll(batchTasks);
                processedTiles += batchTasks.Count;
                LogProgress(processedTiles, totalTiles);
            }

            Debug.Log($"Tile creation completed: {totalTiles} tiles");
            return tiles;
        }

        private void LogProgress(int processed, int total)
        {
            var percentage = (float)processed / total;
            Debug.Log($"Progress: {processed}/{total} tiles ({percentage:P1})");
        }

        private async UniTask CreateTileAsync(int x, int y, int tileValue, HexTile[,] tiles)
        {
            var hexCoord = HexCoordinate.FromOffset(x, y);
            
            if (_hexGridManager.GetHexTile(hexCoord) != null)
            {
                tiles[x, y] = _hexGridManager.GetHexTile(hexCoord);
                return;
            }

            var tileType = (TileType)tileValue;
            var tileObj = await CreateTileGameObjectAsync(tileType);
            
            PositionTile(tileObj, hexCoord);
            var hexTile = SetupHexTileComponent(tileObj, hexCoord, tileType);
            
            tiles[x, y] = hexTile;
            _hexGridManager.RegisterHexTile(hexCoord, hexTile);

            if (ShouldAddDecoration(tileType))
            {
                await AddRandomDecorationAsync(hexTile);
            }
        }

        private void PositionTile(GameObject tileObj, HexCoordinate hexCoord)
        {
            var worldPos = _hexGridManager.HexToWorld(hexCoord);
            tileObj.transform.position = worldPos;
        }

        private HexTile SetupHexTileComponent(GameObject tileObj, HexCoordinate hexCoord, TileType tileType)
        {
            var hexTile = tileObj.GetComponent<HexTile>();
            
            if (hexTile == null)
            {
                hexTile = tileObj.AddComponent<HexTile>();
            }

            hexTile.Initialize(hexCoord, tileType);
            return hexTile;
        }

        private bool ShouldAddDecoration(TileType tileType)
        {
            return tileType == TileType.Terrain && UnityEngine.Random.value < DECORATION_SPAWN_CHANCE;
        }

        private async UniTask<GameObject> CreateTileGameObjectAsync(TileType tileType)
        {
            string prefabKey = GetTilePrefabKey(tileType);
            var tilePrefab = await _assetService.LoadAssetAsync<GameObject>(prefabKey);
            
            if (tilePrefab == null)
                throw new InvalidOperationException($"Could not load tile prefab: {prefabKey}");
            
            return _container.InstantiatePrefab(tilePrefab);
        }

        private string GetTilePrefabKey(TileType tileType)
        {
            if (tileType == TileType.Water)
            {
                return "WaterTile";
            }
            else
            {
                var terrainVariant = UnityEngine.Random.Range(1, 8);
                return $"TerrainTile{terrainVariant:00}";
            }
        }

        private async UniTask AddRandomDecorationAsync(HexTile tile)
        {
            if (_decorationCache.Count == 0)
                return;

            var randomKey = _decorationKeys[UnityEngine.Random.Range(0, _decorationKeys.Length)];
            
            if (!_decorationCache.TryGetValue(randomKey, out var decorationPrefab))
            {
                decorationPrefab = await _assetService.LoadAssetAsync<GameObject>(randomKey);
                if (decorationPrefab == null)
                {
                    Debug.LogWarning($"Could not load decoration prefab: {randomKey}");
                    return;
                }
            }

            var decorationObj = _container.InstantiatePrefab(decorationPrefab);
            PositionDecoration(decorationObj, tile);
            
            tile.SetObstacle(true);
        }

        private void PositionDecoration(GameObject decorationObj, HexTile tile)
        {
            decorationObj.transform.position = tile.transform.position + Vector3.up * 0.1f;
            decorationObj.transform.SetParent(tile.transform);
        }

        private int[,] ParseMapData(string mapText)
        {
            if (string.IsNullOrEmpty(mapText))
                throw new ArgumentException("Map text cannot be null or empty");

            var lines = mapText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length == 0)
                throw new ArgumentException("Map text contains no valid lines");

            var height = lines.Length;
            var width = lines[0].Length;
            var mapData = new int[width, height];

            for (int y = 0; y < height; y++)
            {
                var line = lines[y].Trim();
                for (int x = 0; x < width && x < line.Length; x++)
                {
                    mapData[x, y] = char.IsDigit(line[x]) ? line[x] - '0' : 0;
                }
            }

            Debug.Log($"Parsed map data: {width}x{height}");
            return mapData;
        }
    }
}