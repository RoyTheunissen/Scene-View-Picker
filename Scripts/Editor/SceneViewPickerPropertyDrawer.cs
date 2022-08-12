using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.SceneViewPicker
{
    /// <summary>
    /// Adds scene view picking functionality to all object fields in MonoBehaviours.
    /// </summary>
    [CustomPropertyDrawer(typeof(Object), true)]
    public partial class SceneViewPickerPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SceneViewPicking.PropertyField(position, property, fieldInfo, label);
        }
    }
}
