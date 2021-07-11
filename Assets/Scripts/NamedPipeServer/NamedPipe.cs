using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using UnityEngine;

public class NamedPipe
{
    // need to cleanly close the server from the server
    // need to print from server thread
    public NamedPipe()
    {

    }

    private bool CloseServer = false;

    /// <summary>
    /// Queue of strings to write to the pipe
    /// </summary>
    private ConcurrentQueue<string> wq = new ConcurrentQueue<string>();

    /// <summary>
    /// Queue of strings read from the pipe
    /// </summary>
    private ConcurrentQueue<string> rq = new ConcurrentQueue<string>();

    public void run_server()
    {
        using (var server = new NamedPipeServerStream("Test"))
        {

            ServerStream ss = new ServerStream(server);

            while (!CloseServer)
            {
                try
                {
                    var str = ss.read_from_pipe();
                    //rq.Enqueue(str);
                    Debug.Log($"Read: {str}");

                    str = new string(str.Reverse().ToArray());

                    ss.write_to_pipe(str);
                    Debug.Log($"Wrote: {str}");
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }
        }
        

    }

    public void close_connection()
    {
        CloseServer = true;
    }

    private class ServerStream
    {
        private BinaryReader br;
        private BinaryWriter bw;

        public ServerStream(NamedPipeServerStream server)
        {
            br = new BinaryReader(server);
            bw = new BinaryWriter(server);

            Debug.Log("Waiting for connection...");
            server.WaitForConnection();

            Debug.Log("Connected.");
        }

        /// <summary>
        /// Reads message from input stream, use the length to find how much to read for each message.
        /// Use ASCII encoding.
        /// </summary>
        /// <returns></returns>
        public string read_from_pipe()
        {
            var len = (int)br.ReadUInt32();
            return new string(br.ReadChars(len));
        }

        /// <summary>
        /// Writes message into output stream, each message is prefixed by the length of the string that follows.
        /// expects ASCII encoding
        /// </summary>
        /// <param name="str"></param>
        public void write_to_pipe(string str)
        {
            var buf = Encoding.ASCII.GetBytes(str);
            bw.Write((uint)buf.Length);
            bw.Write(buf);
        }

    }
}
