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

    [CustomPropertyDrawer(typeof(SceneObjectID))]
    internal sealed class SceneObjectIDAttributeDrawer : PropertyDrawer
    {
        private static readonly GUIContent objectIdLabel = new("Object ID");
        private static readonly GUIContent prefabIdLabel = new("Prefab ID");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledGroupScope(true))
            {
                static void ShowProperty(Rect position, SerializedProperty property, GUIContent label)
                {
                    position = EditorGUI.PrefixLabel(position, label);
                    EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                    EditorGUI.TextField(position, property.ulongValue.ToString());
                }

                position.height = EditorExtendUtility.SingleLineHeight;
                ShowProperty(position, property.FindPropertyRelative("objectId"), objectIdLabel);
                position.y += EditorExtendUtility.SinglePropertyHeight;
                ShowProperty(position, property.FindPropertyRelative("prefabId"), prefabIdLabel);
            }
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorExtendUtility.SingleLineHeight * 2 + EditorExtendUtility.StandardVerticalSpacing;
        }
    }

    internal enum ReferenceType
    {
        PrefabReference,
        SceneObjectReference,
        InvalidReference,
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
            private static readonly Dictionary<SceneObjectReference, SceneObject> cache = new();

            public static bool TryGetSceneObject(SceneObjectReference key, out SceneObject obj)
            {
                return cache.TryGetValue(key, out obj) && obj;
            }
            public static void StoreSceneObject(SceneObjectReference key, SceneObject obj)
            {
                cache[key] = obj;
            }
        }

        private static bool TryParseGlobalObjectId(GUID guid, ulong objectId, ulong prefabId, out GlobalObjectId globalObjectId)
        {
            return GlobalObjectId.TryParse($"GlobalObjectId_V1-2-{guid}-{objectId}-{prefabId}", out globalObjectId);
        }

        public static bool TryFindSceneObjectFromCache(GUID guid, ulong objectId, ulong prefabId, out SceneObject sceneObject)
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

        private static (bool found, SceneObject obj) TryResolveSceneObjectReferenceInternal(GUID guid, ulong objectId, ulong prefabId, Type referenceType)
        {
            if (guid != GUID.zero)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid.ToString());
                switch (GetReferenceType(assetPath))
                {
                    case ReferenceType.PrefabReference:
                        var prefabObject = AssetDatabase.LoadAssetAtPath<SceneObject>(assetPath);
                        if (referenceType.IsAssignableFrom(prefabObject.GetType()))
                        {
                            return (true, prefabObject);
                        }
                        break;
                    case ReferenceType.SceneObjectReference:
                        if (IsSceneOpen(assetPath, out bool isLoaded) && isLoaded)
                        {
                            if (TryFindSceneObjectFromCache(guid, objectId, prefabId, out SceneObject sceneObject))
                            {
                                return (true, sceneObject);
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

    public static class SceneObjectPropertyUtility
    {
        internal static void GetSceneObjectReferenceValues(SerializedProperty property, out GUID guid, out ulong objectId, out ulong prefabId)
        {
            var guidProperty = property.FindPropertyRelative("guid");
            ulong upper = guidProperty.FindPropertyRelative((GUID def) => def.Upper).ulongValue;
            ulong lower = guidProperty.FindPropertyRelative((GUID def) => def.Lower).ulongValue;
            guid = new(upper, lower);
            objectId = property.FindPropertyRelative("objectId").ulongValue;
            prefabId = property.FindPropertyRelative("prefabId").ulongValue;
        }
        internal static void SetSceneObjectReferenceValues(SerializedProperty property, GUID guid, ulong objectId, ulong prefabId)
        {
            var guidProperty = property.FindPropertyRelative("guid");
            guidProperty.FindPropertyRelative((GUID def) => def.Upper).ulongValue = guid.Upper;
            guidProperty.FindPropertyRelative((GUID def) => def.Lower).ulongValue = guid.Lower;
            property.FindPropertyRelative("objectId").ulongValue = objectId;
            property.FindPropertyRelative("prefabId").ulongValue = prefabId;
        }

        public static SceneObjectReference<SceneObject> GetSceneObjectReferenceValue(this SerializedProperty property)
        {
            try
            {
                GetSceneObjectReferenceValues(property, out GUID guid, out ulong objectId, out ulong prefabId);
                return new SceneObjectReference<SceneObject>(guid, objectId, prefabId);
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
                SceneObjectPropertyUtility.GetSceneObjectReferenceValues(property, out GUID guid, out ulong objectId, out ulong prefabId);

                Type referenceType = (fieldInfo.FieldType.IsArray ? fieldInfo.FieldType.GetElementType() : fieldInfo.FieldType).GetGenericArguments()[0];

                void DrawPropertyField()
                {
                    if (guid == GUID.zero)
                    {
                        // No object
                        PrefixButton(position, property, null, false, null, referenceType);
                        return;
                    }
                    else
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid.ToString());
                        switch (SceneObjectEditorUtilityInternal.GetReferenceType(assetPath))
                        {
                            case ReferenceType.PrefabReference:
                            {
                                SceneObject prefabObject = AssetDatabase.LoadAssetAtPath<SceneObject>(assetPath);

                                // Fetch nested prefab child
                                bool TryFindCorrectPrefabObject(out SceneObject targetObject)
                                {
                                    targetObject = null;

                                    SceneObjectID targetObjectID = new(objectId, prefabId);

                                    // Try the components on the prefab root first
                                    foreach (SceneObject sceneObject in prefabObject.GetComponents<SceneObject>())
                                    {
                                        if (sceneObject.internalSceneObjectId == targetObjectID)
                                        {
                                            targetObject = sceneObject;
                                            break;
                                        }
                                    }

                                    // Try to fetch reference from current prefab stage
                                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                                    if (prefabStage != null && prefabStage.assetPath == assetPath)
                                    {
                                        if (targetObject)
                                        {
                                            // Try components on the prefab root, but in the prefab stage this time
                                            foreach (var sceneObject in prefabStage.prefabContentsRoot.GetComponents<SceneObject>())
                                            {
                                                if (sceneObject.internalSceneObjectId == targetObjectID)
                                                {
                                                    targetObject = sceneObject;
                                                    break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // Try components in the entire prefab to find the nested component
                                            foreach (var sceneObject in prefabStage.prefabContentsRoot.GetComponentsInChildren<SceneObject>())
                                            {
                                                if (sceneObject.internalSceneObjectId == targetObjectID)
                                                {
                                                    targetObject = sceneObject;
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    return targetObject;
                                }


                                if (!TryFindCorrectPrefabObject(out SceneObject targetObject))
                                {
                                    // Prefab reference
                                    using (new CustomObjectFieldContentScope("Prefab reference", null))
                                    {
                                        if (PrefixButton(position, property, PrefabIconTexture, true, null, referenceType))
                                        {
                                            var currentSelection = Selection.objects;

                                            AssetDatabase.OpenAsset(prefabObject);

                                            // Try to ping the target object
                                            if (TryFindCorrectPrefabObject(out targetObject))
                                            {
                                                EditorGUIUtility.PingObject(targetObject);
                                            }

                                            // Restore selection
                                            Selection.objects = currentSelection;

                                        }
                                    }
                                }
                                else if (referenceType.IsAssignableFrom(targetObject.GetType()))
                                {
                                    // Prefab
                                    if (PrefixButton(position, property, PrefabIconTexture, false, targetObject, referenceType))
                                    {
                                        AssetDatabase.OpenAsset(prefabObject);
                                    }
                                }
                                else
                                {
                                    // Type mismatch
                                    using (new CustomObjectFieldContentScope("Type mismatch", null))
                                    {
                                        PrefixButton(position, property, PrefabIconTexture, false, null, referenceType);
                                    }
                                }
                            }
                            break;

                            case ReferenceType.SceneObjectReference:
                            {
                                bool isSceneOpen = SceneObjectEditorUtilityInternal.IsSceneOpen(assetPath, out bool isLoaded);
                                if (isSceneOpen && isLoaded)
                                {
                                    if (SceneObjectEditorUtilityInternal.TryFindSceneObjectFromCache(guid, objectId, prefabId, out SceneObject targetObject))
                                    {
                                        if (referenceType.IsAssignableFrom(targetObject.GetType()))
                                        {
                                            // Scene object
                                            PrefixButton(position, property, SceneIconTexture, false, targetObject, referenceType);
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
                                            EditorSceneManager.OpenScene(assetPath, isSceneOpen ? OpenSceneMode.Additive : OpenSceneMode.Single);

                                            // Try to ping the target object
                                            if (SceneObjectEditorUtilityInternal.TryFindSceneObjectFromCache(guid, objectId, prefabId, out SceneObject targetObject))
                                            {
                                                EditorGUIUtility.PingObject(targetObject);
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                            default:
                                // Invalid object reference
                                PrefixButton(position, property, null, false, EditorExtendUtility.MissingObject, referenceType);
                                break;
                        }
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
            SceneObject newObj = EditorGUI.ObjectField(position, showValue, referenceType, filter != SceneObjectFilter.OnlyPrefabs) as SceneObject;
            if (EditorGUI.EndChangeCheck())
            {
                if (newObj)
                {
                    if (EditorSceneManager.IsPreviewSceneObject(newObj))
                    {
                        List<int> childIndices = new();
                        var transform = newObj.transform;
                        while (transform.parent)
                        {
                            childIndices.Add(transform.GetSiblingIndex());
                            transform = transform.parent;
                        }
                        var test = PrefabStageUtility.GetPrefabStage(newObj.gameObject).assetPath;
                        GameObject prefabGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(test);
                        if (prefabGameObject)
                        {
                            for (int i = childIndices.Count - 1; i >= 0; i--)
                            {
                                int idx = childIndices[i];
                                if (idx < prefabGameObject.transform.childCount)
                                {
                                    prefabGameObject = prefabGameObject.transform.GetChild(idx).gameObject;
                                }
                                else
                                {
                                    prefabGameObject = null;
                                }
                            }
                            if (prefabGameObject)
                            {
                                int componentIndex = -1;
                                int idx = 0;
                                foreach (var component in newObj.GetComponents<Component>())
                                {
                                    if (component == newObj)
                                    {
                                        componentIndex = idx;
                                        break;
                                    }
                                    idx++;
                                }
                                if (componentIndex != -1)
                                {
                                    var prefabComponents = prefabGameObject.GetComponents<Component>();
                                    if (componentIndex < prefabComponents.Length && prefabComponents[componentIndex].GetType().Equals(newObj.GetType()))
                                    {
                                        newObj = prefabComponents[componentIndex] as SceneObject;
                                    }
                                }
                            }
                        }
                    }

                    GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(newObj);
                    GUID guid = new(globalObjectId.assetGUID.ToString());
                    ulong objectId = globalObjectId.targetObjectId;
                    ulong prefabId = globalObjectId.targetPrefabId;

                    if (guid == default)
                    {
                        // TODO: Nested prefab children
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
                        SceneObjectPropertyUtility.SetSceneObjectReferenceValues(property, guid, objectId, prefabId);
                    }
                    else if (globalObjectId.identifierType == 2)
                    {
                        if (guid != newObj.internalSceneObjectGuid && prefabId == 0)
                        {
                            Debug.LogError($"Object '{newObj}' has an invalid GUID. This probably means it was a prefab instance which turned into a scene object by starting the editor.");
                            return;
                        }

                        if (filter == SceneObjectFilter.OnlyPrefabs)
                        {
                            Debug.LogError($"Field '{fieldInfo.Name}' does not accept scene object references");
                            return;
                        }

                        // Assign scene object or prefab instance
                        SceneObjectPropertyUtility.SetSceneObjectReferenceValues(property, guid, objectId, prefabId);
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