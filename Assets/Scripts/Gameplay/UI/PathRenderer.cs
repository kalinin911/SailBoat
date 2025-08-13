using UnityEngine;
using Core.HexGrid;
using Infrastructure.Events;
using Zenject;

namespace Gameplay.UI
{
    public class PathRenderer : MonoBehaviour, IPathRenderer
    {
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Material _defaultPathMaterial;
        [SerializeField] private float _pathHeight = 0.1f;

        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IGameEventManager _eventManager;

        private void Awake()
        {
            if (_lineRenderer == null)
                _lineRenderer = GetComponent<LineRenderer>();
                
            if (_lineRenderer == null)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
                
            SetupLineRenderer();
        }

        private void Start()
        {
            _eventManager.OnPathCalculated += ShowPath;
            _eventManager.OnBoatMovementCompleted += OnBoatMovementCompleted;
        }

        private void OnDestroy()
        {
            if (_eventManager != null)
            {
                _eventManager.OnPathCalculated -= ShowPath;
                _eventManager.OnBoatMovementCompleted -= OnBoatMovementCompleted;
            }
        }

        private void OnBoatMovementCompleted(Vector3 finalPosition)
        {
            HidePath();
        }

        private void SetupLineRenderer()
        {
            _lineRenderer.material = _defaultPathMaterial;
            _lineRenderer.startWidth = 0.2f;
            _lineRenderer.endWidth = 0.2f;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.sortingOrder = 1;
        }

        public void ShowPath(HexCoordinate[] path)
        {
            if (path == null || path.Length == 0)
            {
                HidePath();
                return;
            }

            _lineRenderer.positionCount = path.Length;
            
            for (int i = 0; i < path.Length; i++)
            {
                var worldPos = _hexGridManager.HexToWorld(path[i]);
                worldPos.y += _pathHeight;
                _lineRenderer.SetPosition(i, worldPos);
            }
            
            _lineRenderer.enabled = true;
        }

        public void HidePath()
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;
        }

        public void SetPathMaterial(Material material)
        {
            if (material != null)
                _lineRenderer.material = material;
        }
    }
}