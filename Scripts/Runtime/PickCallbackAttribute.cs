using UnityEngine;

namespace RoyTheunissen.SceneViewPicker
{
    /// <summary>
    /// Signifies that you want to receive a callback when this field gets picked.
    /// </summary>
    public class PickCallbackAttribute : PropertyAttribute
    {
        private string callbackName;
        public string CallbackName => callbackName;

        public PickCallbackAttribute(string callbackName)
        {
            this.callbackName = callbackName;
        }
    }
}
