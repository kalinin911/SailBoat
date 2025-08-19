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
            var mapTextAsset = await _assetService.LoadAssetAsync<TextAsset>(mapAssetKey);
            
            if (mapTextAsset == null)
            {
                throw new InvalidOperationException($"Could not load map asset: {mapAssetKey}");
            }

            var mapData = ParseMapData(mapTextAsset.text);
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