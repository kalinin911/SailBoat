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
            // Load map text asset
            var mapTextAsset = await _assetService.LoadAssetAsync<TextAsset>(mapAssetKey);
            var mapData = ParseMapData(mapTextAsset.text);
            
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

            string prefabKey = tileType == TileType.Water ? "WaterTile" : "TerrainTile";
            var tilePrefab = await _assetService.LoadAssetAsync<GameObject>(prefabKey);
            var tileObj = _container.InstantiatePrefab(tilePrefab);
            
            tileObj.transform.position = worldPos;
            
            var hexTile = tileObj.GetComponent<HexTile>();
            if (hexTile == null)
                hexTile = tileObj.AddComponent<HexTile>();
                
            hexTile.Initialize(hexCoord, tileType);
            tiles[x, y] = hexTile;
            
            _hexGridManager.RegisterHexTile(hexCoord, hexTile);
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
                        AddRandomObstacle(tile);
                    }
                }
            }
        }

        private async void AddRandomObstacle(HexTile tile)
        {
            string[] obstacleKeys = { "Rock", "Vegetation" };
            var randomKey = obstacleKeys[UnityEngine.Random.Range(0, obstacleKeys.Length)];
            
            var obstaclePrefab = await _assetService.LoadAssetAsync<GameObject>(randomKey);
            var obstacleObj = _container.InstantiatePrefab(obstaclePrefab);
            obstacleObj.transform.position = tile.transform.position;
            obstacleObj.transform.SetParent(tile.transform);
            
            tile.SetObstacle(true);
        }

        private int[,] ParseMapData(string mapText)
        {
            var lines = mapText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var height = lines.Length;
            var width = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            
            var mapData = new int[width, height];
            
            for (int y = 0; y < height; y++)
            {
                var values = lines[y].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int x = 0; x < width; x++)
                {
                    mapData[x, y] = int.Parse(values[x]);
                }
            }
            
            return mapData;
        }
    }
}