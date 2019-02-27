using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace HttpClientCommandExecutor
{
    public static class ReflectionHelper
    {
        public static T GetFieldValue<T>(object instance, string fieldName)
        {
            FieldInfo info = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            return (T)(info.GetValue(instance));
        }

        public static void SetFieldValue<T>(this object instance, string fieldName, T value)
        {
            FieldInfo info = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            info.SetValue(instance, value);
        }
    }
}
