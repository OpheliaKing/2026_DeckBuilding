using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// Button 기본 Inspector가 파생 필드를 숨기므로, ButtonBase 전용 필드를 추가로 그린다.
    /// </summary>
    [CustomEditor(typeof(ButtonBase), true)]
    [CanEditMultipleObjects]
    public class ButtonBaseEditor : ButtonEditor
    {
        private SerializedProperty _soundClipName;

        protected override void OnEnable()
        {
            base.OnEnable();
            _soundClipName = serializedObject.FindProperty("_soundClipName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_soundClipName != null)
                EditorGUILayout.PropertyField(_soundClipName);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }
    }
}
