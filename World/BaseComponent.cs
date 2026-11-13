using System;
using System.Collections.Generic;
using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using Gears.Utilities;
using Gears.World;
using Gears.World.Components;

public class BaseComponent
{
    private bool isEnabled = true;
    private bool isInitialized = false;
    private bool started = false;
    private bool dirty = true;

    public bool IsDirty => dirty;

    // Reference back to the owning GameObject
    public GameObject? GameObject { get; internal set; }

    // Shortcut to the owner's Transform
    public Transform? Transform => GameObject?.Transform;

    // Unity-style enabled property
    public bool enabled
    {
        get => isEnabled;
        set
        {
            if (isEnabled == value) return;
            isEnabled = value;
            if (isEnabled) OnEnable();
            else OnDisable();
        }
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    public virtual void Awake()
    {
        if (isInitialized) return;
        isInitialized = true;
        // Awake does NOT call OnEnable – that is done by the GameObject/component activation
    }

    // Called the first time the component becomes enabled (after Awake)
    public virtual void Start() { }

    public virtual void Update()
    {
        if (!isEnabled) return;
    }

    public virtual void FixedUpdate()
    {
        if (!isEnabled) return;
    }

    public virtual void LateUpdate()
    {
        if (!isEnabled) return;
    }

    // -----------------------------------------------------------------------
    // Enable / Disable
    // -----------------------------------------------------------------------

    public virtual void OnEnable()
    {
        // Ensure Start is called exactly once, the first time we become enabled
        if (!started)
        {
            started = true;
            Start();
        }
    }

    public virtual void OnDisable() { }

    // -----------------------------------------------------------------------
    // Unity-style helpers
    // -----------------------------------------------------------------------

    // Compare the GameObject's tag
    public bool CompareTag(string tag) => GameObject != null && GameObject.Tag == tag;

    // Shortcut to GameObject.GetComponent<T>()
    public T? GetComponent<T>() where T : BaseComponent => GameObject?.GetComponent<T>();

    // Shortcut to GameObject.GetComponentInChildren<T>()
    public T? GetComponentInChildren<T>() where T : BaseComponent => GameObject?.GetComponentInChildren<T>();

    // Shortcut to GameObject.GetComponents<T>()
    public List<T> GetComponents<T>() where T : BaseComponent => GameObject?.GetComponents<T>() ?? new List<T>();

    // Simple SendMessage implementations (reflection based)
    public void SendMessage(string methodName, object? parameter = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
    {
        if (GameObject == null) return;
        foreach (var comp in GameObject.GetComponents<BaseComponent>())
        {
            if (TryInvoke(comp, methodName, parameter)) return;
        }
        if (options == SendMessageOptions.RequireReceiver)
            throw new Exception($"SendMessage failed: No method '{methodName}' found on any component of '{GameObject.Name}'");
    }

    public void SendMessageUpwards(string methodName, object? parameter = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
    {
        var current = GameObject;
        while (current != null)
        {
            foreach (var comp in current.GetComponents<BaseComponent>())
            {
                if (TryInvoke(comp, methodName, parameter)) return;
            }
            current = current.Parent;
        }
        if (options == SendMessageOptions.RequireReceiver)
            throw new Exception($"SendMessageUpwards failed: No method '{methodName}' found in hierarchy of '{GameObject?.Name}'");
    }

    public void BroadcastMessage(string methodName, object? parameter = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
    {
        if (GameObject == null) return;
        BroadcastRecursive(GameObject, methodName, parameter, options);
    }

    private void BroadcastRecursive(GameObject go, string methodName, object? parameter, SendMessageOptions options)
    {
        foreach (var comp in go.GetComponents<BaseComponent>())
            TryInvoke(comp, methodName, parameter);

        foreach (var child in go.Children)
            BroadcastRecursive(child, methodName, parameter, options);
    }

    private bool TryInvoke(BaseComponent target, string methodName, object? parameter)
    {
        var type = target.GetType();
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                    null, parameter == null ? Type.EmptyTypes : new[] { parameter.GetType() }, null);
        if (method != null)
        {
            method.Invoke(target, parameter == null ? null : new[] { parameter });
            return true;
        }
        return false;
    }

    // Debug print
    protected static void Print(object message) => Console.WriteLine(message);

    // -----------------------------------------------------------------------
    // Rendering callbacks (OpenTK-specific)
    // -----------------------------------------------------------------------

    public virtual void OnRenderFrame(double deltaTime) { }
    public virtual void OnResize(Vector2i newSize) { }

    // -----------------------------------------------------------------------
    // Input callbacks
    // -----------------------------------------------------------------------

    public virtual void OnKeyDown(KeyboardKeyEventArgs e) { }
    public virtual void OnKeyUp(KeyboardKeyEventArgs e) { }
    public virtual void OnMouseDown(MouseButtonEventArgs e) { }
    public virtual void OnMouseUp(MouseButtonEventArgs e) { }
    public virtual void OnMouseMove(MouseMoveEventArgs e) { }
    public virtual void OnMouseWheel(MouseWheelEventArgs e) { }

    // -----------------------------------------------------------------------
    // Collision / Trigger stubs
    // -----------------------------------------------------------------------

    public virtual void OnCollisionEnter(object collision) { }
    public virtual void OnCollisionStay(object collision) { }
    public virtual void OnCollisionExit(object collision) { }
    public virtual void OnTriggerEnter(object other) { }
    public virtual void OnTriggerStay(object other) { }
    public virtual void OnTriggerExit(object other) { }

    // -----------------------------------------------------------------------
    // Application events
    // -----------------------------------------------------------------------

    public virtual void OnApplicationPause(bool paused) { }
    public virtual void OnApplicationQuit() { }

    // -----------------------------------------------------------------------
    // Destruction
    // -----------------------------------------------------------------------

    public virtual void OnDestroy() { }

    public void Destroy()
    {
        if (isEnabled) OnDisable();
        OnDestroy();
    }

    // -----------------------------------------------------------------------
    // Utility
    // -----------------------------------------------------------------------

    public override string ToString() => $"[{GetType().Name}] Enabled={isEnabled}, Owner={GameObject?.Name ?? "none"}";
}

// Helper enum for SendMessage options (Unity-compatible)
public enum SendMessageOptions
{
    RequireReceiver,
    DontRequireReceiver
}