using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace XBeeAudio
{
    public class XBeeStreamWrapper : IRandomAccessStream
    {
        private readonly Stream _stream;
        private readonly IOutputStream _outputStream;
        private readonly IInputStream _inputStream;

        public XBeeStreamWrapper(Stream stream)
        {
            _stream = stream;
            _outputStream = _stream.AsOutputStream();
            _inputStream = _stream.AsInputStream();
        }

        public void Dispose()
        {
            _stream?.Dispose();
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            return _inputStream.ReadAsync(buffer, count, options);
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            Debug.WriteLine(buffer.Length);

            return AsyncInfo.Run<uint, uint>(async (token, progress) =>
            {
                await _outputStream.WriteAsync(buffer);
                progress.Report(buffer.Length);
                return buffer.Length;
            });
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            return AsyncInfo.Run(_ => Task.FromResult(true));
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            throw new NotSupportedException();
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            throw new NotSupportedException();
        }

        public void Seek(ulong position)
        {
        }

        public IRandomAccessStream CloneStream()
        {
            throw new NotSupportedException();
        }

        public bool CanRead => _stream.CanRead;
        public bool CanWrite => _stream.CanWrite;
        public ulong Position => 0;

        public ulong Size
        {
            get { return 0; }
            set {  }
        }
    }
}
