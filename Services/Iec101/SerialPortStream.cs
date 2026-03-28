using System;
using System.IO;
using System.IO.Ports;

namespace IEC101MasterTester.Services.Iec101
{
    public sealed class SerialPortStream : Stream
    {
        private readonly SerialPort _serialPort;

        public SerialPortStream(SerialPort serialPort)
        {
            _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
        }

        public override bool CanRead => _serialPort.IsOpen;
        public override bool CanSeek => false;
        public override bool CanWrite => _serialPort.IsOpen;
        public override bool CanTimeout => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.BaseStream.Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                return _serialPort.BaseStream.Read(buffer, offset, count);
            }
            catch (TimeoutException)
            {
                return 0;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }

        public override int ReadByte()
        {
            try
            {
                return _serialPort.BaseStream.ReadByte();
            }
            catch (TimeoutException)
            {
                return -1;
            }
            catch (IOException)
            {
                return -1;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _serialPort.BaseStream.Write(buffer, offset, count);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _serialPort.BaseStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            try
            {
                return _serialPort.BaseStream.EndRead(asyncResult);
            }
            catch (TimeoutException)
            {
                return 0;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _serialPort.BaseStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _serialPort.BaseStream.EndWrite(asyncResult);
        }

        public override int ReadTimeout
        {
            get => _serialPort.ReadTimeout;
            set => _serialPort.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => _serialPort.WriteTimeout;
            set => _serialPort.WriteTimeout = value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.DiscardInBuffer();
                        _serialPort.DiscardOutBuffer();
                        _serialPort.Close();
                    }
                }
                catch
                {
                }
                finally
                {
                    _serialPort.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
