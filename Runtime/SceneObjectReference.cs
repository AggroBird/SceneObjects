using AggroBird.UnityExtend;
using System;
using System.Globalization;
using UnityEngine;

namespace AggroBird.SceneObjects.Editor
{
#if UNITY_EDITOR
    public static class SceneObjectEditorUtility
    {
        internal static Func<GUID, ulong, ulong, Type, (bool found, SceneObject obj)> tryResolveSceneObjectReferenceInternal;

        // Editor only utility function for finding references outside of play time
        // If the return value is true, but the result is null, the object is located in another scene
        public static bool TryResolveSceneObjectReference<T>(SceneObjectReference<T> reference, out T result) where T : SceneObject
        {
            if (reference && tryResolveSceneObjectReferenceInternal != null)
            {
                var (guid, prefabId, objectId) = reference.GetValues();
                (bool found, SceneObject obj) = tryResolveSceneObjectReferenceInternal(guid, prefabId, objectId, typeof(T));
                if (found)
                {
                    result = obj as T;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
#endif
}

namespace AggroBird.SceneObjects
{
    // General reference (can be used to find scene objects)
    public readonly struct SceneObjectReference
    {
        public SceneObjectReference(GUID guid, ulong objectId)
        {
            this.guid = guid;
            this.objectId = objectId;
        }

        // GUID part (prefab GUID or scene GUID)
        public readonly GUID guid;
        // Specific object ID in scene (0 in case of prefab)
        public readonly ulong objectId;

        public static implicit operator bool(SceneObjectReference reference) => reference.guid != GUID.zero;

        public bool Equals(SceneObjectReference other)
        {
            return guid.Equals(other.guid) && objectId.Equals(other.objectId);
        }

        public override bool Equals(object obj)
        {
            return obj is SceneObjectReference other && Equals(other);
        }
        public override int GetHashCode()
        {
            return (guid.GetHashCode() << 2) ^ objectId.GetHashCode();
        }

        public override string ToString()
        {
            return $"{guid.Upper:x16}{guid.Lower:x16}{objectId:x16}";
        }
        public static bool TryParse(string str, out SceneObjectReference reference)
        {
            if (str != null && str.Length == 48)
            {
                if (ulong.TryParse(str.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong upper))
                {
                    if (ulong.TryParse(str.AsSpan(16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong lower))
                    {
                        if (ulong.TryParse(str.AsSpan(32, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong objectId))
                        {
                            reference = new SceneObjectReference(new GUID(upper, lower), objectId);
                            return true;
                        }
                    }
                }
            }
            reference = default;
            return false;
        }
    }

    // Property field (can only be assigned in inspector)
    [Serializable]
    public struct SceneObjectReference<T> where T : SceneObject
    {
        internal SceneObjectReference(GUID guid, ulong prefabId, ulong objectId)
        {
            this.guid = guid;
            this.prefabId = prefabId;
            this.objectId = objectId;
        }

        [SerializeField] private GUID guid;
#if UNITY_EDITOR
        [SerializeField] private ulong prefabId;
#endif
        [SerializeField] private ulong objectId;

        internal readonly (GUID guid, ulong prefabId, ulong objectId) GetValues()
        {
            return (guid, prefabId, objectId);
        }

        public static implicit operator SceneObjectReference(SceneObjectReference<T> reference) => new(reference.guid, reference.objectId);
        public static implicit operator bool(SceneObjectReference<T> reference) => reference.guid != GUID.zero;
    }
}