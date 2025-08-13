using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.HexGrid;
using Core.Pathfinding;
using Core.Camera;
using Gameplay.Map;
using Gameplay.Boat;
using Infrastructure.Events;
using Zenject;

namespace Gameplay
{
    public class GameController : MonoBehaviour, IGameController
    {
        [SerializeField] private string _mapAssetKey = "DefaultMap";
        [SerializeField] private HexCoordinate _boatStartPosition = new HexCoordinate(0, 0);

        [Inject] private IMapGenerator _mapGenerator;
        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IPathfinder _pathfinder;
        [Inject] private IBoatController _boatController;
        [Inject] private ICameraController _cameraController;
        [Inject] private IGameEventManager _eventManager;

        private bool _isGameActive;

        private void Start()
        {
            InitializeGameAsync().Forget();
        }

        public async UniTask InitializeGameAsync()
        {
            Debug.Log("Initializing game...");

            // Generate map
            var tiles = await _mapGenerator.GenerateMapAsync(_mapAssetKey);
            _mapGenerator.AddRandomVegetationAndRocks(tiles);

            // Set boat position
            _boatController.SetPosition(_boatStartPosition);
            
            // Setup camera
            _cameraController.FollowTarget(_boatController.Transform);
            
            // Subscribe to events
            _eventManager.OnHexClicked += OnHexClicked;
            
            StartGame();
            Debug.Log("Game initialized successfully!");
        }

        public void StartGame()
        {
            _isGameActive = true;
            Debug.Log("Game started!");
        }

        public void PauseGame()
        {
            _isGameActive = false;
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            _isGameActive = true;
            Time.timeScale = 1f;
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
            }
            else
            {
                Debug.Log("No valid path found");
            }
        }

        private void OnDestroy()
        {
            if (_eventManager != null)
            {
                _eventManager.OnHexClicked -= OnHexClicked;
            }
        }
    }
}