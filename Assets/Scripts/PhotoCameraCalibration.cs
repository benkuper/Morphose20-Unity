using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Camera/Photo Camera Calibration")]
public class PhotoCameraCalibration : MonoBehaviour
{
    [Serializable]
    public class Landmark
    {
        public string label;
        public Transform surface;
        public Vector3 localPoint;
        public Vector2 imagePoint;
        public bool hasImagePoint;

        public bool IsComplete => surface != null && hasImagePoint;
        public Vector3 WorldPoint => surface.TransformPoint(localPoint);
    }

    [SerializeField, Tooltip("Leave empty to calibrate the Camera on this GameObject.")]
    private Camera targetCamera;

    [SerializeField, Tooltip("Root of the static model whose vertices you will pick in the Scene view.")]
    private Transform modelRoot;

    [SerializeField, Tooltip("The full-screen Raw Image showing the reference picture.")]
    private RawImage referenceImage;

    [SerializeField, Tooltip("Set the Game view to this resolution as well. Used for fitting and reporting pixel error.")]
    private Vector2Int imageResolution = new Vector2Int(1280, 1920);

    [SerializeField, Tooltip("Fit perspective field of view along with camera pose. Disable to keep the current FOV.")]
    private bool fitFieldOfView = true;

    [SerializeField, HideInInspector]
    private List<Landmark> landmarks = new List<Landmark>();

    public Camera TargetCamera => targetCamera != null ? targetCamera : GetComponent<Camera>();
    public Transform ModelRoot => modelRoot;
    public RawImage ReferenceImage => referenceImage;
    public Vector2Int ImageResolution => imageResolution;
    public bool FitFieldOfView => fitFieldOfView;

    // Authoring data is kept in the scene so correspondences survive closing the tool.
    public List<Landmark> Landmarks => landmarks;
}
