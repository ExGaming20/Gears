using System.Collections.Generic;
using Gears;
using Gears.World;
using Gears.World.Components;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

public class GameObject
{
    // -----------------------------------------------------------------------
    // Identity
    // -----------------------------------------------------------------------

    public string Name { get; set; }
    public string Tag { get; set; } = "Untagged";

    /// <summary>
    /// The semantic layer this GameObject belongs to.
    /// Used by Lights (renderingLayers mask) and Cameras (culling mask) to
    /// include or exclude this object.
    /// </summary>
    public Layer Layer { get; set; } = Layer.Default;

    public bool IsStatic { get; set; } = false;

    public bool IsActive { get; private set; } = true;
    public bool IsDestroyed { get; private set; } = false;

    // -----------------------------------------------------------------------
    // Transform (always present)
    // -----------------------------------------------------------------------

    public Transform Transform { get; private set; }

    // -----------------------------------------------------------------------
    // Hierarchy
    // -----------------------------------------------------------------------

    public GameObject? Parent { get; private set; }

    private readonly List<GameObject> _children = new();
    public IReadOnlyList<GameObject> Children => _children.AsReadOnly();

    // -----------------------------------------------------------------------
    // Components
    // -----------------------------------------------------------------------

    private readonly List<BaseComponent> _components = new();

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public GameObject(string name = "GameObject")
    {
        Name = name;
        Transform = new Transform(this);

        _components.Add(Transform);
    }

    // -----------------------------------------------------------------------
    // Activation
    // -----------------------------------------------------------------------

    public void SetActive(bool active)
    {
        if (IsDestroyed || IsActive == active)
        {
            return;
        }

        IsActive = active;

        if (IsActive)
            BroadcastEnable();
        else
            BroadcastDisable();

        foreach (var child in _children)
            child.SetActive(active);
    }

    private void BroadcastEnable()
    {
        foreach (var c in _components)
        {
            if (c.enabled)
                c.OnEnable();
        }
    }

    private void BroadcastDisable()
    {
        foreach (var c in _components)
        {
            if (c.enabled)
                c.OnDisable();
        }
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    public void Update()
    {
        if (!IsActive || IsDestroyed) return;

        foreach (var c in _components) c.Update();
        foreach (var child in _children) child.Update();
    }

    public void FixedUpdate()
    {
        if (!IsActive || IsDestroyed) return;

        foreach (var c in _components) c.FixedUpdate();
        foreach (var child in _children) child.FixedUpdate();
    }

    public void LateUpdate()
    {
        if (!IsActive || IsDestroyed) return;

        foreach (var c in _components) c.LateUpdate();
        foreach (var child in _children) child.LateUpdate();
    }

    // -----------------------------------------------------------------------
    // Rendering & Input
    // -----------------------------------------------------------------------

    public void OnRenderFrame(double deltaTime)
    {
        if (!IsActive || IsDestroyed) return;

        foreach (var c in _components) c.OnRenderFrame(deltaTime);
        foreach (var child in _children) child.OnRenderFrame(deltaTime);
    }

    public void OnResize(Vector2i newSize)
    {
        foreach (var c in _components) c.OnResize(newSize);
        foreach (var child in _children) child.OnResize(newSize);
    }

    public void OnKeyDown(KeyboardKeyEventArgs e)
    {
        if (!IsActive || IsDestroyed) return;
        foreach (var c in _components) c.OnKeyDown(e);
        foreach (var child in _children) child.OnKeyDown(e);
    }

    public void OnKeyUp(KeyboardKeyEventArgs e)
    {
        if (!IsActive || IsDestroyed) return;
        foreach (var c in _components) c.OnKeyUp(e);
        foreach (var child in _children) child.OnKeyUp(e);
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (!IsActive || IsDestroyed) return;
        foreach (var c in _components) c.OnMouseDown(e);
        foreach (var child in _children) child.OnMouseDown(e);
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (!IsActive || IsDestroyed) return;
        foreach (var c in _components) c.OnMouseUp(e);
        foreach (var child in _children) child.OnMouseUp(e);
    }

    public void OnMouseMove(MouseMoveEventArgs e)
    {
        if (!IsActive || IsDestroyed) return;
        foreach (var c in _components) c.OnMouseMove(e);
        foreach (var child in _children) child.OnMouseMove(e);
    }

    public void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (!IsActive || IsDestroyed) return;
        foreach (var c in _components) c.OnMouseWheel(e);
        foreach (var child in _children) child.OnMouseWheel(e);
    }

    // -----------------------------------------------------------------------
    // Collision & Trigger
    // -----------------------------------------------------------------------

