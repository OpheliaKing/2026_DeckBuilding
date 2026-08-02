using UnityEngine;

namespace SHIN
{
    /// <summary>
    /// 무기 프리팹 루트 마커. 위치/타입 데이터는 WeaponData가 담당한다.
    /// </summary>
    public class WeaponObject : MonoBehaviour
    {
    }

    public enum WEAPON_POSITION_TYPE
    {
        NONE,
        RIGHT_HAND,
        LEFT_HAND,
    }
}
