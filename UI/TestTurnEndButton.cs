using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHIN
{
    public class TestTurnEndButton : MonoBehaviour
    {
        public void OnClick()
        {
            GameManager.Instance.InGameManager.EndTurn();
        }
    }

}

