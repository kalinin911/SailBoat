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

        public MapGenerator(IHexGridManager hexGridManager, DiContainer container, IAssetService assetService)
        {
            _hexGridManager = hexGridManager;
            _container = container;
            _assetService = assetService;
        }

        public async UniTask<HexTile[,]> GenerateMapAsync(string mapAssetKey)
        {
            try
            {
                // Try to load map text asset
                var mapTextAsset = await _assetService.LoadAssetAsync<TextAsset>(mapAssetKey);
                
                if (mapTextAsset == null)
                {
                    Debug.LogWarning($"Could not load map asset {mapAssetKey}, generating default map");
                    return await GenerateDefaultMapAsync();
                }

                var mapData = ParseMapData(mapTextAsset.text);
                return await CreateTilesFromDataAsync(mapData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating map: {ex.Message}");
                return await GenerateDefaultMapAsync();
            }
        }

        private async UniTask<HexTile[,]> GenerateDefaultMapAsync()
        {
            // Create a simple 8x8 map with water around edges
            var mapData = new int[8, 8];
            
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    // Water on edges, terrain in middle
                    if (x == 0 || x == 7 || y == 0 || y == 7)
                        mapData[x, y] = 0; // Water
                    else
                        mapData[x, y] = 1; // Terrain
                }
            }
            
            return await CreateTilesFromDataAsync(mapData);
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

            GameObject tileObj = await CreateTileGameObjectAsync(tileType);
            
            if (tileObj == null)
            {
                // Create simple cube as fallback
                tileObj = CreateFallbackTile(tileType);
            }

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
            
            try
            {
                var tilePrefab = await _assetService.LoadAssetAsync<GameObject>(prefabKey);
                if (tilePrefab != null)
                {
                    return _container.InstantiatePrefab(tilePrefab);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not load prefab {prefabKey}: {ex.Message}");
            }
            
            return null;
        }

        private GameObject CreateFallbackTile(TileType tileType)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);
            
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = tileType == TileType.Water ? Color.blue : Color.gray;
            }
            
            cube.name = $"{tileType}Tile_Fallback";
            return cube;
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
            
            try
            {
                var obstaclePrefab = await _assetService.LoadAssetAsync<GameObject>(randomKey);
                
                if (obstaclePrefab != null)
                {
                    var obstacleObj = _container.InstantiatePrefab(obstaclePrefab);
                    obstacleObj.transform.position = tile.transform.position + Vector3.up * 0.1f;
                    obstacleObj.transform.SetParent(tile.transform);
                }
                else
                {
                    // Create fallback obstacle
                    CreateFallbackObstacle(tile, randomKey);
                }
                
                tile.SetObstacle(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not create obstacle {randomKey}: {ex.Message}");
                CreateFallbackObstacle(tile, randomKey);
                tile.SetObstacle(true);
            }
        }

        private void CreateFallbackObstacle(HexTile tile, string obstacleType)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obstacle.transform.position = tile.transform.position + Vector3.up * 0.2f;
            obstacle.transform.localScale = Vector3.one * 0.3f;
            obstacle.transform.SetParent(tile.transform);
            
            var renderer = obstacle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = obstacleType == "Rock" ? Color.gray : Color.green;
            }
            
            obstacle.name = $"{obstacleType}_Fallback";
        }

        private int[,] ParseMapData(string mapText)
        {
            try
            {
                var lines = mapText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var height = lines.Length;
                var width = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                
                var mapData = new int[width, height];
                
                for (int y = 0; y < height; y++)
                {
                    var values = lines[y].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int x = 0; x < width && x < values.Length; x++)
                    {
                        if (int.TryParse(values[x], out var value))
                        {
                            mapData[x, y] = value;
                        }
                        else
                        {
                            mapData[x, y] = 0; // Default to water
                        }
                    }
                }
                
                return mapData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing map data: {ex.Message}");
                throw;
            }
        }
    }
}