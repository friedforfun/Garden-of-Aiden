using System.Threading;
using System.Linq;
using UnityEngine;
using System.Collections;

public class NamedPipeRunner : MonoBehaviour
{
    Thread server;
    NamedPipe np;
    void Start()
    {
        np = new NamedPipe();
        server = new Thread(np.RunServer);
        server.Start();

        StartCoroutine(TestReadWrite());
    }

    private IEnumerator TestReadWrite() 
    {
        for (;;) 
        {
            yield return new WaitForSeconds(0.5f);
            string str;
            if (NamedPipe.TryReadFromPipe(out str))
            {
                Debug.Log($"READ: {str}");
                str = new string(str.Reverse().ToArray());
                NamedPipe.WriteToPipe(str);
                Debug.Log($"WRITE: {str}");
            }
        }
    }

    private void OnDestroy()
    {
        NamedPipe.CloseConnection();
        server.Join();
    }

    public string output = "";
    public string stack = "";

    void OnEnable()
    {
        Application.logMessageReceivedThreaded += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceivedThreaded -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        output = logString;
        stack = stackTrace;
    }


}