    public void OnCollisionEnter(object collision) { foreach (var c in _components) c.OnCollisionEnter(collision); }
    public void OnCollisionStay(object collision) { foreach (var c in _components) c.OnCollisionStay(collision); }
    public void OnCollisionExit(object collision) { foreach (var c in _components) c.OnCollisionExit(collision); }
    public void OnTriggerEnter(object other) { foreach (var c in _components) c.OnTriggerEnter(other); }
    public void OnTriggerStay(object other) { foreach (var c in _components) c.OnTriggerStay(other); }
    public void OnTriggerExit(object other) { foreach (var c in _components) c.OnTriggerExit(other); }

    // -----------------------------------------------------------------------
    // Application events
    // -----------------------------------------------------------------------

    public void OnApplicationPause(bool paused)
    {
        foreach (var c in _components) c.OnApplicationPause(paused);
        foreach (var child in _children) child.OnApplicationPause(paused);
    }

    public void OnApplicationQuit()
    {
        foreach (var c in _components) c.OnApplicationQuit();
        foreach (var child in _children) child.OnApplicationQuit();
    }

    // -----------------------------------------------------------------------
    // Components
    // -----------------------------------------------------------------------

    public T AddComponent<T>() where T : BaseComponent, new()
    {
        var component = new T();
        component.GameObject = this;
        _components.Add(component);
        component.Awake();

        if (IsActive && component.enabled)
            component.OnEnable();

        return component;
    }

    public T? GetComponent<T>() where T : BaseComponent
    {
        foreach (var c in _components)
        {
            if (c is T match)
                return match;
        }
        return null;
    }

    public bool TryGetComponent<T>(out T? result) where T : BaseComponent
    {
        result = GetComponent<T>();
        return result != null;
    }

    public List<T> GetComponents<T>() where T : BaseComponent
    {
        var list = new List<T>();
        foreach (var c in _components)
        {
            if (c is T match)
                list.Add(match);
        }
        return list;
    }

    public T? GetComponentInChildren<T>() where T : BaseComponent
    {
        var found = GetComponent<T>();
        if (found != null) return found;

        foreach (var child in _children)
        {
            found = child.GetComponentInChildren<T>();
            if (found != null) return found;
        }
        return null;
    }

    public bool HasComponent<T>() where T : BaseComponent => GetComponent<T>() != null;

    public bool RemoveComponent<T>() where T : BaseComponent
    {
        var c = GetComponent<T>();
        if (c == null) return false;

        c.Destroy();
        _components.Remove(c);
        return true;
    }

    // -----------------------------------------------------------------------
    // Hierarchy
    // -----------------------------------------------------------------------

    public void SetParent(GameObject? newParent)
    {
        if (IsDestroyed) return;

        Parent?._children.Remove(this);
        Parent = newParent;
        newParent?._children.Add(this);
    }

    public GameObject AddChild(string name = "GameObject")
    {
        var child = new GameObject(name);
        child.SetParent(this);
        return child;
    }

    public GameObject? FindChild(string name)
    {
        foreach (var child in _children)
        {
            if (child.Name == name) return child;
            var found = child.FindChild(name);
            if (found != null) return found;
        }
        return null;
    }

    public void DetachChildren()
    {
        foreach (var child in _children) child.Parent = null;
        _children.Clear();
    }

    public List<GameObject> GetChildrenHierarchy()
    {
        return _children;
    }

    // -----------------------------------------------------------------------
    // Destruction
    // -----------------------------------------------------------------------

    public void Destroy()
    {
        if (IsDestroyed) return;

        IsDestroyed = true;

        for (int i = _children.Count - 1; i >= 0; i--)
        {
            _children[i].Destroy();
        }

        for (int i = _components.Count - 1; i >= 0; i--)
        {
            _components[i].Destroy();
        }

        _children.Clear();
        _components.Clear();

        Parent?._children.Remove(this);
        Parent = null;
    }

    // -----------------------------------------------------------------------
    // Static search helpers
    // -----------------------------------------------------------------------

    public static GameObject? Find(string name, GameObject root)
    {
        if (root.Name == name) return root;
        return root.FindChild(name);
    }

    public static GameObject? Find(string name)
    {
        var activeScene = SceneManager.ActiveScene;
        if (activeScene == null) return null;

        foreach (var obj in activeScene.RootObjects)
        {
            var found = Find(name, obj);
            if (found != null) return found;
        }
        return null;
    }

    public static GameObject? FindWithTag(string tag, GameObject root)
    {
        if (root.Tag == tag) return root;

        foreach (var child in root._children)
        {
            var found = FindWithTag(tag, child);
            if (found != null) return found;
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Utility
    // -----------------------------------------------------------------------

    public override string ToString()
    {
        return $"[GameObject] Name={Name}, Tag={Tag}, Layer={Layer}, Active={IsActive}, " + $"Components={_components.Count}, Children={_children.Count}";
    }
}