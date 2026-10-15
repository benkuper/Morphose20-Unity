using UnityEngine;

[AddComponentMenu("Mudaac/Influencers/Axial")]
public class MudaacTileAxialInfluencer : MudaacTileInfluencer
{
    [Min(0.001f)] public float radius = 1f;

    public override float GetWeightForTile(MudaacTile tile, MudaacTileController controller, float time)
    {
        Vector3 position = tile.RestWorldCenter;
        Vector3 local = transform.InverseTransformPoint(position);
        float distance = new Vector2(local.x, local.z).magnitude / Mathf.Max(radius, 0.001f);
        return Evaluate(distance + GetRandomness(position, time));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(radius * 2f, 10f, radius * 2f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
