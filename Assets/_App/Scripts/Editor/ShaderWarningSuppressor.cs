using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

/// <summary>
/// Editor script để suppress warning về shader properties không thể thêm vào global property sheet
/// Suppress warnings ở editor time bằng cách filter và remove từ LogEntries
/// </summary>
[InitializeOnLoad]
public class ShaderWarningSuppressor
{
    private const string SUPPRESS_KEY = "SuppressShaderPropertyWarnings";
    private static bool shouldSuppress = true;
    private static Type logEntriesType;
    private static MethodInfo clearMethod;
    private static MethodInfo getEntryInternalMethod;
    private static FieldInfo countField;
    private static PropertyInfo countProperty;
    
    static ShaderWarningSuppressor()
    {
        // Load suppression setting
        shouldSuppress = EditorPrefs.GetBool(SUPPRESS_KEY, true);
        
        if (shouldSuppress)
        {
            InitializeLogEntries();
            // Use logMessageReceived to filter warnings immediately
            Application.logMessageReceived += OnLogMessageReceived;
            // Also periodically clean up console
            EditorApplication.update += CleanupConsole;
        }
    }
    
    private static void InitializeLogEntries()
    {
        try
        {
            var assembly = Assembly.GetAssembly(typeof(SceneView));
            logEntriesType = assembly.GetType("UnityEditor.LogEntries");
            
            if (logEntriesType != null)
            {
                clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
                getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
                
                // Try to get Count as property or field
                countProperty = logEntriesType.GetProperty("Count", BindingFlags.Static | BindingFlags.Public);
                if (countProperty == null)
                {
                    countField = logEntriesType.GetField("Count", BindingFlags.Static | BindingFlags.Public);
                }
            }
        }
        catch
        {
            // Silent fail
        }
    }
    
    private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        if (!shouldSuppress)
            return;
        
        // Check if it's a warning about shader properties
        if ((type == LogType.Warning || type == LogType.Error) &&
            (logString.Contains("Shader properties can't be added to this global property sheet") ||
             logString.Contains("_HBlur")))
        {
            // Clear console immediately to remove this warning
            ClearConsoleImmediate();
        }
    }
    
    private static float lastCleanupTime = 0f;
    private static void CleanupConsole()
    {
        // Clean up console every 0.5 seconds instead of every frame
        if (Time.realtimeSinceStartup - lastCleanupTime < 0.5f)
            return;
        
        lastCleanupTime = Time.realtimeSinceStartup;
        
        if (!shouldSuppress || logEntriesType == null)
            return;
        
        try
        {
            int count = GetLogCount();
            
            if (count > 0)
            {
                // Check last few entries for shader warnings
                for (int i = Mathf.Max(0, count - 10); i < count; i++)
                {
                    if (IsShaderWarning(i))
                    {
                        ClearConsoleImmediate();
                        break;
                    }
                }
            }
        }
        catch
        {
            // Silent fail
        }
    }
    
    private static int GetLogCount()
    {
        try
        {
            if (countProperty != null)
            {
                return (int)countProperty.GetValue(null);
            }
            else if (countField != null)
            {
                return (int)countField.GetValue(null);
            }
        }
        catch { }
        return 0;
    }
    
    private static bool IsShaderWarning(int index)
    {
        try
        {
            if (getEntryInternalMethod == null)
                return false;
            
            var logEntryType = logEntriesType.Assembly.GetType("UnityEditor.LogEntry");
            if (logEntryType == null)
                return false;
            
            var logEntry = Activator.CreateInstance(logEntryType);
            getEntryInternalMethod.Invoke(null, new object[] { index, logEntry });
            
            var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
            var modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public);
            
            if (messageField != null && modeField != null)
            {
                string message = (string)messageField.GetValue(logEntry);
                int mode = (int)modeField.GetValue(logEntry);
                
                return (mode == 1 || mode == 2) && // Warning or Error
                       (message.Contains("Shader properties can't be added to this global property sheet") ||
                        message.Contains("_HBlur"));
            }
        }
        catch { }
        
        return false;
    }
    
    private static void ClearConsoleImmediate()
    {
        try
        {
            if (clearMethod != null)
            {
                clearMethod.Invoke(null, null);
            }
        }
        catch { }
    }
    
    [MenuItem("Tools/Shader Warning Suppressor/Toggle Suppression")]
    private static void ToggleSuppression()
    {
        shouldSuppress = !shouldSuppress;
        EditorPrefs.SetBool(SUPPRESS_KEY, shouldSuppress);
        
        if (shouldSuppress)
        {
            InitializeLogEntries();
            Application.logMessageReceived += OnLogMessageReceived;
            EditorApplication.update += CleanupConsole;
            Debug.Log("Shader warning suppression ENABLED");
        }
        else
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            EditorApplication.update -= CleanupConsole;
            Debug.Log("Shader warning suppression DISABLED");
        }
    }
    
    [MenuItem("Tools/Shader Warning Suppressor/Toggle Suppression", true)]
    private static bool ToggleSuppressionValidate()
    {
        Menu.SetChecked("Tools/Shader Warning Suppressor/Toggle Suppression", shouldSuppress);
        return true;
    }
    
    [MenuItem("Tools/Shader Warning Suppressor/Clear Console")]
    private static void ClearConsole()
    {
        ClearConsoleImmediate();
    }
}

