using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StansAssets.SceneManagement
{
    /// <summary>
    /// Provides methods to perform additive scenes loading.
    /// </summary>
    public static class AdditiveScenesLoader
    {
        /// <summary>
        /// Event is fired when scene was unloaded.
        /// </summary>
        public static event Action<Scene> SceneUnloaded = delegate { };

        /// <summary>
        /// Event is fired when new scene was loaded.
        /// </summary>
        public static event Action<Scene, LoadSceneMode> SceneLoaded = delegate { };

        static readonly List<Scene> s_AdditiveScenes = new List<Scene>();
        static readonly Dictionary<string, AsyncOperation> s_LoadSceneOperations = new Dictionary<string, AsyncOperation>();
        static readonly Dictionary<string, List<Action<Scene>>> s_LoadSceneRequests = new Dictionary<string, List<Action<Scene>>>();
        static readonly Dictionary<string, List<Action>> s_UnloadSceneCallbacks = new Dictionary<string, List<Action>>();

        static AdditiveScenesLoader()
        {
             SceneManager.sceneLoaded += AdditiveSceneLoaded;
             SceneManager.sceneUnloaded += SceneUnloadComplete;
        }

        /// <summary>
        /// Load Scene Additively by it's name.
        /// <param name="sceneName">Name of the scene to be loaded.</param>
        /// <param name="loadCompleted">Load Completed callback.</param>
        /// </summary>
        public static AsyncOperation LoadAdditively(string sceneName, Action<Scene> loadCompleted = null)
        {
            if (TryGetLoadedScene(sceneName, out var loadedScene))
            {
                loadCompleted?.Invoke(loadedScene);
                return s_LoadSceneOperations[sceneName];
            }
            if (!s_LoadSceneRequests.ContainsKey(sceneName))
            {
                var callbacks = new List<Action<Scene>>();
                if (loadCompleted != null)
                    callbacks.Add(loadCompleted);


                var loadAsyncOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                s_LoadSceneRequests.Add(sceneName, callbacks);
                s_LoadSceneOperations.Add(sceneName, loadAsyncOperation);
                return loadAsyncOperation;
            }

            if (loadCompleted != null) {
                var callbacks = s_LoadSceneRequests[sceneName] ?? new List<Action<Scene>>();
                callbacks.Add(loadCompleted);
                s_LoadSceneRequests[sceneName] = callbacks;
            }

            return s_LoadSceneOperations[sceneName];
        }

        /// <summary>
        /// Use GetSceneAsyncOperation to retrieve info about scene load progress.
        /// </summary>
        /// <param name="sceneName">Name of the scene.</param>
        /// <returns>Additive scene <see cref="AsyncOperation"/> or `null` if scene load was never requested. </returns>
        public static AsyncOperation GetSceneAsyncOperation(string sceneName)
        {
            return s_LoadSceneOperations.ContainsKey(sceneName)
                ? s_LoadSceneOperations[sceneName]
                : null;
        }

        /// <summary>
        /// Unload scene.
        /// <param name="scene">The scene to be loaded.</param>
        /// <param name="unloadCompleted">Unload Completed callback.</param>
        /// </summary>
        public static void Unload(Scene scene, Action unloadCompleted = null)
        {
            Unload(scene.name, unloadCompleted);
        }

        /// <summary>
        /// Unload scene by name.
        /// <param name="sceneName">Name of the scene to be loaded.</param>
        /// <param name="unloadCompleted">Unload Completed callback.</param>
        /// </summary>
        public static void Unload(string sceneName, Action unloadCompleted = null)
        {
            if (!s_UnloadSceneCallbacks.ContainsKey(sceneName))
            {
                var callbacks = new List<Action>();
                if (unloadCompleted != null)
                    callbacks.Add(unloadCompleted);

                s_UnloadSceneCallbacks.Add(sceneName, callbacks);

                for (var i = 0; i < s_AdditiveScenes.Count; i++)
                {
                    if (s_AdditiveScenes[i].name == sceneName)
                    {
                        s_AdditiveScenes.Remove(s_AdditiveScenes[i]);
                        break;
                    }
                }
                SceneManager.UnloadSceneAsync(sceneName);
            }
            else
            {
                var callbacks = s_UnloadSceneCallbacks[sceneName];
                if (unloadCompleted != null) {
                    if (callbacks == null)
                        callbacks = new List<Action>();
                    callbacks.Add(unloadCompleted);
                }
            }
        }

        /// <summary>
        /// Method to find currently loaded scene by it's name.
        /// </summary>
        /// <param name="sceneName">Scene name.</param>
        /// <param name="scene">Found scene. If methods returns `false` scene value will be set to `default`.</param>
        /// <returns>Returns `true` if scene was found among currently loaded scenes, `false` otherwise.</returns>
        public static bool TryGetLoadedScene(string sceneName, out Scene scene)
        {
            foreach (var additivelyLoadedScene in s_AdditiveScenes)
            {
                if (additivelyLoadedScene.name == sceneName)
                {
                    scene = additivelyLoadedScene;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        static void AdditiveSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            s_AdditiveScenes.Add(scene);
            SceneLoaded.Invoke(scene, mode);

            if (s_LoadSceneRequests.TryGetValue(scene.name, out var callbacks))
            {
                foreach (var callback in callbacks)
                    callback(scene);
                s_LoadSceneRequests.Remove(scene.name);
            }
        }

        static void SceneUnloadComplete(Scene scene)
        {
            SceneUnloaded.Invoke(scene);
            if (s_UnloadSceneCallbacks.TryGetValue(scene.name, out var callbacks))
            {
                foreach (var callback in callbacks)
                    callback();

                s_UnloadSceneCallbacks.Remove(scene.name);
            }

            s_LoadSceneOperations.Remove(scene.name);
        }
    }
}
