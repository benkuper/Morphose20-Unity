// Run this method body with Unity MCP execute_code. Uses a temporary additive scene.
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name);
    passed++; report.AppendLine("PASS: " + name);
};
Vector3 point = new Vector3(0.371f, -1.237f, 2.819f);
float value = MudaacNoise.SampleSigned(point, 0.719f, 42);
check(value == MudaacNoise.SampleSigned(point, 0.719f, 42), "Noise is deterministic for position, time and seed");
bool bounded = true;
float seedDelta = 0f, channelDelta = 0f, oldDiagonalDelta = 0f, temporalDelta = 0f;
var axisDelta = new float[3];
var vectorDelta = new float[3];
for (int i = 0; i < 128; i++)
{
    Vector3 p = point + new Vector3(i * 0.173f, i * -0.311f, i * 0.227f);
    float n = MudaacNoise.SampleSigned(p, 0.719f, 42);
    float normalized = MudaacNoise.Sample01(p, 0.719f, 42);
    bounded &= !float.IsNaN(n) && n >= -1f && n <= 1f && normalized >= 0f && normalized <= 1f;
    seedDelta += Mathf.Abs(n - MudaacNoise.SampleSigned(p, 0.719f, 43));
    channelDelta += Mathf.Abs(n - MudaacNoise.SampleSigned(p, 0.719f, 42, 1));
    oldDiagonalDelta += Mathf.Abs(n - MudaacNoise.SampleSigned(p + new Vector3(0.37f, 0.37f, -0.37f), 0.719f, 42));
    temporalDelta += Mathf.Abs(n - MudaacNoise.SampleSigned(p, 1.089f, 42));
    for (int axis = 0; axis < 3; axis++)
    {
        Vector3 offset = Vector3.zero; offset[axis] = 0.37f;
        axisDelta[axis] += Mathf.Abs(n - MudaacNoise.SampleSigned(p + offset, 0.719f, 42));
    }
    Vector3 vector = MudaacNoise.SampleVectorSigned(p, 0.719f, 42);
    vectorDelta[0] += Mathf.Abs(vector.x - vector.y);
    vectorDelta[1] += Mathf.Abs(vector.y - vector.z);
    vectorDelta[2] += Mathf.Abs(vector.z - vector.x);
}
check(bounded, "Signed and weight samples stay finite and within their promised ranges");
check(seedDelta > 5f, "Changing seed changes the spatial field");
check(channelDelta > 5f, "Separate channels produce distinct fields");
for (int axis = 0; axis < 3; axis++)
    check(axisDelta[axis] > 5f, "Spatial axis " + axis + " independently changes the noise");
check(oldDiagonalDelta > 5f, "Moving along the old (1,1,-1) projection null direction changes the noise");
check(temporalDelta > 5f, "Time evolves the field independently of spatial position");
check(vectorDelta[0] > 5f && vectorDelta[1] > 5f && vectorDelta[2] > 5f,
    "Vector noise has independent, nonidentical components");
bool continuous = true;
for (int i = -8; i <= 8; i++)
{
    Vector3 p = point + Vector3.one * i;
    continuous &= Mathf.Abs(MudaacNoise.SampleSigned(p + Vector3.right * 0.0001f, 1f)
        - MudaacNoise.SampleSigned(p - Vector3.right * 0.0001f, 1f)) < 0.02f;
    continuous &= Mathf.Abs(MudaacNoise.SampleSigned(p, 1.0001f) - MudaacNoise.SampleSigned(p, 0.9999f)) < 0.02f;
}
check(continuous, "Spatial and temporal sampling remain continuous across nearby samples");
var settings = new MudaacNoiseSettings { seed = 7, offset = new Vector3(0.13f, -0.29f, 0.53f) };
check(settings.Sample01(point, 0.3f, 1f, 0f) == settings.Sample01(point, 0.3f, 100f, 0f), "Zero speed freezes the noise");
check(settings.Sample01(point, 0f, 1f, 0.4f) == settings.Sample01(point + Vector3.one * 40f, 0f, 1f, 0.4f),
    "Zero spatial scale gives uniform spatial sampling");
var shiftedSettings = settings; shiftedSettings.offset += new Vector3(0.7f, -0.2f, 0.9f);
check(Mathf.Abs(settings.Sample01(point, 0.3f, 1f, 0.4f) - shiftedSettings.Sample01(point, 0.3f, 1f, 0.4f)) > 0.0001f,
    "Noise offset changes the pattern");

