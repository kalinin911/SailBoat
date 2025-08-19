using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.HexGrid;
using Infrastructure.Events;
using Infrastructure.Services;
using Zenject;

namespace Gameplay.UI
{
    public class PathRenderer : MonoBehaviour, IPathRenderer
    {
        [Header("Path Rendering")] [SerializeField]
        private LineRenderer _lineRenderer;

        [SerializeField] private Material _defaultPathMaterial;
        [SerializeField] private Material _animatedPathMaterial;
        [SerializeField] private float _pathHeight = 0.1f;
        [SerializeField] private float _pathWidth = 0.3f;

        [Header("Animation Settings")] [SerializeField]
        private bool _animatePath = true;

        [SerializeField] private float _animationSpeed = 2f;
        [SerializeField] private float _pulseIntensity = 0.3f;
        [SerializeField] private Color _pathColor = Color.cyan;
        [SerializeField] private Color _pathColorEnd = Color.blue;

        [Header("Path Markers")] [SerializeField]
        private GameObject _startMarkerPrefab;

        [SerializeField] private GameObject _endMarkerPrefab;
        [SerializeField] private GameObject _wayPointMarkerPrefab;
        [SerializeField] private bool _showMarkers = true;

        [Header("Visual Effects")] [SerializeField]
        private ParticleSystem _pathParticles;

        [SerializeField] private bool _enableParticleTrail = true;
        [SerializeField] private float _particleSpacing = 1f;

        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IGameEventManager _eventManager;
        [Inject] private IObjectPool<Transform> _markerPool;

        private List<GameObject> _activeMarkers = new List<GameObject>();
        private List<ParticleSystem> _activeParticles = new List<ParticleSystem>();
        private Coroutine _animationCoroutine;
        private Material _pathMaterialInstance;
        private HexCoordinate[] _currentPath;

        private void Awake()
        {
            SetupLineRenderer();
            SetupMaterials();
        }

        private void Start()
        {
            _eventManager.OnPathCalculated += ShowPath;
            _eventManager.OnBoatMovementCompleted += OnBoatMovementCompleted;
            _eventManager.OnBoatMovementStarted += OnBoatMovementStarted;
        }

        private void OnDestroy()
        {
            if (_eventManager != null)
            {
                _eventManager.OnPathCalculated -= ShowPath;
                _eventManager.OnBoatMovementCompleted -= OnBoatMovementCompleted;
                _eventManager.OnBoatMovementStarted -= OnBoatMovementStarted;
            }

            CleanupMaterials();
        }

        private void SetupLineRenderer()
        {
            if (_lineRenderer == null)
                _lineRenderer = GetComponent<LineRenderer>();

            _lineRenderer.startWidth = _pathWidth;
            _lineRenderer.endWidth = _pathWidth;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.sortingOrder = 1;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void SetupMaterials()
        {
            if (_animatedPathMaterial != null)
            {
                _pathMaterialInstance = Instantiate(_animatedPathMaterial);
            }
            else if (_defaultPathMaterial != null)
            {
                _pathMaterialInstance = Instantiate(_defaultPathMaterial);
            }
            else
            {
                throw new System.InvalidOperationException(
                    "PathRenderer requires either animated or default path material to be assigned");
            }

            _pathMaterialInstance.SetColor("_Color", _pathColor);
            if (_pathMaterialInstance.HasProperty("_EmissionColor"))
            {
                _pathMaterialInstance.SetColor("_EmissionColor", _pathColor);
                _pathMaterialInstance.EnableKeyword("_EMISSION");
            }

            _lineRenderer.material = _pathMaterialInstance;
        }

        public void ShowPath(HexCoordinate[] path)
        {
            if (path == null || path.Length == 0)
            {
                HidePath();
                return;
            }

            _currentPath = path;
            HidePath(); // Clear previous path

            SetupPathLine(path);

            if (_showMarkers)
            {
                CreatePathMarkers(path);
            }

            if (_enableParticleTrail && _pathParticles != null)
            {
                CreateParticleTrail(path);
            }

            if (_animatePath)
            {
                StartPathAnimation();
            }

            _lineRenderer.enabled = true;
        }

        private void SetupPathLine(HexCoordinate[] path)
        {
            _lineRenderer.positionCount = path.Length;

            var smoothPath = CreateSmoothPath(path);

            for (int i = 0; i < smoothPath.Length; i++)
            {
                _lineRenderer.SetPosition(i, smoothPath[i]);
            }

            // Set up gradient color
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(_pathColor, 0.0f),
                    new GradientColorKey(_pathColorEnd, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );
            _lineRenderer.colorGradient = gradient;
        }

        private Vector3[] CreateSmoothPath(HexCoordinate[] path)
        {
            var worldPoints = new Vector3[path.Length];

            for (int i = 0; i < path.Length; i++)
            {
                var worldPos = _hexGridManager.HexToWorld(path[i]);
                worldPos.y += _pathHeight;

                // Add slight curve for better visibility
                if (i > 0 && i < path.Length - 1)
                {
                    worldPos.y += Mathf.Sin((float)i / path.Length * Mathf.PI) * 0.1f;
                }

                worldPoints[i] = worldPos;
            }

            return worldPoints;
        }

        private void CreatePathMarkers(HexCoordinate[] path)
        {
            for (int i = 0; i < path.Length; i++)
            {
                GameObject marker = null;
                var worldPos = _hexGridManager.HexToWorld(path[i]);
                worldPos.y += 0.05f;

                if (i == 0 && _startMarkerPrefab != null)
                {
                    marker = Instantiate(_startMarkerPrefab, worldPos, Quaternion.identity);
                }
                else if (i == path.Length - 1 && _endMarkerPrefab != null)
                {
                    marker = Instantiate(_endMarkerPrefab, worldPos, Quaternion.identity);
                }
                else if (_wayPointMarkerPrefab != null && i % 3 == 0) // Every 3rd waypoint
                {
                    marker = Instantiate(_wayPointMarkerPrefab, worldPos, Quaternion.identity);
                }

                if (marker != null)
                {
                    _activeMarkers.Add(marker);
                    StartCoroutine(AnimateMarker(marker));
                }
            }
        }

        private IEnumerator AnimateMarker(GameObject marker)
        {
            var startPos = marker.transform.position;
            var time = 0f;

            while (marker != null)
            {
                time += Time.deltaTime * _animationSpeed;
                var offset = Mathf.Sin(time) * 0.1f;
                marker.transform.position = startPos + Vector3.up * offset;
                yield return null;
            }
        }

        private void CreateParticleTrail(HexCoordinate[] path)
        {
            for (int i = 0; i < path.Length; i += Mathf.Max(1, Mathf.RoundToInt(_particleSpacing)))
            {
                var worldPos = _hexGridManager.HexToWorld(path[i]);
                worldPos.y += _pathHeight + 0.1f;

                var particles = Instantiate(_pathParticles, worldPos, Quaternion.identity);
                var main = particles.main;
                main.startColor = _pathColor;

                _activeParticles.Add(particles);
                particles.Play();
            }
        }

        private void StartPathAnimation()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }

