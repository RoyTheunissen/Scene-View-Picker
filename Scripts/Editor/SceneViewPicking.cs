using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoyTheunissen.SceneViewPicker
{
    /// <summary>
    /// Scene view picking functionality for drawing a property field with a nice picking button next to it.
    /// </summary>
    public static partial class SceneViewPicking 
    {
        private class CachedIcon
        {
            private string path;
            
            private GUIContent cachedGuiContent;
            private GUIContent GuiContent
            {
                get
                {
                    if (cachedGuiContent == null)
                        cachedGuiContent = new GUIContent(Resources.Load<Texture2D>(path));
                    return cachedGuiContent;
                }
            }

            public CachedIcon(string path)
            {
                this.path = path;
            }

            public static implicit operator GUIContent(CachedIcon cachedIcon)
            {
                return cachedIcon.GuiContent;
            }
        }
        
        private static readonly CachedIcon buttonGuiContentPro = new CachedIcon("SceneViewPickerIcon");
        private static readonly CachedIcon buttonGuiContentPersonal = new CachedIcon("SceneViewPickerIconLightSkin");
        private static readonly CachedIcon buttonGuiContentActive = new CachedIcon("SceneViewPickerIconActive");

        private static GUIStyle cachedButtonStyle;
        private static GUIStyle ButtonStyle
        {
            get
            {
                if (cachedButtonStyle == null)
                    cachedButtonStyle = new GUIStyle();
                return cachedButtonStyle;
            }
        }

        public delegate void DefaultFieldDrawer(
            Rect position, SerializedProperty property, GUIContent label);
        
        private static bool HasTransform(Type type)
        {
            // Yes, interface references have Transforms. Technically the implementor could inherit
            // from System.Object but in practice they will always inherit from MonoBehaviour.
            // Pinky promise.
            if (IsInterfaceReference(type))
                return true;
            
            // ROY: Sadly the 'transform' field is not shared by a common base class between 
            // Component and GameObject, so if we want to support both we have to check it like this. 

            return typeof(Component).IsAssignableFrom(type) ||
                   typeof(GameObject).IsAssignableFrom(type);
        }

        private static bool IsPrefab(Object targetObject)
        {
            Component component = targetObject as Component;

            if (component == null)
                return false;

            return !component.gameObject.scene.IsValid();
        }

        private static Type GetPickType(Type fieldType)
        {
            // If it's an interface reference we actually want to get the type of interface.
            if (IsInterfaceReference(fieldType))
                return fieldType.BaseType.GetGenericArguments()[0];
            
            return fieldType;
        }

        private static bool IsInterfaceReference(Type fieldType)
        {
            if (fieldType.BaseType == null || fieldType.BaseType.BaseType == null)
                return false;
            
            // ROY: This is a little bit hacky but this maintains support for interface references without having a
            // hard dependency on it. That way you and I can both use this package.
            return fieldType.BaseType.BaseType.Name == "InterfaceReferenceBase";
        }

        public static void PropertyField(
            Rect position, SerializedProperty property, FieldInfo fieldInfo, GUIContent label,
            DefaultFieldDrawer defaultFieldDrawer = null)
        {   
            Type pickType = IsCollection(fieldInfo.FieldType)
                ? GetCollectionElementType(fieldInfo.FieldType)
                : fieldInfo.FieldType;

            Type pickTypePacked = pickType;

            // Unpack it. If it's an interface reference, then the pick type is actually the
            // interface type. Separate from the array check because it can ALSO be an array.
            pickType = GetPickType(pickType);

            bool isValid = HasTransform(pickTypePacked) &&
                           !IsPrefab(property.serializedObject.targetObject);

            if (!isValid)
            {
                if (defaultFieldDrawer != null)
                    defaultFieldDrawer(position, property, label);
                else
                    EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            float width = 18;
            float space = 4;

            Rect positionField = position;
            positionField.xMax -= width + space - 4;

            Object previousValue = property.objectReferenceValue;

            // Draw the field itself. Either use the drawer that was supplied or do a generic one.
            EditorGUI.BeginChangeCheck();
            if (defaultFieldDrawer != null)
                defaultFieldDrawer(positionField, property, label);
            else
                EditorGUI.PropertyField(positionField, property, label, true);
            bool didManuallyAssignNewValue = EditorGUI.EndChangeCheck();

            Rect positionPicker = new Rect(
                positionField.xMax + space, positionField.yMin - 1, width, position.height);

            bool wasPicking = PropertyPicking == property ||
                              (PropertyPicking != null &&
                               PropertyPicking.serializedObject ==
                               property.serializedObject &&
                               PropertyPicking.propertyPath == property.propertyPath);

            GUIContent icon;
            if (wasPicking)
                icon = buttonGuiContentActive;
            else
                icon = EditorGUIUtility.isProSkin ? buttonGuiContentPro : buttonGuiContentPersonal;
            
            bool wantsToPick = GUI.Toggle(positionPicker, wasPicking, icon, ButtonStyle);

            // If we manually assigned a new value, try to fire the callback.
            if (didManuallyAssignNewValue)
            {
                Object currentValue = property.objectReferenceValue;

                PickCallbackAttribute attribute = GetAttribute<PickCallbackAttribute>(fieldInfo);

                if (attribute != null)
                {
                    string callback = attribute.CallbackName;

                    FireSceneViewPickerCallback(property, callback, previousValue, currentValue);
                }
            }

            if (wasPicking && (!wantsToPick || didManuallyAssignNewValue))
                StopPicking();

            if (!wasPicking && wantsToPick)
                StartPicking(property, fieldInfo, pickType);
        }

        private static bool IsCollection(Type type)
        {
            if (type.IsArray)
                return true;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return true;

            return false;
        }

        private static Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType();
            
            if (typeof(List<>) == collectionType.GetGenericTypeDefinition())
                return collectionType.GetGenericArguments()[0];
            
            return null;
        }

        private static void StartPicking(SerializedProperty property, FieldInfo fieldInfo, Type pickType)
        {
            PickCallbackAttribute attribute = GetAttribute<PickCallbackAttribute>(fieldInfo);
            string callback = attribute == null ? null : attribute.CallbackName;
            
            StartPicking(property, pickType, callback);
        }
        
        public static T GetAttribute<T>(MemberInfo memberInfo, bool inherit = true)
            where T : Attribute
        {
            object[] attributes = memberInfo.GetCustomAttributes(inherit);
            return attributes.OfType<T>().FirstOrDefault();
        }
    }
}
