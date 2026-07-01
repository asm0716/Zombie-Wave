using UnityEngine;
using UnityEngine.SceneManagement;


// 전역 싱글톤. DontDestroyOnLoad 로 씬이 바뀌어도 데이터 유지.
// 게임 오버/클리어 시 ResetAllData() 호출로 초기화.
// 이 스크립트는 게임 전체에서 공유되는 데이터 관리 역할을 담당한다.
// 코인, 업그레이드 레벨, 현재 능력치, 결과 화면에 표시할 기록을 한 곳에서 관리한다.

public class DataManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    // 다른 스크립트에서 DataManager.Instance로 접근하여 코인, 업그레이드, 결과 기록을 사용할 수 있다.
    public static DataManager Instance { get; private set; }

    // ── 재화 ──────────────────────────────────────────────
    // 현재 플레이어가 보유 중인 코인
    // 좀비 처치나 웨이브 클리어 보상으로 증가하고, 업그레이드 구매 시 감소한다.
    [HideInInspector] public int Coin = 0;

    // ── 강화 레벨 (1 이 기본) ─────────────────────────────
    // 각 업그레이드의 현재 레벨
    // 기본값은 1이며, 상점에서 강화할 때마다 1씩 증가한다.
    [HideInInspector] public int WeaponDamageLv  = 1;
    [HideInInspector] public int MaxAmmoLv       = 1;
    [HideInInspector] public int MaxHpLv         = 1;
    [HideInInspector] public int MoveSpeedLv     = 1;

    // ── 강화 당 증분 수치 (인스펙터 조절) ─────────────────
    // 레벨이 1 증가할 때마다 실제 능력치가 얼마나 증가할지 결정하는 값
    // Inspector에서 조절 가능하게 하여 난이도 밸런스를 쉽게 수정할 수 있다.
    [Header("강화 당 증분")]
    [SerializeField] private float damagePerLv    = 2.5f;   // 공격력 증가량
    [SerializeField] private int   ammoPerLv      = 5;     // 최대 탄환 증가량
    [SerializeField] private float hpPerLv        = 25f;   // 최대 체력 증가량
    [SerializeField] private float speedPerLv     = 0.3f;  // 이동 속도 증가량

    // ── 기본(Lv1) 수치 ────────────────────────────────────
    // 업그레이드를 한 번도 하지 않았을 때의 기본 능력치
    // CurrentDamage, CurrentMaxAmmo 등의 프로퍼티 계산 기준이 된다.
    [Header("기본 수치 (Lv1)")]
    [SerializeField] private float baseDamage     = 20f;
    [SerializeField] private int   baseMaxAmmo    = 30;
    [SerializeField] private float baseMaxHp      = 100f;
    [SerializeField] private float baseMoveSpeed  = 5f;

    // ── 강화 비용 ─────────────────────────────────────────
    // 각 업그레이드의 기본 비용
    // 실제 구매 비용은 아래 costGrowthRate를 적용하여 레벨이 올라갈수록 증가한다.
    [Header("강화 비용")]
    [SerializeField] private int damageCost   = 70;
    [SerializeField] private int ammoCost     = 35;
    [SerializeField] private int hpCost       = 45;
    [SerializeField] private int speedCost    = 40;

    // ── 강화 당 비용 증가율 (인스펙터 조절) ────────────────
    // 업그레이드를 반복 구매할수록 비용이 점점 증가하도록 만드는 비율
    // 예: 1.08f이면 레벨이 오를 때마다 비용이 약 8%씩 증가한다.
    [Header("레벨당 비용 증가율")]
    [SerializeField] private float costGrowthRate = 1.08f; // 레벨마다 8%씩 비용 증가

    // ── 최대 강화 레벨 (인스펙터 조절) ──────────────────────
    // 업그레이드가 무한히 올라가 게임이 너무 쉬워지는 것을 막기 위한 최대 레벨 제한
    [Header("최대 강화 레벨")]
    [SerializeField] private int maxUpgradeLv = 10; // 이 레벨에 도달하면 더 이상 강화 불가

    // ── 현재 적용 수치 (프로퍼티) ─────────────────────────
    // 현재 강화 레벨을 기준으로 실제 게임에 적용되는 능력치를 계산한다.
    // 레벨 1일 때는 기본 수치만 적용되고, 레벨이 오를수록 강화 당 증분이 더해진다.
    public float CurrentDamage    => baseDamage    + damagePerLv  * (WeaponDamageLv - 1);
    public int   CurrentMaxAmmo   => baseMaxAmmo   + ammoPerLv    * (MaxAmmoLv - 1);
    public float CurrentMaxHp     => baseMaxHp     + hpPerLv      * (MaxHpLv - 1);
    public float CurrentMoveSpeed => baseMoveSpeed + speedPerLv   * (MoveSpeedLv - 1);

    // ── 강화 비용 접근자 (레벨에 비례해서 증가) ────────────
    // 현재 레벨에 따라 실제 상점에 표시될 업그레이드 비용을 계산한다.
    // Mathf.Pow를 사용하여 레벨이 높아질수록 비용이 점진적으로 증가한다.
    public int DamageCost => Mathf.RoundToInt(damageCost * Mathf.Pow(costGrowthRate, WeaponDamageLv - 1));
    public int AmmoCost   => Mathf.RoundToInt(ammoCost   * Mathf.Pow(costGrowthRate, MaxAmmoLv - 1));
    public int HpCost     => Mathf.RoundToInt(hpCost     * Mathf.Pow(costGrowthRate, MaxHpLv - 1));
    public int SpeedCost  => Mathf.RoundToInt(speedCost  * Mathf.Pow(costGrowthRate, MoveSpeedLv - 1));

    // ── 최대 레벨 도달 여부 (UpgradeShopUI 에서 버튼 비활성화용) ──
    // 각 업그레이드가 최대 레벨에 도달했는지 확인하는 값
    // 상점 UI에서 최대 레벨일 경우 버튼을 비활성화하거나 MAX 표시를 하는 데 사용한다.
    public bool IsDamageMaxed => WeaponDamageLv >= maxUpgradeLv;
    public bool IsAmmoMaxed   => MaxAmmoLv      >= maxUpgradeLv;
    public bool IsHpMaxed     => MaxHpLv        >= maxUpgradeLv;
    public bool IsSpeedMaxed  => MoveSpeedLv    >= maxUpgradeLv;

    // ─────────────────────────────────────────────────────
    private void Awake()
    {
        // DataManager는 게임 전체에서 하나만 존재해야 하므로 중복 생성을 방지한다.
        // 이미 Instance가 존재하면 새로 만들어진 DataManager는 제거한다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 최초 생성된 DataManager를 전역 Instance로 등록한다.
        Instance = this;

        // 씬이 바뀌어도 코인, 강화 레벨, 결과 기록이 유지되도록 파괴되지 않게 설정한다.
        DontDestroyOnLoad(gameObject);
    }

    // ── 강화 메서드 ───────────────────────────────────────
    // 공격력 강화 구매 처리
    // 최대 레벨이 아니고 코인이 충분할 때만 비용을 차감하고 레벨을 올린다.
    public bool TryUpgradeDamage()
    {
        if (WeaponDamageLv >= maxUpgradeLv) return false; // 최대 레벨 도달 시 강화 불가
        if (Coin < DamageCost) return false;
        Coin -= DamageCost;
        WeaponDamageLv++;
        return true;
    }

    // 최대 탄창 수 강화 구매 처리
    public bool TryUpgradeAmmo()
    {
        if (MaxAmmoLv >= maxUpgradeLv) return false;
        if (Coin < AmmoCost) return false;
        Coin -= AmmoCost;
        MaxAmmoLv++;
        return true;
    }

    // 최대 체력 강화 구매 처리
    public bool TryUpgradeHp()
    {
        if (MaxHpLv >= maxUpgradeLv) return false;
        if (Coin < HpCost) return false;
        Coin -= HpCost;
        MaxHpLv++;
        return true;
    }

    // 이동 속도 강화 구매 처리
    public bool TryUpgradeSpeed()
    {
        if (MoveSpeedLv >= maxUpgradeLv) return false;
        if (Coin < SpeedCost) return false;
        Coin -= SpeedCost;
        MoveSpeedLv++;
        return true;
    }

    // ── 초기화 ────────────────────────────────────────────
    // 게임을 새로 시작하거나 타이틀로 돌아갈 때 모든 진행 데이터를 초기화한다.
    // 코인, 강화 레벨, 결과 기록, 플레이 시간 타이머 상태를 모두 기본값으로 되돌린다.
    public void ResetAllData()
{
    Coin           = 0;
    WeaponDamageLv = 1;
    MaxAmmoLv      = 1;
    MaxHpLv        = 1;
    MoveSpeedLv    = 1;

    TotalKills = 0;
    TotalEarnedCoin = 0;

    _gameStartTime = 0f;
    _isTimerRunning = false;
}

    // ── 런타임 ─────────────────────────────────────────
    // 새로운 플레이를 시작할 때 호출되는 함수
    // 기존 데이터를 초기화한 뒤, 플레이 시간 측정을 시작한다.
    public void StartNewRun()
{
    ResetAllData();

    _gameStartTime = Time.time;
    _isTimerRunning = true;
}


    // ── 코인 지급 ─────────────────────────────────────────
    // 코인을 지급할 때 현재 보유 코인과 총 획득 코인을 함께 증가시킨다.
    // Coin은 상점 구매에 사용되는 현재 재화이고, TotalEarnedCoin은 ResultScene에 표시할 누적 획득량이다.
    public void AddCoin(int amount)
    {
    Coin += amount;
    TotalEarnedCoin += amount;
    }

    // ── 결과 기록 ─────────────────────────────────────────
    // ResultScene에서 표시할 최종 플레이 결과 데이터
    // TotalKills는 처치한 좀비 수, TotalEarnedCoin은 플레이 중 총 획득한 코인 수를 의미한다.
    public int TotalKills { get; private set; } = 0;
    public int TotalEarnedCoin { get; private set; } = 0;

    // 플레이 시간 측정을 위한 시작 시간과 타이머 작동 여부
    private float _gameStartTime = 0f;
    private bool _isTimerRunning = false;

    // 현재 플레이 시간 계산
    // 타이머가 작동 중이면 현재 시간에서 시작 시간을 뺀 값을 반환한다.
    public float PlayTime
    {
        get
        {
        if (!_isTimerRunning) return 0f;
        return Time.time - _gameStartTime;
        }
    }

    // ResultScene에 표시하기 좋은 "분:초" 형식의 플레이 시간 문자열
    // 예: 125초 → 02:05
    public string PlayTimeText
    {
        get
        {
        int totalSeconds = Mathf.FloorToInt(PlayTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
        }
    }

    // ── 킬 수 증가 ─────────────────────────────────────────
    // 좀비가 사망할 때 ZombieHealth 등에서 호출하여 처치 수를 1 증가시킨다.
    public void AddKill()
    {
    TotalKills++;
    }

    
}