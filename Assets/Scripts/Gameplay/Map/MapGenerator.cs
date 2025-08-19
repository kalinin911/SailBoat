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
        private readonly IHexGridManager _hexGridManager;
        private readonly DiContainer _container;
        private readonly IAssetService _assetService;

        // Background management
        private readonly string _backgroundAssetKey = "WaterBackground";

        public MapGenerator(IHexGridManager hexGridManager, DiContainer container, IAssetService assetService)
        {
            _hexGridManager = hexGridManager;
            _container = container;
            _assetService = assetService;
        }

        public async UniTask<HexTile[,]> GenerateMapAsync(string mapAssetKey)
        {
            var mapTextAsset = await _assetService.LoadAssetAsync<TextAsset>(mapAssetKey);

            if (mapTextAsset == null)
            {
                throw new InvalidOperationException($"Could not load map asset: {mapAssetKey}");
            }

            var mapData = ParseMapData(mapTextAsset.text);

            // Create background before generating tiles
            await CreateMapBackgroundAsync(mapData);

            return await CreateTilesFromDataAsync(mapData);
        }

        private async UniTask CreateMapBackgroundAsync(int[,] mapData)
        {
            try
            {
                // Load background prefab
                var backgroundPrefab = await _assetService.LoadAssetAsync<GameObject>(_backgroundAssetKey);

                if (backgroundPrefab == null)
                {
                    Debug.LogWarning($"Could not load background prefab: {_backgroundAssetKey}");
                    return;
                }

                // Instantiate background
                var background = _container.InstantiatePrefab(backgroundPrefab);
                background.name = $"MapBackground_{DateTime.Now.Ticks}";

                // Calculate map bounds
                var mapBounds = CalculateMapBounds(mapData);

                // Position and scale background to cover the entire map
                PositionAndScaleBackground(background, mapBounds);

                Debug.Log($"Background created and positioned for map bounds: {mapBounds}");
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

            // Calculate world positions for corners using proper hex grid layout
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;

            // Sample corners and some middle points to get accurate bounds
            var samplePoints = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(width - 1, 0),
                new Vector2Int(0, height - 1),
                new Vector2Int(width - 1, height - 1),
                new Vector2Int(width / 2, height / 2)
            };

            foreach (var point in samplePoints)
            {
                var worldPos = HexCoordinate.FromOffset(point.x, point.y).ToWorldPosition(hexSize);
                minX = Mathf.Min(minX, worldPos.x);
                maxX = Mathf.Max(maxX, worldPos.x);
                minZ = Mathf.Min(minZ, worldPos.z);
                maxZ = Mathf.Max(maxZ, worldPos.z);
            }

            // Add padding
            var padding = hexSize * 1.5f;
            minX -= padding;
            maxX += padding;
            minZ -= padding;
            maxZ += padding;

            var center = new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f);
            var size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);

            return new Bounds(center, size);
        }

        private void PositionAndScaleBackground(GameObject background, Bounds mapBounds)
        {
            // Position at map center, slightly below the tiles
            var backgroundPosition = mapBounds.center;
            backgroundPosition.y = -0.1f; // Place below tiles
            background.transform.position = backgroundPosition;

            // Set scale directly, don't multiply with original scale
            var scaleMultiplier = CalculateScaleMultiplier(background, mapBounds);
    
            background.transform.localScale = scaleMultiplier; // Direct assignment, not multiplication

            Debug.Log($"Background positioned at {backgroundPosition} with scale {background.transform.localScale}");
        }

        private Vector3 CalculateScaleMultiplier(GameObject background, Bounds mapBounds)
        {
            var mapWidth = mapBounds.size.x;
            var mapDepth = mapBounds.size.z;
    
            Debug.Log($"Map bounds: {mapWidth} x {mapDepth}");
    
            // Scale X and Y, keep Z at 1
            var scaleX = mapWidth;
            var scaleY = mapDepth;  // This should scale Y, not Z
            var scaleZ = 1f;

            Debug.Log($"Applying scale: X={scaleX}, Y={scaleY}, Z={scaleZ}");

            return new Vector3(scaleX, scaleY, scaleZ);
        }

        private async UniTask<HexTile[,]> CreateTilesFromDataAsync(int[,] mapData)
        {
            var width = mapData.GetLength(0);
            var height = mapData.GetLength(1);
            var tiles = new HexTile[width, height];

            var tasks = new List<UniTask>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    tasks.Add(CreateTileAsync(x, y, mapData[x, y], tiles));
                }
            }

            await UniTask.WhenAll(tasks);
            return tiles;
        }

        private async UniTask CreateTileAsync(int x, int y, int tileValue, HexTile[,] tiles)
        {
            var hexCoord = HexCoordinate.FromOffset(x, y);
            var worldPos = _hexGridManager.HexToWorld(hexCoord);
            var tileType = (TileType)tileValue;

            var tileObj = await CreateTileGameObjectAsync(tileType);
            tileObj.transform.position = worldPos;

            var hexTile = tileObj.GetComponent<HexTile>();
            if (hexTile == null)
                hexTile = tileObj.AddComponent<HexTile>();

            hexTile.Initialize(hexCoord, tileType);
            tiles[x, y] = hexTile;

            _hexGridManager.RegisterHexTile(hexCoord, hexTile);
        }

        private async UniTask<GameObject> CreateTileGameObjectAsync(TileType tileType)
        {
            string prefabKey = tileType == TileType.Water ? "WaterTile" : "TerrainTile";

            var tilePrefab = await _assetService.LoadAssetAsync<GameObject>(prefabKey);
            if (tilePrefab == null)
            {
                throw new InvalidOperationException($"Could not load tile prefab: {prefabKey}");
            }

            return _container.InstantiatePrefab(tilePrefab);
        }

        public void AddRandomVegetationAndRocks(HexTile[,] tiles)
        {
            var width = tiles.GetLength(0);
            var height = tiles.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tile = tiles[x, y];
                    if (tile.TileType == TileType.Terrain && UnityEngine.Random.value < 0.3f)
                    {
                        AddRandomObstacleAsync(tile).Forget();
                    }
                }
            }
        }

        private async UniTaskVoid AddRandomObstacleAsync(HexTile tile)
        {
            string[] obstacleKeys = { "Rock", "Vegetation" };
            var randomKey = obstacleKeys[UnityEngine.Random.Range(0, obstacleKeys.Length)];

            var obstaclePrefab = await _assetService.LoadAssetAsync<GameObject>(randomKey);

            if (obstaclePrefab == null)
            {
                Debug.LogWarning($"Could not load obstacle prefab: {randomKey}");
                return;
            }

            var obstacleObj = _container.InstantiatePrefab(obstaclePrefab);
            obstacleObj.transform.position = tile.transform.position + Vector3.up * 0.1f;
            obstacleObj.transform.SetParent(tile.transform);

            tile.SetObstacle(true);
        }

        private int[,] ParseMapData(string mapText)
        {
            var lines = mapText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var height = lines.Length;
            var width = lines[0].Length;

            var mapData = new int[width, height];

            for (int y = 0; y < height; y++)
            {
                var line = lines[y].Trim();
                for (int x = 0; x < width && x < line.Length; x++)
                {
                    if (char.IsDigit(line[x]))
                    {
                        mapData[x, y] = line[x] - '0';
                    }
                    else
                    {
                        mapData[x, y] = 0; // Default to water for invalid chars
                    }
                }
            }

            return mapData;
        }
    }
}