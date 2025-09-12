using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GUID = AggroBird.UnityExtend.GUID;

namespace AggroBird.SceneObjects.Editor
{
    public static class SceneObjectEditorUtility
    {
        static SceneObjectEditorUtility()
        {
            EditorGUI.hyperLinkClicked += EditorGUIHyperLinkClicked;
        }

        private const string SceneObjectReferenceURLKey = "SCENE_OBJECT_REFERENCE";

        private static void EditorGUIHyperLinkClicked(EditorWindow window, HyperLinkClickedEventArgs args)
        {
            if (args.hyperLinkData.TryGetValue(SceneObjectReferenceURLKey, out string value) && value == "true")
            {
                if (args.hyperLinkData.TryGetValue("guid", out string guidStr) && args.hyperLinkData.TryGetValue("objectId", out string objectIdStr) && args.hyperLinkData.TryGetValue("prefabId", out string prefabIdStr))
                {
                    if (GUID.TryParse(guidStr, out GUID guid) && ulong.TryParse(objectIdStr, out ulong objectId) && ulong.TryParse(prefabIdStr, out ulong prefabId))
                    {
                        if (TryResolveSceneObjectReferenceInternal(guid, objectId, prefabId, out SceneObject sceneObject))
                        {
                            GameObject gameObject = sceneObject.gameObject;
                            Selection.activeObject = gameObject;
                            EditorGUIUtility.PingObject(gameObject);
                        }
                    }
                }
            }
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

        internal static bool TryFindRuntimeSceneObject(GUID guid, ulong objectId, ulong prefabId, out SceneObject sceneObject)
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

        internal static bool IsSceneOpen(string scenePath, out bool isLoaded)
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

        internal static ReferenceType GetReferenceType(string assetPath)
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

        internal static bool TryFindSceneObjectInPrefabStage(string assetPath, SceneObjectID targetObjectID, out SceneObject targetObject, out GameObject rootGameObject)
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
        internal static bool TryFindSceneObjectInPrefabAsset(string assetPath, SceneObjectID targetObjectID, out SceneObject targetObject, out GameObject rootGameObject, out bool isPrefabStageObject)
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

        private static bool TryResolveSceneObjectReferenceInternal(GUID guid, ulong objectId, ulong prefabId, out SceneObject sceneObject)
        {
            if (guid != GUID.zero)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid.ToString());
                switch (GetReferenceType(assetPath))
                {
                    case ReferenceType.PrefabReference:
                        SceneObjectID targetObjectID = new(objectId, prefabId);
                        if (TryFindSceneObjectInPrefabAsset(assetPath, targetObjectID, out sceneObject, out _, out _))
                        {
                            return true;
                        }
                        break;
                    case ReferenceType.SceneObjectReference:
                        if (IsSceneOpen(assetPath, out bool isLoaded) && isLoaded)
                        {
                            if (TryFindRuntimeSceneObject(guid, objectId, prefabId, out sceneObject))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            // Different scene
                            sceneObject = null;
                            return true;
                        }
                        break;
                }
            }

            sceneObject = null;
            return false;
        }

        // Utility function for finding references outside of play time
        // If the return value is true, but the result is null, the object is located in another scene
        public static bool TryResolveEditorSceneObjectReference<T>(SceneObjectReference<T> reference, out T result) where T : SceneObject
        {
            if (reference.HasValue())
            {
                if (TryResolveSceneObjectReferenceInternal(reference.guid, reference.objectId, reference.prefabId, out SceneObject sceneObject))
                {
                    if (sceneObject is T casted)
                    {
                        result = casted;
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }
        public static bool TryResolveEditorSceneObjectReference(SceneObjectReference reference, out SceneObject result)
        {
            return TryResolveSceneObjectReferenceInternal(reference.guid, reference.objectId, reference.prefabId, out result);
        }

        // Utility for getting the scene object reference outside of playtime
        // Note that this does not equal the reference during playtime, and can only be used for identification within the editor
        public static SceneObjectReference<T> GetEditorSceneObjectReference<T>(T sceneObject) where T : SceneObject
        {
            return new(sceneObject.internalSceneObjectGuid, sceneObject.internalSceneObjectId);
        }

        // Utility for getting a clickable url for scene objects in the console
        public static string GetSceneObjectReferenceURL(SceneObject sceneObject)
        {
            var reference = GetEditorSceneObjectReference(sceneObject);
            return $"<a {SceneObjectReferenceURLKey}=\"true\" guid=\"{reference.guid}\" objectId=\"{reference.objectId}\" prefabId=\"{reference.prefabId}\">{sceneObject.name}</a>";
        }
    }
}
