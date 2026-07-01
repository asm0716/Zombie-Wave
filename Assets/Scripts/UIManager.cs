using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인게임 HUD 전체 관리.
/// HP바 · 탄수 · 웨이브 · 코인 · 상점/게임오버 패널 개폐.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("HUD — 체력")]
    [SerializeField] private Image    hpBarFill;  // HpBarFill 연결
    [SerializeField] private TMP_Text hpText;

    [Header("HUD — 탄약")]
    [SerializeField] private TMP_Text ammoText;     // "24 / 30"

    [Header("HUD — 웨이브")]
    [SerializeField] private TMP_Text waveText;     // "Wave 2"

    [Header("HUD — 코인")]
    [SerializeField] private TMP_Text coinText;     // "Coin: 150"

    [Header("패널")]
    [SerializeField] private GameObject upgradeShopPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject stageClearPanel;  // 선택

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 시작 시 패널 숨기기
        upgradeShopPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        stageClearPanel?.SetActive(false);

        // 플레이어 이벤트 구독
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.OnHpChanged += UpdateHp;

        WeaponController wc = FindObjectOfType<WeaponController>();
        if (wc != null) wc.OnAmmoChanged += UpdateAmmo;

        // 초기값 세팅
        UpdateWave(1);
        UpdateCoin(DataManager.Instance.Coin);
    }

    // ─────────────────────────────────────────────────────────────
    //  HUD 갱신
    // ─────────────────────────────────────────────────────────────
    public void UpdateHp(float current, float max)
    {
        // Fill Amount 를 0~1 비율로 설정
        if (hpBarFill != null)
            hpBarFill.fillAmount = current / max;

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    public void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
            ammoText.text = $"{current} / {max}";
    }

    public void UpdateWave(int wave) // 1-based
    {
        if (waveText != null)
            waveText.text = $"Wave {wave}";
    }

    public void UpdateCoin(int coin)
    {
        if (coinText != null)
            coinText.text = $"Coin: {coin}";
    }

    // ─────────────────────────────────────────────────────────────
    //  패널 제어
    // ─────────────────────────────────────────────────────────────
    public void ShowUpgradeShop(bool show)
    {
        upgradeShopPanel?.SetActive(show);

        // 상점 열리면 웨이브 텍스트 숨기기
        if (waveText != null)
            waveText.gameObject.SetActive(!show);

        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = show;
    }

    public void ShowGameOver(bool show)
    {
        gameOverPanel?.SetActive(show);
    }

    public void ShowStageClear(bool show)
    {
        stageClearPanel?.SetActive(show);
    }

    // ─────────────────────────────────────────────────────────────
    //  강화 후 전체 UI 동기화 (PlayerStats.ApplyAll 에서 호출)
    // ─────────────────────────────────────────────────────────────
    public void RefreshAll()
    {
        UpdateCoin(DataManager.Instance.Coin);
    }
}