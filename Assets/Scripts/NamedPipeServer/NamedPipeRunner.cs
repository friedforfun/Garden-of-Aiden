using System.Threading;
using TMPro;
using UnityEngine;

public class NamedPipeRunner : MonoBehaviour
{
    Thread server;
    NamedPipe np;
    void Start()
    {
        np = new NamedPipe();
        server = new Thread(np.run_server);
        server.Start();

    }

    private void OnDestroy()
    {
        np.close_connection();
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
