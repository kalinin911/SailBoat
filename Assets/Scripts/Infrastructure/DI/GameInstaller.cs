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
            
            // Performance Systems
            BindPerformanceSystems();
            
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

            // Path renderer
            if (_pathRenderer != null)
                Container.Bind<IPathRenderer>().FromInstance(_pathRenderer).AsSingle();

            // Input handler
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
        }

        private void CreateMissingComponents()
        {
            if (_boatController == null)
                CreateFallbackBoat();
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