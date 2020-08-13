// This file is generated from IBezierSegment.cs. Update the source file to change its contents.

using Microsoft.StandardUI.Media;
using System.Windows;

namespace Microsoft.StandardUI.Wpf.Media
{
    public class BezierSegment : PathSegment, IBezierSegment
    {
        public static readonly System.Windows.DependencyProperty Point1Property = PropertyUtils.Create(nameof(Point1), typeof(PointWpf), typeof(PointWpf), PointWpf.Default);
        public static readonly System.Windows.DependencyProperty Point2Property = PropertyUtils.Create(nameof(Point2), typeof(PointWpf), typeof(PointWpf), PointWpf.Default);
        public static readonly System.Windows.DependencyProperty Point3Property = PropertyUtils.Create(nameof(Point3), typeof(PointWpf), typeof(PointWpf), PointWpf.Default);
        
        public PointWpf Point1
        {
            get => (PointWpf) GetValue(Point1Property);
            set => SetValue(Point1Property, value);
        }
        Point IBezierSegment.Point1
        {
            get => Point1.Point;
            set => Point1 = new PointWpf(value);
        }
        
        public PointWpf Point2
        {
            get => (PointWpf) GetValue(Point2Property);
            set => SetValue(Point2Property, value);
        }
        Point IBezierSegment.Point2
        {
            get => Point2.Point;
            set => Point2 = new PointWpf(value);
        }
        
        public PointWpf Point3
        {
            get => (PointWpf) GetValue(Point3Property);
            set => SetValue(Point3Property, value);
        }
        Point IBezierSegment.Point3
        {
            get => Point3.Point;
            set => Point3 = new PointWpf(value);
        }
    }
}