            _animationCoroutine = StartCoroutine(AnimatePath());
        }

        private IEnumerator AnimatePath()
        {
            float time = 0f;
            var originalWidth = _pathWidth;

            while (_lineRenderer.enabled)
            {
                time += Time.deltaTime * _animationSpeed;

                // Pulsing width effect
                var pulse = 1f + Mathf.Sin(time) * _pulseIntensity;
                _lineRenderer.startWidth = originalWidth * pulse;
                _lineRenderer.endWidth = originalWidth * pulse;

                // Animate material if it has animation properties
                if (_pathMaterialInstance != null)
                {
                    if (_pathMaterialInstance.HasProperty("_MainTex"))
                    {
                        var offset = _pathMaterialInstance.GetTextureOffset("_MainTex");
                        offset.x = time * 0.5f;
                        _pathMaterialInstance.SetTextureOffset("_MainTex", offset);
                    }

                    // Animate emission intensity
                    if (_pathMaterialInstance.HasProperty("_EmissionColor"))
                    {
                        var intensity = 1f + Mathf.Sin(time * 2f) * 0.3f;
                        _pathMaterialInstance.SetColor("_EmissionColor", _pathColor * intensity);
                    }
                }

                yield return null;
            }
        }

        public void HidePath()
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;

            // Stop animation
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }

            // Clean up markers
            foreach (var marker in _activeMarkers)
            {
                if (marker != null)
                    DestroyImmediate(marker);
            }

            _activeMarkers.Clear();

            // Clean up particles
            foreach (var particles in _activeParticles)
            {
                if (particles != null)
                {
                    particles.Stop();
                    DestroyImmediate(particles.gameObject);
                }
            }

            _activeParticles.Clear();
        }

        public void SetPathMaterial(Material material)
        {
            if (material != null)
            {
                _pathMaterialInstance = Instantiate(material);
                _lineRenderer.material = _pathMaterialInstance;
            }
        }

        private void OnBoatMovementStarted(Vector3[] worldPath)
        {
            // Optionally dim the path or change its appearance during movement
            if (_pathMaterialInstance != null && _pathMaterialInstance.HasProperty("_Metallic"))
            {
                _pathMaterialInstance.SetFloat("_Metallic", 0.5f);
            }
        }

        private void OnBoatMovementCompleted(Vector3 finalPosition)
        {
            // Fade out the path gradually
            StartCoroutine(FadeOutPath());
        }

        private IEnumerator FadeOutPath()
        {
            yield return new WaitForSeconds(1f); // Show path for 1 second after completion

            float fadeTime = 2f;
            float elapsed = 0f;
            var originalColor = _pathColor;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);

                var gradient = new Gradient();
                var color1 = originalColor;
                var color2 = _pathColorEnd;
                color1.a = alpha;
                color2.a = alpha;

                gradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(color1, 0.0f),
                        new GradientColorKey(color2, 1.0f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(alpha, 0.0f),
                        new GradientAlphaKey(alpha, 1.0f)
                    }
                );
                _lineRenderer.colorGradient = gradient;

                yield return null;
            }

            HidePath();
        }

        private void CleanupMaterials()
        {
            if (_pathMaterialInstance != null)
            {
                DestroyImmediate(_pathMaterialInstance);
            }
        }

        // Public methods for runtime customization
        public void SetPathColor(Color color)
        {
            _pathColor = color;
            if (_pathMaterialInstance != null)
            {
                _pathMaterialInstance.SetColor("_Color", color);
                if (_pathMaterialInstance.HasProperty("_EmissionColor"))
                {
                    _pathMaterialInstance.SetColor("_EmissionColor", color);
                }
            }
        }

        public void SetAnimationSpeed(float speed)
        {
            _animationSpeed = speed;
        }

        public void EnableAnimation(bool enable)
        {
            _animatePath = enable;
            if (!enable && _animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

        public void SetPathWidth(float width)
        {
            _pathWidth = width;
            if (_lineRenderer != null)
            {
                _lineRenderer.startWidth = width;
                _lineRenderer.endWidth = width;
            }
        }
    }
}