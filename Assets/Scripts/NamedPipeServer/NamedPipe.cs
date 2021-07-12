using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;


public class NamedPipe
{
    private static readonly string PipeName = "UnityPipe";
    private static readonly string CloseSignal = "Close-Connection";

    /// <summary>
    /// Initialised pipe server with default pipe name: "UnityPipe"
    /// </summary>
    public NamedPipe()
    {

    }

    private static int _closeServerBackingValue = 0;

    private static bool CloseServer
    {
        get { return (Interlocked.CompareExchange(ref _closeServerBackingValue, 1, 1) == 1); }
        set
        {
            if (value) Interlocked.CompareExchange(ref _closeServerBackingValue, 1, 0);
            else Interlocked.CompareExchange(ref _closeServerBackingValue, 0, 1);
        }
    }

    /// <summary>
    /// Queue of strings to write to the pipe
    /// </summary>
    private static ConcurrentQueue<string> WriteQueue = new ConcurrentQueue<string>();

    public static void WriteToPipe(string str) {
        WriteQueue.Enqueue(str);
    }

    /// <summary>
    /// Queue of strings read from the pipe
    /// </summary>
    private static ConcurrentQueue<string> ReadQueue = new ConcurrentQueue<string>();

    public static bool TryReadFromPipe(out string str) {
        return ReadQueue.TryDequeue(out str);
    }

    public void RunServer()
    {
        // start an in thread
        // start an out thread

        var reader = new NamedPipeThread(true, 2);
        var writer = new NamedPipeThread(false, 2);
        var ReadThread = new Thread(reader.Run);
        var WriteThread = new Thread(writer.Run);

        ReadThread.Start();
        WriteThread.Start();

        while (!CloseServer)
        {
            Thread.Sleep(10);
        }
        if (!WriteThread.Join(15))
        {
            Debug.Log("Aborting WRITE THREAD");
            WriteThread.Abort();
        }
        if (!ReadThread.Join(15))
        {
            Debug.Log("Aborting READ THREAD");
            ReadThread.Abort();
        }

        WriteThread.Join();
        ReadThread.Join();

        
    }


    public static void CloseConnection()
    {
        Debug.Log("Closing connection");
        CloseServer = true;

        using (NamedPipeClientStream client = new NamedPipeClientStream(PipeName))
        {
            using (BinaryWriter bw = new BinaryWriter(client))
            {
                var buf = Encoding.ASCII.GetBytes(CloseSignal);
                bw.Write((uint)buf.Length);
                bw.Write(buf);
                bw.Flush();
            }
        }
    }






    private class NamedPipeThread
    {
        private bool ReaderThread;
        private int NumberOfThreads;

        public NamedPipeThread(bool ReaderThread, int numThreads)
        {
            this.ReaderThread = ReaderThread;
            NumberOfThreads = numThreads;
        }

        public void Run()
        {
            try
            {
                using (var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, NumberOfThreads))
                {
                    if (ReaderThread)
                    {
                        manageReadStream(server);
                    }

                    if (!ReaderThread)
                    {
                        manageWriteStream(server);
                    }
                }
            }
            catch (ThreadInterruptedException e)
            {
                // do any cleanup needed here
            }
            
        }

        private void manageWriteStream(NamedPipeServerStream server)
        {
            using (ServerStream ss = new ServerStream(server))
            {
                while (!CloseServer)
                {
                    try
                    {
                        string res;
                        if (WriteQueue.TryDequeue(out res))
                        {
                            ss.WriteToPipe(res);
                            Debug.Log("Attempted WRITE");
                        }

                        Debug.Log($"outside WRITE");
                    }
                    catch (Exception ex) when (ex is ObjectDisposedException)
                    {
                        CloseConnection();
                        break;
                    }
                }
            }
        }

        private void manageReadStream(NamedPipeServerStream server)
        {
            using (ServerStream ss = new ServerStream(server))
            {
                while (!CloseServer)
                {
                    try
                    {
                        string str;
                        if (ss.TryReadFromPipe(out str))
                        {
                            ReadQueue.Enqueue(str);
                        }

                        Debug.Log($"Attempted READ");
                    }
                    catch (Exception ex) when (ex is ObjectDisposedException)
                    {
                        CloseConnection();
                        break;
                    }
                }
            }
        }

    }

    private class ServerStream : IDisposable
    {
        private BinaryReader br;
        private BinaryWriter bw;
        private bool disposedValue;

        public ServerStream(NamedPipeServerStream server)
        {
            var encoding = Encoding.ASCII;
            br = new BinaryReader(server, encoding);
            bw = new BinaryWriter(server, encoding);

            //Debug.Log("Waiting for connection...");
            server.WaitForConnection();

            //Debug.Log("Connected.");
        }



        /// <summary>
        /// Reads message from input stream, use the length to find how much to read for each message.
        /// Use ASCII encoding.
        /// </summary>
        /// <param name="str">String out parameter</param>
        /// <exception cref="ObjectDisposedException">When the stream is Disposed during a read</exception>
        /// <returns>True when an output is read</returns>
        public bool TryReadFromPipe(out string str)
        {
            str = "";
            try
            {
                var len = (int)br.ReadUInt32();
                str =  new string(br.ReadChars(len));

                if (str.Equals(CloseSignal))
                    return false;

                return true;
            }
            catch (Exception ex) when (
            ex is EndOfStreamException || 
            ex is IOException
            )
            {
                return false;
            }
            // throws ObjectDisposedException exception
        }

        /// <summary>
        /// Writes message into output stream, each message is prefixed by the length of the string that follows.
        /// expects ASCII encoding
        /// </summary>
        /// <param name="str"></param>
        public void WriteToPipe(string str)
        {
            var buf = Encoding.ASCII.GetBytes(str);
            bw.Write((uint)buf.Length);
            bw.Write(buf);
            bw.Flush();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    br.Dispose();
                    bw.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ServerStream()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
