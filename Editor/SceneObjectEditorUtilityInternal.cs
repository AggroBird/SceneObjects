using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GUID = AggroBird.UnityExtend.GUID;

namespace AggroBird.SceneObjects.Editor
{
    [InitializeOnLoad]
    internal static class SceneObjectEditorUtilityInternal
    {
        static SceneObjectEditorUtilityInternal()
        {
            SceneObjectEditorUtility.tryResolveSceneObjectReferenceInternal += TryResolveSceneObjectReferenceInternal;
            EditorSceneManager.activeSceneChangedInEditMode += ClearSceneReferenceCache;
        }

        private static void ClearSceneReferenceCache(Scene arg0, Scene arg1)
        {
            SceneObjectReferenceCache.ClearCache();
        }

        private static class SceneObjectReferenceCache
        {
            private static readonly Dictionary<SceneObjectReference, SceneObject> cache = new();

            public static bool TryGetSceneObject(SceneObjectReference key, out SceneObject obj)
            {
                return cache.TryGetValue(key, out obj) && obj;
            }
            public static void StoreSceneObject(SceneObjectReference key, SceneObject obj)
            {
                cache[key] = obj;
            }
            public static void ClearCache()
            {
                cache.Clear();
            }
        }

        private static bool TryParseGlobalObjectId(GUID guid, ulong objectId, ulong prefabId, out GlobalObjectId globalObjectId)
        {
            return GlobalObjectId.TryParse($"GlobalObjectId_V1-2-{guid}-{objectId}-{prefabId}", out globalObjectId);
        }

        public static bool TryFindRuntimeSceneObject(GUID guid, ulong objectId, ulong prefabId, out SceneObject sceneObject)
        {
            SceneObjectReference key = new(guid, objectId, prefabId);
            if (!SceneObjectReferenceCache.TryGetSceneObject(key, out sceneObject))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (SceneObject.TryFindSceneObject(key, out SceneObject playingSceneObject))
                    {
                        sceneObject = playingSceneObject;
                        SceneObjectReferenceCache.StoreSceneObject(key, sceneObject);
                    }
                }
                else if (TryParseGlobalObjectId(guid, objectId, prefabId, out GlobalObjectId globalObjectId))
                {
                    // TODO: This can cause the editor to slow down when referencing a broken object
                    // If we can find a faster way to find a scene object through an object id, that would be great
                    // But at time of writing, Unity does not give us any solution for this.
                    sceneObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as SceneObject;
                    if (sceneObject)
                    {
                        SceneObjectReferenceCache.StoreSceneObject(key, sceneObject);
                    }
                }
            }
            return sceneObject;
        }

        public static bool IsSceneOpen(string scenePath, out bool isLoaded)
        {
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scenePath == scene.path)
                {
                    isLoaded = scene.isLoaded;
                    return true;
                }
            }
            isLoaded = false;
            return false;
        }

        public static ReferenceType GetReferenceType(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    return ReferenceType.PrefabReference;
                }
                if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    return ReferenceType.SceneObjectReference;
                }
            }
            return ReferenceType.InvalidReference;
        }

        public static bool TryFindSceneObjectInPrefabStage(string assetPath, SceneObjectID targetObjectID, out SceneObject targetObject, out GameObject rootGameObject)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.assetPath == assetPath)
            {
                rootGameObject = prefabStage.prefabContentsRoot;
                foreach (var sceneObject in rootGameObject.GetComponentsInChildren<SceneObject>(true))
                {
                    if (sceneObject.internalSceneObjectId == targetObjectID)
                    {
                        targetObject = sceneObject;
                        return true;
                    }
                }
            }

            targetObject = null;
            rootGameObject = null;
            return false;
        }
        public static bool TryFindSceneObjectInPrefabAsset(string assetPath, SceneObjectID targetObjectID, out SceneObject targetObject, out GameObject rootGameObject, out bool isPrefabStageObject)
        {
            if (TryFindSceneObjectInPrefabStage(assetPath, targetObjectID, out targetObject, out rootGameObject))
            {
                isPrefabStageObject = true;
                return true;
            }
            else
            {
                isPrefabStageObject = false;
            }

            // Load from asset
            rootGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (rootGameObject)
            {
                foreach (var sceneObject in rootGameObject.GetComponentsInChildren<SceneObject>(true))
                {
                    if (sceneObject.internalSceneObjectId == targetObjectID)
                    {
                        targetObject = sceneObject;
                        return true;
                    }
                }
            }

            targetObject = null;
            return false;
        }

        private static (bool found, SceneObject obj) TryResolveSceneObjectReferenceInternal(GUID guid, ulong objectId, ulong prefabId, Type referenceType)
        {
            if (guid != GUID.zero)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid.ToString());
                switch (GetReferenceType(assetPath))
                {
                    case ReferenceType.PrefabReference:
                        SceneObjectID targetObjectID = new(objectId, prefabId);
                        if (TryFindSceneObjectInPrefabAsset(assetPath, targetObjectID, out SceneObject sceneObject, out _, out _))
                        {
                            if (referenceType.IsAssignableFrom(sceneObject.GetType()))
                            {
                                return (true, sceneObject);
                            }
                        }
                        break;
                    case ReferenceType.SceneObjectReference:
                        if (IsSceneOpen(assetPath, out bool isLoaded) && isLoaded)
                        {
                            if (TryFindRuntimeSceneObject(guid, objectId, prefabId, out sceneObject))
                            {
                                if (referenceType.IsAssignableFrom(sceneObject.GetType()))
                                {
                                    return (true, sceneObject);
                                }
                            }
                        }
                        else
                        {
                            // Different scene
                            return (true, null);
                        }
                        break;
                }
            }

            return (false, null);
        }
    }
}