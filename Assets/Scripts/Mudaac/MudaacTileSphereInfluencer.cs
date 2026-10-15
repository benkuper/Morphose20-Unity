using UnityEngine;

[AddComponentMenu("Mudaac/Influencers/Sphere")]
public class MudaacTileSphereInfluencer : MudaacTileInfluencer
{
    [Min(0.001f)] public float radius = 1f;

    public override float GetWeightForTile(MudaacTile tile, MudaacTileController controller, float time)
    {
        Vector3 position = tile.RestWorldCenter;
        float distance = transform.InverseTransformPoint(position).magnitude / Mathf.Max(radius, 0.001f);
        return Evaluate(distance + GetRandomness(position, time));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Vector3.zero, radius);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
