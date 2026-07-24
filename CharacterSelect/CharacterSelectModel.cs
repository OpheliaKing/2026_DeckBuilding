using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 캐릭터 선택 화면용 3D/표시 모델.
    /// </summary>
    public class CharacterSelectModel : MonoBehaviour
    {
        private CharacterSelectData _data;

        public CharacterSelectData Data => _data;

        public void Initialize(CharacterSelectData data)
        {
            _data = data;
        }
    }
}
