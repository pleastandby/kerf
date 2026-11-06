using System;

namespace Kerf.Engine.Animation
{
    public enum SpringPreset
    {
        Gentle = 0,
        Snappy = 1,
        Bouncy = 2,
        Stiff = 3
    }

    public static class EasingEngine
    {
        /// <summary>
        /// Evaluates closed-form analytical damped harmonic oscillator displacement at time t.
        /// Solves underdamped (zeta &lt; 1), critically damped (zeta == 1), and overdamped (zeta &gt; 1) regimes.
        /// Returns normalized progress p(t) where p(0) = 0 and lim_{t -> inf} p(t) = 1.
        /// </summary>
        public static double EvaluateSpring(double t, double mass, double stiffness, double damping, double initialVelocity)
        {
            if (t <= 0.0) return 0.0;
            if (mass < 0.0001) mass = 0.0001;
            if (stiffness < 0.0001) stiffness = 0.0001;
            if (damping < 0.0) damping = 0.0;

            double omega0 = Math.Sqrt(stiffness / mass);
            double gamma = damping / (2.0 * mass);
            double disc = gamma * gamma - omega0 * omega0;

            if (Math.Abs(disc) < 1e-7)
            {
                // Critically Damped (zeta = 1.0)
                return 1.0 - Math.Exp(-gamma * t) * (1.0 + (gamma - initialVelocity) * t);
            }
            else if (disc < 0.0)
            {
                // Underdamped (zeta < 1.0)
                double omegaD = Math.Sqrt(omega0 * omega0 - gamma * gamma);
                double b = (gamma - initialVelocity) / omegaD;
                return 1.0 - Math.Exp(-gamma * t) * (Math.Cos(omegaD * t) + b * Math.Sin(omegaD * t));
            }
            else
            {
                // Overdamped (zeta > 1.0)
                double mu = Math.Sqrt(disc);
                double r1 = -gamma + mu;
                double r2 = -gamma - mu;
                double c1 = (initialVelocity - gamma - mu) / (2.0 * mu);
                double c2 = (-initialVelocity + gamma - mu) / (2.0 * mu);
                return 1.0 + c1 * Math.Exp(r1 * t) + c2 * Math.Exp(r2 * t);
            }
        }

        /// <summary>
        /// Solves cubic Bezier curve x(s) = u via Newton-Raphson with bisection fallback, evaluating y(s) with free vertical overshoot.
        /// </summary>
        public static double EvaluateBezier(double u, double x1, double y1, double x2, double y2)
        {
            return CubicBezierEase.Evaluate(u, x1, y1, x2, y2);
        }

        /// <summary>
        /// Returns physical parameters for standard spring presets.
        /// </summary>
        public static void GetSpringPreset(SpringPreset preset, out double mass, out double stiffness, out double damping, out double initialVelocity)
        {
            initialVelocity = 0.0;
            switch (preset)
            {
                case SpringPreset.Gentle:
                    mass = 1.0;
                    stiffness = 100.0;
                    damping = 15.0;
                    break;
                case SpringPreset.Snappy:
                    mass = 1.0;
                    stiffness = 200.0;
                    damping = 25.0;
                    break;
                case SpringPreset.Bouncy:
                    mass = 1.0;
                    stiffness = 150.0;
                    damping = 10.0;
                    break;
                case SpringPreset.Stiff:
                    mass = 1.0;
                    stiffness = 300.0;
                    damping = 30.0;
                    break;
                default:
                    mass = 1.0;
                    stiffness = 100.0;
                    damping = 10.0;
                    break;
            }
        }

        /// <summary>
        /// Applies standard spring preset parameters to a keyframe.
        /// </summary>
        public static void ApplySpringPreset(Keyframe kf, SpringPreset preset)
        {
            if (kf == null) return;
            double m, k, c, v0;
            GetSpringPreset(preset, out m, out k, out c, out v0);
            kf.Interp = InterpMode.Spring;
            kf.Mass = m;
            kf.Stiffness = k;
            kf.Damping = c;
            kf.InitialVelocity = v0;
        }

