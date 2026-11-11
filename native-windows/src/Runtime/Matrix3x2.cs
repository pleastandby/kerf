using System;

namespace Kerf.Runtime
{
    public struct Point
    {
        public double X;
        public double Y;

        public Point(double x, double y)
        {
            X = (double.IsNaN(x) || double.IsInfinity(x)) ? 0.0 : x;
            Y = (double.IsNaN(y) || double.IsInfinity(y)) ? 0.0 : y;
        }

        public override string ToString()
        {
            return string.Format("({0}, {1})", X, Y);
        }
    }

    public struct Matrix3x2
    {
        public double M11;
        public double M12;
        public double M21;
        public double M22;
        public double OffsetX;
        public double OffsetY;

        public static readonly Matrix3x2 Identity = new Matrix3x2(1.0, 0.0, 0.0, 1.0, 0.0, 0.0);

        public Matrix3x2(double m11, double m12, double m21, double m22, double ox, double oy)
        {
            M11 = (double.IsNaN(m11) || double.IsInfinity(m11)) ? 1.0 : m11;
            M12 = (double.IsNaN(m12) || double.IsInfinity(m12)) ? 0.0 : m12;
            M21 = (double.IsNaN(m21) || double.IsInfinity(m21)) ? 0.0 : m21;
            M22 = (double.IsNaN(m22) || double.IsInfinity(m22)) ? 1.0 : m22;
            OffsetX = (double.IsNaN(ox) || double.IsInfinity(ox)) ? 0.0 : ox;
            OffsetY = (double.IsNaN(oy) || double.IsInfinity(oy)) ? 0.0 : oy;
        }

        public bool IsIdentity
        {
            get
            {
                return Math.Abs(M11 - 1.0) < 1e-9 &&
                       Math.Abs(M12) < 1e-9 &&
                       Math.Abs(M21) < 1e-9 &&
                       Math.Abs(M22 - 1.0) < 1e-9 &&
                       Math.Abs(OffsetX) < 1e-9 &&
                       Math.Abs(OffsetY) < 1e-9;
            }
        }

        public static Matrix3x2 Multiply(Matrix3x2 a, Matrix3x2 b)
        {
            return new Matrix3x2(
                a.M11 * b.M11 + a.M12 * b.M21,
                a.M11 * b.M12 + a.M12 * b.M22,
                a.M21 * b.M11 + a.M22 * b.M21,
                a.M21 * b.M12 + a.M22 * b.M22,
                a.OffsetX * b.M11 + a.OffsetY * b.M21 + b.OffsetX,
                a.OffsetX * b.M12 + a.OffsetY * b.M22 + b.OffsetY
            );
        }

        public static Matrix3x2 operator *(Matrix3x2 a, Matrix3x2 b)
        {
            return Multiply(a, b);
        }

        public static Matrix3x2 CreateTranslation(double x, double y)
        {
            double tx = (double.IsNaN(x) || double.IsInfinity(x)) ? 0.0 : x;
            double ty = (double.IsNaN(y) || double.IsInfinity(y)) ? 0.0 : y;
            return new Matrix3x2(1.0, 0.0, 0.0, 1.0, tx, ty);
        }

        public static Matrix3x2 CreateScale(double sx, double sy, double centerX = 0.0, double centerY = 0.0)
        {
            double sX = (double.IsNaN(sx) || double.IsInfinity(sx)) ? 1.0 : sx;
            double sY = (double.IsNaN(sy) || double.IsInfinity(sy)) ? 1.0 : sy;
            double cx = (double.IsNaN(centerX) || double.IsInfinity(centerX)) ? 0.0 : centerX;
            double cy = (double.IsNaN(centerY) || double.IsInfinity(centerY)) ? 0.0 : centerY;

            return new Matrix3x2(
                sX, 0.0,
                0.0, sY,
                cx * (1.0 - sX),
                cy * (1.0 - sY)
            );
        }

        public static Matrix3x2 CreateRotation(double radians, double centerX = 0.0, double centerY = 0.0)
        {
            double r = (double.IsNaN(radians) || double.IsInfinity(radians)) ? 0.0 : radians;
            double cos = Math.Cos(r);
            double sin = Math.Sin(r);
            double cx = (double.IsNaN(centerX) || double.IsInfinity(centerX)) ? 0.0 : centerX;
            double cy = (double.IsNaN(centerY) || double.IsInfinity(centerY)) ? 0.0 : centerY;

            return new Matrix3x2(
                cos, sin,
                -sin, cos,
                cx * (1.0 - cos) + cy * sin,
                cy * (1.0 - cos) - cx * sin
            );
        }

        public static Matrix3x2 CreateRotationDegrees(double degrees, double centerX = 0.0, double centerY = 0.0)
        {
            double d = (double.IsNaN(degrees) || double.IsInfinity(degrees)) ? 0.0 : degrees;
            return CreateRotation(d * (Math.PI / 180.0), centerX, centerY);
        }

        public Point TransformPoint(double x, double y)
        {
            double px = (double.IsNaN(x) || double.IsInfinity(x)) ? 0.0 : x;
            double py = (double.IsNaN(y) || double.IsInfinity(y)) ? 0.0 : y;
            return new Point(
                px * M11 + py * M21 + OffsetX,
                px * M12 + py * M22 + OffsetY
            );
        }

        public Point Transform(Point p)
        {
            return TransformPoint(p.X, p.Y);
        }

        public override string ToString()
        {
            return string.Format("[[{0}, {1}], [{2}, {3}], [{4}, {5}]]",
                M11, M12, M21, M22, OffsetX, OffsetY);
        }
    }
}
