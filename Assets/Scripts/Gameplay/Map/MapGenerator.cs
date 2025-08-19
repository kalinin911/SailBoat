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
            await CreateMapBackgroundAsync(mapData);
            return await CreateTilesFromDataAsync(mapData);
        }

        private async UniTask CreateMapBackgroundAsync(int[,] mapData)
        {
            try
            {
                var backgroundPrefab = await _assetService.LoadAssetAsync<GameObject>(_backgroundAssetKey);

                if (backgroundPrefab == null)
                {
                    Debug.LogWarning($"Could not load background prefab: {_backgroundAssetKey}");
                    return;
                }

                var background = _container.InstantiatePrefab(backgroundPrefab);
                background.name = $"MapBackground_{DateTime.Now.Ticks}";

                var mapBounds = CalculateMapBounds(mapData);
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

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;

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
            var backgroundPosition = mapBounds.center;
            backgroundPosition.y = -0.1f;
            background.transform.position = backgroundPosition;

            var scaleMultiplier = CalculateScaleMultiplier(background, mapBounds);
            background.transform.localScale = scaleMultiplier;

            Debug.Log($"Background positioned at {backgroundPosition} with scale {background.transform.localScale}");
        }

        private Vector3 CalculateScaleMultiplier(GameObject background, Bounds mapBounds)
        {
            var mapWidth = mapBounds.size.x;
            var mapDepth = mapBounds.size.z;

            Debug.Log($"Map bounds: {mapWidth} x {mapDepth}");

            var scaleX = mapWidth;
            var scaleY = mapDepth;
            var scaleZ = 1f;

            Debug.Log($"Applying scale: X={scaleX}, Y={scaleY}, Z={scaleZ}");

            return new Vector3(scaleX, scaleY, scaleZ);
        }

        private async UniTask<HexTile[,]> CreateTilesFromDataAsync(int[,] mapData)
        {
            var width = mapData.GetLength(0);
            var height = mapData.GetLength(1);
            var totalTiles = width * height;
            var tiles = new HexTile[width, height];

            Debug.Log($"Creating {totalTiles} tiles in batches");

            var tilesPerBatch = 50;
            var processedTiles = 0;

            for (int x = 0; x < width; x++)
            {
                var batchTasks = new List<UniTask>();

                for (int y = 0; y < height; y++)
                {
                    batchTasks.Add(CreateTileAsync(x, y, mapData[x, y], tiles));
                    
                    if (batchTasks.Count >= tilesPerBatch)
                    {
                        await UniTask.WhenAll(batchTasks);
                        processedTiles += batchTasks.Count;
                        
                        Debug.Log($"Progress: {processedTiles}/{totalTiles} tiles ({(float)processedTiles/totalTiles:P1})");
                        batchTasks.Clear();
                        await UniTask.NextFrame();
                    }
                }

                if (batchTasks.Count > 0)
                {
                    await UniTask.WhenAll(batchTasks);
                    processedTiles += batchTasks.Count;
                    Debug.Log($"Progress: {processedTiles}/{totalTiles} tiles ({(float)processedTiles/totalTiles:P1})");
                    await UniTask.NextFrame();
                }
            }

            Debug.Log($"Tile creation completed: {totalTiles} tiles");
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
            if (tileType == TileType.Water)
            {
                var tilePrefab = await _assetService.LoadAssetAsync<GameObject>("WaterTile");
                if (tilePrefab == null)
                {
                    throw new InvalidOperationException($"Could not load tile prefab: WaterTile");
                }
                return _container.InstantiatePrefab(tilePrefab);
            }
            else
            {
                // Only try to load terrain variants (remove fallback to old TerrainTile key)
                var terrainVariant = UnityEngine.Random.Range(1, 8); // 1 to 7
                var prefabKey = $"TerrainTile{terrainVariant:00}"; // TerrainTile01, TerrainTile02, etc.
                
                var tilePrefab = await _assetService.LoadAssetAsync<GameObject>(prefabKey);
                
                if (tilePrefab == null)
                {
                    throw new InvalidOperationException($"Could not load terrain tile prefab: {prefabKey}. Make sure TerrainTile01-07 are set up in Addressables.");
                }
                
                return _container.InstantiatePrefab(tilePrefab);
            }
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
                        mapData[x, y] = 0;
                    }
                }
            }

            return mapData;
        }
    }
}