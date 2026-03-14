using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class WaterReflection
{
    private static readonly int PlanarReflectionTextureId = Shader.PropertyToID("_PlanarReflectionTex");
    private static readonly int PlanarReflectionVpId = Shader.PropertyToID("_PlanarReflectionVP");

    private readonly Transform _ownerTransform;
    private readonly Material _fluidMaterial;
    private readonly List<MeshRenderer> _visibleFluidRenderers = new(128);
    private readonly List<MeshRenderer> _hiddenRenderers = new(128);
    private readonly Plane[] _cameraFrustumPlanes = new Plane[6];

    private Camera _planarReflectionCamera;
    private Skybox _planarReflectionSkybox;
    private UniversalAdditionalCameraData _planarReflectionCameraData;
    private UniversalRenderPipeline.SingleCameraRequest _planarReflectionRequest;
    private RenderTexture _planarReflectionTexture;
    private bool _enabled;
    private bool _isRendering;
    private int _lastRenderFrame = -999999;
    private float _renderScale;
    private LayerMask _cullingMask;
    private float _clipPlaneOffset;
    private bool _renderShadows;
    private bool _allowHdr;
    private float _farClip;
    private int _updateInterval;

    public WaterReflection(Transform ownerTransform, Material fluidMaterial)
    {
        _ownerTransform = ownerTransform;
        _fluidMaterial = fluidMaterial;
    }

    public void UpdateSettings(
        bool enabled,
        float renderScale,
        LayerMask cullingMask,
        float clipPlaneOffset,
        bool renderShadows,
        bool allowHdr,
        float farClip,
        int updateInterval)
    {
        _enabled = enabled;
        _renderScale = renderScale;
        _cullingMask = cullingMask;
        _clipPlaneOffset = clipPlaneOffset;
        _renderShadows = renderShadows;
        _allowHdr = allowHdr;
        _farClip = farClip;
        _updateInterval = updateInterval;

        if (!_enabled)
        {
            ApplyFallback();
        }
    }

    public void ResetVisibleFluidRenderers()
    {
        _visibleFluidRenderers.Clear();
    }

    public void AddVisibleFluidRenderer(MeshRenderer renderer)
    {
        if (renderer != null)
        {
            _visibleFluidRenderers.Add(renderer);
        }
    }

    public void TryRender(
        Camera sourceCamera,
        Camera interactionCamera,
        float planeY,
        Func<WorldDebug.OverlayVisibility> hideOverlay,
        Action<WorldDebug.OverlayVisibility> restoreOverlay)
    {
        if (!_enabled || _fluidMaterial == null || sourceCamera == null || sourceCamera.pixelWidth <= 0 || sourceCamera.pixelHeight <= 0)
        {
            ApplyFallback();
            return;
        }

        if (_isRendering || interactionCamera == null || sourceCamera != interactionCamera || sourceCamera == _planarReflectionCamera)
        {
            return;
        }

        if (!HasVisibleFluidGeometry(sourceCamera))
        {
            ApplyFallback();
            return;
        }

        int updateInterval = Mathf.Max(1, _updateInterval);
        if (_planarReflectionTexture != null && Time.frameCount - _lastRenderFrame < updateInterval)
        {
            return;
        }

        EnsurePlanarReflectionCamera();
        EnsurePlanarReflectionTexture(sourceCamera);
        if (_planarReflectionCamera == null || _planarReflectionTexture == null)
        {
            ApplyFallback();
            return;
        }

        _planarReflectionCamera.CopyFrom(sourceCamera);
        _planarReflectionCamera.enabled = false;
        _planarReflectionCamera.targetTexture = _planarReflectionTexture;
        _planarReflectionCamera.cullingMask = sourceCamera.cullingMask & _cullingMask.value;
        _planarReflectionCamera.useOcclusionCulling = sourceCamera.useOcclusionCulling;
        _planarReflectionCamera.allowHDR = _allowHdr;
        _planarReflectionCamera.allowMSAA = false;
        _planarReflectionCamera.depthTextureMode = DepthTextureMode.None;
        _planarReflectionCamera.farClipPlane = Mathf.Min(sourceCamera.farClipPlane, _farClip);
        _planarReflectionCamera.ResetWorldToCameraMatrix();
        _planarReflectionCamera.ResetProjectionMatrix();
        _planarReflectionCamera.ResetCullingMatrix();

        if (sourceCamera.TryGetComponent(out Skybox sourceSkybox))
        {
            _planarReflectionSkybox.enabled = sourceSkybox.enabled;
            _planarReflectionSkybox.material = sourceSkybox.material;
        }

        UniversalAdditionalCameraData sourceAdditionalData = sourceCamera.GetUniversalAdditionalCameraData();
        if (sourceAdditionalData != null && _planarReflectionCameraData != null)
        {
            _planarReflectionCameraData.renderType = CameraRenderType.Base;
            _planarReflectionCameraData.renderShadows = _renderShadows && sourceAdditionalData.renderShadows;
            _planarReflectionCameraData.renderPostProcessing = false;
            _planarReflectionCameraData.requiresColorOption = CameraOverrideOption.Off;
            _planarReflectionCameraData.requiresDepthOption = CameraOverrideOption.Off;
            _planarReflectionCameraData.allowXRRendering = false;
            _planarReflectionCameraData.volumeLayerMask = 0;
            _planarReflectionCameraData.volumeTrigger = null;
        }

        Vector3 planePosition = new(0f, planeY, 0f);
        Vector3 planeNormal = Vector3.up;
        float planeOffset = -Vector3.Dot(planeNormal, planePosition) - _clipPlaneOffset;
        Vector4 reflectionPlane = new(planeNormal.x, planeNormal.y, planeNormal.z, planeOffset);
        Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(reflectionPlane);

        Vector3 sourcePosition = sourceCamera.transform.position;
        Vector3 reflectedPosition = reflectionMatrix.MultiplyPoint(sourcePosition);
        Vector3 sourceEuler = sourceCamera.transform.eulerAngles;
        Vector3 reflectedEuler = new(-sourceEuler.x, sourceEuler.y, sourceEuler.z);

        _planarReflectionCamera.transform.position = reflectedPosition;
        _planarReflectionCamera.transform.eulerAngles = reflectedEuler;
        _planarReflectionCamera.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * reflectionMatrix;

        Vector4 clipPlane = CameraSpacePlane(_planarReflectionCamera, planePosition, planeNormal, 1f, _clipPlaneOffset);
        _planarReflectionCamera.projectionMatrix = sourceCamera.CalculateObliqueMatrix(clipPlane);
        _planarReflectionCamera.cullingMatrix = _planarReflectionCamera.projectionMatrix * _planarReflectionCamera.worldToCameraMatrix;

        WorldDebug.OverlayVisibility overlayVisibility = hideOverlay != null ? hideOverlay() : default;
        HideFluidRenderers();

        _isRendering = true;
        bool previousInvertCulling = GL.invertCulling;

        try
        {
            GL.invertCulling = true;
            _planarReflectionRequest.destination = _planarReflectionTexture;
            _planarReflectionRequest.mipLevel = 0;
            _planarReflectionRequest.slice = 0;
            _planarReflectionRequest.face = CubemapFace.Unknown;

            if (RenderPipeline.SupportsRenderRequest(_planarReflectionCamera, _planarReflectionRequest))
            {
                RenderPipeline.SubmitRenderRequest(_planarReflectionCamera, _planarReflectionRequest);
            }
        }
        finally
        {
            GL.invertCulling = previousInvertCulling;
            _isRendering = false;
            RestoreAfterRender(overlayVisibility, restoreOverlay);
        }

        Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(_planarReflectionCamera.projectionMatrix, true);
        Shader.SetGlobalTexture(PlanarReflectionTextureId, _planarReflectionTexture);
        Shader.SetGlobalMatrix(PlanarReflectionVpId, gpuProjection * _planarReflectionCamera.worldToCameraMatrix);
        _fluidMaterial.SetTexture(PlanarReflectionTextureId, _planarReflectionTexture);
        _fluidMaterial.SetMatrix(PlanarReflectionVpId, gpuProjection * _planarReflectionCamera.worldToCameraMatrix);
        _lastRenderFrame = Time.frameCount;
    }

    public void ReleaseResources()
    {
        ApplyFallback();

        if (_planarReflectionTexture != null)
        {
            _planarReflectionTexture.Release();
            UnityEngine.Object.Destroy(_planarReflectionTexture);
            _planarReflectionTexture = null;
        }

        if (_planarReflectionCamera != null)
        {
            UnityEngine.Object.Destroy(_planarReflectionCamera.gameObject);
            _planarReflectionCamera = null;
        }

        _planarReflectionSkybox = null;
        _planarReflectionCameraData = null;
        _planarReflectionRequest = null;
    }

    public void ApplyFallback()
    {
        Shader.SetGlobalTexture(PlanarReflectionTextureId, Texture2D.blackTexture);
        Shader.SetGlobalMatrix(PlanarReflectionVpId, Matrix4x4.identity);
        _lastRenderFrame = -999999;
        if (_fluidMaterial != null)
        {
            _fluidMaterial.SetTexture(PlanarReflectionTextureId, Texture2D.blackTexture);
            _fluidMaterial.SetMatrix(PlanarReflectionVpId, Matrix4x4.identity);
        }
    }

    private void EnsurePlanarReflectionCamera()
    {
        if (_planarReflectionCamera != null)
        {
            return;
        }

        GameObject reflectionCameraObject = new("Planar Reflection Camera")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };

        reflectionCameraObject.transform.SetParent(_ownerTransform, false);
        _planarReflectionCamera = reflectionCameraObject.AddComponent<Camera>();
        _planarReflectionCamera.enabled = false;
        _planarReflectionSkybox = _planarReflectionCamera.gameObject.AddComponent<Skybox>();
        _planarReflectionCameraData = _planarReflectionCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        _planarReflectionRequest = new UniversalRenderPipeline.SingleCameraRequest();
    }

    private void EnsurePlanarReflectionTexture(Camera sourceCamera)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(sourceCamera.pixelWidth * _renderScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(sourceCamera.pixelHeight * _renderScale));

        if (_planarReflectionTexture != null && _planarReflectionTexture.width == width && _planarReflectionTexture.height == height)
        {
            return;
        }

        if (_planarReflectionTexture != null)
        {
            _planarReflectionTexture.Release();
            UnityEngine.Object.Destroy(_planarReflectionTexture);
        }

        RenderTextureFormat format = sourceCamera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
        if (!_allowHdr)
        {
            format = RenderTextureFormat.ARGB32;
        }

        _planarReflectionTexture = new RenderTexture(width, height, 16, format)
        {
            name = "PlanarReflectionRT",
            hideFlags = HideFlags.HideAndDontSave,
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        _planarReflectionTexture.Create();
    }

    private bool HasVisibleFluidGeometry(Camera sourceCamera)
    {
        if (_visibleFluidRenderers.Count == 0 || sourceCamera == null)
        {
            return false;
        }

        GeometryUtility.CalculateFrustumPlanes(sourceCamera, _cameraFrustumPlanes);
        for (int i = 0; i < _visibleFluidRenderers.Count; i++)
        {
            MeshRenderer renderer = _visibleFluidRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, renderer.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void HideFluidRenderers()
    {
        _hiddenRenderers.Clear();

        for (int i = 0; i < _visibleFluidRenderers.Count; i++)
        {
            MeshRenderer renderer = _visibleFluidRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            renderer.enabled = false;
            _hiddenRenderers.Add(renderer);
        }
    }

    private void RestoreAfterRender(WorldDebug.OverlayVisibility overlayVisibility, Action<WorldDebug.OverlayVisibility> restoreOverlay)
    {
        for (int i = 0; i < _hiddenRenderers.Count; i++)
        {
            if (_hiddenRenderers[i] != null)
            {
                _hiddenRenderers[i].enabled = true;
            }
        }

        _hiddenRenderers.Clear();
        restoreOverlay?.Invoke(overlayVisibility);
    }

    private static Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sideSign, float clipPlaneOffset)
    {
        Vector3 offsetPosition = position + (normal * clipPlaneOffset);
        Matrix4x4 worldToCameraMatrix = camera.worldToCameraMatrix;
        Vector3 cameraSpacePosition = worldToCameraMatrix.MultiplyPoint(offsetPosition);
        Vector3 cameraSpaceNormal = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(
            cameraSpaceNormal.x,
            cameraSpaceNormal.y,
            cameraSpaceNormal.z,
            -Vector3.Dot(cameraSpacePosition, cameraSpaceNormal));
    }

    private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 reflection = Matrix4x4.zero;

        reflection.m00 = 1f - (2f * plane[0] * plane[0]);
        reflection.m01 = -2f * plane[0] * plane[1];
        reflection.m02 = -2f * plane[0] * plane[2];
        reflection.m03 = -2f * plane[3] * plane[0];

        reflection.m10 = -2f * plane[1] * plane[0];
        reflection.m11 = 1f - (2f * plane[1] * plane[1]);
        reflection.m12 = -2f * plane[1] * plane[2];
        reflection.m13 = -2f * plane[3] * plane[1];

        reflection.m20 = -2f * plane[2] * plane[0];
        reflection.m21 = -2f * plane[2] * plane[1];
        reflection.m22 = 1f - (2f * plane[2] * plane[2]);
        reflection.m23 = -2f * plane[3] * plane[2];

        reflection.m30 = 0f;
        reflection.m31 = 0f;
        reflection.m32 = 0f;
        reflection.m33 = 1f;

        return reflection;
    }
}
