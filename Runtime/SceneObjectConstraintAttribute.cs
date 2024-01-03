using System;

namespace AggroBird.SceneObjects
{
    public enum SceneObjectFilter
    {
        AllObjects,
        OnlySceneObjects,
        OnlyPrefabs,
    }

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