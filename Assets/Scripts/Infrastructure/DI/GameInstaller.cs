using UnityEngine;
using Core.HexGrid;
using Core.Pathfinding;
using Core.Input;
using Core.Camera;
using Gameplay;
using Gameplay.Map;
using Gameplay.Boat;
using Gameplay.UI;
using Infrastructure.Events;
using Infrastructure.Services;
using Infrastructure.SceneManagement;
using Infrastructure.Bootstrap;
using Zenject;

namespace Infrastructure.DI
{
    public class GameInstaller : MonoInstaller
    {
        [Header("Scene References")]
        [SerializeField] private BoatController _boatController;
        [SerializeField] private CameraController _cameraController;
        [SerializeField] private PathRenderer _pathRenderer;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private GameBootstrap _gameBootstrap;

        [Header("Performance")]
        [SerializeField] private PerformanceManager _performanceManager;

        [Header("Settings")]
        [SerializeField] private bool _enablePerformanceOptimizations = true;

        public override void InstallBindings()
        {
            // Infrastructure Services
            Container.Bind<IGameEventManager>().To<GameEventManager>().AsSingle();
            Container.Bind<IAssetService>().To<AddressableAssetService>().AsSingle();
            Container.Bind<ISceneManager>().To<SceneManager>().AsSingle();

            // Core Systems
            Container.Bind<IHexGridManager>().To<HexGridManager>().AsSingle();
            Container.Bind<IPathfinder>().To<AStarPathfinder>().AsSingle();

            // Gameplay Systems
            Container.Bind<IMapGenerator>().To<MapGenerator>().AsSingle();
            Container.Bind<IGameController>().To<GameController>().FromComponentInHierarchy().AsSingle();

            // Component Bindings
            BindSceneComponents();
            
            // Performance Systems
            BindPerformanceSystems();
            
            // Object Pool
            CreateObjectPools();
        }

        private void BindSceneComponents()
        {
            // Validate required components
            ValidateRequiredComponents();

            // Boat controller
            Container.Bind<IBoatController>().FromInstance(_boatController).AsSingle();

            // Camera controller
            Container.Bind<ICameraController>().FromInstance(_cameraController).AsSingle();

            // Path renderer
            Container.Bind<IPathRenderer>().FromInstance(_pathRenderer).AsSingle();

            // Input handler
            Container.Bind<IInputHandler>().FromInstance(_inputHandler).AsSingle();
            Container.BindInterfacesTo<InputHandler>().FromInstance(_inputHandler).AsSingle();

            // Main camera
            Container.Bind<Camera>().FromInstance(_mainCamera).AsSingle();
        }

        private void ValidateRequiredComponents()
        {
            if (_boatController == null)
                throw new System.InvalidOperationException("BoatController is required but not assigned in GameInstaller");
            
            if (_cameraController == null)
                throw new System.InvalidOperationException("CameraController is required but not assigned in GameInstaller");
            
            if (_pathRenderer == null)
                throw new System.InvalidOperationException("PathRenderer is required but not assigned in GameInstaller");
            
            if (_inputHandler == null)
                throw new System.InvalidOperationException("InputHandler is required but not assigned in GameInstaller");
            
            if (_mainCamera == null)
                throw new System.InvalidOperationException("Main Camera is required but not assigned in GameInstaller");
        }

        private void BindPerformanceSystems()
        {
            if (_performanceManager != null)
            {
                Container.BindInstance(_performanceManager).AsSingle();
            }
            else if (_enablePerformanceOptimizations)
            {
                Container.Bind<PerformanceManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            }
            
            Container.Bind<TileCullingManager>().FromComponentInHierarchy().AsSingle();
            Container.Bind<DeviceCapabilityDetector>().FromComponentInHierarchy().AsSingle();
        }

        private void CreateObjectPools()
        {
            var markerPoolParent = new GameObject("MarkerPool").transform;
            
            // Create a simple Transform prefab for markers
            var markerPrefab = new GameObject("MarkerPrefab");
            markerPrefab.SetActive(false);
            
            var markerPool = new ObjectPool<Transform>(
                markerPrefab.transform,
                Container,
                markerPoolParent,
                "PathMarkers"
            );
            
            Container.Bind<IObjectPool<Transform>>().FromInstance(markerPool).AsSingle();
        }

        public override void Start()
        {
            base.Start();
            
            if (_enablePerformanceOptimizations && Application.isMobilePlatform)
            {
                Application.targetFrameRate = 30;
                QualitySettings.vSyncCount = 0;
                
                var performanceManager = Container.TryResolve<PerformanceManager>();
                performanceManager?.EnableMobileMode(true);
            }
        }
    }
}