using System;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Coherent noise with independent X/Y/Z coordinates and a separate animation time.</summary>
public static class MudaacNoise
{
    /// <summary>Sample signed noise in [-1, 1]. Channel selects a separate deterministic field.</summary>
    public static float SampleSigned(Vector3 position, float time = 0f, int seed = 0, int channel = 0)
    {
        // A 3D spatial slice of 4D simplex noise: time evolves the field instead of sliding
        // a 2D projection across it. Fractional seed offsets avoid lattice-aligned origins.
        float4 point = new float4(position.x, position.y, position.z, time) + GetOffset(seed, channel);
        return Mathf.Clamp(noise.snoise(point), -1f, 1f);
    }

    /// <summary>Sample a weight in [0, 1].</summary>
    public static float Sample01(Vector3 position, float time = 0f, int seed = 0, int channel = 0)
        => SampleSigned(position, time, seed, channel) * 0.5f + 0.5f;

    /// <summary>Sample three independent signed channels; the vector is not normalized.</summary>
    public static Vector3 SampleVectorSigned(Vector3 position, float time = 0f, int seed = 0, int channel = 0)
        => new Vector3(SampleSigned(position, time, seed, channel),
            SampleSigned(position, time, seed, unchecked(channel + 1)),
            SampleSigned(position, time, seed, unchecked(channel + 2)));

    private static float4 GetOffset(int seed, int channel)
    {
        unchecked
        {
            uint hash = (uint)seed * 0x9e3779b9u + (uint)channel * 0x85ebca6bu;
            return new float4(HashCoordinate(hash + 0x68bc21ebu), HashCoordinate(hash + 0x02e5be93u),
                HashCoordinate(hash + 0x967a889bu), HashCoordinate(hash + 0x368cc8b7u));
        }
    }

    private static float HashCoordinate(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
        }
        return (value & 0x00ffffffu) * (128f / 16777216f) + 0.12345f;
    }
}

/// <summary>Reusable Inspector controls for any noise-driven modifier or influencer.</summary>
[Serializable]
public struct MudaacNoiseSettings
{
    [Tooltip("Deterministic pattern seed. The same seed, position, time and channel give the same sample.")]
    public int seed;
    [Tooltip("Offset in noise coordinates, applied after spatial scale.")]
    public Vector3 offset;

    public float Sample01(Vector3 worldPosition, float spatialScale = 1f, float time = 0f, float speed = 0f, int channel = 0)
        => MudaacNoise.Sample01(worldPosition * spatialScale + offset, time * speed, seed, channel);

    public float SampleSigned(Vector3 worldPosition, float spatialScale = 1f, float time = 0f, float speed = 0f, int channel = 0)
        => MudaacNoise.SampleSigned(worldPosition * spatialScale + offset, time * speed, seed, channel);

    public Vector3 SampleVectorSigned(Vector3 worldPosition, float spatialScale = 1f, float time = 0f, float speed = 0f, int channel = 0)
        => MudaacNoise.SampleVectorSigned(worldPosition * spatialScale + offset, time * speed, seed, channel);
}
