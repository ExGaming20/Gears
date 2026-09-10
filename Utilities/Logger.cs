using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

public class Logger
{
    public static readonly Logger Instance = new Logger();

    private static readonly List<_Log> Logs = new List<_Log>(512);
    private const int MaxLogs = 512;

    public void Log(string message, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = "")
    {
        AddLog(new _Log(message, LogType.Info, new _Location(filePath, lineNumber, memberName)));
    }

    public void LogWarning(string message, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = "")
    {
        AddLog(new _Log(message, LogType.Warning, new _Location(filePath, lineNumber, memberName)));
    }

    public void LogError(string message, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string memberName = "")
    {
        var location = new _Location(filePath, lineNumber, memberName);
        AddLog(new _Log(message, LogType.Error, location));
        // throw new Exception($"[{location}] {message}");
    }

    private void AddLog(_Log log)
    {
        lock (Logs)
        {
            if (Logs.Count >= MaxLogs)
            {
                Logs.RemoveAt(0);
            }

            Logs.Add(log);
        }
    }

    public void ClearLogs()
    {
        lock (Logs) Logs.Clear();
    }

public void PrintLogs()
        {
            lock (Logs)
            {
                try
                {
                    Console.Clear();
                }
                catch (IOException)
                {
                    // No console available (e.g., piped output), skip clear
                }

                foreach (var log in Logs)
                {
                    Console.WriteLine($"[{log.Timestamp:HH:mm:ss}] [{log.Type}] {log.Message} (at {log.Location})");
                }
            }
        }
}

public struct _Log
{
    public readonly string Message;
    public readonly _Location Location;
    public readonly DateTime Timestamp;
    public readonly LogType Type;

    public _Log(string message, LogType type, _Location location)
    {
        Message = message;
        Timestamp = DateTime.Now;
        Type = type;
        Location = location;
    }
}

public struct _Location
{
    public readonly string FilePath;
    public readonly int LineNumber;
    private readonly string MemberName;

    public _Location(string filePath, int lineNumber, string memberName)
    {
        FilePath = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
        LineNumber = lineNumber;
        MemberName = memberName;
    }

    public override string ToString() => $"{FilePath}:{LineNumber} {MemberName}";
}

public enum LogType 
{
    Info,
    Warning,
    Error
}