using AggroBird.UnityExtend.Editor;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GUID = AggroBird.UnityExtend.GUID;
using UnityObject = UnityEngine.Object;

namespace AggroBird.SceneObjects.Editor
{
    [CustomPropertyDrawer(typeof(SceneObjectGUIDAttribute))]
    internal sealed class SceneObjectGUIDAttributeDrawer : PropertyDrawer
    {
        private static readonly GUIContent content = new("GUID");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledGroupScope(true))
            {
                position = EditorGUI.PrefixLabel(position, content);
                if (property.hasMultipleDifferentValues)
                {
                    EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                    EditorGUI.TextField(position, string.Empty);
                }
                else
                {
                    ulong upper = property.FindPropertyRelative((GUID def) => def.Upper).ulongValue;
                    ulong lower = property.FindPropertyRelative((GUID def) => def.Lower).ulongValue;
                    EditorGUI.TextField(position, $"{upper:x16}{lower:x16}");
                }
            }
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }
    }

    [CustomPropertyDrawer(typeof(SceneObjectIDAttribute))]
    internal sealed class SceneObjectIDAttributeDrawer : PropertyDrawer
    {
        private static readonly GUIContent content = new("Object ID");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledGroupScope(true))
            {
                position = EditorGUI.PrefixLabel(position, content);
                if (property.hasMultipleDifferentValues)
                {
                    EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                    EditorGUI.TextField(position, string.Empty);
                }
                else
                {
                    EditorGUI.TextField(position, property.ulongValue.ToString());
                }
            }
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }
    }

    [InitializeOnLoad]
    internal static class SceneObjectEditorUtilityInternal
    {
        static SceneObjectEditorUtilityInternal()
        {
            SceneObjectEditorUtility.tryResolveSceneObjectReferenceInternal += TryResolveSceneObjectReferenceInternal;
        }

        internal static class SceneObjectReferenceCache
        {
            private static readonly Dictionary<EditorSceneObjectReference, SceneObject> cache = new();

            public static bool TryGetSceneObject(EditorSceneObjectReference key, out SceneObject obj)
            {
                return cache.TryGetValue(key, out obj) && obj;
            }
            public static void StoreSceneObject(EditorSceneObjectReference key, SceneObject obj)
            {
                cache[key] = obj;
            }
        }

        private static bool TryLoadPrefabAsset(GUID guid, Type type, out SceneObject prefab)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid.ToString());
            if (!string.IsNullOrEmpty(path))
            {
                prefab = AssetDatabase.LoadAssetAtPath(path, type) as SceneObject;
                return prefab;
            }
            prefab = null;
            return false;
        }

        private static bool TryParseGlobalObjectId(GUID guid, ulong prefabId, ulong objectId, out GlobalObjectId globalObjectId)
        {
            return prefabId != 0 ?
                GlobalObjectId.TryParse($"GlobalObjectId_V1-2-{guid}-{prefabId}-{objectId}", out globalObjectId) :
                GlobalObjectId.TryParse($"GlobalObjectId_V1-2-{guid}-{objectId}-0", out globalObjectId);
        }

        public static bool TryFindPrefabAssetFromCache(GUID guid, ulong prefabId, ulong objectId, Type referenceType, out SceneObject prefabObject)
        {
            EditorSceneObjectReference key = new(guid, prefabId, objectId);
            if (!SceneObjectReferenceCache.TryGetSceneObject(key, out prefabObject))
            {
                if (TryLoadPrefabAsset(guid, referenceType, out prefabObject))
                {
                    SceneObjectReferenceCache.StoreSceneObject(key, prefabObject);
                }
            }
            return prefabObject;
        }
        public static bool TryFindSceneObjectFromCache(GUID guid, ulong prefabId, ulong objectId, out SceneObject sceneObject)
        {
            EditorSceneObjectReference key = new(guid, prefabId, objectId);
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
                else if (TryParseGlobalObjectId(guid, prefabId, objectId, out GlobalObjectId globalObjectId))
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

        private static (bool found, SceneObject obj) TryResolveSceneObjectReferenceInternal(GUID guid, ulong prefabId, ulong objectId, Type referenceType)
        {
            if (guid != GUID.zero)
            {
                if (prefabId != 0 && objectId == 0)
                {
                    // Any of prefab
                    if (TryFindPrefabAssetFromCache(guid, prefabId, objectId, referenceType, out SceneObject prefabObject))
                    {
                        return (true, prefabObject);
                    }
                }
                else if (objectId != 0)
                {
                    // Scene object
                    string scenePath = AssetDatabase.GUIDToAssetPath(guid.ToString());
                    if (!string.IsNullOrEmpty(scenePath))
                    {
                        if (IsSceneOpen(scenePath, out bool isLoaded) && isLoaded)
                        {
                            if (TryFindSceneObjectFromCache(guid, prefabId, objectId, out SceneObject sceneObject))
                            {
                                return (true, sceneObject);
                            }
                        }
                        else
                        {
                            // Different scene
                            return (true, null);
                        }
                    }
                }
            }

            return (false, null);
        }
    }

    public static class SceneObjectPropertyUtility
    {
        internal static void GetSceneObjectReferenceValues(SerializedProperty property, out GUID guid, out ulong prefabId, out ulong objectId)
        {
            var guidProperty = property.FindPropertyRelative("guid");
            ulong upper = guidProperty.FindPropertyRelative((GUID def) => def.Upper).ulongValue;
            ulong lower = guidProperty.FindPropertyRelative((GUID def) => def.Lower).ulongValue;
            guid = new(upper, lower);
            prefabId = property.FindPropertyRelative("prefabId").ulongValue;
            objectId = property.FindPropertyRelative("objectId").ulongValue;
        }
        internal static void SetSceneObjectReferenceValues(SerializedProperty property, GUID guid, ulong prefabId, ulong objectId)
        {
            var guidProperty = property.FindPropertyRelative("guid");
            guidProperty.FindPropertyRelative((GUID def) => def.Upper).ulongValue = guid.Upper;
            guidProperty.FindPropertyRelative((GUID def) => def.Lower).ulongValue = guid.Lower;
            property.FindPropertyRelative("prefabId").ulongValue = prefabId;
            property.FindPropertyRelative("objectId").ulongValue = objectId;
        }

        public static SceneObjectReference<SceneObject> GetSceneObjectReferenceValue(this SerializedProperty property)
        {
            try
            {
                GetSceneObjectReferenceValues(property, out GUID guid, out ulong prefabId, out ulong objectId);
                return new SceneObjectReference<SceneObject>(guid, prefabId, objectId);
            }
            catch
            {
                Debug.LogError("type is not a scene object reference value");
                return default;
            }
        }
    }

    [CustomPropertyDrawer(typeof(SceneObjectReference<>))]
    internal sealed class SceneObjectReferencePropertyDrawer : PropertyDrawer
    {
        private static GUIStyle buttonStyle;

        private static bool sceneIconLoaded = false;
        private static Texture sceneIconTexture;
        private static Texture SceneIconTexture
        {
            get
            {
                if (!sceneIconLoaded)
                {
                    sceneIconLoaded = true;
                    sceneIconTexture = EditorGUIUtility.IconContent("d_SceneAsset Icon").image;
                }
                return sceneIconTexture;
            }
        }
        private static bool prefabIconLoaded = false;
        private static Texture prefabIconTexture;
        private static Texture PrefabIconTexture
        {
            get
            {
                if (!prefabIconLoaded)
                {
                    prefabIconLoaded = true;
                    prefabIconTexture = EditorGUIUtility.IconContent("d_Prefab Icon").image;
                }
                return prefabIconTexture;
            }
        }

        // Hack to override the content of the mixed value content
        // Used to display custom information within an object field
        // Would be cashmoney if exposed in Unity but seems all internal
        private readonly ref struct CustomObjectFieldContentScope
        {
            private static readonly FieldInfo mixedValueContentFieldInfo = typeof(EditorGUI).GetField("s_MixedValueContent", BindingFlags.Static | BindingFlags.NonPublic);

            private readonly GUIContent contentReference;
            private readonly string originalText;
            private readonly string originalTooltip;
            private readonly bool currentMixedValueState;

            public CustomObjectFieldContentScope(string text, string tooltip)
            {
                contentReference = mixedValueContentFieldInfo.GetValue(null) as GUIContent;
                if (contentReference != null)
                {
                    originalText = contentReference.text;
                    originalTooltip = contentReference.tooltip;
                    contentReference.text = text;
                    contentReference.tooltip = tooltip;
                }
                else
                {
                    originalText = originalTooltip = default;
                }

                currentMixedValueState = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = true;
            }

            public void Dispose()
            {
                if (contentReference != null)
                {
                    contentReference.text = originalText;
                    contentReference.tooltip = originalTooltip;
                }
                EditorGUI.showMixedValue = currentMixedValueState;
            }
        }


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, label);

            if (!property.hasMultipleDifferentValues)
            {
                SceneObjectPropertyUtility.GetSceneObjectReferenceValues(property, out GUID guid, out ulong prefabId, out ulong objectId);

                Type referenceType = (fieldInfo.FieldType.IsArray ? fieldInfo.FieldType.GetElementType() : fieldInfo.FieldType).GetGenericArguments()[0];

                void DrawPropertyField()
                {
                    if (guid == GUID.zero && prefabId == 0 && objectId == 0)
                    {
                        // No object
                        PrefixButton(position, property, null, false, null, referenceType);
                        return;
                    }
                    else if (guid != GUID.zero && prefabId != 0 && objectId == 0)
                    {
                        // Any of prefab
                        if (SceneObjectEditorUtilityInternal.TryFindPrefabAssetFromCache(guid, prefabId, objectId, referenceType, out SceneObject prefabObject))
                        {
                            if (PrefixButton(position, property, PrefabIconTexture, true, prefabObject, referenceType))
                            {
                                AssetDatabase.OpenAsset(prefabObject);
                            }
                        }
                        else
                        {
                            PrefixButton(position, property, PrefabIconTexture, false, EditorExtendUtility.MissingObject, referenceType);
                        }
                        return;
                    }
                    else if (guid != GUID.zero && objectId != 0)
                    {
                        // Scene object
                        string scenePath = AssetDatabase.GUIDToAssetPath(guid.ToString());
                        if (!string.IsNullOrEmpty(scenePath))
                        {
                            bool isSceneOpen = SceneObjectEditorUtilityInternal.IsSceneOpen(scenePath, out bool isLoaded);
                            if (isSceneOpen && isLoaded)
                            {
                                if (SceneObjectEditorUtilityInternal.TryFindSceneObjectFromCache(guid, prefabId, objectId, out SceneObject sceneObject))
                                {
                                    var sceneObjectType = sceneObject.GetType();
                                    if (sceneObjectType.Equals(referenceType) || sceneObjectType.IsSubclassOf(referenceType))
                                    {
                                        PrefixButton(position, property, SceneIconTexture, false, sceneObject, referenceType);
                                    }
                                    else
                                    {
                                        // Type mismatch
                                        using (new CustomObjectFieldContentScope("Type mismatch", null))
                                        {
                                            PrefixButton(position, property, SceneIconTexture, false, null, referenceType);
                                        }
                                    }
                                }
                                else
                                {
                                    // Missing object
                                    PrefixButton(position, property, SceneIconTexture, false, EditorExtendUtility.MissingObject, referenceType);
                                }
                            }
                            else
                            {
                                // Different scene
                                using (new CustomObjectFieldContentScope("Scene reference", null))
                                {
                                    if (PrefixButton(position, property, SceneIconTexture, true, null, referenceType))
                                    {
                                        EditorSceneManager.OpenScene(scenePath, isSceneOpen ? OpenSceneMode.Additive : OpenSceneMode.Single);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Missing scene
                            using (new CustomObjectFieldContentScope("Missing Scene", null))
                            {
                                PrefixButton(position, property, SceneIconTexture, false, null, referenceType);
                            }
                        }
                    }
                    else
                    {
                        // Invalid object reference
                        PrefixButton(position, property, null, false, EditorExtendUtility.MissingObject, referenceType);
                    }
                }

                DrawPropertyField();
            }
            else
            {
                EditorGUI.showMixedValue = true;
                EditorGUI.ObjectField(position, null, typeof(UnityObject), true);
            }

            EditorGUI.EndProperty();
        }

        private bool PrefixButton(Rect position, SerializedProperty property, Texture content, bool clickable, UnityObject showValue, Type referenceType)
        {
            buttonStyle ??= new GUIStyle(GUI.skin.button) { padding = new RectOffset(1, 1, 1, 1) };
            Rect buttonRect = position;
            buttonRect.width = 18;
            position.x += 20;
            position.width -= 20;
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            ObjectField(position, property, showValue, referenceType);
            EditorGUI.indentLevel = indent;
            bool guiEnabled = GUI.enabled;
            GUI.enabled = clickable;
            bool result = GUI.Button(buttonRect, content, buttonStyle);
            GUI.enabled = guiEnabled;
            return result;
        }

        private void ObjectField(Rect position, SerializedProperty property, UnityObject showValue, Type referenceType)
        {
            SceneObjectConstraintAttribute constraint = fieldInfo.GetCustomAttribute<SceneObjectConstraintAttribute>();
            SceneObjectFilter filter = constraint == null ? SceneObjectFilter.AllObjects : constraint.filter;

            EditorGUI.BeginChangeCheck();
            UnityObject newObj = EditorGUI.ObjectField(position, showValue, referenceType, filter != SceneObjectFilter.OnlyPrefabs);
            if (EditorGUI.EndChangeCheck())
            {
                if (newObj)
                {
                    GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(newObj);
                    if (globalObjectId.assetGUID == default)
                    {
                        Debug.LogError($"Object '{newObj}' has no GUID, possibly because it is within a scene that has not been saved yet.");
                        return;
                    }

                    if (globalObjectId.identifierType == 1)
                    {
                        if (filter == SceneObjectFilter.OnlySceneObjects)
                        {
                            Debug.LogError($"Field '{fieldInfo.Name}' does not accept prefab references");
                            return;
                        }

                        // Assign prefab
                        SceneObjectPropertyUtility.SetSceneObjectReferenceValues(property, new GUID(globalObjectId.assetGUID.ToString()), globalObjectId.targetObjectId, 0);
                    }
                    else if (globalObjectId.identifierType == 2)
                    {
                        if (filter == SceneObjectFilter.OnlyPrefabs)
                        {
                            Debug.LogError($"Field '{fieldInfo.Name}' does not accept scene object references");
                            return;
                        }

                        if (globalObjectId.targetPrefabId != 0)
                        {
                            // Assigned prefab instance
                            SceneObjectPropertyUtility.SetSceneObjectReferenceValues(property, new GUID(globalObjectId.assetGUID.ToString()), globalObjectId.targetObjectId, globalObjectId.targetPrefabId);
                        }
                        else
                        {
                            // Assigned regular object
                            SceneObjectPropertyUtility.SetSceneObjectReferenceValues(property, new GUID(globalObjectId.assetGUID.ToString()), 0, globalObjectId.targetObjectId);
                        }
                    }
                    else
                    {
                        Debug.LogError($"Object '{newObj}' is not a valid scene object reference.");
                    }
                }
                else
                {
                    // Assigned null
                    SceneObjectPropertyUtility.SetSceneObjectReferenceValues(property, GUID.zero, 0, 0);
                }
            }
        }
    }
}