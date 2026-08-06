using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SHIN
{
    /// <summary>
    /// 인게임 내 캐릭터 기본 스탯 / 버프 보너스 / 활성 버프 목록 팝업.
    /// </summary>
    public class CharacterStatusUI : UIBase
    {
        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Button _dimButton;

        [SerializeField]
        private TextMeshProUGUI _statText;

        [SerializeField]
        private TextMeshProUGUI _buffListTitleText;

        [SerializeField]
        private TextMeshProUGUI _buffListText;

        private bool _bound;
        private bool _fontsApplied;

        public override UI_TYPE UiType => UI_TYPE.Popup;

        private void OnEnable()
        {
            BindButtons();
            ApplyFonts();
            Refresh();
        }

        public void Refresh()
        {
            UnitInfo unit = ResolvePlayerUnitInfo();
            if (_statText != null)
                _statText.text = BuildStatText(unit);

            if (_buffListTitleText != null)
                _buffListTitleText.text = "버프 리스트";

            if (_buffListText != null)
                _buffListText.text = BuildBuffListText(unit);
        }

        private void BindButtons()
        {
            if (_bound)
                return;

            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnClickClose);
            if (_dimButton != null)
                _dimButton.onClick.AddListener(OnClickClose);

            _bound = true;
        }

        private void ApplyFonts()
        {
            if (_fontsApplied)
                return;

            // OptionUI/Inventory와 동일: 본문 Gowun Dodum, 제목 Gowun Batang Bold
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                if (text == null)
                    continue;

                if (text == _buffListTitleText || text.gameObject.name == "BuffListTitle")
                    UiFont.ApplyTitle(text);
                else
                    UiFont.ApplyBody(text);
            }

            _fontsApplied = true;
        }

        private void OnClickClose()
        {
            GameManager.Instance?.SoundManager?.PlaySe(PublicVariable.Address.UiButtonClickSe);
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager != null && uiManager.Current == this)
            {
                uiManager.Close();
                return;
            }

            gameObject.SetActive(false);
        }

        private static UnitInfo ResolvePlayerUnitInfo()
        {
            var inGame = GameManager.Instance?.InGameManager;
            if (inGame == null)
                return null;

            // 현재 턴이 플레이어면 그 캐릭터, 아니면 첫 생존 플레이어
            var current = inGame.CurrentActor;
            if (current?.UnitInfo != null &&
                current.UnitInfo.UnitType == UNIT_TYPE.PLAYER)
            {
                return current.UnitInfo;
            }

            var players = inGame.PlayerCharacters;
            if (players == null)
                return null;

            for (int i = 0; i < players.Count; i++)
            {
                var character = players[i];
                if (character?.UnitInfo != null && character.IsAlive)
                    return character.UnitInfo;
            }

            return players.Count > 0 ? players[0]?.UnitInfo : null;
        }

        private static string BuildStatText(UnitInfo unit)
        {
            if (unit?.UnitData == null)
                return "캐릭터 정보 없음";

            var data = unit.UnitData;
            var sb = new StringBuilder(256);

            AppendStatLine(sb, "atk", data.unitBaseAttack,
                unit.GetBuffValueSum(BUFF_EFFECT_TYPE.ATTACK_UP) +
                unit.GetBuffValueSum(BUFF_EFFECT_TYPE.STRENGTH));

            AppendStatLine(sb, "def", data.unitBaseDefense,
                unit.GetBuffValueSum(BUFF_EFFECT_TYPE.DEFENSE_UP));

            int hpBonus = Mathf.FloorToInt(unit.GetBuffValueSum(BUFF_EFFECT_TYPE.HP_UP));
            sb.Append("hp : ");
            sb.Append(unit.CurrentHp);
            sb.Append('/');
            sb.Append(data.unitBaseHp);
            if (hpBonus != 0)
            {
                sb.Append(FormatBonus(hpBonus));
                sb.Append(" → ");
                sb.Append(unit.MaxHp);
            }

            sb.AppendLine();

            AppendStatLine(sb, "spd", data.unitBaseSpeed,
                unit.GetBuffValueSum(BUFF_EFFECT_TYPE.SPEED_UP));

            AppendStatLine(sb, "cost", data.unitBaseMaxCardCost,
                unit.GetBuffValueSum(BUFF_EFFECT_TYPE.MAX_COST_UP));

            int block = unit.CurrentBlock;
            if (block > 0)
            {
                sb.Append("block : ");
                sb.Append(block);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendStatLine(StringBuilder sb, string label, int baseValue, float bonusRaw)
        {
            int bonus = Mathf.FloorToInt(bonusRaw);
            sb.Append(label);
            sb.Append(" : ");
            sb.Append(baseValue);
            if (bonus != 0)
                sb.Append(FormatBonus(bonus));
            sb.AppendLine();
        }

        private static string FormatBonus(int bonus)
        {
            return bonus > 0 ? $"(+{bonus})" : $"({bonus})";
        }

        private static string BuildBuffListText(UnitInfo unit)
        {
            if (unit?.ActiveBuffs == null || unit.ActiveBuffs.Count == 0)
                return "(없음)";

            var sb = new StringBuilder(256);
            IReadOnlyList<ActiveBuff> buffs = unit.ActiveBuffs;
            for (int i = 0; i < buffs.Count; i++)
            {
                ActiveBuff buff = buffs[i];
                if (buff == null || buff.EffectType == BUFF_EFFECT_TYPE.NONE)
                    continue;

                sb.Append(GetBuffDisplayName(buff.EffectType));
                sb.Append("  ");
                sb.Append(Mathf.FloorToInt(buff.Value));
                sb.Append(" / ");
                sb.Append(buff.RemainingTurns);
                sb.Append("턴");
                sb.AppendLine();
            }

            string result = sb.ToString().TrimEnd();
            return string.IsNullOrEmpty(result) ? "(없음)" : result;
        }

        private static string GetBuffDisplayName(BUFF_EFFECT_TYPE type)
        {
            switch (type)
            {
                case BUFF_EFFECT_TYPE.ATTACK_UP: return "공격증가";
                case BUFF_EFFECT_TYPE.DEFENSE_UP: return "방어증가";
                case BUFF_EFFECT_TYPE.HP_UP: return "최대체력증가";
                case BUFF_EFFECT_TYPE.SPEED_UP: return "속도증가";
                case BUFF_EFFECT_TYPE.MAX_COST_UP: return "코스트증가";
                case BUFF_EFFECT_TYPE.STRENGTH: return "힘";
                case BUFF_EFFECT_TYPE.BLOCK: return "방어도";
                case BUFF_EFFECT_TYPE.VULNERABLE: return "취약";
                case BUFF_EFFECT_TYPE.WEAK: return "약화";
                case BUFF_EFFECT_TYPE.LIFESTEAL: return "흡혈";
                case BUFF_EFFECT_TYPE.THORNS: return "가시";
                case BUFF_EFFECT_TYPE.REGEN: return "재생";
                case BUFF_EFFECT_TYPE.POISON: return "독";
                default: return type.ToString();
            }
        }
    }
}
