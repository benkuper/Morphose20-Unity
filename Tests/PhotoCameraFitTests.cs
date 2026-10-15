using System;
using System.Collections.Generic;

// Standalone tests; run with RunPhotoCameraFitTests.ps1, no Unity scene changes.
internal static class PhotoCameraFitTests
{
    private static readonly double[] TruthPosition = { 0.4, 0.2, -5.0 };
    private static readonly double[] StartPosition = { 0.9, 0.5, -4.5 };
    private static readonly double[,] TruthRotation = Multiply(Rotation(1, -15), Rotation(0, 5));
    private static readonly double[,] StartRotation = Multiply(Rotation(1, -8), Rotation(0, 1));

    public static int Main()
    {
        try
        {
            CheckFit("Pose + FOV", 38, true, 1, 0, false);
            CheckFit("Pose only", 38, false, 1, 0, false);
            CheckFit("Pose only preserves a FOV below the fitted range", 3, false, 1, 0, false);
            CheckFit("Small translated model", 38, true, 0.001, 100, false);
            CheckFit("Large model", 38, true, 1000, 0, false);
            CheckFit("Imperfect landmark clicks", 38, true, 1, 0, true);
            var points = CreatePoints(38, 1, 0, false);
            Require(!Fit(points.GetRange(0, 5), 60, true, StartPosition, out _, out _), "Fewer than six pairs must be rejected");
            Console.WriteLine("PASS: insufficient correspondences rejected");
            var line = new List<PhotoCameraFit.Correspondence>();
            for (int i = 0; i < 8; ++i)
                line.Add(new PhotoCameraFit.Correspondence(i, 0, 0, 0.1 * i, 0.5));
            Require(!Fit(line, 60, true, StartPosition, out _, out _), "Collinear 3D landmarks must be rejected");
            Console.WriteLine("PASS: degenerate geometry rejected");
            Require(!Fit(points, 60, true, new double[] { 0, 0, 20 }, out _, out _), "Points behind the initial camera must be rejected");
            Console.WriteLine("PASS: behind-camera initialization rejected");
            Console.WriteLine("All 9 camera fitting tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckFit(string name, double fov, bool solveFov, double scale, double offset, bool noise)
    {
        var points = CreatePoints(fov, scale, offset, noise);
        var start = new[] { StartPosition[0] * scale + offset, StartPosition[1] * scale + offset, StartPosition[2] * scale + offset };
        Require(Fit(points, solveFov ? 65 : fov, solveFov, start, out var result, out string error), name + ": " + error);
        var actualRotation = Multiply(StartRotation, RotationVectorInverse(result.RotationVector));
        double independentlyMeasuredSquaredPixels = 0;
        foreach (var point in points)
        {
            var uv = Project(new[] { point.X, point.Y, point.Z }, result.Position, actualRotation, result.FieldOfView);
            independentlyMeasuredSquaredPixels += Math.Pow((uv[0] - point.U) * 1280, 2) + Math.Pow((uv[1] - point.V) * 1920, 2);
        }
        double rms = Math.Sqrt(independentlyMeasuredSquaredPixels / points.Count);
        Require(Math.Abs(rms - result.RmsPixels) < 0.00001, name + ": reported pixel RMS must match an independent reprojection");
        Require(rms < (noise ? 2 : 0.001), name + ": fit error too high: " + rms);
        Require(Math.Abs(result.FieldOfView - fov) < (noise ? 1 : 0.001), name + ": FOV incorrect: " + result.FieldOfView);
        if (!noise)
            for (int axis = 0; axis < 3; ++axis)
                Require(Math.Abs(result.Position[axis] - (TruthPosition[axis] * scale + offset)) / scale < 0.0001, name + ": camera position incorrect");
        Console.WriteLine("PASS: " + name + " (RMS " + rms.ToString("F6") + " px, FOV " + result.FieldOfView.ToString("F4") + ")");
    }

    private static List<PhotoCameraFit.Correspondence> CreatePoints(double fov, double scale, double offset, bool noise)
    {
        var points = new List<PhotoCameraFit.Correspondence>();
        for (int row = 0; row < 3; ++row)
            for (int column = 0; column < 4; ++column)
            {
                var world = new[] { (column - 1.5) * 0.65, (row - 1) * 0.9, ((row * 3 + column * 5) % 7 - 3) * 0.21 };
                var uv = Project(world, TruthPosition, TruthRotation, fov);
                int i = points.Count;
                points.Add(new PhotoCameraFit.Correspondence(world[0] * scale + offset, world[1] * scale + offset,
                    world[2] * scale + offset, uv[0] + (noise ? Math.Sin(i * 5.1) / 1280 : 0), uv[1] + (noise ? Math.Cos(i * 2.3) / 1920 : 0)));
            }
        return points;
    }

    private static bool Fit(List<PhotoCameraFit.Correspondence> points, double fov, bool fitFov, double[] start,
        out PhotoCameraFit.Solution result, out string error)
    {
        return PhotoCameraFit.TryFit(points, start, StartRotation, fov, 1280, 1920, fitFov, out result, out error);
    }

    private static double[] Project(double[] point, double[] position, double[,] rotation, double fov)
    {
        var camera = new double[3];
        for (int row = 0; row < 3; ++row)
            for (int column = 0; column < 3; ++column)
                camera[row] += rotation[row, column] * (point[column] - position[column]);
        double focal = 0.5 / Math.Tan(fov * Math.PI / 360);
        return new[] { 0.5 + focal * camera[0] / camera[2] / (1280.0 / 1920), 0.5 + focal * camera[1] / camera[2] };
    }

    private static double[,] Rotation(int axis, double degrees)
    {
        double angle = degrees * Math.PI / 180;
        double c = Math.Cos(angle), s = Math.Sin(angle);
        if (axis == 0)
            return new[,] { { 1.0, 0, 0 }, { 0, c, -s }, { 0, s, c } };
        return new[,] { { c, 0, s }, { 0, 1, 0 }, { -s, 0, c } };
    }

    private static double[,] RotationVectorInverse(double[] vector)
    {
        double angle = Math.Sqrt(vector[0] * vector[0] + vector[1] * vector[1] + vector[2] * vector[2]);
        if (angle < 1e-12)
            return new[,] { { 1.0, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        // Quaternion formula, independent of the solver's Rodrigues implementation.
        double factor = -Math.Sin(angle / 2) / angle;
        double x = vector[0] * factor, y = vector[1] * factor, z = vector[2] * factor, w = Math.Cos(angle / 2);
        return new[,] { { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w) },
            { 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w) },
            { 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) } };
    }

    private static double[,] Multiply(double[,] a, double[,] b)
    {
        var result = new double[3, 3];
        for (int row = 0; row < 3; ++row)
            for (int column = 0; column < 3; ++column)
                for (int k = 0; k < 3; ++k)
                    result[row, column] += a[row, k] * b[k, column];
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
