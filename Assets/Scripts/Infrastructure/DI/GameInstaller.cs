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
using Infrastructure.UI;
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

        [Header("Performance & UI")]
        [SerializeField] private PerformanceManager _performanceManager;
        [SerializeField] private LoadingScreenManager _loadingScreenManager;

        [Header("Fallback Settings")]
        [SerializeField] private bool _createFallbackComponents = true;
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
            
            // Performance & UI Systems
            BindPerformanceAndUISystems();
            
            // Object Pool - Create a simple pool for Transform markers
            CreateObjectPools();
            
            // Create fallbacks if needed
            if (_createFallbackComponents)
            {
                CreateMissingComponents();
            }
        }

        private void BindSceneComponents()
        {
            // Boat controller
            if (_boatController != null)
                Container.Bind<IBoatController>().FromInstance(_boatController).AsSingle();
            else if (_createFallbackComponents)
                CreateFallbackBoat();

            // Camera controller
            if (_cameraController != null)
                Container.Bind<ICameraController>().FromInstance(_cameraController).AsSingle();

            // Enhanced path renderer
            if (_pathRenderer != null)
                Container.Bind<IPathRenderer>().FromInstance(_pathRenderer).AsSingle();

            // Enhanced input handler
            if (_inputHandler != null)
            {
                Container.Bind<IInputHandler>().FromInstance(_inputHandler).AsSingle();
                Container.BindInterfacesTo<InputHandler>().FromInstance(_inputHandler).AsSingle();
            }

            // Main camera
            if (_mainCamera != null)
                Container.Bind<Camera>().FromInstance(_mainCamera).AsSingle();
            else
            {
                var fallbackCamera = Camera.main ?? FindObjectOfType<Camera>();
                if (fallbackCamera != null)
                    Container.Bind<Camera>().FromInstance(fallbackCamera).AsSingle();
            }
        }

        private void BindPerformanceAndUISystems()
        {
            // Performance manager
            if (_performanceManager != null)
            {
                Container.BindInstance(_performanceManager).AsSingle();
            }
            else if (_enablePerformanceOptimizations)
            {
                Container.Bind<PerformanceManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            }

            // Loading screen manager - Create simple version without UI dependencies
            if (_loadingScreenManager != null)
            {
                Container.BindInstance(_loadingScreenManager).AsSingle();
            }
            else
            {
                // Create a simple loading screen manager without UI components
                var loadingScreenGO = new GameObject("LoadingScreenManager");
                var loadingScreenManager = loadingScreenGO.AddComponent<LoadingScreenManager>();
                Container.BindInstance(loadingScreenManager).AsSingle();
                _loadingScreenManager = loadingScreenManager;
                Debug.Log("Created simple LoadingScreenManager without UI dependencies");
            }
        }

        private void CreateObjectPools()
        {
            // Create a simple marker pool for the path renderer
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

        private void CreateMissingComponents()
        {
            if (_boatController == null)
                CreateFallbackBoat();
            
            if (_pathRenderer == null)
                CreateFallbackPathRenderer();
                
            if (_inputHandler == null)
                CreateFallbackInputHandler();
        }

        private void CreateFallbackBoat()
        {
            Debug.LogWarning("BoatController not assigned, creating fallback boat");
            
            var boatGO = BoatPrefabCreator.CreateFallbackBoat();
            var boatController = boatGO.AddComponent<BoatController>();
            boatGO.transform.position = Vector3.zero;
            
            Container.Bind<IBoatController>().FromInstance(boatController).AsSingle();
            _boatController = boatController;
        }

        private void CreateFallbackPathRenderer()
        {
            Debug.LogWarning("EnhancedPathRenderer not assigned, creating fallback");
            
            var pathRendererGO = new GameObject("EnhancedPathRenderer (Fallback)");
            var pathRenderer = pathRendererGO.AddComponent<PathRenderer>();
            
            Container.Bind<IPathRenderer>().FromInstance(pathRenderer).AsSingle();
            _pathRenderer = pathRenderer;
        }

        private void CreateFallbackInputHandler()
        {
            Debug.LogWarning("EnhancedInputHandler not assigned, creating fallback");
            
            var inputHandlerGO = new GameObject("EnhancedInputHandler (Fallback)");
            var inputHandler = inputHandlerGO.AddComponent<InputHandler>();
            
            Container.Bind<IInputHandler>().FromInstance(inputHandler).AsSingle();
            Container.BindInterfacesTo<InputHandler>().FromInstance(inputHandler).AsSingle();
            _inputHandler = inputHandler;
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