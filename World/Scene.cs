using System;
using System.Collections.Generic;
using Gears.Graphics;
using Gears.World.Components;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;

namespace Gears.World
{
    public class Scene
    {
        public string Name { get; set; }
        public bool IsLoaded { get; private set; } = false;

        private readonly List<GameObject> _rootObjects = new();
        public IReadOnlyList<GameObject> RootObjects => _rootObjects.AsReadOnly();

        private readonly List<GameObject> _pendingAdd = new();
        private readonly List<GameObject> _pendingRemove = new();

        public double TotalTime { get; private set; } = 0.0;
        public double DeltaTime { get; private set; } = 0.0;

        private double _fixedAccumulator = 0.0;
        private double _fixedProbeAccumulator = 0.0;

        public double FixedTimestep { get; set; } = 1.0 / 50.0;
        public double FixedProbeUpdatedTimestep { get; set; } = 1.0;

        public static GameObject? ActiveCamera { get; set; }

        public static Texture? SkyBox;

        public Scene(string name = "Scene")
        {
            Name = name;
        }

        public virtual void OnLoad()
        {
            IsLoaded = true;
        }

        public virtual void OnUnload()
        {
            IsLoaded = false;

            foreach (var obj in _rootObjects)
            {
                obj.Destroy();
            }

            _rootObjects.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
            SkyBox?.Delete();
        }

        public void Tick(double deltaTime)
        {
            DeltaTime = deltaTime;
            TotalTime += deltaTime;

            FlushPendingChanges();

            _fixedAccumulator += deltaTime;
            _fixedProbeAccumulator += deltaTime;

            while (_fixedProbeAccumulator >= FixedProbeUpdatedTimestep)
            {
                foreach (var obj in _rootObjects)
                {
                    if (obj.HasComponent<EProbe>())
                    {
                        var probe = obj.GetComponent<EProbe>();
                        if (probe != null)
                        {
                            probe.UpdateProbe();
                        }
                    }
                }

                _fixedProbeAccumulator -= FixedProbeUpdatedTimestep;
            }

            while (_fixedAccumulator >= FixedTimestep)
            {
                foreach (var obj in _rootObjects)
                {
                    obj.FixedUpdate();
                }

                _fixedAccumulator -= FixedTimestep;
            }

            foreach (var obj in _rootObjects)
            {
                obj.Update();
            }

            foreach (var obj in _rootObjects)
            {
                obj.LateUpdate();
            }
        }

        public void Render(double deltaTime)
        {
            FlushPendingChanges();

            foreach (var obj in _rootObjects)
            {
                obj.OnRenderFrame(deltaTime);
            }
        }

        public void OnResize(Vector2i newSize)
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnResize(newSize);
            }
        }

        public void OnKeyDown(KeyboardKeyEventArgs e)
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnKeyDown(e);
            }
        }

        public void OnKeyUp(KeyboardKeyEventArgs e)
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnKeyUp(e);
            }
        }

        public void OnMouseDown(MouseButtonEventArgs e)
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnMouseDown(e);
            }
        }

        public void OnMouseUp(MouseButtonEventArgs e)
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnMouseUp(e);
            }
        }

        public void OnMouseMove(MouseMoveEventArgs e)
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnMouseMove(e);
            }
        }

        public void OnMouseWheel(MouseWheelEventArgs e)
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnMouseWheel(e);
            }
        }

        public void OnApplicationPause(bool paused)
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnApplicationPause(paused);
            }
        }

        public void OnApplicationQuit()
        {
            foreach (var obj in _rootObjects)
            {
                obj.OnApplicationQuit();
            }
        }

        public void RemoveGameObject(GameObject obj)
        {
            if (!_pendingRemove.Contains(obj))
            {
                _pendingRemove.Add(obj);
            }
        }

        public void DestroyGameObject(GameObject obj)
        {
            RemoveGameObject(obj);
            obj.Destroy();
        }

        public GameObject? Find(string name)
        {
            foreach (var obj in _rootObjects)
            {
                if (obj.Name == name)
                {
                    return obj;
                }

                var found = obj.FindChild(name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public GameObject? FindWithTag(string tag)
        {
            foreach (var obj in _rootObjects)
            {
                var found = GameObject.FindWithTag(tag, obj);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public List<GameObject> FindAllWithTag(string tag)
        {
            var results = new List<GameObject>();

            foreach (var obj in _rootObjects)
            {
                CollectByTag(tag, obj, results);
            }

            return results;
        }

        public List<T> FindAllComponents<T>() where T : BaseComponent
        {
            var results = new List<T>();

            foreach (var obj in _rootObjects)
            {
                CollectComponents(obj, results);
            }

            return results;
        }

        private void FlushPendingChanges()
        {
            foreach (var obj in _pendingAdd)
            {
                _rootObjects.Add(obj);
            }

            _pendingAdd.Clear();

            foreach (var obj in _pendingRemove)
            {
                _rootObjects.Remove(obj);
            }

            _pendingRemove.Clear();
        }

        private static void CollectByTag(string tag, GameObject obj, List<GameObject> results)
        {
            if (obj.Tag == tag)
            {
                results.Add(obj);
            }

            foreach (var child in obj.Children)
            {
                CollectByTag(tag, child, results);
            }
        }

        private static void CollectComponents<T>(GameObject obj, List<T> results) where T : BaseComponent
        {
            results.AddRange(obj.GetComponents<T>());

            foreach (var child in obj.Children)
            {
                CollectComponents(child, results);
            }
        }

        public void MakeGameObject(GameObject obj)
        {
            if (!_rootObjects.Contains(obj) && !_pendingAdd.Contains(obj))
            {
                _pendingAdd.Add(obj);
            }
        }

        public void DestroyGameObject(string name)
        {
            GameObject? obj = Find(name);

            if (obj == null)
            {
                return;
            }

            if (!_pendingRemove.Contains(obj))
            {
                _pendingRemove.Add(obj);
            }

            obj.Destroy();
        }

        public void ReparentGameObject(string name, string parentName)
        {
            GameObject? obj = Find(name);
            GameObject? parent = Find(parentName);

            if (obj == null || parent == null)
            {
                return;
            }

            obj.SetParent(parent);
        }

        public override string ToString()
        {
            return $"[Scene] Name={Name}, Loaded={IsLoaded}, " + $"RootObjects={_rootObjects.Count}, Time={TotalTime:F2}s";
        }
    }
}