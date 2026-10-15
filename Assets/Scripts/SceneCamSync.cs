using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class SceneCamSync : MonoBehaviour
{
    [SerializeField, Tooltip("Continuously place the Camera at the last active Scene view. Turn off to keep its placement.")]
    private bool followSceneView;

    [SerializeField, Tooltip("Camera to move. Leave empty to use the Camera on this GameObject.")]
    private Camera targetCamera;

    [SerializeField, Tooltip("Also match the Scene view's field of view and perspective/orthographic projection. Disables physical camera properties while following.")]
    private bool syncLens = true;

#if UNITY_EDITOR
    private void OnEnable()
    {
        EditorApplication.update += SyncFromSceneView;
    }

    private void OnDisable()
    {
        EditorApplication.update -= SyncFromSceneView;
    }

    private void SyncFromSceneView()
    {
        if (!followSceneView || !isActiveAndEnabled || EditorUtility.IsPersistent(this))
            return;

        SceneView sceneView = SceneView.lastActiveSceneView;
        Camera destinationCamera = targetCamera != null ? targetCamera : GetComponent<Camera>();
        if (sceneView == null || sceneView.camera == null || destinationCamera == null
            || EditorUtility.IsPersistent(destinationCamera))
            return;

        Camera sourceCamera = sceneView.camera;
        Transform destinationTransform = destinationCamera.transform;
        Transform sourceTransform = sourceCamera.transform;
        bool transformChanged = destinationTransform.position != sourceTransform.position
            || destinationTransform.rotation != sourceTransform.rotation;
        bool lensChanged = syncLens && (destinationCamera.orthographic != sourceCamera.orthographic
            || !Mathf.Approximately(destinationCamera.fieldOfView, sourceCamera.fieldOfView)
            || !Mathf.Approximately(destinationCamera.orthographicSize, sourceCamera.orthographicSize)
            || destinationCamera.usePhysicalProperties
            || destinationCamera.lensShift != sourceCamera.lensShift);

        if (!transformChanged && !lensChanged)
            return;

        bool recordChanges = !Application.IsPlaying(destinationCamera.gameObject);
        if (transformChanged)
        {
            if (recordChanges)
                Undo.RecordObject(destinationTransform, "Follow Scene View");

            destinationTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);

            if (recordChanges)
                PrefabUtility.RecordPrefabInstancePropertyModifications(destinationTransform);
        }

        if (lensChanged)
        {
            if (recordChanges)
                Undo.RecordObject(destinationCamera, "Follow Scene View");

            destinationCamera.usePhysicalProperties = false;
            destinationCamera.orthographic = sourceCamera.orthographic;
            destinationCamera.fieldOfView = sourceCamera.fieldOfView;
            destinationCamera.orthographicSize = sourceCamera.orthographicSize;
            destinationCamera.lensShift = sourceCamera.lensShift;

            if (recordChanges)
                PrefabUtility.RecordPrefabInstancePropertyModifications(destinationCamera);
        }

        // ExecuteAlways alone does not refresh the Game view continuously in Edit mode.
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
#endif
}