var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var root = new GameObject("Noise checks");
try
{
    var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
    a.name = "Concrete panel A"; a.transform.SetParent(root.transform, false); a.transform.position = point;
    var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
    b.name = "Concrete panel B"; b.transform.SetParent(root.transform, false);
    b.transform.position = point + new Vector3(0.37f, 0.37f, -0.37f);
    var controller = root.AddComponent<MudaacTileController>();
    var influencer = root.AddComponent<MudaacTileInfluencer>();
    influencer.randomness = 1f; influencer.randomScale = 0.8f; influencer.randomSpeed = 0.4f; influencer.noise = settings;
    var tileA = controller.Tiles[0]; var tileB = controller.Tiles[1];
    check(Mathf.Abs(influencer.GetWeightForTile(tileA, controller, 1f)
        - influencer.GetWeightForTile(tileB, controller, 1f)) > 0.0001f,
        "Noise influencer no longer repeats weights along the old diagonal direction");
    influencer.randomSpeed = 0f;
    check(influencer.GetWeightForTile(tileA, controller, 1f) == influencer.GetWeightForTile(tileA, controller, 100f),
        "Influencer Random Speed zero freezes its mask");
    influencer.randomness = 0f;
    check(influencer.GetWeightForTile(tileA, controller, 1f) == 1f,
        "Zero randomness preserves full default influencer weight");
    influencer.randomness = 1f;
    float restWeight = influencer.GetWeightForTile(tileA, controller, 1f);
    a.transform.position += Vector3.one * 10f;
    check(influencer.GetWeightForTile(tileA, controller, 1f) == restWeight,
        "Influencer noise follows the authored position without movement feedback");
    a.transform.position = point;

    // Place the influencer at the captured panel centre without moving its controller.
    var masks = new GameObject("Mask shapes"); masks.transform.SetParent(root.transform, false);
    var shaped = masks.AddComponent<MudaacTileSphereInfluencer>();
    masks.transform.position = tileA.RestWorldCenter;
    shaped.radius = 10f; shaped.randomness = 1f; shaped.randomScale = 0.8f; shaped.noise = settings;
    influencer.randomScale = shaped.randomScale; influencer.noise = settings;
    check(Mathf.Abs(shaped.GetWeightForTile(tileA, controller, 1f)
        - influencer.GetWeightForTile(tileA, controller, 1f)) < 0.00001f,
        "Shaped and noise influencers share the same noise field at the shape centre");

    var moving = root.AddComponent<MudaacTileMoveModifier>();
    moving.noise = settings; moving.spatialScale = 0.8f; moving.frequency = 0.4f;
    moving.motionAmplitude = Vector3.one * 0.5f;
    moving.rotationAmplitude = new Vector3(10f, 15f, 20f);
    controller.EvaluateTiles(2.3f);
    Vector3 movedPosition = tileA.Transform.position;
    Quaternion movedRotation = tileA.Transform.rotation;
    for (int i = 0; i < 100; i++) controller.EvaluateTiles(2.3f);
    check(Vector3.Distance(tileA.Transform.position, movedPosition) < 0.00001f
        && Quaternion.Angle(tileA.Transform.rotation, movedRotation) < 0.001f,
        "Motion is deterministic and does not accumulate over repeated evaluations");
    controller.EvaluateTiles(2.301f);
    check(Vector3.Distance(tileA.Transform.position, movedPosition) < 0.02f
        && Quaternion.Angle(tileA.Transform.rotation, movedRotation) < 1f,
        "Noise motion and rotation animate continuously");
    moving.frequency = 0f; controller.EvaluateTiles(1f); movedPosition = tileA.Transform.position;
    movedRotation = tileA.Transform.rotation; controller.EvaluateTiles(100f);
    check(tileA.Transform.position == movedPosition && Quaternion.Angle(tileA.Transform.rotation, movedRotation) < 0.001f,
        "Motion Frequency zero freezes motion and rotation");
    moving.modifierWeight = 0f; controller.EvaluateTiles(3f);
    check(tileA.Transform.localPosition == tileA.RestLocalPosition && tileA.Transform.localScale == tileA.RestLocalScale,
        "Zero motion weight restores the authored pose");
    report.AppendLine("All " + passed + " noise checks passed.");
    return report.ToString();
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
}