        /// <summary>
        /// Returns canonical Bezier control points for all 21 easing presets per RESEARCH.md Table Pattern 3.
        /// </summary>
        public static void GetPresetControlPoints(EasingPreset preset, out double x1, out double y1, out double x2, out double y2)
        {
            switch (preset)
            {
                case EasingPreset.Linear:
                    x1 = 0.0; y1 = 0.0; x2 = 1.0; y2 = 1.0; break;
                case EasingPreset.Ease:
                    x1 = 0.25; y1 = 0.10; x2 = 0.25; y2 = 1.00; break;
                case EasingPreset.EaseIn:
                    x1 = 0.42; y1 = 0.00; x2 = 1.00; y2 = 1.00; break;
                case EasingPreset.EaseOut:
                    x1 = 0.00; y1 = 0.00; x2 = 0.58; y2 = 1.00; break;
                case EasingPreset.EaseInOut:
                    x1 = 0.42; y1 = 0.00; x2 = 0.58; y2 = 1.00; break;
                case EasingPreset.QuadIn:
                    x1 = 0.11; y1 = 0.00; x2 = 0.50; y2 = 0.00; break;
                case EasingPreset.QuadOut:
                    x1 = 0.50; y1 = 1.00; x2 = 0.89; y2 = 1.00; break;
                case EasingPreset.QuadInOut:
                    x1 = 0.45; y1 = 0.00; x2 = 0.55; y2 = 1.00; break;
                case EasingPreset.CubicIn:
                    x1 = 0.32; y1 = 0.00; x2 = 0.67; y2 = 0.00; break;
                case EasingPreset.CubicOut:
                    x1 = 0.33; y1 = 1.00; x2 = 0.68; y2 = 1.00; break;
                case EasingPreset.CubicInOut:
                    x1 = 0.65; y1 = 0.00; x2 = 0.35; y2 = 1.00; break;
                case EasingPreset.QuartIn:
                    x1 = 0.50; y1 = 0.00; x2 = 0.75; y2 = 0.00; break;
                case EasingPreset.QuartOut:
                    x1 = 0.25; y1 = 1.00; x2 = 0.50; y2 = 1.00; break;
                case EasingPreset.QuartInOut:
                    x1 = 0.76; y1 = 0.00; x2 = 0.24; y2 = 1.00; break;
                case EasingPreset.QuintIn:
                    x1 = 0.64; y1 = 0.00; x2 = 0.78; y2 = 0.00; break;
                case EasingPreset.QuintOut:
                    x1 = 0.22; y1 = 1.00; x2 = 0.36; y2 = 1.00; break;
                case EasingPreset.QuintInOut:
                    x1 = 0.83; y1 = 0.00; x2 = 0.17; y2 = 1.00; break;
                case EasingPreset.Back:
                    x1 = 0.34; y1 = 1.56; x2 = 0.64; y2 = 1.00; break;
                case EasingPreset.Elastic:
                    x1 = 0.50; y1 = 1.50; x2 = 0.75; y2 = 1.00; break;
                case EasingPreset.Bounce:
                    x1 = 0.34; y1 = 1.40; x2 = 0.64; y2 = 1.00; break;
                case EasingPreset.Smooth:
                    x1 = 0.40; y1 = 0.00; x2 = 0.60; y2 = 1.00; break;
                case EasingPreset.CustomBezier:
                default:
                    x1 = 0.25; y1 = 0.10; x2 = 0.25; y2 = 1.00; break;
            }
        }

