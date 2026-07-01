using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 웨이브 클리어 후 열리는 강화 상점 UI.
/// 강화 버튼 클릭 시 DataManager 에서 코인 차감 후 수치 즉시 반영.
/// 닫기 버튼 클릭 시 GameManager.OnShopClosed() 호출.
/// </summary>
public class UpgradeShopUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────────────────────────

    [Header("강화 버튼")]
    [SerializeField] private Button btnDamage; // 공격력 강화 버튼
    [SerializeField] private Button btnAmmo;   // 최대 탄환 강화 버튼
    [SerializeField] private Button btnHp;     // 최대 체력 강화 버튼
    [SerializeField] private Button btnSpeed;  // 이동속도 강화 버튼
    [SerializeField] private Button btnClose;  // 다음 웨이브 시작 버튼

    [Header("비용 텍스트")]
    [SerializeField] private TMP_Text txtDamageCost; // 공격력 강화 비용
    [SerializeField] private TMP_Text txtAmmoCost;   // 탄환 강화 비용
    [SerializeField] private TMP_Text txtHpCost;     // 체력 강화 비용
    [SerializeField] private TMP_Text txtSpeedCost;  // 속도 강화 비용

    [Header("레벨 텍스트")]
    [SerializeField] private TMP_Text txtDamageLv; // 현재 공격력 레벨
    [SerializeField] private TMP_Text txtAmmoLv;   // 현재 탄환 레벨
    [SerializeField] private TMP_Text txtHpLv;     // 현재 체력 레벨
    [SerializeField] private TMP_Text txtSpeedLv;  // 현재 속도 레벨

    [Header("현재 수치 텍스트 (선택)")]
    [SerializeField] private TMP_Text txtDamageVal; // 현재 공격력 수치
    [SerializeField] private TMP_Text txtAmmoVal;   // 현재 탄환 수치
    [SerializeField] private TMP_Text txtHpVal;     // 현재 체력 수치
    [SerializeField] private TMP_Text txtSpeedVal;  // 현재 속도 수치

    [Header("보유 코인")]
    [SerializeField] private TMP_Text txtCoin; // 현재 보유 코인 표시

    [Header("웨이브 정보")]
    [SerializeField] private TMP_Text txtWaveInfo; // "Wave 1 클리어!" 같은 텍스트

    // ─────────────────────────────────────────────────────────────
    //  컴포넌트 캐시
    // ─────────────────────────────────────────────────────────────

    private PlayerStats _stats;

    // ─────────────────────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _stats = FindObjectOfType<PlayerStats>();

        // 버튼 클릭 이벤트 등록
        btnDamage?.onClick.AddListener(OnBuyDamage);
        btnAmmo?  .onClick.AddListener(OnBuyAmmo);
        btnHp?    .onClick.AddListener(OnBuyHp);
        btnSpeed? .onClick.AddListener(OnBuySpeed);
        btnClose? .onClick.AddListener(OnClose);
    }

    // 패널이 활성화될 때마다 UI 갱신
    private void OnEnable()
    {
        RefreshUI();

        // 웨이브 정보 텍스트 갱신
        if (txtWaveInfo != null)
        {
            int wave = WaveSpawner.Instance != null ? WaveSpawner.Instance.CurrentWave + 1 : 1;
            txtWaveInfo.text = $"Wave {wave} Clear!";
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  강화 버튼 콜백
    // ─────────────────────────────────────────────────────────────

    // 공격력 강화 버튼 클릭
    private void OnBuyDamage()
    {
        if (DataManager.Instance.TryUpgradeDamage())
        {
            _stats?.ApplyAll(); // 강화 수치 즉시 반영
            RefreshUI();        // UI 갱신
        }
        else
        {
            StartCoroutine(FlashCoinText()); // 코인 부족 피드백
        }
    }

    // 탄환 강화 버튼 클릭
    private void OnBuyAmmo()
    {
        if (DataManager.Instance.TryUpgradeAmmo())
        {
            _stats?.ApplyAll();
            RefreshUI();
        }
        else
        {
            StartCoroutine(FlashCoinText());
        }
    }

    // 체력 강화 버튼 클릭
    private void OnBuyHp()
    {
        if (DataManager.Instance.TryUpgradeHp())
        {
            _stats?.ApplyAll();
            RefreshUI();
        }
        else
        {
            StartCoroutine(FlashCoinText());
        }
    }

    // 이동속도 강화 버튼 클릭
    private void OnBuySpeed()
    {
        if (DataManager.Instance.TryUpgradeSpeed())
        {
            _stats?.ApplyAll();
            RefreshUI();
        }
        else
        {
            StartCoroutine(FlashCoinText());
        }
    }

    // 닫기(다음 웨이브) 버튼 클릭
    private void OnClose()
    {
        // GameManager 에서 마지막 웨이브 여부 판단 후 처리
        GameManager.Instance?.OnShopClosed();
    }

    // ─────────────────────────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 강화 후 모든 UI 텍스트를 최신 DataManager 값으로 갱신.
    /// </summary>
    private void RefreshUI()
    {
        DataManager dm = DataManager.Instance;

        // 보유 코인
        if (txtCoin != null)
            txtCoin.text = $"Coin: {dm.Coin}";

        // 강화 비용
        if (txtDamageCost != null) txtDamageCost.text = $"{dm.DamageCost} Coin";
        if (txtAmmoCost   != null) txtAmmoCost.text   = $"{dm.AmmoCost} Coin";
        if (txtHpCost     != null) txtHpCost.text     = $"{dm.HpCost} Coin";
        if (txtSpeedCost  != null) txtSpeedCost.text  = $"{dm.SpeedCost} Coin";

        // 현재 레벨
        if (txtDamageLv != null) txtDamageLv.text = $"Lv.  {dm.WeaponDamageLv}";
        if (txtAmmoLv   != null) txtAmmoLv.text   = $"Lv.  {dm.MaxAmmoLv}";
        if (txtHpLv     != null) txtHpLv.text     = $"Lv.  {dm.MaxHpLv}";
        if (txtSpeedLv  != null) txtSpeedLv.text  = $"Lv.  {dm.MoveSpeedLv}";

        // 현재 수치
        if (txtDamageVal != null) txtDamageVal.text = $"Val: {dm.CurrentDamage}";
        if (txtAmmoVal   != null) txtAmmoVal.text   = $"Val: {dm.CurrentMaxAmmo}";
        if (txtHpVal     != null) txtHpVal.text     = $"Val: {dm.CurrentMaxHp}";
        if (txtSpeedVal  != null) txtSpeedVal.text  = $"Val: {dm.CurrentMoveSpeed:F1}";

        // 코인 부족 시 버튼 비활성화 (구매 불가 표시)
        SetButtonInteractable(btnDamage, dm.Coin >= dm.DamageCost);
        SetButtonInteractable(btnAmmo,   dm.Coin >= dm.AmmoCost);
        SetButtonInteractable(btnHp,     dm.Coin >= dm.HpCost);
        SetButtonInteractable(btnSpeed,  dm.Coin >= dm.SpeedCost);
    }

    // 버튼 활성/비활성 설정
    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn != null) btn.interactable = interactable;
    }

    // ─────────────────────────────────────────────────────────────
    //  코인 부족 피드백
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 코인이 부족할 때 코인 텍스트를 잠깐 빨간색으로 표시.
    /// </summary>
    private IEnumerator FlashCoinText()
    {
        if (txtCoin == null) yield break;

        Color original = txtCoin.color;
        txtCoin.color  = Color.red;

        yield return new WaitForSeconds(0.4f);

        txtCoin.color = original;
    }
}