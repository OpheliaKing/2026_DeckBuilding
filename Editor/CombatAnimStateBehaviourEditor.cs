#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SHIN
{
    [CustomEditor(typeof(CombatAnimStateBehaviour))]
    public class CombatAnimStateBehaviourEditor : Editor
    {
        private static Animator _previewAnimator;
        private static float _previewNormalizedTime;
        private static bool _isPreviewPlaying;
        private static double _lastEditorTime;
        private static readonly List<GameObject> PreviewEffects = new();

        private string _detectedStatePath;
        private string _previewStatePath;
        private int _previewLayerIndex;
        private float _previewClipLength = 1f;
        private string _previewMessage;
        private MessageType _previewMessageType = MessageType.Info;

        private void OnEnable()
        {
            DetectOwnerState();
            EditorApplication.update += UpdatePreviewPlayback;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreviewPlayback;
            _isPreviewPlaying = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Scene Animation Preview", EditorStyles.boldLabel);

            _previewAnimator = (Animator)EditorGUILayout.ObjectField(
                new GUIContent("Scene Animator", "씬에 배치된 미리보기 모델의 Animator"),
                _previewAnimator,
                typeof(Animator),
                true);

            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(_detectedStatePath)))
            {
                _previewStatePath = EditorGUILayout.TextField(
                    new GUIContent("State Path", "자동 탐색 실패 시 Base Layer.StateName 형태로 입력"),
                    _previewStatePath);
            }

            if (!string.IsNullOrEmpty(_detectedStatePath))
                EditorGUILayout.LabelField("Detected State", _detectedStatePath);

            EditorGUI.BeginChangeCheck();
            float normalizedTime = EditorGUILayout.Slider(
                new GUIContent("Normalized Time", "0=애니메이션 시작, 1=끝"),
                _previewNormalizedTime,
                0f,
                1f);
            if (EditorGUI.EndChangeCheck())
            {
                _previewNormalizedTime = normalizedTime;
                PreviewCurrentPose();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isPreviewPlaying ? "Pause" : "Play", GUILayout.Height(26f)))
                TogglePreviewPlayback();

            if (GUILayout.Button("Preview Pose", GUILayout.Height(26f)))
                PreviewCurrentPose();

            if (GUILayout.Button("Reset Pose", GUILayout.Height(26f)))
                ResetPreviewPose();
            EditorGUILayout.EndHorizontal();

            if (Selection.activeGameObject != null)
            {
                var selectedAnimator = Selection.activeGameObject.GetComponentInChildren<Animator>();
                if (selectedAnimator != null && selectedAnimator != _previewAnimator &&
                    GUILayout.Button("Use Selected Scene Animator"))
                {
                    _previewAnimator = selectedAnimator;
                    PreviewCurrentPose();
                }
            }

            if (!string.IsNullOrEmpty(_previewMessage))
                EditorGUILayout.HelpBox(_previewMessage, _previewMessageType);

            DrawParticleCuePreview();
        }

        private void DrawParticleCuePreview()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Particle Cue Preview", EditorStyles.boldLabel);

            serializedObject.Update();
            var particleCues = serializedObject.FindProperty("_particleCues");
            if (particleCues == null || !particleCues.isArray || particleCues.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Particle Timings에 Cue를 먼저 추가하세요.", MessageType.Info);
                return;
            }

            for (int i = 0; i < particleCues.arraySize; i++)
            {
                var cue = particleCues.GetArrayElementAtIndex(i);
                var time = cue.FindPropertyRelative("NormalizedTime");
                var address = cue.FindPropertyRelative("ParticleAddress");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"Cue {i}  t={time.floatValue:0.###}",
                    string.IsNullOrWhiteSpace(address.stringValue) ? "(주소 없음)" : address.stringValue);

                if (GUILayout.Button("Pose", GUILayout.Width(48f)))
                {
                    _previewNormalizedTime = Mathf.Clamp01(time.floatValue);
                    PreviewCurrentPose();
                }

                if (GUILayout.Button("Spawn", GUILayout.Width(54f)))
                {
                    _previewNormalizedTime = Mathf.Clamp01(time.floatValue);
                    PreviewCurrentPose();
                    SpawnParticlePreview(cue);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (PreviewEffects.Count > 0 && GUILayout.Button("Clear Preview Effects"))
                ClearPreviewEffects();
        }

        private void TogglePreviewPlayback()
        {
            if (!ValidatePreviewAnimator())
                return;

            _isPreviewPlaying = !_isPreviewPlaying;
            _lastEditorTime = EditorApplication.timeSinceStartup;

            if (_isPreviewPlaying && _previewNormalizedTime >= 1f)
                _previewNormalizedTime = 0f;
        }

        private void UpdatePreviewPlayback()
        {
            if (!_isPreviewPlaying || _previewAnimator == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastEditorTime);
            _lastEditorTime = now;

            _previewNormalizedTime += delta / Mathf.Max(0.01f, _previewClipLength);
            if (_previewNormalizedTime >= 1f)
            {
                _previewNormalizedTime = 1f;
                _isPreviewPlaying = false;
            }

            PreviewCurrentPose();
            Repaint();
        }

        private void PreviewCurrentPose()
        {
            if (!ValidatePreviewAnimator())
                return;

            string statePath = GetPreviewStatePath();
            int fullPathHash = Animator.StringToHash(statePath);
            int shortNameHash = Animator.StringToHash(GetShortStateName(statePath));
            int stateHash = _previewAnimator.HasState(_previewLayerIndex, fullPathHash)
                ? fullPathHash
                : shortNameHash;

            if (!_previewAnimator.HasState(_previewLayerIndex, stateHash))
            {
                SetMessage($"Animator에서 State를 찾지 못했습니다: {statePath}", MessageType.Warning);
                return;
            }

            _previewAnimator.enabled = true;
            _previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _previewAnimator.Play(stateHash, _previewLayerIndex, Mathf.Clamp01(_previewNormalizedTime));
            _previewAnimator.Update(0f);

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            SetMessage($"미리보기: {statePath} / {_previewNormalizedTime:0.###}", MessageType.Info);
        }

        private void ResetPreviewPose()
        {
            _isPreviewPlaying = false;
            _previewNormalizedTime = 0f;

            if (_previewAnimator != null)
            {
                _previewAnimator.Rebind();
                _previewAnimator.Update(0f);
                SceneView.RepaintAll();
            }
        }

        private void SpawnParticlePreview(SerializedProperty cue)
        {
            if (!ValidatePreviewAnimator())
                return;

            string address = cue.FindPropertyRelative("ParticleAddress").stringValue;
            if (string.IsNullOrWhiteSpace(address))
            {
                SetMessage("Particle Address가 비어 있습니다.", MessageType.Warning);
                return;
            }

            var spawnSpace = (ParticleSpawnSpace)cue.FindPropertyRelative("SpawnSpace").enumValueIndex;
            Vector3 positionOffset = cue.FindPropertyRelative("PositionOffset").vector3Value;
            Vector3 rotationOffset = cue.FindPropertyRelative("RotationOffset").vector3Value;
            Transform origin = _previewAnimator.transform;

            // Play 모드: 런타임과 동일하게 ResourceManager + 캐릭터 풀 경로 사용
            if (Application.isPlaying)
            {
                var character = _previewAnimator.GetComponentInParent<CharacterBase>();
                if (character == null)
                    character = _previewAnimator.GetComponent<CharacterBase>();

                if (character == null)
                {
                    SetMessage("Play 모드 Spawn은 CharacterBase가 필요합니다.", MessageType.Warning);
                    return;
                }

                character.SpawnParticleEffect(address, spawnSpace, positionOffset, rotationOffset, origin);
                SetMessage($"ResourceManager 경로로 스폰: {address}", MessageType.Info);
                return;
            }

            // Edit 모드: ResourceManager(Addressables 런타임)는 사용 불가 → AssetDatabase로 프리뷰
            GameObject prefab = LoadParticlePrefabForEditor(address);
            if (prefab == null)
            {
                SetMessage(
                    $"에디터에서 프리팹을 찾지 못했습니다: {address}\n" +
                    "주소에 Assets/... 전체 경로를 쓰거나 Play 모드에서 Spawn하세요.",
                    MessageType.Error);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
                instance = Instantiate(prefab);

            Undo.RegisterCreatedObjectUndo(instance, "Spawn Particle Preview");
            instance.name = $"{prefab.name}_Preview";

            if (spawnSpace == ParticleSpawnSpace.Child)
            {
                instance.transform.SetParent(origin, false);
                instance.transform.localPosition = positionOffset;
                instance.transform.localRotation = Quaternion.Euler(rotationOffset);
            }
            else
            {
                instance.transform.SetParent(null, true);
                instance.transform.SetPositionAndRotation(
                    origin.TransformPoint(positionOffset),
                    origin.rotation * Quaternion.Euler(rotationOffset));
            }

            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[i].Play(true);
            }

            PreviewEffects.Add(instance);
            Selection.activeGameObject = instance;
            SceneView.RepaintAll();
            SetMessage($"에디터 프리뷰 생성(AssetDatabase): {address}", MessageType.Info);
        }

        /// <summary>
        /// Edit 모드 전용. 실제 전투 스폰은 ResourceManager를 씁니다.
        /// </summary>
        private static GameObject LoadParticlePrefabForEditor(string address)
        {
            var direct = AssetDatabase.LoadAssetAtPath<GameObject>(address);
            if (direct != null)
                return direct;

            // 파일명만 있는 경우 단일 매치면 사용
            string fileName = Path.GetFileNameWithoutExtension(address);
            if (string.IsNullOrEmpty(fileName))
                return null;

            string[] guids = AssetDatabase.FindAssets($"{fileName} t:Prefab");
            if (guids.Length == 1)
                return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }

        private static void ClearPreviewEffects()
        {
            for (int i = PreviewEffects.Count - 1; i >= 0; i--)
            {
                if (PreviewEffects[i] != null)
                    Undo.DestroyObjectImmediate(PreviewEffects[i]);
            }
            PreviewEffects.Clear();
        }

        private bool ValidatePreviewAnimator()
        {
            if (_previewAnimator == null)
            {
                SetMessage("Scene Animator를 지정하세요.", MessageType.Warning);
                return false;
            }

            if (_previewAnimator.runtimeAnimatorController == null)
            {
                SetMessage("선택한 Animator에 Runtime Animator Controller가 없습니다.", MessageType.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(GetPreviewStatePath()))
            {
                SetMessage("미리보기 State Path를 찾거나 입력해야 합니다.", MessageType.Warning);
                return false;
            }

            return true;
        }

        private string GetPreviewStatePath()
        {
            return string.IsNullOrEmpty(_detectedStatePath) ? _previewStatePath : _detectedStatePath;
        }

        private void DetectOwnerState()
        {
            _detectedStatePath = null;
            _previewStatePath = string.Empty;
            _previewLayerIndex = 0;
            _previewClipLength = 1f;

            string assetPath = AssetDatabase.GetAssetPath(target);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (controller == null)
                return;

            for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
            {
                var layer = controller.layers[layerIndex];
                if (TryFindOwnerState(
                    layer.stateMachine,
                    layer.name,
                    target as CombatAnimStateBehaviour,
                    out string statePath,
                    out float clipLength))
                {
                    _detectedStatePath = statePath;
                    _previewStatePath = statePath;
                    _previewLayerIndex = layerIndex;
                    _previewClipLength = Mathf.Max(0.01f, clipLength);
                    return;
                }
            }
        }

        private static bool TryFindOwnerState(
            AnimatorStateMachine stateMachine,
            string path,
            CombatAnimStateBehaviour behaviour,
            out string statePath,
            out float clipLength)
        {
            foreach (var childState in stateMachine.states)
            {
                var state = childState.state;
                foreach (var stateBehaviour in state.behaviours)
                {
                    if (stateBehaviour != behaviour)
                        continue;

                    statePath = $"{path}.{state.name}";
                    clipLength = state.motion is AnimationClip clip ? clip.length : 1f;
                    return true;
                }
            }

            foreach (var childMachine in stateMachine.stateMachines)
            {
                if (TryFindOwnerState(
                    childMachine.stateMachine,
                    $"{path}.{childMachine.stateMachine.name}",
                    behaviour,
                    out statePath,
                    out clipLength))
                {
                    return true;
                }
            }

            statePath = null;
            clipLength = 1f;
            return false;
        }

        private static string GetShortStateName(string statePath)
        {
            int lastDot = statePath.LastIndexOf('.');
            return lastDot >= 0 ? statePath.Substring(lastDot + 1) : statePath;
        }

        private void SetMessage(string message, MessageType type)
        {
            _previewMessage = message;
            _previewMessageType = type;
            Repaint();
        }
    }
}
#endif
