using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kerf.UI
{
    public static class VectorIcons
    {
        public static Geometry SelectGeometry { get; private set; }
        public static Geometry MarqueeGeometry { get; private set; }
        public static Geometry HandGeometry { get; private set; }
        public static Geometry FrameGeometry { get; private set; }
        public static Geometry RectGeometry { get; private set; }
        public static Geometry EllipseGeometry { get; private set; }
        public static Geometry StarGeometry { get; private set; }
        public static Geometry PolygonGeometry { get; private set; }
        public static Geometry PenGeometry { get; private set; }
        public static Geometry TextGeometry { get; private set; }
        public static Geometry LineGeometry { get; private set; }
        public static Geometry ArrowGeometry { get; private set; }
        public static Geometry UndoGeometry { get; private set; }
        public static Geometry RedoGeometry { get; private set; }
        public static Geometry EyeOpenGeometry { get; private set; }
        public static Geometry EyeClosedGeometry { get; private set; }
        public static Geometry LockClosedGeometry { get; private set; }
        public static Geometry LockOpenGeometry { get; private set; }
        public static Geometry HomeGeometry { get; private set; }
        public static Geometry SaveGeometry { get; private set; }
        public static Geometry ExportGeometry { get; private set; }
        public static Geometry UnionGeometry { get; private set; }
        public static Geometry SubtractGeometry { get; private set; }
        public static Geometry IntersectGeometry { get; private set; }
        public static Geometry ExcludeGeometry { get; private set; }
        public static Geometry FolderGeometry { get; private set; }
        public static Geometry ChevronRightGeometry { get; private set; }
        public static Geometry ChevronDownGeometry { get; private set; }
        public static Geometry TrashGeometry { get; private set; }
        public static Geometry CopyGeometry { get; private set; }

        static VectorIcons()
        {
            SelectGeometry = Parse("M 2 2 L 9 19 L 12 12 L 19 9 Z");
            MarqueeGeometry = Parse("M 3 3 L 8 3 M 11 3 L 16 3 M 19 3 L 21 3 L 21 5 M 21 8 L 21 13 M 21 16 L 21 21 L 19 21 M 16 21 L 11 21 M 8 21 L 3 21 L 3 19 M 3 16 L 3 11 M 3 8 L 3 5 Z");
            HandGeometry = Parse("M 10 4 A 2 2 0 0 1 14 4 L 14 10 L 15 10 A 2 2 0 0 1 19 10 L 19 15 C 19 20 15 22 10 22 C 5 22 4 18 4 14 L 4 8 A 2 2 0 0 1 8 8 L 8 10 L 10 10 Z");
            FrameGeometry = Parse("M 4 2 L 4 22 M 20 2 L 20 22 M 2 4 L 22 4 M 2 20 L 22 20");
            RectGeometry = Parse("M 3 5 C 3 3.9 3.9 3 5 3 L 19 3 C 20.1 3 21 3.9 21 5 L 21 19 C 21 20.1 20.1 21 19 21 L 5 21 C 3.9 21 3 20.1 3 19 Z");
            EllipseGeometry = Parse("M 12 2 A 10 10 0 1 1 11.99 2 Z");
            StarGeometry = Parse("M 12 2 L 15.09 8.26 L 22 9.27 L 17 14.14 L 18.18 21.02 L 12 17.77 L 5.82 21.02 L 7 14.14 L 2 9.27 L 8.91 8.26 Z");
            PolygonGeometry = Parse("M 12 2 L 21.5 7.5 L 21.5 18.5 L 12 24 L 2.5 18.5 L 2.5 7.5 Z");
            PenGeometry = Parse("M 12 19 L 19 12 L 22 15 L 15 22 Z M 18 13 L 11 6 L 14 3 L 21 10 Z M 2 22 L 5 21 L 11 15 L 9 13 L 3 19 Z");
            TextGeometry = Parse("M 4 4 L 20 4 M 12 4 L 12 20 M 8 20 L 16 20");
            LineGeometry = Parse("M 4 20 L 20 4");
            ArrowGeometry = Parse("M 5 19 L 19 5 M 12 5 L 19 5 L 19 12");
            UndoGeometry = Parse("M 9 14 L 4 9 L 9 4 M 5 9 L 15 9 C 18 9 20 11 20 14 C 20 17 18 19 15 19 L 11 19");
            RedoGeometry = Parse("M 15 14 L 20 9 L 15 4 M 19 9 L 9 9 C 6 9 4 11 4 14 C 4 17 6 19 9 19 L 13 19");
            EyeOpenGeometry = Parse("M 1 12 C 4 5 20 5 23 12 C 20 19 4 19 1 12 Z M 12 8 A 4 4 0 1 0 12.01 8 Z");
            EyeClosedGeometry = Parse("M 2 2 L 22 22 M 1 12 C 4 5 20 5 23 12 C 21 16 16.5 18.5 12 18.5");
            LockClosedGeometry = Parse("M 7 11 L 7 7 C 7 4.2 9.2 2 12 2 C 14.8 2 17 4.2 17 7 L 17 11 M 5 11 L 19 11 C 20.1 11 21 11.9 21 13 L 21 20 C 21 21.1 20.1 22 19 22 L 5 22 C 3.9 22 3 21.1 3 20 L 3 13 C 3 11.9 3.9 11 5 11 Z");
            LockOpenGeometry = Parse("M 7 11 L 7 7 C 7 4.2 9.2 2 12 2 C 14.8 2 17 4.2 17 7 M 5 11 L 19 11 C 20.1 11 21 11.9 21 13 L 21 20 C 21 21.1 20.1 22 19 22 L 5 22 C 3.9 22 3 21.1 3 20 L 3 13 C 3 11.9 3.9 11 5 11 Z");
            HomeGeometry = Parse("M 3 9 L 12 2 L 21 9 L 21 20 C 21 21.1 20.1 22 19 22 L 5 22 C 3.9 22 3 21.1 3 20 Z M 9 22 L 9 12 L 15 12 L 15 22");
            SaveGeometry = Parse("M 19 21 L 5 21 C 3.9 21 3 20.1 3 19 L 3 5 C 3 3.9 3.9 3 5 3 L 16 3 L 21 8 L 21 19 C 21 20.1 20.1 21 19 21 Z M 17 21 L 17 13 L 7 13 L 7 21 M 7 3 L 7 8 L 15 8");
            ExportGeometry = Parse("M 4 12 L 4 20 C 4 21.1 4.9 22 6 22 L 18 22 C 19.1 22 20 21.1 20 20 L 20 12 M 12 2 L 12 16 M 7 7 L 12 2 L 17 7");
            UnionGeometry = Parse("M 8 2 L 20 2 L 20 14 L 14 14 L 14 20 L 2 20 L 2 8 L 8 8 Z");
            SubtractGeometry = Parse("M 2 2 L 14 2 L 14 8 L 8 8 L 8 14 L 2 14 Z");
            IntersectGeometry = Parse("M 8 8 L 14 8 L 14 14 L 8 14 Z");
            ExcludeGeometry = Parse("M 2 2 L 14 2 L 14 8 L 8 8 L 8 14 L 2 14 Z M 8 14 L 14 14 L 14 20 L 20 20 L 20 8 L 14 8");
            FolderGeometry = Parse("M 2 4 C 2 2.9 2.9 2 4 2 L 9 2 L 11 4 L 20 4 C 21.1 4 22 4.9 22 6 L 22 18 C 22 19.1 21.1 20 20 20 L 4 20 C 2.9 20 2 19.1 2 18 Z");
            ChevronRightGeometry = Parse("M 9 6 L 15 12 L 9 18");
            ChevronDownGeometry = Parse("M 6 9 L 12 15 L 18 9");
            CopyGeometry = Parse("M 8 4 L 18 4 C 19.1 4 20 4.9 20 6 L 20 16 C 20 17.1 19.1 18 18 18 L 8 18 C 6.9 18 6 17.1 6 16 L 6 6 C 6 4.9 6.9 4 8 4 Z M 4 8 L 4 20 C 4 21.1 4.9 22 6 22 L 16 22");
            TrashGeometry = Parse("M 3 6 L 21 6 M 19 6 L 19 20 C 19 21.1 18.1 22 17 22 L 7 22 C 5.9 22 5 21.1 5 20 L 5 6 M 8 6 L 8 4 C 8 2.9 8.9 2 10 2 L 14 2 C 15.1 2 16 2.9 16 4 L 16 6");
        }

        private static Geometry Parse(string pathData)
        {
            var geo = Geometry.Parse(pathData);
            geo.Freeze();
            return geo;
        }

        public static System.Windows.Shapes.Path CreateIcon(Geometry geo, Brush strokeBrush, Brush fillBrush = null, double size = 16.0, double strokeThickness = 1.5)
        {
            var p = new System.Windows.Shapes.Path
            {
                Data = geo,
                Stroke = strokeBrush,
                Fill = fillBrush,
                StrokeThickness = strokeThickness,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return p;
        }
    }
}
