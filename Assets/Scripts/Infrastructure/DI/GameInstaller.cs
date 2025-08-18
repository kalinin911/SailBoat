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

            // Component Bindings - with null checks
            if (_boatController != null)
                Container.Bind<IBoatController>().FromInstance(_boatController).AsSingle();
            else
                CreateFallbackBoat();

            if (_cameraController != null)
                Container.Bind<ICameraController>().FromInstance(_cameraController).AsSingle();
            else
                Debug.LogWarning("CameraController not assigned in GameInstaller");

            if (_pathRenderer != null)
                Container.Bind<IPathRenderer>().FromInstance(_pathRenderer).AsSingle();
            else
                Debug.LogWarning("PathRenderer not assigned in GameInstaller");

            if (_inputHandler != null)
            {
                Container.Bind<IInputHandler>().FromInstance(_inputHandler).AsSingle();
                Container.BindInterfacesTo<InputHandler>().FromInstance(_inputHandler).AsSingle();
            }
            else
                Debug.LogWarning("InputHandler not assigned in GameInstaller");

            if (_mainCamera != null)
                Container.Bind<Camera>().FromInstance(_mainCamera).AsSingle();
            else
            {
                var fallbackCamera = Camera.main ?? FindObjectOfType<Camera>();
                if (fallbackCamera != null)
                    Container.Bind<Camera>().FromInstance(fallbackCamera).AsSingle();
            }
        }

        private void CreateFallbackBoat()
        {
            Debug.LogWarning("BoatController not assigned, creating fallback boat");
            
            var boatGO = BoatPrefabCreator.CreateFallbackBoat();
            var boatController = boatGO.AddComponent<BoatController>();
            
            Container.Bind<IBoatController>().FromInstance(boatController).AsSingle();
            _boatController = boatController;
        }
    }
}