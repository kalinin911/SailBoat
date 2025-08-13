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

        public override void InstallBindings()
        {
            // Infrastructure Services
            Container.Bind<IGameEventManager>().To<GameEventManager>().AsSingle();
            Container.Bind<IAssetService>().To<AddressableAssetService>().AsSingle();

            // Core Systems
            Container.Bind<IHexGridManager>().To<HexGridManager>().AsSingle();
            Container.Bind<IPathfinder>().To<AStarPathfinder>().AsSingle();

            // Gameplay Systems
            Container.Bind<IMapGenerator>().To<MapGenerator>().AsSingle();
            Container.Bind<IGameController>().To<GameController>().FromComponentInHierarchy().AsSingle();

            // Component Bindings
            Container.Bind<IBoatController>().FromInstance(_boatController).AsSingle();
            Container.Bind<ICameraController>().FromInstance(_cameraController).AsSingle();
            Container.Bind<IPathRenderer>().FromInstance(_pathRenderer).AsSingle();
            Container.Bind<IInputHandler>().FromInstance(_inputHandler).AsSingle();
            Container.Bind<Camera>().FromInstance(_mainCamera).AsSingle();

            // Bind Input Handler as IInitializable for auto-initialization
            Container.BindInterfacesTo<InputHandler>().FromInstance(_inputHandler).AsSingle();
        }
    }
}