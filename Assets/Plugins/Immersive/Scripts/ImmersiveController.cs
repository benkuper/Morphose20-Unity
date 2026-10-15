using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class ImmersiveController : MonoBehaviour
{
    private enum SurfaceId
    {
        None = -1,
        Front,
        Back,
        Left,
        Right,
        Floor,
        Ceiling
    }

    public enum ResolutionMode
    {
        Height,
        Width,
        Depth
    }

    public enum VisualMode
    {
        Default,
        DebugMaterialA,
        DebugMaterialB
    }

    [Serializable]
    private sealed class SurfaceRig
    {
        public SurfaceId id;
        public GameObject wall;
        public MeshRenderer renderer;
        public Material runtimeMaterial;
        public Camera camera;
        public HDAdditionalCameraData cameraData;
        public RenderTexture renderTexture;
        public bool ownsRenderTexture;
        public readonly Vector3[] cornersWorld = new Vector3[4];
    }

    private const string CamerasContainerName = "Cameras";
    private const string WallsContainerName = "Walls";

    [Header("References")]
    [SerializeField] private GameObject cameraPrefab;

    [Header("Room Dimensions (meters)")]
    [Min(0.01f)][SerializeField] private float roomWidth = 5f;
    [Min(0.01f)][SerializeField] private float roomHeight = 3f;
    [Min(0.01f)][SerializeField] private float roomDepth = 5f;
    [Tooltip("Room geometry offset from the controller. Scene edge edits adjust this to keep the opposite edge and camera position fixed.")]
    [SerializeField] private Vector3 roomOffset;

    [Header("Enabled Surfaces")]
    [SerializeField] private bool leftWall = true;
    [SerializeField] private bool rightWall = true;
    [SerializeField] private bool frontWall = true;
    [SerializeField] private bool backWall = true;
    [SerializeField] private bool floor = true;
    [SerializeField] private bool ceiling = true;

    [Header("Resolution Settings")]
    [SerializeField] private ResolutionMode resolutionMode = ResolutionMode.Height;
    [Min(16)][SerializeField] private int desiredResolutionValue = 1080;
    [Min(16)][SerializeField] private int resolutionHeight = 1080;
    [Min(16)][SerializeField] private int resolutionWidth = 1920;
    [Min(16)][SerializeField] private int resolutionDepth = 1080;
    [SerializeField] private int depthBufferBits = 24;
    [SerializeField] private RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGB32;
    [Range(1, 4)]
    [SerializeField] private int resolutionDivider = 1;

    [Header("Rendering Quality")]
    [Tooltip("TAA smooths edges and soft-shadow noise. Choose SMAA for fast motion that shows temporal trails.")]
    [SerializeField] private HDAdditionalCameraData.AntialiasingMode antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
    [Tooltip("Reduce visible banding in smooth lighting gradients on the final output.")]
    [SerializeField] private bool dithering = true;

    [Header("Visual Settings")]
    [Tooltip("Show the room's preview planes without disabling their cameras or texture outputs.")]
    [SerializeField] private bool showRoomPlanes = true;
    [SerializeField] private VisualMode visualMode = VisualMode.Default;
    [SerializeField] private Material debugMaterialA;
    [SerializeField] private Material debugMaterialB;

    [Header("Game View Preview")]
    [Tooltip("Room camera output to display in Game view. The selected surface must be enabled. None disables the preview.")]
    [SerializeField] private SurfaceId previewCamera = SurfaceId.Front;

    [Header("Outputs")]
    [SerializeField] private bool enableSpoutSender = true;
    [SerializeField] private bool enableNdiSender = true;

    [Header("Render Textures")]
    [SerializeField] public RenderTexture leftRT;
    [SerializeField] public RenderTexture rightRT;
    [SerializeField] public RenderTexture frontRT;
    [SerializeField] public RenderTexture backRT;
    [SerializeField] public RenderTexture floorRT;
    [SerializeField] public RenderTexture ceilingRT;

    private readonly Dictionary<SurfaceId, SurfaceRig> _rigs = new Dictionary<SurfaceId, SurfaceRig>(6);
    private Transform _camerasContainer;
    private Transform _wallsContainer;
    private Camera _previewCamera;
    private bool _requiresSync = true;
#if UNITY_EDITOR
    private readonly RenderPipeline.StandardRequest _previewRenderRequest = new RenderPipeline.StandardRequest();
#endif

    private void OnEnable()
    {
        EnsureContainers();
        _requiresSync = true;
        ProcessPendingChanges();
        UpdateRigs();
        UpdateGameViewPreview();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= DelayedProcessPendingChanges;
#endif

        ReleaseGameViewPreview();

        if (!Application.isPlaying)
        {
            ReleaseAllResources();
        }
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= DelayedProcessPendingChanges;
#endif

        ReleaseAllResources();
    }

    private void OnValidate()
    {
        roomWidth = Mathf.Max(0.01f, roomWidth);
        roomHeight = Mathf.Max(0.01f, roomHeight);
        roomDepth = Mathf.Max(0.01f, roomDepth);
        desiredResolutionValue = Mathf.Max(16, desiredResolutionValue);
        resolutionHeight = Mathf.Max(16, resolutionHeight);
        resolutionWidth = Mathf.Max(16, resolutionWidth);
        resolutionDepth = Mathf.Max(16, resolutionDepth);



        NormalizeResolutionInputs();

        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureContainers();
        _requiresSync = true;

#if UNITY_EDITOR
        EditorApplication.delayCall -= DelayedProcessPendingChanges;
        EditorApplication.delayCall += DelayedProcessPendingChanges;
#endif
    }

#if UNITY_EDITOR
    private void DelayedProcessPendingChanges()
    {
        if (!this || !isActiveAndEnabled)
        {
            return;
        }

        ProcessPendingChanges();
        UpdateRigs();
        UpdateGameViewPreview();
    }
#endif

    private void Update()
    {
        ProcessPendingChanges();
        UpdateRigs();
        UpdateGameViewPreview();
    }

    private void UpdateGameViewPreview()
    {
        if (previewCamera == SurfaceId.None)
        {
            ReleaseGameViewPreview();
            return;
        }

#if UNITY_EDITOR
        // Game view only renders display cameras in Edit mode, so refresh the selected texture explicitly.
        if (!Application.isPlaying && RenderPipelineManager.currentPipeline is HDRenderPipeline
            && _rigs.TryGetValue(previewCamera, out var rig) && rig.camera != null
            && rig.camera.isActiveAndEnabled && rig.renderTexture != null)
        {
            _previewRenderRequest.destination = rig.renderTexture;
            RenderPipeline.SubmitRenderRequest(rig.camera, _previewRenderRequest);
        }
#endif

        if (_previewCamera != null)
        {
            return;
        }

        // A display camera keeps Game view active while the room cameras keep rendering to textures.
        var go = new GameObject("Game View Preview") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(transform, false);
        _previewCamera = go.AddComponent<Camera>();
        _previewCamera.clearFlags = CameraClearFlags.SolidColor;
        _previewCamera.backgroundColor = Color.black;
        _previewCamera.cullingMask = 0;
        _previewCamera.depth = 100f;
        _previewCamera.targetDisplay = 0;
        _previewCamera.allowHDR = false;
        _previewCamera.allowMSAA = false;
        var cameraData = go.AddComponent<HDAdditionalCameraData>();
        cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        cameraData.backgroundColorHDR = Color.black;
        cameraData.xrRendering = false;
        cameraData.fullscreenPassthrough = true;
        RenderPipelineManager.endContextRendering += RenderGameViewPreview;
    }

    private void RenderGameViewPreview(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (_previewCamera == null || !isActiveAndEnabled || !cameras.Contains(_previewCamera))
        {
            return;
        }

        // HDRP runs custom camera rendering before its queued room cameras. Present after they finish.
        var camera = _previewCamera;
        context.SetupCameraProperties(camera);
        var target = camera.targetTexture != null
            ? new RenderTargetIdentifier(camera.targetTexture)
            : new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
        var cmd = CommandBufferPool.Get("Immersive Game View Preview");
        try
        {
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, Color.black);

            if (_rigs.TryGetValue(previewCamera, out var rig) && rig.renderTexture != null)
            {
                var texture = rig.renderTexture;
                var viewport = camera.pixelRect;
                var scale = Mathf.Min(viewport.width / texture.width, viewport.height / texture.height);
                var width = texture.width * scale;
                var height = texture.height * scale;
                cmd.SetViewport(new Rect(viewport.x + (viewport.width - width) * 0.5f,
                    viewport.y + (viewport.height - height) * 0.5f, width, height));

                var material = Blitter.GetBlitMaterial(TextureDimension.Tex2D);
                // HDRP's display target uses the opposite vertical orientation to a render texture.
                var scaleBias = camera.targetTexture == null && SystemInfo.graphicsUVStartsAtTop
                    ? new Vector4(1f, -1f, 0f, 1f)
                    : new Vector4(1f, 1f, 0f, 0f);
                Blitter.BlitTexture(cmd, new RenderTargetIdentifier(texture), scaleBias,
                    material, material.FindPass("Bilinear"));
            }

            context.ExecuteCommandBuffer(cmd);
            context.Submit();
        }
        finally
        {
            CommandBufferPool.Release(cmd);
        }
    }

    private void ReleaseGameViewPreview()
    {
        RenderPipelineManager.endContextRendering -= RenderGameViewPreview;

        if (_previewCamera != null)
        {
            _previewCamera.enabled = false;
            SafeDestroy(_previewCamera.gameObject);
            _previewCamera = null;
        }
    }

    private void ProcessPendingChanges()
    {
        if (!this)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureContainers();
        CleanupDuplicateGeneratedChildren();

        if (_requiresSync)
        {
            SyncRigs();
            _requiresSync = false;
        }
    }

    private void EnsureContainers()
    {
        if (_camerasContainer == null)
        {
            _camerasContainer = FindOrCreateContainer(CamerasContainerName);
        }

        if (_wallsContainer == null)
        {
            _wallsContainer = FindOrCreateContainer(WallsContainerName);
        }
    }

    private Transform FindOrCreateContainer(string containerName)
    {
        var child = transform.Find(containerName);
        if (child != null)
        {
            return child;
        }

        var go = new GameObject(containerName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void SyncRigs()
    {
        SyncSingleRig(SurfaceId.Front, frontWall);
        SyncSingleRig(SurfaceId.Back, backWall);
        SyncSingleRig(SurfaceId.Left, leftWall);
        SyncSingleRig(SurfaceId.Right, rightWall);
        SyncSingleRig(SurfaceId.Floor, floor);
        SyncSingleRig(SurfaceId.Ceiling, ceiling);
    }

    private void SyncSingleRig(SurfaceId id, bool shouldExist)
    {
        _rigs.TryGetValue(id, out var rig);

        if (shouldExist)
        {
            if (rig == null)
            {
                rig = TryRebindExistingRig(id) ?? CreateRig(id);
                _rigs[id] = rig;
            }
            return;
        }

        if (rig != null)
        {
            DestroyRig(rig);
            _rigs.Remove(id);
        }
        else
        {
            DestroyExistingRigObjects(id);
        }
    }

    private SurfaceRig TryRebindExistingRig(SurfaceId id)
    {
        if (_wallsContainer == null || _camerasContainer == null)
        {
            return null;
        }

        var wall = FindFirstChildByExactName(_wallsContainer, id + "_Wall");
        var cameraTransform = FindFirstChildByExactName(_camerasContainer, id + "_Camera");

        if (wall == null || cameraTransform == null)
        {
            return null;
        }

        var camera = cameraTransform.GetComponent<Camera>();
        if (camera == null)
        {
            camera = cameraTransform.gameObject.AddComponent<Camera>();
        }

        var renderer = wall.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return null;
        }

        var rig = new SurfaceRig
        {
            id = id,
            wall = wall.gameObject,
            renderer = renderer,
            camera = camera,
            renderTexture = camera.targetTexture as RenderTexture
        };

        rig.runtimeMaterial = renderer.sharedMaterial;
        return rig;
    }

    private void DestroyExistingRigObjects(SurfaceId id)
    {
        DestroyAllChildrenByExactName(_wallsContainer, id + "_Wall");
        DestroyAllChildrenByExactName(_camerasContainer, id + "_Camera");
    }

    private void CleanupDuplicateGeneratedChildren()
    {
        CleanupDuplicatesForSurface(SurfaceId.Front);
        CleanupDuplicatesForSurface(SurfaceId.Back);
        CleanupDuplicatesForSurface(SurfaceId.Left);
        CleanupDuplicatesForSurface(SurfaceId.Right);
        CleanupDuplicatesForSurface(SurfaceId.Floor);
        CleanupDuplicatesForSurface(SurfaceId.Ceiling);
    }

    private void CleanupDuplicatesForSurface(SurfaceId id)
    {
        KeepOnlyFirstChildByExactName(_wallsContainer, id + "_Wall");
        KeepOnlyFirstChildByExactName(_camerasContainer, id + "_Camera");
    }

    private static Transform FindFirstChildByExactName(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static void KeepOnlyFirstChildByExactName(Transform parent, string name)
    {
        if (parent == null)
        {
            return;
        }

        Transform first = null;

        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (!string.Equals(child.name, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (first == null)
            {
                first = child;
                continue;
            }

            SafeDestroy(child.gameObject);
        }
    }

    private static void DestroyAllChildrenByExactName(Transform parent, string name)
    {
        if (parent == null)
        {
            return;
        }

        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.Ordinal))
            {
                SafeDestroy(child.gameObject);
            }
        }
    }

    private SurfaceRig CreateRig(SurfaceId id)
    {
        var rig = new SurfaceRig { id = id };

        rig.wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        rig.wall.name = id + "_Wall";
        rig.wall.transform.SetParent(_wallsContainer, false);

        var collider = rig.wall.GetComponent<Collider>();
        if (collider != null)
        {
            SafeDestroy(collider);
        }

        rig.renderer = rig.wall.GetComponent<MeshRenderer>();
        rig.runtimeMaterial = new Material(Shader.Find("Unlit/Texture"))
        {
            name = id + "_WallRuntimeMat"
        };
        rig.renderer.sharedMaterial = rig.runtimeMaterial;

        if (cameraPrefab != null)
        {
            var camGo = Instantiate(cameraPrefab, _camerasContainer);
            camGo.name = id + "_Camera";
            rig.camera = camGo.GetComponent<Camera>();
            if (rig.camera == null)
            {
                rig.camera = camGo.AddComponent<Camera>();
            }
        }
        else
        {
            var camGo = new GameObject(id + "_Camera");
            camGo.transform.SetParent(_camerasContainer, false);
            rig.camera = camGo.AddComponent<Camera>();
        }

        rig.camera.enabled = true;
        return rig;
    }

    private void UpdateRigs()
    {
        if (_camerasContainer == null)
        {
            return;
        }

        var eye = _camerasContainer.position;

        foreach (var pair in _rigs)
        {
            var rig = pair.Value;
            UpdateCameraBackground(rig);
            UpdateSurfaceGeometry(rig);
            UpdateRenderTexture(rig);
            UpdateWallMaterial(rig);
            UpdateCameraProjection(rig, eye);
            UpdateSenderState(rig);
        }
    }

    private void UpdateCameraBackground(SurfaceRig rig)
    {
        if (rig.camera == null)
        {
            return;
        }

        rig.camera.clearFlags = CameraClearFlags.SolidColor;
        rig.camera.backgroundColor = Color.black;

        if (rig.cameraData == null)
        {
            rig.cameraData = rig.camera.GetComponent<HDAdditionalCameraData>();
            if (rig.cameraData == null)
            {
                rig.cameraData = rig.camera.gameObject.AddComponent<HDAdditionalCameraData>();
            }
        }

        rig.cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        rig.cameraData.backgroundColorHDR = Color.black;
        rig.cameraData.antialiasing = antialiasing;
        rig.cameraData.SMAAQuality = HDAdditionalCameraData.SMAAQualityLevel.High;
        rig.cameraData.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
        rig.cameraData.taaHistorySharpening = 0.25f;
        rig.cameraData.taaAntiFlicker = 0.6f;
        rig.cameraData.taaMotionVectorRejection = 0.5f;
        rig.cameraData.taaAntiHistoryRinging = true;
        rig.cameraData.dithering = dithering;
    }

    private void UpdateSurfaceGeometry(SurfaceRig rig)
    {
        GetSurfaceData(rig.id, out var centerLocal, out var rightLocal, out var upLocal, out var width, out var height);

        var normalLocal = Vector3.Cross(rightLocal, upLocal).normalized;
        var worldCenter = transform.TransformPoint(centerLocal);
        var worldRight = transform.TransformDirection(rightLocal).normalized;
        var worldUp = transform.TransformDirection(upLocal).normalized;

        rig.cornersWorld[0] = worldCenter - worldRight * (width * 0.5f) - worldUp * (height * 0.5f);
        rig.cornersWorld[1] = worldCenter + worldRight * (width * 0.5f) - worldUp * (height * 0.5f);
        rig.cornersWorld[2] = worldCenter - worldRight * (width * 0.5f) + worldUp * (height * 0.5f);
        rig.cornersWorld[3] = worldCenter + worldRight * (width * 0.5f) + worldUp * (height * 0.5f);

        rig.wall.transform.localPosition = centerLocal;
        rig.wall.transform.localRotation = Quaternion.LookRotation(normalLocal, upLocal);
        rig.wall.transform.localScale = new Vector3(width, height, 1f);
    }

    private void GetSurfaceData(
        SurfaceId id,
        out Vector3 center,
        out Vector3 right,
        out Vector3 up,
        out float width,
        out float height)
    {
        switch (id)
        {
            case SurfaceId.Front:
                center = new Vector3(0f, roomHeight * 0.5f, 0f);
                right = Vector3.right;
                up = Vector3.up;
                width = roomWidth;
                height = roomHeight;
                break;

            case SurfaceId.Back:
                center = new Vector3(0f, roomHeight * 0.5f, -roomDepth);
                right = -Vector3.right;
                up = Vector3.up;
                width = roomWidth;
                height = roomHeight;
                break;

            case SurfaceId.Left:
                center = new Vector3(-roomWidth * 0.5f, roomHeight * 0.5f, -roomDepth * 0.5f);
                right = Vector3.forward;
                up = Vector3.up;
                width = roomDepth;
                height = roomHeight;
                break;

            case SurfaceId.Right:
                center = new Vector3(roomWidth * 0.5f, roomHeight * 0.5f, -roomDepth * 0.5f);
                right = -Vector3.forward;
                up = Vector3.up;
                width = roomDepth;
                height = roomHeight;
                break;

            case SurfaceId.Floor:
                center = new Vector3(0f, 0f, -roomDepth * 0.5f);
                right = Vector3.right;
                up = -Vector3.forward;
                width = roomWidth;
                height = roomDepth;
                break;

            case SurfaceId.Ceiling:
                center = new Vector3(0f, roomHeight, -roomDepth * 0.5f);
                right = Vector3.right;
                up = Vector3.forward;
                width = roomWidth;
                height = roomDepth;
                break;

            default:
                center = Vector3.zero;
                right = Vector3.right;
                up = Vector3.up;
                width = 1f;
                height = 1f;
                break;
        }

        center += roomOffset;
    }

    private void UpdateRenderTexture(SurfaceRig rig)
    {
        var assignedRenderTexture = GetAssignedRenderTexture(rig.id);

        if (assignedRenderTexture != null)
        {
            if (rig.renderTexture != assignedRenderTexture)
            {
                if (rig.camera != null && rig.camera.targetTexture == rig.renderTexture)
                {
                    rig.camera.targetTexture = null;
                }

                if (rig.ownsRenderTexture && rig.renderTexture != null)
                {
                    SafeDestroy(rig.renderTexture);
                }

                rig.renderTexture = assignedRenderTexture;
                rig.ownsRenderTexture = false;
            }

            rig.camera.targetTexture = rig.renderTexture;
            return;
        }

        GetSurfaceData(rig.id, out _, out _, out _, out var wallWidth, out var wallHeight);
        var size = ComputeRenderTextureSize(wallWidth, wallHeight) / resolutionDivider;

        if (!rig.ownsRenderTexture && rig.renderTexture != null)
        {
            if (rig.camera != null && rig.camera.targetTexture == rig.renderTexture)
            {
                rig.camera.targetTexture = null;
            }

            rig.renderTexture = null;
        }

        if (rig.renderTexture != null && (rig.renderTexture.width != size.x || rig.renderTexture.height != size.y))
        {
            if (rig.camera != null && rig.camera.targetTexture == rig.renderTexture)
            {
                rig.camera.targetTexture = null;
            }

            if (rig.runtimeMaterial != null)
            {
                SetMaterialTexture(rig.runtimeMaterial, null);
            }

            SafeDestroy(rig.renderTexture);
            rig.renderTexture = null;
        }

        if (rig.renderTexture == null)
        {
            rig.renderTexture = new RenderTexture(size.x, size.y, depthBufferBits, renderTextureFormat)
            {
                name = rig.id.ToString(),
                antiAliasing = 1,
                autoGenerateMips = false,
                useMipMap = false
            };
            rig.renderTexture.Create();
            rig.ownsRenderTexture = true;
        }

        if (rig.ownsRenderTexture)
        {
            rig.renderTexture.name = rig.id.ToString();
        }

        rig.camera.targetTexture = rig.renderTexture;
    }

    private RenderTexture GetAssignedRenderTexture(SurfaceId id)
    {
        switch (id)
        {
            case SurfaceId.Left:
                return leftRT;
            case SurfaceId.Right:
                return rightRT;
            case SurfaceId.Front:
                return frontRT;
            case SurfaceId.Back:
                return backRT;
            case SurfaceId.Floor:
                return floorRT;
            case SurfaceId.Ceiling:
                return ceilingRT;
            default:
                return null;
        }
    }

    private Vector2Int ComputeRenderTextureSize(float wallWidth, float wallHeight)
    {
        // Thin rooms can otherwise exceed the GPU texture limit while dragging an edge.
        var maximumResolution = Mathf.Max(16, SystemInfo.maxTextureSize / 16 * 16);
        var pixelsPerMeter = Mathf.Min(GetPixelsPerMeter(),
            maximumResolution / Mathf.Max(wallWidth, wallHeight));
        var width = ComputeAlignedResolution(wallWidth, pixelsPerMeter);
        var height = ComputeAlignedResolution(wallHeight, pixelsPerMeter);

        return new Vector2Int(width, height);
    }

    private static int ComputeAlignedResolution(float surfaceSizeMeters, float pixelsPerMeter)
    {
        var resolution = Mathf.Max(16, Mathf.RoundToInt(surfaceSizeMeters * pixelsPerMeter));

        // NDI requires dimensions aligned to 16-pixel blocks.
        return AlignUpToMultiple(resolution, 16);
    }

    private static int AlignUpToMultiple(int value, int multiple)
    {
        if (multiple <= 1)
        {
            return value;
        }

        var remainder = value % multiple;
        return remainder == 0 ? value : value + (multiple - remainder);
    }

    private float GetPixelsPerMeter()
    {
        var referenceSizeMeters = GetReferenceDimensionMeters();
        var pixelsPerMeter = desiredResolutionValue / Mathf.Max(0.01f, referenceSizeMeters);
        return pixelsPerMeter;
    }

    private void NormalizeResolutionInputs()
    {
        var referenceSizeMeters = GetReferenceDimensionMeters();
        var pixelsPerMeter = desiredResolutionValue / Mathf.Max(0.01f, referenceSizeMeters);

        resolutionWidth = ComputeAlignedResolution(roomWidth, pixelsPerMeter);
        resolutionHeight = ComputeAlignedResolution(roomHeight, pixelsPerMeter);
        resolutionDepth = ComputeAlignedResolution(roomDepth, pixelsPerMeter);
    }

    private float GetReferenceDimensionMeters()
    {
        switch (resolutionMode)
        {
            case ResolutionMode.Width:
                return roomWidth;

            case ResolutionMode.Depth:
                return roomDepth;

            case ResolutionMode.Height:
            default:
                return roomHeight;
        }
    }

    private void UpdateWallMaterial(SurfaceRig rig)
    {
        if (rig.renderer == null)
        {
            return;
        }

        rig.renderer.enabled = showRoomPlanes;

        if (visualMode == VisualMode.DebugMaterialA && debugMaterialA != null)
        {
            rig.renderer.sharedMaterial = debugMaterialA;
            return;
        }

        if (visualMode == VisualMode.DebugMaterialB && debugMaterialB != null)
        {
            rig.renderer.sharedMaterial = debugMaterialB;
            return;
        }

        if (rig.runtimeMaterial == null)
        {
            rig.runtimeMaterial = new Material(Shader.Find("Unlit/Texture"))
            {
                name = rig.id + "_WallRuntimeMat"
            };
        }

        SetMaterialTexture(rig.runtimeMaterial, rig.renderTexture);
        rig.renderer.sharedMaterial = rig.runtimeMaterial;
    }

    private static void SetMaterialTexture(Material material, Texture texture)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BlitTexture"))
        {
            material.SetTexture("_BlitTexture", texture);
        }
    }

    private void UpdateCameraProjection(SurfaceRig rig, Vector3 eyeWorld)
    {
        if (rig.camera == null)
        {
            return;
        }

        var pa = rig.cornersWorld[0];
        var pb = rig.cornersWorld[1];
        var pc = rig.cornersWorld[2];

        var screenRight = (pb - pa).normalized;
        var screenUp = (pc - pa).normalized;
        var screenNormal = Vector3.Cross(screenRight, screenUp).normalized;

        if (Vector3.Dot(screenNormal, eyeWorld - pa) < 0f)
        {
            screenNormal = -screenNormal;
        }

        var cameraForward = -screenNormal;
        if (cameraForward.sqrMagnitude <= 0f)
        {
            return;
        }

        rig.camera.transform.SetPositionAndRotation(
            eyeWorld,
            Quaternion.LookRotation(cameraForward, screenUp));

        var va = pa - eyeWorld;
        var vb = pb - eyeWorld;
        var vc = pc - eyeWorld;

        var distanceToPlane = Vector3.Dot(cameraForward, va);
        if (distanceToPlane <= 0.001f)
        {
            return;
        }

        var near = rig.camera.nearClipPlane;
        var far = rig.camera.farClipPlane;

        var left = Vector3.Dot(screenRight, va) * near / distanceToPlane;
        var right = Vector3.Dot(screenRight, vb) * near / distanceToPlane;
        var bottom = Vector3.Dot(screenUp, va) * near / distanceToPlane;
        var top = Vector3.Dot(screenUp, vc) * near / distanceToPlane;

        rig.camera.projectionMatrix = PerspectiveOffCenter(left, right, bottom, top, near, far);
    }

    private static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
    {
        var m = new Matrix4x4();

        var x = (2f * near) / (right - left);
        var y = (2f * near) / (top - bottom);
        var a = (right + left) / (right - left);
        var b = (top + bottom) / (top - bottom);
        var c = -(far + near) / (far - near);
        var d = -(2f * far * near) / (far - near);

        m[0, 0] = x;
        m[0, 1] = 0f;
        m[0, 2] = a;
        m[0, 3] = 0f;

        m[1, 0] = 0f;
        m[1, 1] = y;
        m[1, 2] = b;
        m[1, 3] = 0f;

        m[2, 0] = 0f;
        m[2, 1] = 0f;
        m[2, 2] = c;
        m[2, 3] = d;

        m[3, 0] = 0f;
        m[3, 1] = 0f;
        m[3, 2] = -1f;
        m[3, 3] = 0f;

        return m;
    }

    private void UpdateSenderState(SurfaceRig rig)
    {
        if (rig.camera == null)
        {
            return;
        }

        var cameraObject = rig.camera.gameObject;
        var streamName = rig.id.ToString();
        var spoutAllowed = enableSpoutSender && IsSpoutAllowedOnCurrentGraphicsApi();

        ConfigureSenderComponent(cameraObject, new[] { "SpoutSender" }, spoutAllowed, streamName, rig.camera, rig.renderTexture, true);
        ConfigureSenderComponent(cameraObject, new[] { "NDISender", "NdiSender" }, enableNdiSender, streamName, rig.camera, rig.renderTexture, true);
    }

    private static bool IsSpoutAllowedOnCurrentGraphicsApi()
    {
        // Spout supports D3D11 only. In a D3D12 editor it will stay disabled.
        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || Application.isPlaying;
    }

    private static void ConfigureSenderComponent(
        GameObject go,
        string[] typeNames,
        bool enabled,
        string streamName,
        Camera sourceCamera,
        RenderTexture sourceTexture,
        bool forceTextureCapture)
    {
        var behaviours = go.GetComponents<MonoBehaviour>();
        for (var i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            if (!MatchesTypeName(behaviour.GetType(), typeNames))
            {
                continue;
            }

            behaviour.enabled = enabled;
            SetSenderName(behaviour, streamName);
            SetCameraMember(behaviour, "sourceCamera", sourceCamera);
            SetTextureMember(behaviour, "sourceTexture", sourceTexture);

            if (forceTextureCapture)
            {
                SetEnumMemberByName(behaviour, "captureMethod", "Texture");
            }
        }
    }

    private static bool MatchesTypeName(Type type, string[] typeNames)
    {
        for (var i = 0; i < typeNames.Length; i++)
        {
            if (string.Equals(type.Name, typeNames[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetSenderName(MonoBehaviour behaviour, string value)
    {
        SetStringMember(behaviour, "spoutName", value);
        SetStringMember(behaviour, "ndiName", value);
        SetStringMember(behaviour, "senderName", value);
        SetStringMember(behaviour, "streamName", value);
    }

    private static void SetStringMember(object target, string memberName, string value)
    {
        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(target, value);
            return;
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(string))
        {
            field.SetValue(target, value);
        }
    }

    private static void SetTextureMember(object target, string memberName, Texture value)
    {
        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && typeof(Texture).IsAssignableFrom(property.PropertyType))
        {
            property.SetValue(target, value);
            return;
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && typeof(Texture).IsAssignableFrom(field.FieldType))
        {
            field.SetValue(target, value);
        }
    }

    private static void SetCameraMember(object target, string memberName, Camera value)
    {
        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && typeof(Camera).IsAssignableFrom(property.PropertyType))
        {
            property.SetValue(target, value);
            return;
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && typeof(Camera).IsAssignableFrom(field.FieldType))
        {
            field.SetValue(target, value);
        }
    }

    private static void SetEnumMemberByName(object target, string memberName, string enumValueName)
    {
        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType.IsEnum)
        {
            var enumValue = FindEnumValue(property.PropertyType, enumValueName);
            if (enumValue != null)
            {
                property.SetValue(target, enumValue);
            }
            return;
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsEnum)
        {
            var enumValue = FindEnumValue(field.FieldType, enumValueName);
            if (enumValue != null)
            {
                field.SetValue(target, enumValue);
            }
        }
    }

    private static object FindEnumValue(Type enumType, string enumValueName)
    {
        var names = Enum.GetNames(enumType);
        for (var i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], enumValueName, StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse(enumType, names[i]);
            }
        }

        return null;
    }

    private void DestroyRig(SurfaceRig rig)
    {
        if (rig.runtimeMaterial != null)
        {
            SetMaterialTexture(rig.runtimeMaterial, null);
            SafeDestroy(rig.runtimeMaterial);
        }

        if (rig.renderTexture != null)
        {
            if (rig.camera != null && rig.camera.targetTexture == rig.renderTexture)
            {
                rig.camera.targetTexture = null;
            }

            if (rig.ownsRenderTexture)
            {
                SafeDestroy(rig.renderTexture);
            }
        }

        if (rig.wall != null)
        {
            SafeDestroy(rig.wall);
        }

        if (rig.camera != null)
        {
            SafeDestroy(rig.camera.gameObject);
        }
    }

    private void ReleaseAllResources()
    {
        ReleaseGameViewPreview();

        foreach (var pair in _rigs)
        {
            DestroyRig(pair.Value);
        }

        _rigs.Clear();
    }

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }
}
