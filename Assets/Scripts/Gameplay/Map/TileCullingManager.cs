using System.Collections.Generic;
using UnityEngine;
using Core.HexGrid;
using Infrastructure.Services;
using Zenject;

namespace Gameplay.Map
{
    /// <summary>
    /// Culls tiles based on camera frustum and distance for mobile optimization
    /// </summary>
    public class TileCullingManager : MonoBehaviour
    {
        [Header("Culling Settings")]
        [SerializeField] private float _cullingDistance = 30f;
        [SerializeField] private float _updateInterval = 0.2f; // 5 FPS update rate
        [SerializeField] private int _maxTilesPerUpdate = 20;
        [SerializeField] private bool _enableFrustumCulling = true;
        [SerializeField] private bool _enableDistanceCulling = true;

        [Header("LOD Settings")]
        [SerializeField] private float _lodDistance1 = 15f; // Full detail
        [SerializeField] private float _lodDistance2 = 25f; // Reduced detail
        [SerializeField] private float _lodDistance3 = 35f; // Minimal detail

        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private Camera _mainCamera;
        [Inject(Optional = true)] private PerformanceManager _performanceManager;

        private readonly HashSet<HexTile> _visibleTiles = new HashSet<HexTile>();
        private readonly HashSet<HexTile> _culledTiles = new HashSet<HexTile>();
        private readonly Queue<HexTile> _updateQueue = new Queue<HexTile>();
        private readonly List<HexTile> _allTiles = new List<HexTile>();
        
        private Transform _cameraTransform;
        private Plane[] _frustumPlanes;
        private float _lastUpdateTime;
        private Vector3 _lastCameraPosition;
        private bool _isEnabled = true;
        private bool _tilesCollected = false;

        private void Start()
        {
            _cameraTransform = _mainCamera.transform;
            _isEnabled = Application.isMobilePlatform || (_performanceManager?.GetCurrentFPS() < 45f);
            
            if (_isEnabled)
            {
                // Collect all tiles once at start
                CollectAllTiles();
                InvokeRepeating(nameof(UpdateCulling), 1f, _updateInterval); // Start after 1 second
            }
        }

        private void CollectAllTiles()
        {
            _allTiles.Clear();
            
            // Find all HexTile components in the scene
            var foundTiles = FindObjectsOfType<HexTile>();
            _allTiles.AddRange(foundTiles);
            
            _tilesCollected = true;
            Debug.Log($"TileCullingManager: Collected {_allTiles.Count} tiles for culling");
        }

        private void UpdateCulling()
        {
            if (!_isEnabled || _cameraTransform == null || !_tilesCollected) return;

            // Skip update if camera hasn't moved much
            if (Vector3.Distance(_cameraTransform.position, _lastCameraPosition) < 1f)
                return;

            _lastCameraPosition = _cameraTransform.position;
            _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_mainCamera);

            ProcessTileVisibility();
        }

        private void ProcessTileVisibility()
        {
            var processedThisFrame = 0;

            // Process tiles from our collected list
            foreach (var tile in _allTiles)
            {
                if (tile == null) continue; // Skip destroyed tiles

                if (processedThisFrame >= _maxTilesPerUpdate)
                {
                    // Queue remaining tiles for next frame
                    _updateQueue.Enqueue(tile);
                    continue;
                }

                UpdateTileVisibility(tile);
                processedThisFrame++;
            }

            // Process queued tiles from previous frames
            while (_updateQueue.Count > 0 && processedThisFrame < _maxTilesPerUpdate)
            {
                var queuedTile = _updateQueue.Dequeue();
                if (queuedTile != null)
                {
                    UpdateTileVisibility(queuedTile);
                    processedThisFrame++;
                }
            }
        }

        private void UpdateTileVisibility(HexTile tile)
        {
            if (tile == null) return;

            var tilePosition = tile.transform.position;
            var distanceToCamera = Vector3.Distance(tilePosition, _cameraTransform.position);
            
            bool shouldBeVisible = ShouldTileBeVisible(tile, tilePosition, distanceToCamera);
            bool isCurrentlyVisible = _visibleTiles.Contains(tile);

            if (shouldBeVisible && !isCurrentlyVisible)
            {
                ShowTile(tile, distanceToCamera);
            }
            else if (!shouldBeVisible && isCurrentlyVisible)
            {
                CullTile(tile);
            }
            else if (shouldBeVisible && isCurrentlyVisible)
            {
                // Update LOD level for visible tiles
                UpdateTileLOD(tile, distanceToCamera);
            }
        }

