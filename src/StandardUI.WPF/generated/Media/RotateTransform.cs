// This file is generated from IRotateTransform.cs. Update the source file to change its contents.

using System.StandardUI.Media;

namespace System.StandardUI.Wpf.Media
{
    public class RotateTransform : Transform, IRotateTransform
    {
        public static readonly Windows.DependencyProperty AngleProperty = PropertyUtils.Register(nameof(Angle), typeof(double), typeof(RotateTransform), 0.0);
        public static readonly Windows.DependencyProperty CenterXProperty = PropertyUtils.Register(nameof(CenterX), typeof(double), typeof(RotateTransform), 0.0);
        public static readonly Windows.DependencyProperty CenterYProperty = PropertyUtils.Register(nameof(CenterY), typeof(double), typeof(RotateTransform), 0.0);
        
        public double Angle
        {
            get => (double) GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }
        
        public double CenterX
        {
            get => (double) GetValue(CenterXProperty);
            set => SetValue(CenterXProperty, value);
        }
        
        public double CenterY
        {
            get => (double) GetValue(CenterYProperty);
            set => SetValue(CenterYProperty, value);
        }
    }
}
