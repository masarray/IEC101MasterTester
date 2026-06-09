using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace IEC101MasterTester.Controls
{
    public sealed class NucLinkTraceTapeControl : FrameworkElement
    {
        private const double LeftMargin = 72;
        private const double RightMargin = 14;
        private const double WaveStrokeThickness = 1.8;
        private const double BurstBandHalfThickness = 5.0;
        private const float BurstHighlightThreshold = 0.9f;
        private static readonly Brush PanelBrush = CreateBrush(10, 20, 33);
        private static readonly Brush UpperLaneBrush = CreateBrush(22, 34, 49, 60);
        private static readonly Brush LowerLaneBrush = CreateBrush(16, 26, 39, 38);
        private static readonly Brush BaseLineBrush = CreateBrush(120, 140, 170, 95);
        private static readonly Brush LabelBrush = CreateBrush(142, 163, 188);
        private static readonly Brush ActiveWaveBrush = CreateBrush(50, 193, 108, 235);
        private static readonly Brush StandbyWaveBrush = CreateBrush(90, 162, 255, 235);
        private static readonly Brush ActiveBurstHighlightBrush = CreateBrush(50, 193, 108, 70);
        private static readonly Brush StandbyBurstHighlightBrush = CreateBrush(90, 162, 255, 70);
        private static readonly Brush MarkerBandBrush = CreateBrush(243, 182, 51, 45);
        private static readonly Brush MarkerLineBrush = CreateBrush(243, 182, 51);
        private static readonly Brush BadgeFillBrush = CreateBrush(30, 42, 58);
        private static readonly Typeface LabelTypeface = new Typeface(new FontFamily("Plus Jakarta Sans"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Typeface BadgeTypeface = new Typeface(new FontFamily("Plus Jakarta Sans"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
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

        public bool IsLaneAActive
        {
            get { return (bool)GetValue(IsLaneAActiveProperty); }
            set { SetValue(IsLaneAActiveProperty, value); }
        }

        public static readonly DependencyProperty IsLaneAActiveProperty =
            DependencyProperty.Register(nameof(IsLaneAActive), typeof(bool), typeof(NucLinkTraceTapeControl),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsLaneBActive
        {
            get { return (bool)GetValue(IsLaneBActiveProperty); }
            set { SetValue(IsLaneBActiveProperty, value); }
        }

        public static readonly DependencyProperty IsLaneBActiveProperty =
            DependencyProperty.Register(nameof(IsLaneBActive), typeof(bool), typeof(NucLinkTraceTapeControl),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

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
            DrawLane(dc, _laneA, _laneARect, IsLaneAActive);
            DrawLane(dc, _laneB, _laneBRect, IsLaneBActive);
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

        private void DrawLane(DrawingContext dc, IReadOnlyList<float> buffer, Rect laneRect, bool isActive)
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

            List<Point> wavePoints = BuildWavePoints(buffer, laneRect, laneCenter, highY, lowY);
            if (wavePoints.Count == 0)
            {
                return;
            }

            Brush waveBrush = GetWaveStrokeBrush(isActive);
            Brush burstBrush = GetWaveHighlightBrush(isActive);
            Pen wavePen = CreateWavePen(waveBrush);
            StreamGeometry waveGeometry = BuildWaveGeometry(wavePoints);

            dc.PushClip(new RectangleGeometry(laneRect));
            RenderBurstHighlights(dc, buffer, wavePoints, laneRect, burstBrush);
            dc.DrawGeometry(null, wavePen, waveGeometry);
            dc.Pop();
        }

        private List<Point> BuildWavePoints(IReadOnlyList<float> buffer, Rect laneRect, double centerY, double highY, double lowY)
        {
            List<Point> points = new List<Point>(buffer.Count);
            double dx = GetBucketWidth(buffer.Count);
            for (int i = 0; i < buffer.Count; i++)
            {
                double x = laneRect.Left + (i * dx);
                double y = GetLaneY(buffer[i], centerY, highY, lowY);
                points.Add(new Point(x, y));
            }

            return points;
        }

        private void RenderBurstHighlights(DrawingContext dc, IReadOnlyList<float> buffer, IReadOnlyList<Point> wavePoints, Rect laneRect, Brush highlightBrush)
        {
            foreach (BurstSegment segment in GetBurstSegments(buffer, laneRect))
            {
                StreamGeometry geometry = BuildBurstBandGeometry(wavePoints, segment.StartX, segment.EndX, laneRect);
                if (geometry != null)
                {
                    dc.DrawGeometry(highlightBrush, null, geometry);
                }
            }
        }

        private List<BurstSegment> GetBurstSegments(IReadOnlyList<float> buffer, Rect laneRect)
        {
            List<BurstSegment> segments = new List<BurstSegment>();
            if (buffer == null || buffer.Count == 0)
            {
                return segments;
            }

            double dx = GetBucketWidth(buffer.Count);
            int startIndex = -1;
            for (int i = 0; i < buffer.Count; i++)
            {
                bool isBurst = buffer[i] >= BurstHighlightThreshold;
                if (isBurst && startIndex < 0)
                {
                    startIndex = i;
                }
                else if (!isBurst && startIndex >= 0)
                {
                    segments.Add(new BurstSegment(
                        laneRect.Left + (startIndex * dx),
                        laneRect.Left + (i * dx)));
                    startIndex = -1;
                }
            }

            if (startIndex >= 0)
            {
                segments.Add(new BurstSegment(
                    laneRect.Left + (startIndex * dx),
                    laneRect.Left + ((buffer.Count - 1) * dx)));
            }

            return segments;
        }

        private StreamGeometry BuildBurstBandGeometry(IReadOnlyList<Point> wavePoints, double startX, double endX, Rect laneRect)
        {
            List<Point> segmentPoints;
            if (!TryCollectPointsInRange(wavePoints, startX, endX, out segmentPoints))
            {
                double fallbackCenterY = wavePoints.Count > 0 ? wavePoints[0].Y : laneRect.Top + (laneRect.Height / 2.0);
                return BuildNarrowFallbackBand(startX, endX, fallbackCenterY, laneRect);
            }

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                Point firstUpper = ClampToLaneBand(new Point(segmentPoints[0].X, segmentPoints[0].Y - BurstBandHalfThickness), laneRect);
                context.BeginFigure(firstUpper, true, true);

                for (int i = 1; i < segmentPoints.Count; i++)
                {
                    context.LineTo(ClampToLaneBand(new Point(segmentPoints[i].X, segmentPoints[i].Y - BurstBandHalfThickness), laneRect), true, false);
                }

                for (int i = segmentPoints.Count - 1; i >= 0; i--)
                {
                    context.LineTo(ClampToLaneBand(new Point(segmentPoints[i].X, segmentPoints[i].Y + BurstBandHalfThickness), laneRect), true, false);
                }
            }

            if (geometry.CanFreeze)
            {
                geometry.Freeze();
            }

            return geometry;
        }

        private bool TryCollectPointsInRange(IReadOnlyList<Point> wavePoints, double startX, double endX, out List<Point> segmentPoints)
        {
            segmentPoints = new List<Point>();
            if (wavePoints == null || wavePoints.Count == 0 || endX < startX)
            {
                return false;
            }

            Point? startBoundary = null;
            Point? endBoundary = null;

            for (int i = 0; i < wavePoints.Count - 1; i++)
            {
                Point current = wavePoints[i];
                Point next = wavePoints[i + 1];

                if (!startBoundary.HasValue && startX >= current.X && startX <= next.X)
                {
                    startBoundary = InterpolateBoundaryPoint(current, next, startX);
                    segmentPoints.Add(startBoundary.Value);
                }

                if (current.X >= startX && current.X <= endX)
                {
                    segmentPoints.Add(current);
                }

                if (!endBoundary.HasValue && endX >= current.X && endX <= next.X)
                {
                    endBoundary = InterpolateBoundaryPoint(current, next, endX);
                    segmentPoints.Add(endBoundary.Value);
                    break;
                }
            }

            Point last = wavePoints[wavePoints.Count - 1];
            if (last.X >= startX && last.X <= endX)
            {
                segmentPoints.Add(last);
            }

            segmentPoints = segmentPoints
                .OrderBy(point => point.X)
                .GroupBy(point => Math.Round(point.X, 3))
                .Select(group => group.First())
                .ToList();

            return segmentPoints.Count >= 2;
        }

        private static Point InterpolateBoundaryPoint(Point start, Point end, double boundaryX)
        {
            if (Math.Abs(end.X - start.X) < 0.0001)
            {
                return new Point(boundaryX, start.Y);
            }

            double ratio = (boundaryX - start.X) / (end.X - start.X);
            ratio = Math.Max(0, Math.Min(1, ratio));
            return new Point(boundaryX, start.Y + ((end.Y - start.Y) * ratio));
        }

        private StreamGeometry BuildNarrowFallbackBand(double startX, double endX, double centerY, Rect laneRect)
        {
            double left = Math.Max(laneRect.Left, Math.Min(startX, endX));
            double right = Math.Min(laneRect.Right, Math.Max(startX, endX));
            if (right - left < 1)
            {
                right = Math.Min(laneRect.Right, left + 2);
            }

            double top = Math.Max(laneRect.Top + 2, centerY - BurstBandHalfThickness);
            double bottom = Math.Min(laneRect.Bottom - 2, centerY + BurstBandHalfThickness);
            Rect bandRect = new Rect(new Point(left, top), new Point(right, bottom));
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(new Point(bandRect.Left, bandRect.Top), true, true);
                context.LineTo(new Point(bandRect.Right, bandRect.Top), true, false);
                context.LineTo(new Point(bandRect.Right, bandRect.Bottom), true, false);
                context.LineTo(new Point(bandRect.Left, bandRect.Bottom), true, false);
            }

            if (geometry.CanFreeze)
            {
                geometry.Freeze();
            }

            return geometry;
        }

        private static Point ClampToLaneBand(Point point, Rect laneRect)
        {
            return new Point(
                Math.Max(laneRect.Left, Math.Min(laneRect.Right, point.X)),
                Math.Max(laneRect.Top + 2, Math.Min(laneRect.Bottom - 2, point.Y)));
        }

        private static StreamGeometry BuildWaveGeometry(IReadOnlyList<Point> wavePoints)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(wavePoints[0], false, false);
                for (int i = 1; i < wavePoints.Count; i++)
                {
                    context.LineTo(wavePoints[i], true, false);
                }
            }

            if (geometry.CanFreeze)
            {
                geometry.Freeze();
            }

            return geometry;
        }

        private static Brush GetWaveStrokeBrush(bool isActive)
        {
            return isActive ? ActiveWaveBrush : StandbyWaveBrush;
        }

        private static Brush GetWaveHighlightBrush(bool isActive)
        {
            return isActive ? ActiveBurstHighlightBrush : StandbyBurstHighlightBrush;
        }

        private static Pen CreateWavePen(Brush brush)
        {
            Pen pen = new Pen(brush, WaveStrokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            return pen;
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

        private sealed class BurstSegment
        {
            public BurstSegment(double startX, double endX)
            {
                StartX = startX;
                EndX = endX;
            }

            public double StartX { get; private set; }

            public double EndX { get; private set; }
        }
    }
}