        private bool ShouldTileBeVisible(HexTile tile, Vector3 position, float distance)
        {
            // Distance culling
            if (_enableDistanceCulling && distance > _cullingDistance)
                return false;

            // Frustum culling
            if (_enableFrustumCulling)
            {
                var bounds = new Bounds(position, Vector3.one * 2f);
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                    return false;
            }

            return true;
        }

        private void ShowTile(HexTile tile, float distance)
        {
            tile.gameObject.SetActive(true);
            _visibleTiles.Add(tile);
            _culledTiles.Remove(tile);
            
            UpdateTileLOD(tile, distance);
        }

        private void CullTile(HexTile tile)
        {
            tile.gameObject.SetActive(false);
            _visibleTiles.Remove(tile);
            _culledTiles.Add(tile);
        }

        private void UpdateTileLOD(HexTile tile, float distance)
        {
            var lodLevel = GetLODLevel(distance);
            ApplyLOD(tile, lodLevel);
        }

        private int GetLODLevel(float distance)
        {
            if (distance <= _lodDistance1) return 0; // Highest quality
            if (distance <= _lodDistance2) return 1; // Medium quality
            if (distance <= _lodDistance3) return 2; // Low quality
            return 3; // Minimal quality
        }

        private void ApplyLOD(HexTile tile, int lodLevel)
        {
            var renderers = tile.GetComponentsInChildren<Renderer>();
            var colliders = tile.GetComponentsInChildren<Collider>();

            switch (lodLevel)
            {
                case 0: // Full detail
                    SetRenderersEnabled(renderers, true);
                    SetCollidersEnabled(colliders, true);
                    break;
            
                case 1: // Reduced detail
                    SetRenderersEnabled(renderers, true);
                    SetCollidersEnabled(colliders, false);
                    break;
            
                case 2: // Low detail
                    SetRenderersEnabled(renderers, true);
                    SetCollidersEnabled(colliders, false);
                    DisableDecorations(tile);
                    break;
            
                case 3: // Minimal detail
                    SetRenderersEnabled(renderers, false);
                    SetCollidersEnabled(colliders, false);
                    break;
            }
        }

        private void SetRenderersEnabled(Renderer[] renderers, bool isEnabled)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                    renderer.enabled = isEnabled;
            }
        }

        private void SetCollidersEnabled(Collider[] colliders, bool isEnabled)
        {
            foreach (var collider in colliders)
            {
                if (collider != null)
                    collider.enabled = isEnabled;
            }
        }

        private void DisableDecorations(HexTile tile)
        {
            foreach (Transform child in tile.transform)
            {
                if (child.name.Contains("Decoration") || 
                    child.name.Contains("Grass") || 
                    child.name.Contains("Rock") ||
                    child.name.Contains("Plant"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        // Public API
        public void SetCullingDistance(float distance)
        {
            _cullingDistance = distance;
        }

        public void EnableCulling(bool enable)
        {
            _isEnabled = enable;
        }

        public void RefreshTileList()
        {
            CollectAllTiles();
        }

        public CullingStatistics GetStatistics()
        {
            return new CullingStatistics
            {
                VisibleTiles = _visibleTiles.Count,
                CulledTiles = _culledTiles.Count,
                TotalTiles = _allTiles.Count,
                CullingDistance = _cullingDistance,
                UpdateInterval = _updateInterval
            };
        }

        private void OnDrawGizmosSelected()
        {
            if (_cameraTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_cameraTransform.position, _cullingDistance);
                
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_cameraTransform.position, _lodDistance1);
                
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_cameraTransform.position, _lodDistance2);
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_cameraTransform.position, _lodDistance3);
            }
        }

        private void OnDestroy()
        {
            CancelInvoke();
        }
    }

    [System.Serializable]
    public class CullingStatistics
    {
        public int VisibleTiles;
        public int CulledTiles;
        public int TotalTiles;
        public float CullingDistance;
        public float UpdateInterval;
        
        public float CullRatio => TotalTiles > 0 ? (float)CulledTiles / TotalTiles : 0f;
    }
}