using UnityEditor;
using UnityEngine;
using GUID = AggroBird.UnityExtend.GUID;

namespace AggroBird.SceneObjects.Editor
{
    public static class SceneObjectPropertyUtility
    {
        internal static void GetSceneObjectReferenceValues(SerializedProperty property, out GUID guid, out ulong objectId, out ulong prefabId)
        {
            var guidProperty = property.FindPropertyRelative(nameof(SceneObjectReference.guid));
            ulong upper = guidProperty.FindPropertyRelative(nameof(GUID.upper)).ulongValue;
            ulong lower = guidProperty.FindPropertyRelative(nameof(GUID.lower)).ulongValue;
            guid = new(upper, lower);
            objectId = property.FindPropertyRelative(nameof(SceneObjectReference.objectId)).ulongValue;
            prefabId = property.FindPropertyRelative(nameof(SceneObjectReference.prefabId)).ulongValue;
        }
        internal static void SetSceneObjectReferenceValues(SerializedProperty property, GUID guid, ulong objectId, ulong prefabId)
        {
            var guidProperty = property.FindPropertyRelative(nameof(SceneObjectReference.guid));
            guidProperty.FindPropertyRelative(nameof(GUID.upper)).ulongValue = guid.upper;
            guidProperty.FindPropertyRelative(nameof(GUID.lower)).ulongValue = guid.lower;
            property.FindPropertyRelative(nameof(SceneObjectReference.objectId)).ulongValue = objectId;
            property.FindPropertyRelative(nameof(SceneObjectReference.prefabId)).ulongValue = prefabId;
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
}