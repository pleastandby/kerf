using System;

namespace Kerf.Engine.Animation
{
    public static class CubicBezierEase
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static double CubicBezierEval(double t, double p1, double p2)
        {
            double oneMinusT = 1.0 - t;
            return 3.0 * oneMinusT * oneMinusT * t * p1 + 3.0 * oneMinusT * t * t * p2 + t * t * t;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static double CubicBezierDerivative(double t, double p1, double p2)
        {
            double oneMinusT = 1.0 - t;
            return 3.0 * oneMinusT * oneMinusT * p1
                 + 6.0 * oneMinusT * t * (p2 - p1)
                 + 3.0 * t * t * (1.0 - p2);
        }

        /// <summary>
        /// Solves x(s) = u for s in [0, 1] using Newton-Raphson with bisection fallback, then evaluates y(s).
        /// </summary>
        public static double Evaluate(double u, double x1, double y1, double x2, double y2)
        {
            x1 = Math.Max(0.0, Math.Min(1.0, x1));
            x2 = Math.Max(0.0, Math.Min(1.0, x2));
            if (u <= 0.0) return 0.0;
            if (u >= 1.0) return 1.0;

            // 1. Solve x(s) = u using 8 Newton-Raphson steps
            double s = u;
            for (int i = 0; i < 8; i++)
            {
                double xErr = CubicBezierEval(s, x1, x2) - u;
                if (Math.Abs(xErr) < 1e-7)
                {
                    return CubicBezierEval(s, y1, y2);
                }
                double d = CubicBezierDerivative(s, x1, x2);
                if (Math.Abs(d) < 1e-7) break;
                s -= xErr / d;
                if (s < 0.0 || s > 1.0) break;
            }

            // 2. Fallback: Bisection for 24 iterations
            double low = 0.0;
            double high = 1.0;
            s = 0.5 * (low + high);
            for (int i = 0; i < 24; i++)
            {
                s = 0.5 * (low + high);
                double xVal = CubicBezierEval(s, x1, x2);
                if (Math.Abs(xVal - u) < 1e-7) break;
                if (xVal < u)
                {
                    low = s;
                }
                else
                {
                    high = s;
                }
            }

            double result = CubicBezierEval(s, y1, y2);
            if (double.IsNaN(result) || double.IsInfinity(result)) return u;
            return result;
        }

        // Standard Easing Presets
        public static double Ease(double u)
        {
            return Evaluate(u, 0.25, 0.1, 0.25, 1.0);
        }

        public static double EaseIn(double u)
        {
            return Evaluate(u, 0.42, 0.0, 1.0, 1.0);
        }

        public static double EaseOut(double u)
        {
            return Evaluate(u, 0.0, 0.0, 0.58, 1.0);
        }

        public static double EaseInOut(double u)
        {
            return Evaluate(u, 0.42, 0.0, 0.58, 1.0);
        }

        public static double Linear(double u)
        {
            return Math.Max(0.0, Math.Min(1.0, u));
        }
    }
}