        /// <summary>
        /// Evaluates exact progress on [0, 1] for all motion presets.
        /// </summary>
        public static double EvaluatePreset(EasingPreset preset, double u)
        {
            if (u <= 0.0) return 0.0;
            if (u >= 1.0) return 1.0;

            switch (preset)
            {
                case EasingPreset.Linear:
                    return u;
                case EasingPreset.Ease:
                    return CubicBezierEase.Evaluate(u, 0.25, 0.10, 0.25, 1.00);
                case EasingPreset.EaseIn:
                    return CubicBezierEase.Evaluate(u, 0.42, 0.00, 1.00, 1.00);
                case EasingPreset.EaseOut:
                    return CubicBezierEase.Evaluate(u, 0.00, 0.00, 0.58, 1.00);
                case EasingPreset.EaseInOut:
                    return CubicBezierEase.Evaluate(u, 0.42, 0.00, 0.58, 1.00);
                case EasingPreset.QuadIn:
                    return u * u;
                case EasingPreset.QuadOut:
                    return 1.0 - (1.0 - u) * (1.0 - u);
                case EasingPreset.QuadInOut:
                    return (u < 0.5) ? (2.0 * u * u) : (1.0 - Math.Pow(-2.0 * u + 2.0, 2) / 2.0);
                case EasingPreset.CubicIn:
                    return u * u * u;
                case EasingPreset.CubicOut:
                    return 1.0 - Math.Pow(1.0 - u, 3);
                case EasingPreset.CubicInOut:
                    return (u < 0.5) ? (4.0 * u * u * u) : (1.0 - Math.Pow(-2.0 * u + 2.0, 3) / 2.0);
                case EasingPreset.QuartIn:
                    return u * u * u * u;
                case EasingPreset.QuartOut:
                    return 1.0 - Math.Pow(1.0 - u, 4);
                case EasingPreset.QuartInOut:
                    return (u < 0.5) ? (8.0 * u * u * u * u) : (1.0 - Math.Pow(-2.0 * u + 2.0, 4) / 2.0);
                case EasingPreset.QuintIn:
                    return u * u * u * u * u;
                case EasingPreset.QuintOut:
                    return 1.0 - Math.Pow(1.0 - u, 5);
                case EasingPreset.QuintInOut:
                    return (u < 0.5) ? (16.0 * u * u * u * u * u) : (1.0 - Math.Pow(-2.0 * u + 2.0, 5) / 2.0);
                case EasingPreset.Back:
                    return 1.0 + 2.70158 * Math.Pow(u - 1.0, 3) + 1.70158 * Math.Pow(u - 1.0, 2);
                case EasingPreset.Elastic:
                    return Math.Pow(2.0, -10.0 * u) * Math.Sin((10.0 * u - 0.75) * (2.0 * Math.PI / 3.0)) + 1.0;
                case EasingPreset.Bounce:
                    return EvaluateBounce(u);
                case EasingPreset.Smooth:
                    return 3.0 * u * u - 2.0 * u * u * u;
                case EasingPreset.CustomBezier:
                default:
                    return CubicBezierEase.Evaluate(u, 0.25, 0.10, 0.25, 1.00);
            }
        }

        private static double EvaluateBounce(double u)
        {
            if (u < (1.0 / 2.75))
            {
                return 7.5625 * u * u;
            }
            else if (u < (2.0 / 2.75))
            {
                u -= (1.5 / 2.75);
                return 7.5625 * u * u + 0.75;
            }
            else if (u < (2.5 / 2.75))
            {
                u -= (2.25 / 2.75);
                return 7.5625 * u * u + 0.9375;
            }
            else
            {
                u -= (2.625 / 2.75);
                return 7.5625 * u * u + 0.984375;
            }
        }

        /// <summary>
        /// Updates the keyframe's Preset, Interp mode, and canonical control points.
        /// </summary>
        public static void ApplyPreset(Keyframe kf, EasingPreset preset)
        {
            if (kf == null) return;
            kf.Preset = preset;
            if (preset == EasingPreset.Linear)
            {
                kf.Interp = InterpMode.Linear;
            }
            else
            {
                kf.Interp = InterpMode.Cubic;
            }
            double x1, y1, x2, y2;
            GetPresetControlPoints(preset, out x1, out y1, out x2, out y2);
            kf.X1 = x1;
            kf.Y1 = y1;
            kf.X2 = x2;
            kf.Y2 = y2;
        }
    }
}
