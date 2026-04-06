using System;
using System.IO.Ports;

namespace ConsoleApp6
{
    internal sealed class SerialService : IDisposable
    {
        private SerialPort _serialPort;

        public string PortName => _serialPort?.PortName ?? string.Empty;

        public event EventHandler<string> DataReceived;
        public event EventHandler<byte[]> DataReceivedBytes;
        public event EventHandler<string> Error;
        public event EventHandler<bool> ConnectionChanged;

        public bool IsConnected
        {
            get
            {
                return _serialPort != null && _serialPort.IsOpen;
            }
        }

        public string[] GetPortNames()
        {
            return SerialPort.GetPortNames();
        }

        public bool Connect(string portName, int baudRate)
        {
            try
            {
                Disconnect();
                _serialPort = new SerialPort(portName, baudRate);
                _serialPort.DataReceived += OnSerialDataReceived;
                _serialPort.Open();
                OnConnectionChanged(true);
                return true;
            }
            catch (Exception ex)
            {
                OnError("COM connect failed: " + ex.Message);
                return false;
            }
        }

        public void Disconnect()
        {
            if (_serialPort == null)
            {
                return;
            }

            try
            {
                _serialPort.DataReceived -= OnSerialDataReceived;
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort.Dispose();
            }
            catch (Exception ex)
            {
                OnError("COM disconnect failed: " + ex.Message);
            }
            finally
            {
                _serialPort = null;
                OnConnectionChanged(false);
            }
        }

        public void Send(string message)
        {
            try
            {
                if (!IsConnected)
                {
                    return;
                }

                _serialPort.WriteLine(message);
            }
            catch (Exception ex)
            {
                OnError("COM send failed: " + ex.Message);
            }
        }

        public void SendByte(byte value)
        {
            try
            {
                if (!IsConnected)
                {
                    return;
                }

                var buffer = new[] { value };
                _serialPort.Write(buffer, 0, 1);
            }
            catch (Exception ex)
            {
                OnError("COM send byte failed: " + ex.Message);
            }
        }

        public void SendBytes(byte[] values)
        {
            try
            {
                if (!IsConnected || values == null || values.Length == 0)
                {
                    return;
                }

                _serialPort.Write(values, 0, values.Length);
            }
            catch (Exception ex)
            {
                OnError("COM send bytes failed: " + ex.Message);
            }
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (!IsConnected)
                {
                    return;
                }

                var count = _serialPort.BytesToRead;
                if (count <= 0)
                {
                    return;
                }

                var rawBuffer = new byte[count];
                var read = _serialPort.Read(rawBuffer, 0, count);
                if (read <= 0)
                {
                    return;
                }

                var data = rawBuffer;
                if (read != count)
                {
                    data = new byte[read];
                    Array.Copy(rawBuffer, data, read);
                }

                OnDataReceivedBytes(data);

                var raw = _serialPort.Encoding.GetString(data);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    OnDataReceived(raw.Trim());
                }
            }
            catch (Exception ex)
            {
                OnError("COM receive failed: " + ex.Message);
            }
        }

        private void OnDataReceived(string data)
        {
            var handler = DataReceived;
            if (handler != null)
            {
                handler(this, data);
            }
        }

        private void OnDataReceivedBytes(byte[] data)
        {
            var handler = DataReceivedBytes;
            if (handler != null)
            {
                handler(this, data);
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

        private void OnConnectionChanged(bool connected)
        {
            var handler = ConnectionChanged;
            if (handler != null)
            {
                handler(this, connected);
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
