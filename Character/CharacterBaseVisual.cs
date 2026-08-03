using System.Threading.Tasks;
using UnityEngine;

namespace SHIN
{
    public partial class CharacterBase
    {
        [Header("Visual Model")]
        [SerializeField]
        [Tooltip("비주얼 모델을 붙일 부모. 비우면 자신 Transform 사용")]
        private Transform _modelRoot;

        private GameObject _visualModelInstance;

        /// <summary>
        /// UnitData.unitModelPath가 있으면 공용 셸 아래에 비주얼 모델을 장착한다.
        /// 몬스터처럼 unitModelPath가 비어 있으면 스킵.
        /// </summary>
        public async Task AttachVisualModelAsync(ResourceManager resourceManager)
        {
            if (_unitInfo?.UnitData == null)
                return;

            string modelPath = _unitInfo.UnitData.unitModelPath;
            if (string.IsNullOrEmpty(modelPath))
                return;

            if (resourceManager == null)
            {
                Debug.LogError($"[Visual] ResourceManager가 없습니다: {name}");
                return;
            }

            Transform parent = _modelRoot != null ? _modelRoot : transform;
            ClearVisualModel(resourceManager);
            HideExistingVisualPlaceholders(parent);

            GameObject model = await resourceManager.InstantiateAsync(modelPath, parent);
            if (model == null)
            {
                Debug.LogError($"[Visual] 모델 생성 실패: {name} / {modelPath}");
                return;
            }

            Transform modelTransform = model.transform;
            modelTransform.localPosition = Vector3.zero;
            modelTransform.localRotation = Quaternion.identity;
            modelTransform.localScale = Vector3.one;
            model.SetActive(true);
            _visualModelInstance = model;

            // 선택 화면 전용 컴포넌트는 전투에서 비활성
            var selectModel = model.GetComponentInChildren<CharacterSelectModel>(true);
            if (selectModel != null)
                selectModel.enabled = false;

            // 비활성 플레이스홀더 Animator보다 새 모델을 우선한다.
            _animator = model.GetComponentInChildren<Animator>(true);
            if (_animator == null)
                InvalidateAnimatorCache();
        }

        public void ClearVisualModel(ResourceManager resourceManager = null)
        {
            if (_visualModelInstance == null)
                return;

            resourceManager ??= GameManager.Instance?.ResourceManager;
            if (resourceManager != null)
                resourceManager.ReleaseInstance(_visualModelInstance);
            else
                Destroy(_visualModelInstance);

            _visualModelInstance = null;
            InvalidateAnimatorCache();
        }

        private void HideExistingVisualPlaceholders(Transform parent)
        {
            if (parent == null)
                return;

            // 기존에 박아 둔 Animator 보유 비주얼(플레이스홀더)은 끈다.
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                    continue;

                Transform visualRoot = FindDirectChildAncestor(animator.transform, parent);
                if (visualRoot == null || visualRoot == parent)
                    continue;

                if (_visualModelInstance != null && visualRoot.IsChildOf(_visualModelInstance.transform))
                    continue;

                visualRoot.gameObject.SetActive(false);
            }
        }

        private static Transform FindDirectChildAncestor(Transform from, Transform parent)
        {
            Transform current = from;
            while (current != null)
            {
                if (current.parent == parent)
                    return current;
                if (current == parent)
                    return null;
                current = current.parent;
            }

            return null;
        }
    }
}
