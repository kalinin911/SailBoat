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
        [SerializeField] private HexCoordinate _boatStartPosition = new HexCoordinate(0, 0);

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
            var essentialAssets = new[]
            {
                _mapAssetKey,
                "WaterTile",
                "TerrainTile",
                "Rock",
                "Vegetation"
            };

            if (_assetService is AddressableAssetService addressableAssetService)
            {
                await addressableAssetService.PreloadAssetsAsync(essentialAssets);
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

            await UniTask.Run(() => _mapGenerator.AddRandomVegetationAndRocks(tiles));
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
            Debug.Log($"OnHexClicked: offset={hexOffset}, world={worldPosition}, boatMoving={_boatController.IsMoving}");
    
            if (_boatController.IsMoving)
                return;

            var targetHex = HexCoordinate.FromOffset(hexOffset.x, hexOffset.y);
            Debug.Log($"Target hex: {targetHex}, walkable: {_hexGridManager.IsWalkable(targetHex)}");
    
            if (!_hexGridManager.IsWalkable(targetHex))
                return;

            var path = _pathfinder.FindPath(_boatController.CurrentHex, targetHex);
            Debug.Log($"Path found: {path.Length} steps, boat at: {_boatController.CurrentHex}");
    
            if (path.Length > 0)
            {
                _eventManager.TriggerPathCalculated(path);
                _boatController.MoveToAsync(path).Forget();
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