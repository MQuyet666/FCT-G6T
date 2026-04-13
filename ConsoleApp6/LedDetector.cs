using OpenCvSharp;
using System.Collections.Generic;

namespace ConsoleApp6
{
    internal sealed class LedDetector
    {
        internal readonly record struct LedRoiDetection(Rect Roi, int PixelCount, bool LedOn);

        private enum LedColor
        {
            All,
            Red,
            Yellow,
            Cyan
        }

        public Rect Roi { get; set; } = new Rect(1550, 1100, 200, 200); // Rect(1550, 1100, 200, 200);
        public Rect Roi3 { get; set; } = new Rect(1550, 1100, 200, 200);
        public Rect Roi2 { get; set; } = new Rect(1550, 1500, 600, 600);
        public Rect Roi1 { get; set; } = new Rect(1550, 1500, 1000, 1000);
        public bool UseMultipleRois { get; set; }
        public int PixelThreshold { get; set; } = 120;

        private readonly Scalar _lowerRed1 = new Scalar(0, 120, 120);
        private readonly Scalar _upperRed1 = new Scalar(10, 255, 255);
        private readonly Scalar _lowerRed2 = new Scalar(170, 120, 120);
        private readonly Scalar _upperRed2 = new Scalar(180, 255, 255);
        private readonly Scalar _lowerGreen = new Scalar(35, 80, 80);
        private readonly Scalar _upperGreen = new Scalar(85, 255, 255);
        private readonly Scalar _lowerYellow = new Scalar(15, 80, 80);
        private readonly Scalar _upperYellow = new Scalar(45, 255, 255);
        private readonly Scalar _lowerCyan = new Scalar(85, 80, 80);
        private readonly Scalar _upperCyan = new Scalar(100, 255, 255);

        public bool IsLedOn(Mat frame, out int redPixelCount)
        {
            var detections = DetectLeds(frame, out redPixelCount);
            foreach (var detection in detections)
            {
                if (detection.LedOn)
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<LedRoiDetection> DetectLeds(Mat frame, out int redPixelCount)
        {
            redPixelCount = 0;
            var detections = new List<LedRoiDetection>();
            if (frame == null || frame.Empty())
            {
                return detections;
            }

            foreach (var roi in GetRois())
            {
                var safeRoi = ClampRoi(frame.Width, frame.Height, roi);
                if (safeRoi.Width <= 0 || safeRoi.Height <= 0)
                {
                    continue;
                }

                var color = GetRoiColor(roi);
                var count = CountLedPixels(frame, safeRoi, color);
                redPixelCount += count;
                detections.Add(new LedRoiDetection(safeRoi, count, count >= PixelThreshold));
            }

            return detections;
        }

        public IEnumerable<Rect> GetRois()
        {
            if (UseMultipleRois)
            {
                yield return Roi1;
                yield return Roi2;
                yield return Roi3;
                yield break;
            }

            yield return Roi;
        }

        private LedColor GetRoiColor(Rect roi)
        {
            if (!UseMultipleRois)
            {
                return LedColor.All;
            }

            if (roi == Roi1)
            {
                return LedColor.Cyan;
            }

            if (roi == Roi2)
            {
                return LedColor.Yellow;
            }

            if (roi == Roi3)
            {
                return LedColor.Red;
            }

            return LedColor.All;
        }

        private int CountLedPixels(Mat frame, Rect roi, LedColor color)
        {
            using (var roiMat = new Mat(frame, roi))
            using (var hsv = new Mat())
            using (var mask1 = new Mat())
            using (var mask2 = new Mat())
            using (var redMask = new Mat())
            using (var greenMask = new Mat())
            using (var yellowMask = new Mat())
            using (var cyanMask = new Mat())
            using (var mask = new Mat())
            {
                Cv2.CvtColor(roiMat, hsv, ColorConversionCodes.BGR2HSV);
                Cv2.InRange(hsv, _lowerRed1, _upperRed1, mask1);
                Cv2.InRange(hsv, _lowerRed2, _upperRed2, mask2);
                Cv2.BitwiseOr(mask1, mask2, redMask);
                Cv2.InRange(hsv, _lowerGreen, _upperGreen, greenMask);
                Cv2.InRange(hsv, _lowerYellow, _upperYellow, yellowMask);
                Cv2.InRange(hsv, _lowerCyan, _upperCyan, cyanMask);
                if (color == LedColor.Red)
                {
                    return Cv2.CountNonZero(redMask);
                }

                if (color == LedColor.Yellow)
                {
                    return Cv2.CountNonZero(yellowMask);
                }

                if (color == LedColor.Cyan)
                {
                    return Cv2.CountNonZero(cyanMask);
                }

                Cv2.BitwiseOr(redMask, greenMask, mask);
                Cv2.BitwiseOr(mask, yellowMask, mask);
                Cv2.BitwiseOr(mask, cyanMask, mask);
                return Cv2.CountNonZero(mask);
            }
        }

        private static Rect ClampRoi(int frameWidth, int frameHeight, Rect roi)
        {
            var x = roi.X < 0 ? 0 : roi.X;
            var y = roi.Y < 0 ? 0 : roi.Y;
            var w = roi.Width;
            var h = roi.Height;

            if (x + w > frameWidth)
            {
                w = frameWidth - x;
            }

            if (y + h > frameHeight)
            {
                h = frameHeight - y;
            }

            return new Rect(x, y, w, h);
        }
    }
}
