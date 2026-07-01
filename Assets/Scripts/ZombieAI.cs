using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent 기반 좀비 AI.
/// isBoss = true 시 보스 수치(체력·공격·속도·크기)가 자동 적용된다.
/// WaveSpawner.SetBoss() 로 외부에서 플래그를 주입한다.
/// </summary>
// 좀비의 이동, 추적, 공격, 사망 애니메이션 처리를 담당하는 AI 스크립트
// NavMeshAgent를 이용해 플레이어를 추적하고, 일정 거리 안에 들어오면 공격을 시도한다.
// 일반 좀비와 보스 좀비의 수치를 구분해서 적용할 수 있도록 구성되어 있다.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(ZombieHealth))]
public class ZombieAI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  인스펙터 — 일반 좀비
    // ─────────────────────────────────────────────────────────────
    [Header("일반 좀비 — 이동")]
    // 일반 좀비가 플레이어를 추적할 때 사용하는 이동 속도와 가속도
    [SerializeField] private float normalMoveSpeed   = 3.5f;
    [SerializeField] private float normalAcceleration = 8f;

    [Header("일반 좀비 — 공격")]
    // 일반 좀비의 공격 데미지, 공격 가능 거리, 공격 속도 설정
    // normalAttackRate는 초당 공격 횟수로, 값이 높을수록 더 자주 공격한다.
    [SerializeField] private float normalAttackDamage = 10f;
    [SerializeField] private float normalAttackRange  = 1.8f;
    [SerializeField] private float normalAttackRate   = 1.0f;   // 초당 공격 횟수

    // ─────────────────────────────────────────────────────────────
    //  인스펙터 — 보스 좀비
    // ─────────────────────────────────────────────────────────────
    [Header("보스 좀비 — 이동")]
    // 보스 좀비 전용 이동 속도와 가속도
    // 일반 좀비보다 느리지만 더 강한 적으로 사용하기 위한 값이다.
    [SerializeField] private float bossMoveSpeed    = 2.5f;
    [SerializeField] private float bossAcceleration = 5f;

    [Header("보스 좀비 — 공격")]
    // 보스 좀비 전용 공격 수치
    // 공격력과 공격 범위가 일반 좀비보다 크게 설정될 수 있다.
    [SerializeField] private float bossAttackDamage = 20f;
    [SerializeField] private float bossAttackRange  = 2.5f;
    [SerializeField] private float bossAttackRate   = 0.6f;

    [Header("보스 좀비 — 외형")]
    // 보스 좀비로 설정될 경우 적용할 크기
    // SetBoss(true)가 호출되면 이 값으로 transform.localScale이 변경된다.
    [SerializeField] private Vector3 bossScale      = new Vector3(1.8f, 1.8f, 1.8f);

    // ─────────────────────────────────────────────────────────────
    //  런타임
    // ─────────────────────────────────────────────────────────────
    // 현재 좀비가 보스인지 여부
    // WaveSpawner에서 스폰 직후 SetBoss()를 호출하여 설정한다.
    public  bool IsBoss { get; private set; } = false;

    // 좀비 이동을 담당하는 NavMeshAgent
    private NavMeshAgent    _agent;

    // 좀비 체력과 사망 상태를 관리하는 ZombieHealth
    private ZombieHealth    _health;

    // 좀비 애니메이션 제어용 Animator
    // 에셋에 Animator가 없을 수도 있으므로 null을 허용한다.
    private Animator        _anim;          // 선택 (에셋에 따라)

    // 추적 대상인 플레이어 Transform
    private Transform       _player;

    // 현재 적용된 공격 수치
    // 일반 좀비인지 보스인지에 따라 ApplyStats()에서 값이 달라진다.
    private float _attackDamage;
    private float _attackRange;
    private float _attackRate;

    // 마지막으로 공격한 시간
    // 공격 쿨타임을 계산하기 위해 사용한다.
    private float _lastAttackTime;

    // Animator 파라미터 해시 (에셋 Animator 파라미터명과 맞춰야 함)
    // 문자열을 매번 직접 사용하는 대신 해시값으로 저장하여 애니메이션 파라미터를 효율적으로 제어한다.
    private static readonly int HashSpeed    = Animator.StringToHash("Speed");
    private static readonly int HashAttack   = Animator.StringToHash("Attack");
    private static readonly int HashDead     = Animator.StringToHash("Dead");

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        // 같은 오브젝트에 붙어 있는 필수 컴포넌트를 가져온다.
        // RequireComponent로 보장되어 있으므로 NavMeshAgent와 ZombieHealth는 반드시 존재해야 한다.
        _agent  = GetComponent<NavMeshAgent>();
        _health = GetComponent<ZombieHealth>();

        // Animator는 모델 자식 오브젝트에 붙어 있을 수 있으므로 자식까지 검색한다.
        _anim = GetComponentInChildren<Animator>(); // 없으면 null 허용
    }

    private void Start()
    {
        // 플레이어 찾기
        // 플레이어 오브젝트는 "Player" 태그를 기준으로 탐색한다.
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _player = playerGO.transform;

        // 일반 좀비 또는 보스 좀비에 맞는 이동/공격/체력 수치를 적용한다.
        ApplyStats();
    }

    private void Update()
    {
        // 좀비가 이미 죽었거나 플레이어를 찾지 못한 경우 AI 동작을 중단한다.
        if (_health.IsDead || _player == null) return;

        // 게임 오버 상태라면 좀비의 추적과 공격을 멈춘다.
        if (GameManager.Instance.IsGameOver)   return;

        // 플레이어 위치로 이동하고, 공격 범위 안에 있으면 공격을 시도한다.
        ChasePlayer();
        TryAttack();
    }

    // ─────────────────────────────────────────────────────────────
    //  수치 적용
    // ─────────────────────────────────────────────────────────────
    /// <summary>WaveSpawner 가 Instantiate 직후 호출</summary>
    // 외부에서 이 좀비가 보스인지 일반 좀비인지 설정하는 함수
    // WaveSpawner가 보스 프리팹을 생성한 직후 SetBoss(true)를 호출하여 보스 수치를 적용한다.
    public void SetBoss(bool isBoss)
    {
        IsBoss = isBoss;
        ApplyStats();
    }

    // 현재 IsBoss 값에 따라 이동 속도, 공격력, 공격 범위, 공격 속도, 체력을 적용한다.
    private void ApplyStats()
    {
        if (IsBoss)
        {
            // 보스 좀비일 경우 보스 전용 이동/공격 수치를 NavMeshAgent와 내부 공격 변수에 적용한다.
            _agent.speed        = bossMoveSpeed;
            _agent.acceleration = bossAcceleration;
            _attackDamage       = bossAttackDamage;
            _attackRange        = bossAttackRange;
            _attackRate         = bossAttackRate;

            // 보스는 일반 좀비보다 크게 보이도록 스케일을 변경한다.
            transform.localScale = bossScale;
        }
        else
        {
            // 일반 좀비일 경우 일반 좀비 전용 이동/공격 수치를 적용한다.
            _agent.speed        = normalMoveSpeed;
            _agent.acceleration = normalAcceleration;
            _attackDamage       = normalAttackDamage;
            _attackRange        = normalAttackRange;
            _attackRate         = normalAttackRate;
        }

        // ZombieHealth에도 보스 여부를 전달하여 체력 값을 초기화한다.
        _health.InitHealth(IsBoss);
    }

    // ─────────────────────────────────────────────────────────────
    //  추적
    // ─────────────────────────────────────────────────────────────
    private void ChasePlayer()
    {
        // NavMeshAgent의 목적지를 플레이어 위치로 설정하여 자동으로 길을 찾아 이동하게 한다.
        _agent.SetDestination(_player.position);

        // Animator 속도 파라미터 (에셋에 없으면 오류 없이 패스)
        // 실제 이동 속도를 기준으로 Speed 값을 전달하여 Idle/Walk/Run 애니메이션 전환에 사용한다.
        if (_anim != null)
    {
    float speedPercent = _agent.velocity.magnitude / Mathf.Max(_agent.speed, 0.01f);
    _anim.SetFloat(HashSpeed, speedPercent, 0.1f, Time.deltaTime);
    }
    }

    // ─────────────────────────────────────────────────────────────
    //  공격
    // ─────────────────────────────────────────────────────────────
    private void TryAttack()
    {
        // 좀비와 플레이어 사이의 거리를 계산한다.
        float dist = Vector3.Distance(transform.position, _player.position);

        // 공격 범위 밖이면 공격하지 않는다.
        if (dist > _attackRange) return;

        // 공격 속도를 기반으로 공격 쿨타임을 계산한다.
        // 예: _attackRate가 1이면 1초에 1번 공격한다.
        float cooldown = 1f / _attackRate;

        // 아직 쿨타임이 지나지 않았으면 공격하지 않는다.
        if (Time.time - _lastAttackTime < cooldown) return;

        // 이번 공격 시간을 기록하여 다음 공격 쿨타임 계산에 사용한다.
        _lastAttackTime = Time.time;

        // Animator 공격 트리거
        // Attack 트리거가 있는 Animator Controller에서는 공격 모션으로 전환된다.
        _anim?.SetTrigger(HashAttack);

        // 플레이어 피해 적용
        // 플레이어 오브젝트에서 PlayerController를 가져와 데미지를 전달한다.
        PlayerController pc = _player.GetComponent<PlayerController>();
        pc?.TakeDamage(_attackDamage);
    }

    // ─────────────────────────────────────────────────────────────
    //  사망 처리 (ZombieHealth 가 호출)
    // ─────────────────────────────────────────────────────────────
    // ZombieHealth에서 체력이 0이 되었을 때 호출하는 함수
    // 이동을 멈추고 NavMeshAgent를 비활성화한 뒤 사망 애니메이션을 실행한다.
    public void OnDead()
    {
        // 사망한 좀비가 더 이상 플레이어를 추적하지 않도록 정지시킨다.
        _agent.isStopped = true;

        // NavMeshAgent를 비활성화하여 사망 후 이동 계산을 멈춘다.
        _agent.enabled   = false;

        // Dead 트리거를 보내 사망 애니메이션으로 전환한다.
        _anim?.SetTrigger(HashDead);
    }
}