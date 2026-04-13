using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DVPCameraType;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ConsoleApp6
{
    internal sealed class CameraService : IDisposable
    {
        private readonly LedDetector _ledDetector;
        private uint _cameraHandle;
        private bool _cameraOpened;
        private bool _cameraStarted;
        private CancellationTokenSource _cts;
        private Task _captureTask;
        private bool _lastLedOn;

        public event EventHandler<Bitmap> FrameReady;
        public event EventHandler<bool> LedStatusChanged;
        public event EventHandler<IReadOnlyList<LedDetector.LedRoiDetection>> RoiLedStatusUpdated;
        public event EventHandler<string> Error;

        public CameraService(LedDetector ledDetector)
        {
            _ledDetector = ledDetector;
        }

        public bool Start(int cameraIndex)
        {
            try
            {
                Stop();

                uint count = 0;
                var status = DVPCamera.dvpRefresh(ref count);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    OnError("dvpRefresh failed: " + status);
                    return false;
                }

                if (count == 0)
                {
                    OnError("No DVP camera detected.");
                    return false;
                }

                if (cameraIndex < 0 || cameraIndex >= count)
                {
                    OnError("Invalid camera index: " + cameraIndex);
                    return false;
                }

                status = DVPCamera.dvpOpen((uint)cameraIndex, dvpOpenMode.OPEN_NORMAL, ref _cameraHandle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    OnError("dvpOpen failed: " + status);
                    return false;
                }

                _cameraOpened = true;

                status = DVPCamera.dvpStart(_cameraHandle);
                if (status != dvpStatus.DVP_STATUS_OK)
                {
                    OnError("dvpStart failed: " + status);
                    DVPCamera.dvpClose(_cameraHandle);
                    _cameraOpened = false;
                    _cameraHandle = 0;
                    return false;
                }

                _cameraStarted = true;

                _cts = new CancellationTokenSource();
                _captureTask = Task.Run(() => CaptureLoop(_cts.Token), _cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                OnError("Camera start failed: " + ex.Message);
                return false;
            }
        }

        public void Refresh(int cameraIndex)
        {
            Start(cameraIndex);
        }

        public void Stop()
        {
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    if (_captureTask != null)
                    {
                        try
                        {
                            _captureTask.Wait(800);
                        }
                        catch
                        {
                        }
                    }
                    _cts.Dispose();
                    _cts = null;
                }

                if (_cameraStarted)
                {
                    DVPCamera.dvpStop(_cameraHandle);
                    _cameraStarted = false;
                }

                if (_cameraOpened)
                {
                    DVPCamera.dvpClose(_cameraHandle);
                    _cameraOpened = false;
                    _cameraHandle = 0;
                }
            }
            catch (Exception ex)
            {
                OnError("Camera stop failed: " + ex.Message);
            }
        }

        private void CaptureLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_cameraStarted || _cameraHandle == 0)
                    {
                        Thread.Sleep(120);
                        continue;
                    }

                    dvpFrame frame = new dvpFrame();
                    IntPtr frameBuffer = IntPtr.Zero;

                    var status = DVPCamera.dvpGetFrame(_cameraHandle, ref frame, ref frameBuffer, 1000);
                    if (status != dvpStatus.DVP_STATUS_OK)
                    {
                        continue;
                    }

                    using (var mat = Mat.FromPixelData(frame.iHeight, frame.iWidth, MatType.CV_8UC3, frameBuffer))
                    using (var sourceFrame = mat.Clone())
                    {
                        int redCount;
                        var detections = _ledDetector.DetectLeds(sourceFrame, out redCount);
                        var ledOn = false;
                        if (_ledDetector.UseMultipleRois)
                        {
                            var activeCount = 0;
                            var allOn = true;
                            foreach (var detection in detections)
                            {
                                activeCount++;
                                if (!detection.LedOn)
                                {
                                    allOn = false;
                                }
                            }

                            ledOn = activeCount == 3 && allOn;
                        }
                        else
                        {
                            foreach (var detection in detections)
                            {
                                if (detection.LedOn)
                                {
                                    ledOn = true;
                                    break;
                                }
                            }
                        }
                        if (ledOn != _lastLedOn)
                        {
                            _lastLedOn = ledOn;
                            OnLedStatusChanged(ledOn);
                        }

                        OnRoiLedStatusUpdated(detections.ToArray());

                        using (var displayFrame = sourceFrame.Clone())
                        {
                            foreach (var detection in detections)
                            {
                                Cv2.Rectangle(displayFrame, detection.Roi, detection.LedOn ? Scalar.Lime : Scalar.Red, 2);
                            }
                            var bmp = BitmapConverter.ToBitmap(displayFrame);
                            OnFrameReady(bmp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnError("Camera loop error: " + ex.Message);
                    Thread.Sleep(100);
                }
            }
        }

        private void OnFrameReady(Bitmap bitmap)
        {
            var handler = FrameReady;
            if (handler != null)
            {
                handler(this, bitmap);
            }
        }

        private void OnLedStatusChanged(bool ledOn)
        {
            var handler = LedStatusChanged;
            if (handler != null)
            {
                handler(this, ledOn);
            }
        }

        private void OnRoiLedStatusUpdated(IReadOnlyList<LedDetector.LedRoiDetection> detections)
        {
            var handler = RoiLedStatusUpdated;
            if (handler != null)
            {
                handler(this, detections);
            }
        }

        private void OnError(string message)
        {
            var handler = Error;
            if (handler != null)
            {
                handler(this, message);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
