using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace IEC101MasterTester.Controls
{
    public sealed class NucLinkTraceTapeControl : FrameworkElement
    {
        private const int MaxSamples = 300;
        private const double LeftMargin = 72;
        private const double RightMargin = 14;
        private static readonly Brush PanelBrush = CreateBrush(10, 20, 33);
        private static readonly Brush UpperLaneBrush = CreateBrush(22, 34, 49, 60);
        private static readonly Brush LowerLaneBrush = CreateBrush(16, 26, 39, 38);
        private static readonly Brush BaseLineBrush = CreateBrush(120, 140, 170, 95);
        private static readonly Brush LabelBrush = CreateBrush(142, 163, 188);
        private static readonly Brush LinkABrush = CreateBrush(90, 162, 255, 230);
        private static readonly Brush LinkBBrush = CreateBrush(50, 193, 108, 230);
        private static readonly Brush GiHighlightBrush = CreateBrush(243, 182, 51, 70);
        private static readonly Brush MarkerBandBrush = CreateBrush(243, 182, 51, 45);
        private static readonly Brush MarkerLineBrush = CreateBrush(243, 182, 51);
        private static readonly Brush BadgeFillBrush = CreateBrush(30, 42, 58);
        private static readonly Typeface LabelTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Typeface BadgeTypeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private IReadOnlyList<float> _laneA;
        private IReadOnlyList<float> _laneB;
        private Rect _plotRect;
        private Rect _laneARect;
        private Rect _laneBRect;
        private int _selectedBucketIndex = -1;

        public event EventHandler<DateTime> SelectedTimeChanged;

        public DateTime WindowStart
        {
            get { return (DateTime)GetValue(WindowStartProperty); }
            set { SetValue(WindowStartProperty, value); }
        }

        public static readonly DependencyProperty WindowStartProperty =
            DependencyProperty.Register(nameof(WindowStart), typeof(DateTime), typeof(NucLinkTraceTapeControl),
                new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsRender));

        public DateTime WindowEnd
        {
            get { return (DateTime)GetValue(WindowEndProperty); }
            set { SetValue(WindowEndProperty, value); }
        }

        public static readonly DependencyProperty WindowEndProperty =
            DependencyProperty.Register(nameof(WindowEnd), typeof(DateTime), typeof(NucLinkTraceTapeControl),
                new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsRender));

        public DateTime? SelectedTime
        {
            get { return (DateTime?)GetValue(SelectedTimeProperty); }
            set { SetValue(SelectedTimeProperty, value); }
        }

        public static readonly DependencyProperty SelectedTimeProperty =
            DependencyProperty.Register(nameof(SelectedTime), typeof(DateTime?), typeof(NucLinkTraceTapeControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedTimeChanged));

        public void SetBuffers(IReadOnlyList<float> laneA, IReadOnlyList<float> laneB)
        {
            _laneA = laneA;
            _laneB = laneB;
            _selectedBucketIndex = ResolveBucketIndex(SelectedTime);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            dc.DrawRectangle(PanelBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            Rect contentRect = new Rect(LeftMargin, 8, Math.Max(0, ActualWidth - LeftMargin - RightMargin), Math.Max(0, ActualHeight - 18));
            double halfHeight = contentRect.Height / 2.0;
            dc.DrawRoundedRectangle(UpperLaneBrush, null, new Rect(contentRect.Left, contentRect.Top, contentRect.Width, halfHeight), 6, 6);
            dc.DrawRoundedRectangle(LowerLaneBrush, null, new Rect(contentRect.Left, contentRect.Top + halfHeight, contentRect.Width, halfHeight), 6, 6);

            double laneTop = 16;
            double laneBottom = ActualHeight - 10;
            double laneHeight = Math.Max(26, (laneBottom - laneTop) / 2.0);
            _plotRect = new Rect(LeftMargin, laneTop, GetUsableWidth(), Math.Max(0, laneBottom - laneTop));
            _laneARect = new Rect(_plotRect.Left, laneTop, _plotRect.Width, laneHeight);
            _laneBRect = new Rect(_plotRect.Left, laneTop + laneHeight, _plotRect.Width, laneHeight);

            DrawText(dc, "LINK A", 8, laneTop + 4, LabelTypeface, 10, LabelBrush);
            DrawText(dc, "LINK B", 8, laneTop + laneHeight + 4, LabelTypeface, 10, LabelBrush);
            DrawLane(dc, _laneA, _laneARect, LinkABrush);
            DrawLane(dc, _laneB, _laneBRect, LinkBBrush);
            DrawMarker(dc, WindowEnd, true);

            if (SelectedTime.HasValue)
            {
                DrawSelectedMarker(dc);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            Point position = e.GetPosition(this);
            int bucketIndex = HitTestBucketIndex(position);
            if (bucketIndex < 0)
            {
                return;
            }

            _selectedBucketIndex = bucketIndex;
            DateTime? selected = GetBucketTime(bucketIndex);
            if (selected.HasValue)
            {
                SelectedTimeChanged?.Invoke(this, selected.Value);
            }
        }

        private void DrawLane(DrawingContext dc, IReadOnlyList<float> buffer, Rect laneRect, Brush laneBrush)
        {
            if (buffer == null || buffer.Count == 0)
            {
                return;
            }

            double laneTop = laneRect.Top;
            double laneHeight = laneRect.Height;
            double laneCenter = laneTop + (laneHeight / 2.0);
            double highY = laneTop + 4;
            double lowY = laneTop + laneHeight - 6;
            Pen basePen = new Pen(BaseLineBrush, 1);
            if (basePen.CanFreeze)
            {
                basePen.Freeze();
            }

            dc.DrawLine(basePen, new Point(laneRect.Left, lowY), new Point(laneRect.Right, lowY));
            dc.DrawLine(basePen, new Point(laneRect.Left, laneCenter), new Point(laneRect.Right, laneCenter));

            double dx = GetBucketWidth(buffer.Count);
            StreamGeometry lineGeometry = new StreamGeometry();
            StreamGeometry fillGeometry = new StreamGeometry();

            using (StreamGeometryContext line = lineGeometry.Open())
            using (StreamGeometryContext fill = fillGeometry.Open())
            {
                bool started = false;
                bool giOpen = false;
                double giStartX = 0;
                for (int i = 0; i < buffer.Count; i++)
                {
                    double x = laneRect.Left + (i * dx);
                    double y = GetLaneY(buffer[i], laneCenter, highY, lowY);
                    bool isGiBurst = buffer[i] > 0.95f;

                    if (!started)
                    {
                        line.BeginFigure(new Point(x, y), false, false);
                        fill.BeginFigure(new Point(x, laneCenter), true, true);
                        fill.LineTo(new Point(x, y), true, false);
                        started = true;
                    }
                    else
                    {
                        line.LineTo(new Point(x, y), true, false);
                        fill.LineTo(new Point(x, y), true, false);
                    }

                    if (isGiBurst && !giOpen)
                    {
                        giOpen = true;
                        giStartX = x;
                    }
                    else if (!isGiBurst && giOpen)
                    {
                        dc.DrawRectangle(GiHighlightBrush, null, new Rect(giStartX, laneTop + 2, Math.Max(2, x - giStartX), laneHeight - 4));
                        giOpen = false;
                    }
                }

                if (started)
                {
                    double endX = laneRect.Left + ((buffer.Count - 1) * dx);
                    fill.LineTo(new Point(endX, laneCenter), true, false);
                    if (giOpen)
                    {
                        dc.DrawRectangle(GiHighlightBrush, null, new Rect(giStartX, laneTop + 2, Math.Max(2, endX - giStartX), laneHeight - 4));
                    }
                }
            }

            lineGeometry.Freeze();
            fillGeometry.Freeze();

            double average = 0;
            for (int i = 0; i < buffer.Count; i++)
            {
                average += buffer[i];
            }

            average /= buffer.Count;
            average = Math.Max(0.08, Math.Min(1.0, average));

            bool strongGi = average > 0.95;
            Brush fillBrush = strongGi ? CreateBrush(243, 182, 51, 55) : CreateAlphaBrush(laneBrush, (byte)(20 + (average * 55)));
            Pen wavePen = new Pen(
                strongGi ? CreateBrush(243, 182, 51, 235) : CreateAlphaBrush(laneBrush, (byte)(80 + (average * 175))),
                strongGi ? 4.2 : 1.2 + (average * 2.2));
            if (wavePen.CanFreeze)
            {
                wavePen.Freeze();
            }

            dc.DrawGeometry(fillBrush, wavePen, fillGeometry);
            dc.DrawGeometry(null, wavePen, lineGeometry);
        }

        private void DrawMarker(DrawingContext dc, DateTime time, bool isNowMarker)
        {
            double x = TimeToX(time);
            Pen pen = new Pen(MarkerLineBrush, isNowMarker ? 1.5 : 1.2);
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            if (isNowMarker)
            {
                dc.DrawRectangle(MarkerBandBrush, null, new Rect(Math.Max(_plotRect.Left, x - 8), 4, 8, Math.Max(0, ActualHeight - 8)));
            }

            dc.DrawLine(pen, new Point(x, _plotRect.Top), new Point(x, _plotRect.Bottom));

            if (!isNowMarker)
            {
                string text = time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                FormattedText formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BadgeTypeface, 10, Brushes.White, 1.0);
                double badgeWidth = formatted.Width + 16;
                Rect badgeRect = new Rect(
                    Math.Max(_plotRect.Left, Math.Min(_plotRect.Right - badgeWidth - 6, x - (badgeWidth / 2.0))),
                    6,
                    badgeWidth,
                    formatted.Height + 6);

                dc.DrawRoundedRectangle(BadgeFillBrush, pen, badgeRect, 5, 5);
                dc.DrawText(formatted, new Point(badgeRect.Left + 8, badgeRect.Top + 3));
            }
        }

        private void DrawSelectedMarker(DrawingContext dc)
        {
            DateTime? selected = GetBucketTime(_selectedBucketIndex);
            if (!selected.HasValue)
            {
                selected = SelectedTime.HasValue ? ClampToWindow(SelectedTime.Value) : (DateTime?)null;
            }

            if (!selected.HasValue)
            {
                return;
            }

            DrawMarker(dc, selected.Value, false);
        }

        private int HitTestBucketIndex(Point point)
        {
            IReadOnlyList<float> reference = GetReferenceBuffer();
            if (reference == null || reference.Count == 0 || !_plotRect.Contains(point))
            {
                return -1;
            }

            double ratio = (point.X - _plotRect.Left) / Math.Max(1, _plotRect.Width);
            ratio = Math.Max(0, Math.Min(1, ratio));
            int index = (int)(ratio * reference.Count);
            return Math.Max(0, Math.Min(reference.Count - 1, index));
        }

        private DateTime? GetBucketTime(int bucketIndex)
        {
            IReadOnlyList<float> reference = GetReferenceBuffer();
            if (reference == null || reference.Count == 0 || WindowStart == DateTime.MinValue || WindowEnd == DateTime.MinValue)
            {
                return null;
            }

            int clampedIndex = Math.Max(0, Math.Min(reference.Count - 1, bucketIndex));
            double bucketSeconds = (WindowEnd - WindowStart).TotalSeconds / Math.Max(1, reference.Count);
            return WindowStart.AddSeconds(clampedIndex * bucketSeconds);
        }

        private int ResolveBucketIndex(DateTime? time)
        {
            IReadOnlyList<float> reference = GetReferenceBuffer();
            if (!time.HasValue || reference == null || reference.Count == 0 || WindowStart == DateTime.MinValue || WindowEnd == DateTime.MinValue)
            {
                return -1;
            }

            double totalSeconds = Math.Max(0.001, (WindowEnd - WindowStart).TotalSeconds);
            double ratio = (ClampToWindow(time.Value) - WindowStart).TotalSeconds / totalSeconds;
            int index = (int)(ratio * reference.Count);
            return Math.Max(0, Math.Min(reference.Count - 1, index));
        }

        private DateTime ClampToWindow(DateTime time)
        {
            if (time < WindowStart)
            {
                return WindowStart;
            }

            if (time > WindowEnd)
            {
                return WindowEnd;
            }

            return time;
        }

        private double TimeToX(DateTime time)
        {
            double ratio = (time - WindowStart).TotalSeconds / Math.Max(1, (WindowEnd - WindowStart).TotalSeconds);
            ratio = Math.Max(0, Math.Min(1, ratio));
            return _plotRect.Left + (_plotRect.Width * ratio);
        }

        private double GetUsableWidth()
        {
            return Math.Max(1, ActualWidth - LeftMargin - RightMargin);
        }

        private double GetBucketWidth(int sampleCount)
        {
            return _plotRect.Width / Math.Max(1, sampleCount);
        }

        private IReadOnlyList<float> GetReferenceBuffer()
        {
            if (_laneA != null && _laneA.Count > 0)
            {
                return _laneA;
            }

            if (_laneB != null && _laneB.Count > 0)
            {
                return _laneB;
            }

            return null;
        }

        private static double GetLaneY(float sample, double centerY, double highY, double lowY)
        {
            double amplitude = (centerY - highY) * sample * 2.0;
            double y = centerY - amplitude;
            return Math.Max(highY, Math.Min(lowY, y));
        }

        private static void DrawText(DrawingContext dc, string text, double x, double y, Typeface typeface, double size, Brush brush)
        {
            dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush, 1.0), new Point(x, y));
        }

        private static SolidColorBrush CreateBrush(byte r, byte g, byte b, byte a = 255)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        private static SolidColorBrush CreateAlphaBrush(Brush source, byte alpha)
        {
            SolidColorBrush solid = source as SolidColorBrush;
            Color baseColor = solid != null ? solid.Color : Colors.White;
            return CreateBrush(baseColor.R, baseColor.G, baseColor.B, alpha);
        }

        private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            NucLinkTraceTapeControl control = d as NucLinkTraceTapeControl;
            if (control == null)
            {
                return;
            }

            DateTime? selectedTime = e.NewValue is DateTime
                ? (DateTime)e.NewValue
                : (DateTime?)null;
            control._selectedBucketIndex = control.ResolveBucketIndex(selectedTime);
        }
    }
}
