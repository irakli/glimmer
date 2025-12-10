using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IrakliChkuaseli.UI.Glimmer
{
    internal class GlimmerElementData
    {
        public Graphic graphic;
        public Material originalMaterial;
        public Color originalColor;
        public Material glimmerMaterial;
        public float cornerRadius;
    }

    internal struct TextGlimmerRect
    {
        public Vector2 min;
        public Vector2 max;
        public Vector2 size;
        public float cornerRadius;
    }

    /// <summary>
    /// Main controller for skeleton loading effect. Place as a sibling to UI elements
    /// that should display skeleton shimmer while loading.
    /// </summary>
    /// <remarks>
    /// <para>Target Graphics are assigned via the Inspector (explicit references).
    /// Use "Refresh Targets" button to auto-discover Graphics in the hierarchy.</para>
    /// <para>Use <see cref="Show"/> and <see cref="Hide"/> to control visibility,
    /// or the async variants <see cref="ShowAsync"/> and <see cref="HideAsync"/> for fade transitions.</para>
    /// </remarks>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Effects/Glimmer Group")]
    public class GlimmerGroup : MaskableGraphic
    {
        private const string ShaderName = "UI/Glimmer/Shimmer"; 

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ShimmerColorProp = Shader.PropertyToID("_ShimmerColor");
        private static readonly int ShimmerSpeedProp = Shader.PropertyToID("_ShimmerSpeed");
        private static readonly int ShimmerWidthProp = Shader.PropertyToID("_ShimmerWidth");
        private static readonly int ShimmerSinCosProp = Shader.PropertyToID("_ShimmerSinCos");
        private static readonly int CornerRadiusProp = Shader.PropertyToID("_CornerRadius");
        private static readonly int RectSizeProp = Shader.PropertyToID("_RectSize");
        private static readonly int OuterUVProp = Shader.PropertyToID("_OuterUV");
        private static readonly int AlphaProp = Shader.PropertyToID("_Alpha");

        [SerializeField] private List<Graphic> targetGraphics = new();
        [SerializeField] private bool autoRefreshTargets = true;

        [SerializeField] private Color baseColor = new(0.85f, 0.85f, 0.85f, 0.5f);
        [SerializeField] private Color shimmerColor = new(0.95f, 0.95f, 0.95f, 0.5f);
        [SerializeField] private float cornerRadius = 4f;

        [SerializeField] private float shimmerDuration = 1.2f;
        [SerializeField] [Range(-45f, 45f)] private float shimmerAngle = 20f;
        [SerializeField] [Range(0.1f, 0.5f)] private float shimmerWidth = 0.3f;

        [SerializeField] private bool previewInEditor = true;

        private bool _isInitialized;
        private bool _wasShowingBeforeDisable;
        private readonly List<GlimmerElementData> _elements = new();
        private readonly List<TextGlimmerRect> _textRects = new();
        private readonly Dictionary<int, Material> _materialCache = new();
        private Shader _shimmerShader;

        /// <summary>
        /// Whether the skeleton effect is currently showing.
        /// </summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// The list of Graphics that will be affected by the skeleton effect.
        /// </summary>
        public IReadOnlyList<Graphic> TargetGraphics => targetGraphics;

        /// <summary>
        /// Fired when skeleton visibility changes. Parameter is true when showing, false when hidden.
        /// </summary>
        public event Action<bool> StateChanged;

        /// <summary>
        /// Fired when visual properties change (colors, animation, corner radius).
        /// Use for integrations that need to respond to appearance changes.
        /// </summary>
        public event Action PropertiesChanged;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>
        /// Pre-initializes the skeleton system without showing it.
        /// Call this during screen setup to avoid first-show latency.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            EnsureShader();
            _isInitialized = true;
        }

        /// <summary>
        /// Activates skeleton shimmer effect on all target Graphics.
        /// </summary>
        public void Show()
        {
            if (IsShowing) return;
            IsShowing = true;

            if (!_isInitialized) Initialize();

            if (_shimmerShader == null)
            {
                Debug.LogError($"[Glimmer] Cannot show skeleton - shader not found.", this);
                IsShowing = false;
                return;
            }

            ApplySkeletonToTargets();
            CreateTextSkeletonMaterial();
            SetVerticesDirty();

            StateChanged?.Invoke(true);
        }

        /// <summary>
        /// Hides skeleton effect and restores original materials.
        /// </summary>
        public void Hide()
        {
            if (!IsShowing) return;
            IsShowing = false;

            RestoreOriginalMaterials();
            _textRects.Clear();

            if (material != null && material != defaultMaterial)
            {
                DestroyMaterial(material);
                material = null;
            }

            SetVerticesDirty();
            StateChanged?.Invoke(false);
        }

        /// <summary>
        /// Toggles between showing and hiding the skeleton effect.
        /// </summary>
        public void Toggle()
        {
            if (IsShowing) Hide();
            else Show();
        }

#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Activates skeleton effect with a fade-in animation.
        /// </summary>
        /// <param name="duration">Fade duration in seconds (default: 0.2)</param>
        /// <param name="ct">Cancellation token for async cancellation</param>
        /// <returns>Awaitable that completes when fade finishes</returns>
        public async Awaitable ShowAsync(float duration = 0.2f, CancellationToken ct = default)
        {
            Show();

            if (duration <= 0) return;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var alpha = Mathf.Clamp01(elapsed / duration);
                SetAlpha(alpha);
                await Awaitable.NextFrameAsync(ct);
            }

            SetAlpha(1f);
        }

        /// <summary>
        /// Hides skeleton effect with a fade-out animation.
        /// </summary>
        /// <param name="duration">Fade duration in seconds (default: 0.2)</param>
        /// <param name="ct">Cancellation token for async cancellation</param>
        /// <returns>Awaitable that completes when fade finishes and skeleton is hidden</returns>
        public async Awaitable HideAsync(float duration = 0.2f, CancellationToken ct = default)
        {
            if (!IsShowing) return;

            if (duration <= 0)
            {
                Hide();
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var alpha = 1f - Mathf.Clamp01(elapsed / duration);
                SetAlpha(alpha);
                await Awaitable.NextFrameAsync(ct);
            }

            Hide();
        }
