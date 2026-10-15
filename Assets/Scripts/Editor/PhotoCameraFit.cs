using System;
using System.Collections.Generic;

// Pure numerical code: no scene mutation or Unity dependencies.
internal static class PhotoCameraFit
{
    internal struct Correspondence
    {
        public double X, Y, Z, U, V;

        public Correspondence(double x, double y, double z, double u, double v)
        {
            X = x; Y = y; Z = z; U = u; V = v;
        }
    }

    internal sealed class Solution
    {
        public double[] Position;
        // World-space rotation applied before the initial camera rotation.
        public double[] RotationVector;
        public double FieldOfView;
        public double RmsPixels;
        public bool AtFovLimit;
    }

    private const double MinFov = 5;
    private const double MaxFov = 150;

    public static bool TryFit(IReadOnlyList<Correspondence> points, double[] initialPosition,
        double[,] initialWorldToCamera, double initialFov, int width, int height, bool fitFov,
        out Solution solution, out string error)
    {
        solution = null;
        error = null;
        if (points.Count < 6 || width <= 0 || height <= 0)
        {
            error = "Add at least six complete landmark pairs and use a positive image resolution.";
            return false;
        }

        var center = new double[3];
        foreach (var point in points)
        {
            if (!Finite(point.X) || !Finite(point.Y) || !Finite(point.Z)
                || !Finite(point.U) || !Finite(point.V))
            {
                error = "A landmark contains an invalid coordinate.";
                return false;
            }
            center[0] += point.X / points.Count;
            center[1] += point.Y / points.Count;
            center[2] += point.Z / points.Count;
        }

        double scale = 0, imageSpread = 0;
        foreach (var point in points)
        {
            double x = point.X - center[0], y = point.Y - center[1], z = point.Z - center[2];
            scale = Math.Max(scale, Math.Sqrt(x * x + y * y + z * z));
            foreach (var other in points)
                imageSpread = Math.Max(imageSpread, Math.Abs(point.U - other.U) + Math.Abs(point.V - other.V));
        }
        if (scale < 1e-10 || imageSpread < 1e-5 || !HasArea(points, scale))
        {
            error = "Landmarks must cover an area of the model and picture; coincident or collinear points cannot constrain a camera.";
            return false;
        }

        var normalized = new Correspondence[points.Count];
        for (int i = 0; i < points.Count; ++i)
        {
            var point = points[i];
            normalized[i] = new Correspondence((point.X - center[0]) / scale,
                (point.Y - center[1]) / scale, (point.Z - center[2]) / scale, point.U, point.V);
        }

        int count = fitFov ? 7 : 6;
        var start = new double[7];
        for (int i = 0; i < 3; ++i)
            start[i] = (initialPosition[i] - center[i]) / scale;
        if (!Finite(initialFov) || initialFov <= 0 || initialFov >= 180)
        {
            error = "Initial field of view must be between 0 and 180 degrees.";
            return false;
        }
        if (fitFov)
            initialFov = Math.Max(MinFov, Math.Min(MaxFov, initialFov));
        start[6] = LogFocal(initialFov);
        double aspect = (double)width / height;
        var residual = new double[points.Count * 2];
        if (!Evaluate(normalized, start, initialWorldToCamera, aspect, residual, out double initialCost))
        {
            error = "Some landmarks are behind the camera. Roughly aim the camera at all landmarks before fitting.";
            return false;
        }

        double[] best = (double[])start.Clone();
        double bestCost = initialCost;
        // Different FOV/distance seeds help with the strong focal-length/distance ambiguity.
        double[] seeds = fitFov ? new[] { initialFov, 15.0, 30.0, 60.0, 90.0, 120.0 } : new[] { initialFov };
        foreach (double seed in seeds)
        {
            var candidate = (double[])start.Clone();
            double distanceRatio = Math.Exp(LogFocal(seed) - start[6]);
            for (int i = 0; i < 3; ++i)
                candidate[i] *= distanceRatio;
            candidate[6] = LogFocal(seed);
            double cost = Optimize(normalized, candidate, initialWorldToCamera, aspect, count);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = candidate;
            }
        }

        if (!Finite(bestCost))
        {
            error = "The camera fit failed. Check correspondences and start from a closer camera view.";
            return false;
        }

