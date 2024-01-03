using System;

namespace AggroBird.SceneObjects
{
    public enum SceneObjectFilter
    {
        AllObjects,
        OnlySceneObjects,
        OnlyPrefabs,
    }

    // Add this to scene object reference properties to specify what kind
    // of references the property can accept
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SceneObjectConstraintAttribute : Attribute
    {
        public SceneObjectConstraintAttribute(SceneObjectFilter filter)
        {
            this.filter = filter;
        }

        public readonly SceneObjectFilter filter;
    }
}