#endif

        /// <summary>
        /// Sets the skeleton colors at runtime.
        /// </summary>
        /// <param name="newBaseColor">Background placeholder color</param>
        /// <param name="newShimmerColor">Shimmer highlight color</param>
        public void SetColors(Color newBaseColor, Color newShimmerColor)
        {
            baseColor = newBaseColor;
            shimmerColor = newShimmerColor;

            if (IsShowing)
                UpdateAllShaderProperties();

            PropertiesChanged?.Invoke();
        }

        /// <summary>
        /// Sets the shimmer animation parameters at runtime.
        /// </summary>
        /// <param name="duration">Seconds for one shimmer cycle</param>
        /// <param name="angle">Shimmer angle in degrees (-45 to 45)</param>
        /// <param name="width">Shimmer band width (0.1 to 0.5)</param>
        public void SetAnimation(float duration, float angle, float width)
        {
            shimmerDuration = Mathf.Max(0.1f, duration);
            shimmerAngle = Mathf.Clamp(angle, -45f, 45f);
            shimmerWidth = Mathf.Clamp(width, 0.1f, 0.5f);

            if (IsShowing)
                UpdateAllShaderProperties();

            PropertiesChanged?.Invoke();
        }

        /// <summary>
        /// Forces a refresh of all skeleton materials and shader properties.
        /// Call after modifying target Graphics at runtime.
        /// </summary>
        public void Refresh()
        {
            if (!IsShowing) return;

            UpdateAllShaderProperties();

            // Update rect sizes in materials
            foreach (var element in _elements)
            {
                if (element.glimmerMaterial != null && element.graphic != null)
                {
                    var rect = element.graphic.rectTransform.rect;
                    element.glimmerMaterial.SetVector(RectSizeProp, new Vector4(rect.width, rect.height, 0, 0));
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Discovers all Graphics in the parent hierarchy and adds them to targetGraphics.
        /// Elements with GlimmerElement.IgnoreGlimmer set to true are skipped.
        /// </summary>
        [ContextMenu("Refresh Targets")]
        public void RefreshTargets()
        {
            targetGraphics.Clear();
            var searchRoot = transform.parent != null ? transform.parent : transform;

            foreach (var g in searchRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (g == this) continue;
                var element = g.GetComponent<GlimmerElement>();
                if (element != null && element.IgnoreGlimmer) continue;
                targetGraphics.Add(g);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Clears all target Graphics from the list.
        /// </summary>
        [ContextMenu("Clear Targets")]
        public void ClearTargets()
        {
            targetGraphics.Clear();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void SetAlpha(float alpha)
        {
            foreach (var element in _elements)
            {
                if (element.glimmerMaterial != null)
                    element.glimmerMaterial.SetFloat(AlphaProp, alpha);
            }

            if (material != null)
                material.SetFloat(AlphaProp, alpha);
        }

        private void UpdateAllShaderProperties()
        {
            foreach (var element in _elements)
            {
                if (element.glimmerMaterial != null)
                    ApplyShaderProperties(element.glimmerMaterial, element.cornerRadius);
            }

            if (material != null)
                ApplyShaderProperties(material, cornerRadius);
        }

        private void ApplyShaderProperties(Material mat, float radius)
        {
            mat.SetColor(BaseColorProp, baseColor);
            mat.SetColor(ShimmerColorProp, shimmerColor);
            mat.SetFloat(ShimmerSpeedProp, 1f / shimmerDuration);
            mat.SetFloat(ShimmerWidthProp, shimmerWidth);

            var angleRad = shimmerAngle * Mathf.Deg2Rad;
            var sin = Mathf.Sin(angleRad);
            var cos = Mathf.Cos(angleRad);
            var diagRangeReciprocal = 1f / (Mathf.Abs(sin) + Mathf.Abs(cos));
            mat.SetVector(ShimmerSinCosProp, new Vector4(sin, cos, diagRangeReciprocal, 0));

            mat.SetFloat(CornerRadiusProp, radius);
        }

        private void EnsureShader()
        {
            if (_shimmerShader != null) return;

            _shimmerShader = Shader.Find(ShaderName);
            if (_shimmerShader == null)
            {
                Debug.LogError($"[Glimmer] Shader '{ShaderName}' not found. " +
                               "Ensure the SkeletonUI package is properly installed.", this);
            }
        }

        private Material GetOrCreateMaterial(Graphic graphic)
        {
            var instanceId = graphic.GetInstanceID();
            if (_materialCache.TryGetValue(instanceId, out var cached))
                return cached;

            var mat = new Material(_shimmerShader);
            _materialCache[instanceId] = mat;
            return mat;
        }

        private static void DestroyMaterial(Material mat)
        {
            if (mat == null) return;

            if (Application.isPlaying)
                Destroy(mat);
            else
                DestroyImmediate(mat);
        }

        private void ClearMaterialCache()
        {
            foreach (var mat in _materialCache.Values)
                DestroyMaterial(mat);

            _materialCache.Clear();
        }

        private void CreateTextSkeletonMaterial()
        {
            if (_shimmerShader == null || _textRects.Count == 0)
            {
                material = null;
                return;
            }

            var mat = new Material(_shimmerShader);
            ApplyShaderProperties(mat, cornerRadius);
            mat.SetFloat(AlphaProp, 1f);
            mat.SetVector(RectSizeProp, new Vector4(1000, 1000, 0, 0));

            material = mat;
        }

        private void ApplySkeletonToTargets()
        {
            _elements.Clear();
            _textRects.Clear();

            if (targetGraphics.Count == 0)
            {
                Debug.LogWarning($"[Glimmer] No target Graphics assigned for '{name}'. " +
                                 "Use 'Refresh Targets' in the Inspector or manually assign Graphics.", this);
                return;
            }

            foreach (var graphic in targetGraphics)
            {
                if (graphic == null)
                {
                    Debug.LogWarning($"[Glimmer] Null graphic in target list for '{name}'. " +
                                     "Use 'Refresh Targets' to fix.", this);
                    continue;
                }

                if (graphic.rectTransform == null)
                    continue;

                // Check IgnoreGlimmer at runtime (not just during RefreshTargets)
                var glimmerElement = graphic.GetComponent<GlimmerElement>();
                if (glimmerElement != null && glimmerElement.IgnoreGlimmer)
                    continue;

                var element = new GlimmerElementData
                {
                    graphic = graphic,
                    originalMaterial = graphic.material,
                    originalColor = graphic.color,
                    cornerRadius = GetCornerRadius(graphic.rectTransform)
                };

                if (graphic is TMP_Text)
                {
                    element.glimmerMaterial = null;
                    graphic.color = new Color(0, 0, 0, 0);

                    var textRect = graphic.rectTransform;
                    var corners = new Vector3[4];
                    textRect.GetWorldCorners(corners);

                    var localMin = rectTransform.InverseTransformPoint(corners[0]);
                    var localMax = rectTransform.InverseTransformPoint(corners[2]);
                    var size = new Vector2(localMax.x - localMin.x, localMax.y - localMin.y);

                    _textRects.Add(new TextGlimmerRect
                    {
                        min = new Vector2(localMin.x, localMin.y),
                        max = new Vector2(localMax.x, localMax.y),
                        size = size,
                        cornerRadius = GetCornerRadius(graphic.rectTransform)
                    });
                }
                else
                {
                    var mat = GetOrCreateMaterial(graphic);
                    ApplyShaderProperties(mat, element.cornerRadius);
                    mat.SetFloat(AlphaProp, 1f);

                    var rect = graphic.rectTransform.rect;
                    mat.SetVector(RectSizeProp, new Vector4(rect.width, rect.height, 0, 0));

                    // Get outer UV bounds from sprite (for sprite atlas support)
                    var outerUV = new Vector4(0, 0, 1, 1);
                    if (graphic is Image image && image.sprite != null)
                        outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite);
                    mat.SetVector(OuterUVProp, outerUV);

                    element.glimmerMaterial = mat;
                    graphic.material = mat;
                    graphic.color = Color.white;
                }

                _elements.Add(element);
            }
        }

        private void RestoreOriginalMaterials()
        {
            foreach (var element in _elements)
            {
                if (element.graphic != null)
                {
                    element.graphic.material = element.originalMaterial;
                    element.graphic.color = element.originalColor;
                }
            }

            _elements.Clear();
        }

        private float GetCornerRadius(RectTransform rect)
        {
            var element = rect.GetComponent<GlimmerElement>();
            if (element != null && element.HasCornerRadiusOverride)
                return element.CornerRadius;

            return cornerRadius;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (!IsShowing || _textRects.Count == 0)
                return;

            // Per-text rect data encoded in UV1: (width, height, cornerRadius, 1.0 flag)
            // UV1.w > 0 signals shader to use per-vertex data instead of uniforms
            // Using UV1 (not Color) because Unity clamps Color values to 0-1 range

            foreach (var textRect in _textRects)
            {
                var startIndex = vh.currentVertCount;

                // UV1 carries rect data: width, height, corner radius, and a flag (1.0)
                var rectData = new Vector4(textRect.size.x, textRect.size.y, textRect.cornerRadius, 1f);

                var v0 = new UIVertex
                {
                    position = new Vector3(textRect.min.x, textRect.min.y),
                    color = Color.white,
                    uv0 = new Vector2(0, 0),
                    uv1 = rectData
                };
                var v1 = new UIVertex
                {
                    position = new Vector3(textRect.min.x, textRect.max.y),
                    color = Color.white,
                    uv0 = new Vector2(0, 1),
                    uv1 = rectData
                };
                var v2 = new UIVertex
                {
                    position = new Vector3(textRect.max.x, textRect.max.y),
                    color = Color.white,
                    uv0 = new Vector2(1, 1),
                    uv1 = rectData
                };
                var v3 = new UIVertex
                {
                    position = new Vector3(textRect.max.x, textRect.min.y),
                    color = Color.white,
                    uv0 = new Vector2(1, 0),
                    uv1 = rectData
                };

                vh.AddVert(v0);
                vh.AddVert(v1);
                vh.AddVert(v2);
                vh.AddVert(v3);

                vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Auto-refresh targets if list is empty and auto-refresh is enabled
            if (autoRefreshTargets && targetGraphics.Count == 0 && !Application.isPlaying)
            {
                // Delay to avoid issues during serialization
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && targetGraphics.Count == 0)
                        RefreshTargets();
                };
            }

            // Update shader properties if showing (no hide/show cycle needed)
            if (IsShowing)
                UpdateAllShaderProperties();

            PropertiesChanged?.Invoke();
        }
#endif

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_wasShowingBeforeDisable)
            {
                _wasShowingBeforeDisable = false;
                Show();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _wasShowingBeforeDisable = IsShowing;
            if (IsShowing)
                Hide();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Hide();
            ClearMaterialCache();
        }
    }
}