        double fov = 2 * Math.Atan(0.5 / Math.Exp(best[6])) * 180 / Math.PI;
        solution = new Solution
        {
            Position = new[] { center[0] + best[0] * scale, center[1] + best[1] * scale, center[2] + best[2] * scale },
            RotationVector = new[] { best[3], best[4], best[5] },
            FieldOfView = fov,
            RmsPixels = Math.Sqrt(bestCost / points.Count) * height,
            AtFovLimit = fitFov && (fov < MinFov + 0.05 || fov > MaxFov - 0.05)
        };
        return true;
    }

    private static double Optimize(Correspondence[] points, double[] parameters, double[,] rotation,
        double aspect, int count)
    {
        int rows = points.Length * 2;
        var residual = new double[rows];
        var probe = new double[rows];
        var jacobian = new double[rows, count];
        if (!Evaluate(points, parameters, rotation, aspect, residual, out double cost))
            return double.PositiveInfinity;

        double damping = 0.001;
        for (int iteration = 0; iteration < 180; ++iteration)
        {
            for (int column = 0; column < count; ++column)
            {
                double saved = parameters[column];
                double step = 1e-6 * Math.Max(1, Math.Abs(saved));
                parameters[column] += step;
                bool valid = Evaluate(points, parameters, rotation, aspect, probe, out _);
                parameters[column] = saved;
                if (!valid)
                    return cost;
                for (int row = 0; row < rows; ++row)
                    jacobian[row, column] = (probe[row] - residual[row]) / step;
            }

            var matrix = new double[count, count];
            var rhs = new double[count];
            for (int a = 0; a < count; ++a)
            {
                for (int row = 0; row < rows; ++row)
                    rhs[a] -= jacobian[row, a] * residual[row];
                for (int b = 0; b < count; ++b)
                    for (int row = 0; row < rows; ++row)
                        matrix[a, b] += jacobian[row, a] * jacobian[row, b];
                matrix[a, a] += damping * Math.Max(matrix[a, a], 1e-8);
            }
            if (!Solve(matrix, rhs))
                break;

            var trial = (double[])parameters.Clone();
            for (int column = 0; column < count; ++column)
                trial[column] += rhs[column];
            if (count == 7)
                trial[6] = Math.Max(LogFocal(MaxFov), Math.Min(LogFocal(MinFov), trial[6]));
            bool improved = Evaluate(points, trial, rotation, aspect, probe, out double trialCost) && trialCost < cost;
            if (improved)
            {
                double improvement = cost - trialCost;
                Array.Copy(trial, parameters, parameters.Length);
                Array.Copy(probe, residual, rows);
                cost = trialCost;
                damping = Math.Max(1e-10, damping * 0.3);
                if (cost < 1e-20 || improvement < 1e-14 * Math.Max(1, cost))
                    break;
            }
            else
            {
                damping *= 10;
                if (damping > 1e12)
                    break;
            }
        }
        return cost;
    }

    private static bool Evaluate(Correspondence[] points, double[] p, double[,] rotation,
        double aspect, double[] residual, out double cost)
    {
        cost = 0;
        double angle = Math.Sqrt(p[3] * p[3] + p[4] * p[4] + p[5] * p[5]);
        double a = angle < 1e-8 ? 1 - angle * angle / 6 : Math.Sin(angle) / angle;
        double b = angle < 1e-8 ? 0.5 - angle * angle / 24 : (1 - Math.Cos(angle)) / (angle * angle);
        double focal = Math.Exp(p[6]);
        for (int i = 0; i < points.Length; ++i)
        {
            var point = points[i];
            double x = point.X - p[0], y = point.Y - p[1], z = point.Z - p[2];
            // Rodrigues with -omega converts the world-space rotation increment into camera space.
            double cx = p[5] * y - p[4] * z;
            double cy = p[3] * z - p[5] * x;
            double cz = p[4] * x - p[3] * y;
            double rx = x + a * cx + b * (p[5] * cy - p[4] * cz);
            double ry = y + a * cy + b * (p[3] * cz - p[5] * cx);
            double rz = z + a * cz + b * (p[4] * cx - p[3] * cy);
            x = rotation[0, 0] * rx + rotation[0, 1] * ry + rotation[0, 2] * rz;
            y = rotation[1, 0] * rx + rotation[1, 1] * ry + rotation[1, 2] * rz;
            z = rotation[2, 0] * rx + rotation[2, 1] * ry + rotation[2, 2] * rz;
            if (z <= 1e-7)
            {
                cost = double.PositiveInfinity;
                return false;
            }
            residual[2 * i] = focal * x / z + (0.5 - point.U) * aspect;
            residual[2 * i + 1] = focal * y / z + 0.5 - point.V;
            cost += residual[2 * i] * residual[2 * i] + residual[2 * i + 1] * residual[2 * i + 1];
        }
        return Finite(cost);
    }

    private static bool Solve(double[,] matrix, double[] rhs)
    {
        int count = rhs.Length;
        for (int column = 0; column < count; ++column)
        {
            int pivot = column;
            for (int row = column + 1; row < count; ++row)
                if (Math.Abs(matrix[row, column]) > Math.Abs(matrix[pivot, column]))
                    pivot = row;
            if (Math.Abs(matrix[pivot, column]) < 1e-18)
                return false;
            for (int j = column; j < count; ++j)
            {
                double swap = matrix[column, j];
                matrix[column, j] = matrix[pivot, j];
                matrix[pivot, j] = swap;
            }
            double rhsSwap = rhs[column]; rhs[column] = rhs[pivot]; rhs[pivot] = rhsSwap;
            for (int row = column + 1; row < count; ++row)
            {
                double factor = matrix[row, column] / matrix[column, column];
                for (int j = column; j < count; ++j)
                    matrix[row, j] -= factor * matrix[column, j];
                rhs[row] -= factor * rhs[column];
            }
        }
        for (int row = count - 1; row >= 0; --row)
        {
            for (int j = row + 1; j < count; ++j)
                rhs[row] -= matrix[row, j] * rhs[j];
            rhs[row] /= matrix[row, row];
        }
        return true;
    }

    private static bool HasArea(IReadOnlyList<Correspondence> points, double scale)
    {
        var first = points[0];
        foreach (var a in points)
        {
            double ax = (a.X - first.X) / scale, ay = (a.Y - first.Y) / scale, az = (a.Z - first.Z) / scale;
            foreach (var b in points)
            {
                double bx = (b.X - first.X) / scale, by = (b.Y - first.Y) / scale, bz = (b.Z - first.Z) / scale;
                double cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
                if (cx * cx + cy * cy + cz * cz > 1e-8)
                    return true;
            }
        }
        return false;
    }

    private static double LogFocal(double fov) => Math.Log(0.5 / Math.Tan(fov * Math.PI / 360));
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
