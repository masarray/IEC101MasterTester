using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IEC101MasterTester.Controls
{
    public partial class SemiCircleGauge : UserControl
    {
        private static readonly DependencyProperty AnimatedValueProperty =
            DependencyProperty.Register(
                "AnimatedValue",
                typeof(double),
                typeof(SemiCircleGauge),
                new PropertyMetadata(0d, OnAnimatedValueChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(SemiCircleGauge),
                new PropertyMetadata(0d, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum),
                typeof(double),
                typeof(SemiCircleGauge),
                new PropertyMetadata(0d, OnGaugeRangeChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum),
                typeof(double),
                typeof(SemiCircleGauge),
                new PropertyMetadata(100d, OnGaugeRangeChanged));

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(
                nameof(DisplayText),
                typeof(string),
                typeof(SemiCircleGauge),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty StateTextProperty =
            DependencyProperty.Register(
                nameof(StateText),
                typeof(string),
                typeof(SemiCircleGauge),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty GaugeBrushProperty =
            DependencyProperty.Register(
                nameof(GaugeBrush),
                typeof(Brush),
                typeof(SemiCircleGauge),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(34, 197, 94))));

        public static readonly DependencyProperty TrackBrushProperty =
            DependencyProperty.Register(
                nameof(TrackBrush),
                typeof(Brush),
                typeof(SemiCircleGauge),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(30, 41, 59))));

        public static readonly DependencyProperty ValueTextBrushProperty =
            DependencyProperty.Register(
                nameof(ValueTextBrush),
                typeof(Brush),
                typeof(SemiCircleGauge),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(230, 238, 248))));

        public SemiCircleGauge()
        {
            InitializeComponent();
            Loaded += SemiCircleGauge_Loaded;
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            set => SetValue(DisplayTextProperty, value);
        }

        public string StateText
        {
            get => (string)GetValue(StateTextProperty);
            set => SetValue(StateTextProperty, value);
        }

        public Brush GaugeBrush
        {
            get => (Brush)GetValue(GaugeBrushProperty);
            set => SetValue(GaugeBrushProperty, value);
        }

        public Brush TrackBrush
        {
            get => (Brush)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public Brush ValueTextBrush
        {
            get => (Brush)GetValue(ValueTextBrushProperty);
            set => SetValue(ValueTextBrushProperty, value);
        }

        private double AnimatedValue
        {
            get => (double)GetValue(AnimatedValueProperty);
            set => SetValue(AnimatedValueProperty, value);
        }

        private void SemiCircleGauge_Loaded(object sender, RoutedEventArgs e)
        {
            AnimatedValue = ClampValue(Value);
            UpdateArcGeometry(AnimatedValue);
            AnimateToValue(Value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SemiCircleGauge gauge = d as SemiCircleGauge;
            gauge?.AnimateToValue((double)e.NewValue);
        }

        private static void OnGaugeRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SemiCircleGauge gauge = d as SemiCircleGauge;
            if (gauge == null)
            {
                return;
            }

            gauge.AnimatedValue = gauge.ClampValue(gauge.AnimatedValue);
            gauge.UpdateArcGeometry(gauge.AnimatedValue);
        }

        private static void OnAnimatedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SemiCircleGauge gauge = d as SemiCircleGauge;
            gauge?.UpdateArcGeometry((double)e.NewValue);
        }

        private void AnimateToValue(double targetValue)
        {
            if (!IsLoaded)
            {
                AnimatedValue = ClampValue(targetValue);
                return;
            }

            DoubleAnimation animation = new DoubleAnimation
            {
                To = ClampValue(targetValue),
                Duration = TimeSpan.FromMilliseconds(480),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(AnimatedValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private double ClampValue(double value)
        {
            double min = Minimum;
            double max = Maximum;
            if (max <= min)
            {
                max = min + 1d;
            }

            return Math.Max(min, Math.Min(max, value));
        }

        private void UpdateArcGeometry(double value)
        {
            double min = Minimum;
            double max = Maximum <= min ? min + 1d : Maximum;
            double ratio = (value - min) / (max - min);
            ratio = Math.Max(0d, Math.Min(1d, ratio));

            if (ratio <= 0d)
            {
                ValuePath.Data = Geometry.Empty;
                return;
            }

            const double centerX = 110d;
            const double centerY = 125d;
            const double radius = 90d;
            Point startPoint = new Point(20d, 125d);

            double angle = Math.PI - (Math.PI * ratio);
            double endX = centerX + (radius * Math.Cos(angle));
            double endY = centerY - (radius * Math.Sin(angle));

            PathFigure figure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false,
                IsFilled = false
            };

            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(endX, endY),
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false
            });

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            ValuePath.Data = geometry;
        }
    }
}
