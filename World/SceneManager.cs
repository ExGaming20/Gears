using System;
using System.Collections.Generic;

namespace Gears.World
{
    /// <summary>
    /// Registers Scene factories by name and controls which Scene(s) are currently loaded/active.
    /// Mirrors Unity's SceneManager: a single active scene, with optional additive loading of more.
    /// </summary>
    public static class SceneManager
    {
        private static readonly Dictionary<string, Func<Scene>> _registry = new();
        private static readonly List<Scene> _loaded = new();

        public static Scene? ActiveScene { get; private set; }
        public static IReadOnlyList<Scene> LoadedScenes => _loaded.AsReadOnly();

        public static event Action<Scene>? SceneLoaded;
        public static event Action<Scene>? SceneUnloaded;
        public static event Action<Scene?, Scene?>? ActiveSceneChanged;

        // -----------------------------------------------------------------------
        // Registration
        // -----------------------------------------------------------------------

        public static void Register(string name, Func<Scene> factory)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Scene name cannot be empty.", nameof(name));
            _registry[name] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static void Unregister(string name) => _registry.Remove(name);

        public static bool IsRegistered(string name) => _registry.ContainsKey(name);

        // -----------------------------------------------------------------------
        // Loading
        // -----------------------------------------------------------------------

        /// <summary>
        /// Loads the scene registered under <paramref name="name"/> and makes it active.
        /// If <paramref name="additive"/> is false (default), every currently loaded scene is
        /// unloaded first — Unity's "Single" load mode. Pass true to keep existing scenes loaded
        /// alongside the new one.
        /// </summary>
        public static Scene LoadScene(string name, bool additive = false)
        {
            if (!_registry.TryGetValue(name, out var factory))
                throw new InvalidOperationException($"No scene registered under name '{name}'. Call SceneManager.Register first.");

            if (!additive)
                UnloadAll();

            Scene scene = factory();
            scene.OnLoad();
            _loaded.Add(scene);
            SceneLoaded?.Invoke(scene);

            SetActiveScene(scene);

            return scene;
        }

        public static void UnloadScene(Scene scene)
        {
            if (scene == null || !_loaded.Remove(scene)) return;

            scene.OnUnload();
            SceneUnloaded?.Invoke(scene);

            if (ActiveScene == scene)
                SetActiveScene(_loaded.Count > 0 ? _loaded[^1] : null);
        }

        public static void UnloadScene(string name)
        {
            Scene? scene = FindLoadedScene(name);
            if (scene != null) UnloadScene(scene);
        }

        public static void UnloadAll()
        {
            for (int i = _loaded.Count - 1; i >= 0; i--)
                UnloadScene(_loaded[i]);
        }

        // -----------------------------------------------------------------------
        // Active scene
        // -----------------------------------------------------------------------

        public static void SetActiveScene(Scene? scene)
        {
            if (scene != null && !_loaded.Contains(scene))
                throw new InvalidOperationException("Cannot activate a scene that isn't currently loaded.");

            if (ActiveScene == scene) return;

            Scene? previous = ActiveScene;
            ActiveScene = scene;
            ActiveSceneChanged?.Invoke(previous, scene);
        }

        // -----------------------------------------------------------------------
        // Queries
        // -----------------------------------------------------------------------

        public static Scene? FindLoadedScene(string name)
        {
            foreach (var s in _loaded)
                if (s.Name == name) return s;
            return null;
        }

        public static bool IsLoaded(string name) => FindLoadedScene(name) != null;
    }
}