using UnityEditor;
using UnityEngine;

namespace SHIN
{
    [CustomEditor(typeof(CameraManager))]
    public class CameraManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var cameraManager = (CameraManager)target;
            if (cameraManager == null)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Shake Test", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Play 모드에서 버튼을 누르면 현재 Inspector 값으로 흔들립니다.", MessageType.Info);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Level1", GUILayout.Height(28f)))
                    cameraManager.TestShake(CameraShakeLevel.Level1);
                if (GUILayout.Button("Test Level2", GUILayout.Height(28f)))
                    cameraManager.TestShake(CameraShakeLevel.Level2);
                if (GUILayout.Button("Test Level3", GUILayout.Height(28f)))
                    cameraManager.TestShake(CameraShakeLevel.Level3);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Test Selected Level", GUILayout.Height(28f)))
                    cameraManager.TestShakeSelected();
            }
        }
    }
}
