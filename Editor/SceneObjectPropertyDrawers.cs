using AggroBird.UnityExtend.Editor;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.BeginProperty(position, label, property);

                position = EditorGUI.PrefixLabel(position, content);
                if (property.hasMultipleDifferentValues)
                {
                    EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                    EditorGUI.TextField(position, string.Empty);
                }
                else
                {
                    ulong upper = property.FindPropertyRelative(nameof(GUID.upper)).ulongValue;
                    ulong lower = property.FindPropertyRelative(nameof(GUID.lower)).ulongValue;
                    EditorGUI.TextField(position, $"{upper:x16}{lower:x16}");
                }

                EditorGUI.EndProperty();
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
            using (new EditorGUI.DisabledScope(true))
            {
                position.height = EditorExtendUtility.SingleLineHeight;
                EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(SceneObjectID.objectId)), objectIdLabel);
                position.y += EditorExtendUtility.TotalPropertyHeight;
                EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(SceneObjectID.prefabId)), prefabIdLabel);
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
                        switch (SceneObjectEditorUtility.GetReferenceType(assetPath))
                        {
                            case ReferenceType.PrefabReference:
                            {
                                SceneObjectID targetObjectID = new(objectId, prefabId);

                                if (SceneObjectEditorUtility.TryFindSceneObjectInPrefabAsset(assetPath, targetObjectID, out SceneObject targetObject, out GameObject rootGameObject, out bool isPrefabStageObject))
                                {
                                    if (referenceType.IsAssignableFrom(targetObject.GetType()))
                                    {
                                        // Prefab reference
                                        if (PrefixButton(position, property, PrefabIconTexture, !isPrefabStageObject, targetObject, referenceType))
                                        {
                                            var currentSelection = Selection.objects;

                                            AssetDatabase.OpenAsset(rootGameObject);

                                            // Try to ping the target object
                                            if (SceneObjectEditorUtility.TryFindSceneObjectInPrefabStage(assetPath, targetObjectID, out targetObject, out _))
                                            {
                                                EditorGUIUtility.PingObject(targetObject);
                                            }

                                            // Restore selection
                                            Selection.objects = currentSelection;
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
                                else
                                {
                                    // Missing object
                                    PrefixButton(position, property, PrefabIconTexture, false, EditorExtendUtility.MissingObject, referenceType);
                                }
                            }
                            break;

                            case ReferenceType.SceneObjectReference:
                            {
                                bool isSceneOpen = SceneObjectEditorUtility.IsSceneOpen(assetPath, out bool isLoaded);
                                if (isSceneOpen && isLoaded)
                                {
                                    if (SceneObjectEditorUtility.TryFindRuntimeSceneObject(guid, objectId, prefabId, out SceneObject targetObject))
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

                                            // Try to ping the target object after opening the scene
                                            if (SceneObjectEditorUtility.TryFindRuntimeSceneObject(guid, objectId, prefabId, out SceneObject targetObject))
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
                        // Attempt to resolve prefab stage reference
                        string assetPath = PrefabStageUtility.GetPrefabStage(newObj.gameObject).assetPath;
                        GameObject prefabGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        if (prefabGameObject)
                        {
                            // Build index stack
                            List<int> childIndices = new();
                            var transform = newObj.transform;
                            while (transform.parent)
                            {
                                childIndices.Add(transform.GetSiblingIndex());
                                transform = transform.parent;
                            }
                            // Rewind sibling indices on prefab object
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
                                    break;
                                }
                            }
                            if (prefabGameObject)
                            {
                                // Find correct component by index
                                // This code assumes that GetComponents returns components in the same order as the inspector
                                int componentIndex = Array.IndexOf(newObj.GetComponents<Component>(), newObj);
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
                    }
                    else if (globalObjectId.identifierType == 1)
                    {
                        if (filter == SceneObjectFilter.OnlySceneObjects)
                        {
                            Debug.LogError($"Field '{fieldInfo.Name}' does not accept prefab references");
                        }
                        else
                        {
                            // Assign prefab
                            SceneObjectPropertyUtility.SetSceneObjectReferenceValues(property, guid, objectId, prefabId);
                        }
                    }
                    else if (globalObjectId.identifierType == 2)
                    {
                        if (guid != newObj.internalSceneObjectGuid && prefabId == 0)
                        {
                            Debug.LogError($"Object '{newObj}' has an invalid GUID. This probably means it was a prefab instance which turned into a scene object by starting the editor.");
                        }
                        else if (filter == SceneObjectFilter.OnlyPrefabs)
                        {
                            Debug.LogError($"Field '{fieldInfo.Name}' does not accept scene object references");
                        }
                        else
                        {
                            // Assign scene object or prefab instance
                            SceneObjectPropertyUtility.SetSceneObjectReferenceValues(property, guid, objectId, prefabId);
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