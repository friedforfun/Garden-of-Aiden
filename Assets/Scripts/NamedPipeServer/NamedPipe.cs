using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;


public static class UnityPipe
{
    private static readonly string PipeName = "UnityPipe";

    private static readonly string CloseConn = "--Server-Shutting-Down--";

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

    private static int _closeConnectionBackingValue = 0;

    private static bool CloseConnection
    {
        get { return (Interlocked.CompareExchange(ref _closeConnectionBackingValue, 1, 1) == 1); }
        set
        {
            if (value) Interlocked.CompareExchange(ref _closeConnectionBackingValue, 1, 0);
            else Interlocked.CompareExchange(ref _closeConnectionBackingValue, 0, 1);
        }
    }

    private static Thread ServerThread;

    public static void RunServer()
    {
        NPServer server = new NPServer();
        ServerThread = new Thread(server.Run);
        ServerThread.Start();
    }

    public static void StopServer()
    {
        // Send signal to clients to close connection

        ServerThread.Join();
    }

    private class NPServer : IDisposable
    {
        private bool disposedValue;
        private NamedPipeServerStream server;
        private BinaryReader br;
        private BinaryWriter bw;

        public NPServer()
        {
            server = new NamedPipeServerStream(PipeName);
        }

        // outer loop keeping server alive

        // inner loop for each connection

        // write queue thread | read queue thread

        public void Run()
        {
            using (server)
            {
                while (!CloseServer)
                {

                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    br.Dispose();
                    bw.Dispose();
                    server.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~NPServer()
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

    private class PipeReadQueue: ConcurrentQueue<string>
    {

    }

    private class PipeWriteQueue: ConcurrentQueue<string>
    {

    }
}

public class NamedPipe
{
    private static readonly string PipeName = "UnityPipe";
    private static readonly string CloseSignal = "Close-Connection";

    private static readonly Int32 ReadTimeout = 5000; // Read timeout duration

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

    private static int _closeConnectionBackingValue = 0;

    private static bool CloseConnection
    {
        get { return (Interlocked.CompareExchange(ref _closeConnectionBackingValue, 1, 1) == 1); }
        set
        {
            if (value) Interlocked.CompareExchange(ref _closeConnectionBackingValue, 1, 0);
            else Interlocked.CompareExchange(ref _closeConnectionBackingValue, 0, 1);
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
    /// Queue of strings read from the pipe, !! needs to prevent writing when close connection is true !!
    /// </summary>
    private static ConcurrentQueue<string> ReadQueue = new ConcurrentQueue<string>();

    public static bool TryReadFromPipe(out string str) {
        return ReadQueue.TryDequeue(out str);
    }

    public void RunServer()
    {
        using (NamedPipeServerStream server = new NamedPipeServerStream(PipeName))
        {
            server.ReadTimeout = ReadTimeout;
            while(!CloseServer) 
            {
                try 
                {                
                    // got connection, begin protocol
                    // listen for --connected--
                    using (ServerStream ss = new ServerStream(server)) 
                    { // Server stream blocks thread until got a connection
                        string str;
                        if (ss.TryReadFromPipe(out str))
                        {
                            if (!str.Equals("--connected--"))
                                throw new MessageProtocolException(str);
                        } 
                        else
                        {
                            throw new MessageProtocolException("No connected message recieved.");
                        }

                        // Connection opened sucessfully

                        var reader = new NamedPipeReadThread(2, ss);
                        var writer = new NamedPipeWriteThread(2, ss);

                        var readThread = new Thread(reader.Run);
                        var writeThread = new Thread(writer.Run);

                        readThread.Start();
                        writeThread.Start();

                        readThread.Join();
                        writeThread.Join();


                    } // Blocks here until everything has been read from pipe



/*
                        bool streaming = true;
                        while (streaming) 
                        {
                            // check if client will close stream
                            if (ss.TryReadFromPipe(out str)) 
                            {
                                if (str.Equals("--end-of-stream--")) 
                                {
                                    streaming = false;
                                }
                                else 
                                {

                                }
                            }
                        }
                    */
                }
                catch (ObjectDisposedException e) 
                {
                    ShutdownServer();
                }
                catch (MessageProtocolException e) 
                {
                    Debug.Log($"Message protocol violated on message: {e.Message}");
                    Disconnect();
                }
                


            }
        }
        

        
    }

    public static void ShutdownServer() 
    {
        Debug.Log("Closing server");
        CloseServer = true;
    }

    public static void Disconnect()
    {
        Debug.Log("Closing connection");
        CloseConnection = true;

    }


   

    private class NamedPipeWriteThread 
    {
        private int NumberOfThreads;
        private ServerStream serverStream;
        public NamedPipeWriteThread( int numThreads, ServerStream ss)
        {
            NumberOfThreads = numThreads;
            serverStream = ss;
        }

        public void Run()
        {
            while (!CloseConnection)
            {
                try
                {
                    string res;
                    if (WriteQueue.TryDequeue(out res))
                    {
                        serverStream.TryWriteToPipe(res);
                    }
                }
                catch (Exception ex) when (ex is ObjectDisposedException)
                {
                    Disconnect();
                    break;
                }
            }
            
        }
    }

    private class NamedPipeReadThread
    {
        private int NumberOfThreads;
        private ServerStream serverStream;
        public NamedPipeReadThread(int numThreads, ServerStream ss)
        {
            NumberOfThreads = numThreads;
            serverStream = ss;
        }
       

        public void Run()
        {
            while (!CloseConnection)
            {
                try
                {
                    string str;
                    if (serverStream.TryReadFromPipe(out str))
                    {
                        if (!str.Equals("--end-of-stream--")) 
                        {
                            ReadQueue.Enqueue(str);
                        }
                        else 
                        {
                            Disconnect();
                        }
                    }

                    Debug.Log($"Attempted READ");
                }
                catch (InvalidOperationException ex)
                {
                    Debug.Log($"Got read timeout exception");
                    Disconnect();
                }
                catch (Exception ex) when (ex is ObjectDisposedException)
                {
                    Disconnect();
                    
                }
            }
        }
    }

    private class ServerStream : IDisposable
    {
        private BinaryReader br;
        private BinaryWriter bw;

        private NamedPipeServerStream server;
        private bool disposedValue;

        public ServerStream(NamedPipeServerStream server)
        {
            var encoding = Encoding.ASCII;
            br = new BinaryReader(server, encoding);
            bw = new BinaryWriter(server, encoding);
            this.server = server;

            //Debug.Log("Waiting for connection...");
            this.server.WaitForConnection();

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
        public bool TryWriteToPipe(string str)
        {
            var buf = Encoding.ASCII.GetBytes(str);
            if (!CloseServer)
            { //!! Potential race condition here
                bw.Write((uint)buf.Length);
                bw.Write(buf);
                bw.Flush();
                return true;
            } else 
            {
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    br.Dispose();
                    bw.Dispose();
                    this.server.Disconnect();
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


[Serializable]
class MessageProtocolException: Exception
{
    public MessageProtocolException() 
    {

    }

    public MessageProtocolException(string message): base(String.Format("Named pipe protocol violation: {0}", message)) 
    {

    }
}