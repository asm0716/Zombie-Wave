using System.Collections;
using UnityEngine;

/// <summary>
/// 좀비 체력 관리.
/// 사망 시 코인 지급 → WaveSpawner 에 킬 카운트 보고 → 오브젝트 제거.
/// </summary>
// 좀비의 체력, 피격 처리, 사망 처리, 코인 지급, 킬 수 기록을 담당하는 스크립트
// WeaponController의 Raycast가 좀비를 맞췄을 때 TakeDamage()가 호출되고,
// 체력이 0 이하가 되면 Die() 코루틴을 통해 사망 애니메이션, 보상 지급, 오브젝트 제거가 진행된다.
public class ZombieHealth : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  인스펙터
    // ─────────────────────────────────────────────────────────────
    [Header("일반 좀비 체력")]
    // 일반 좀비의 최대 체력과 처치 시 지급할 코인 수
    // Basic, Small, Crawl, Tank 등 일반 적으로 취급되는 좀비가 이 값을 사용한다.
    [SerializeField] private float normalMaxHp    = 60f;
    [SerializeField] private int   normalCoinDrop = 10;

    [Header("보스 좀비 체력")]
    // 보스 좀비의 최대 체력과 처치 시 지급할 코인 수
    // ZombieAI에서 isBoss가 true로 설정되면 이 값이 적용된다.
    [SerializeField] private float bossMaxHp    = 500f; // 인스펙터 조절 가능
    [SerializeField] private int   bossCoinDrop = 80;

    [Header("사망 후 제거 딜레이")]
    // 사망 애니메이션이 재생될 시간을 확보하기 위해 바로 삭제하지 않고 일정 시간 뒤 제거한다.
    [SerializeField] private float destroyDelay = 2.5f; // 사망 애니 재생 시간

    [Header("체력바 UI (선택)")]
    // 좀비 머리 위에 표시되는 World Space 체력바 Slider
    // 체력이 줄어들 때 value 값을 비율로 갱신한다.
    [SerializeField] private UnityEngine.UI.Slider hpSlider; // World Space Canvas Slider

    [Header("사운드")]
    // 좀비 피격음과 사망음을 재생하기 위한 오디오 관련 변수
    // zombieAudioSource는 피격음을 재생하고, deathClip은 PlayClipAtPoint로 재생한다.
    [SerializeField] private AudioSource zombieAudioSource;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip deathClip;

    // 피격음과 사망음의 볼륨 조절값
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 0.8f;

    // ─────────────────────────────────────────────────────────────
    //  런타임
    // ─────────────────────────────────────────────────────────────
    // 현재 좀비가 죽었는지 여부
    // 한 번 죽은 좀비가 중복으로 데미지를 받거나 보상을 여러 번 지급하지 않도록 막는 데 사용한다.
    public bool IsDead { get; private set; } = false;

    // 현재 체력, 최대 체력, 처치 시 지급할 코인 값
    // InitHealth()에서 일반 좀비/보스 여부에 따라 초기화된다.
    private float     _currentHp;
    private float     _maxHp;
    private int       _coinDrop;

    // 카메라 Transform 저장용
    // 체력바가 항상 플레이어 카메라를 바라보게 만드는 빌보드 처리에 사용한다.
    private Transform _cam; // 카메라 Transform (체력바 빌보드용)

    // ─────────────────────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────────────────────
    private void Start()
    {
        // 메인 카메라 캐시 (체력바가 항상 카메라를 바라보게 하기 위함)
        _cam = Camera.main.transform;

        // 인스펙터에서 AudioSource가 연결되지 않은 경우 현재 오브젝트에서 찾아 사용한다.
        if (zombieAudioSource == null)
    {
        zombieAudioSource = GetComponent<AudioSource>();
    }

    // 현재 오브젝트에 AudioSource가 없으면 새로 추가한다.
    if (zombieAudioSource == null)
    {
        zombieAudioSource = gameObject.AddComponent<AudioSource>();
    }

    // 좀비 사운드는 시작하자마자 재생되지 않게 하고,
    // 위치감을 주기 위해 3D 사운드로 설정한다.
    zombieAudioSource.playOnAwake = false;
    zombieAudioSource.spatialBlend = 1f;
    }

    // ─────────────────────────────────────────────────────────────
    //  체력바 빌보드 (매 프레임 카메라 방향으로 회전)
    // ─────────────────────────────────────────────────────────────
    private void LateUpdate()
    {
        // 체력바가 연결되어 있고 카메라가 있을 때만 실행
        if (hpSlider == null || _cam == null) return;

        // 카메라와 같은 방향으로 체력바 회전
        // World Space 체력바가 어느 방향에서 보더라도 플레이어 카메라를 향하게 만든다.
        hpSlider.transform.LookAt(hpSlider.transform.position + _cam.forward);
    }

    // ─────────────────────────────────────────────────────────────
    //  체력 초기화 (ZombieAI.ApplyStats() 에서 isBoss 결정 후 호출)
    // ─────────────────────────────────────────────────────────────
    public void InitHealth(bool isBoss)
    {
        // 보스 여부에 따라 최대 체력과 코인 보상을 다르게 설정한다.
        _maxHp    = isBoss ? bossMaxHp    : normalMaxHp;
        _coinDrop = isBoss ? bossCoinDrop : normalCoinDrop;

        // 현재 체력을 최대 체력으로 초기화하고 사망 상태를 해제한다.
        _currentHp = _maxHp;
        IsDead    = false;

        // 체력바 초기화
        // Slider는 0~1 비율로 사용하므로 최대 체력 상태를 1로 표시한다.
        if (hpSlider != null)
        {
            hpSlider.minValue = 0;
            hpSlider.maxValue = 1;
            hpSlider.value    = 1; // 최대 체력 = 1
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  피격 (WeaponController 의 Raycast 가 호출)
    // ─────────────────────────────────────────────────────────────

    // 좀비가 총에 맞았을 때 피격 사운드를 재생하는 함수
    private void PlayHitSound()
{
    if (zombieAudioSource != null && hitClip != null)
    {
        zombieAudioSource.PlayOneShot(hitClip, hitVolume);
    }
}

    // 외부에서 좀비에게 데미지를 줄 때 호출되는 함수
    // WeaponController가 Raycast로 좀비를 맞췄을 때 현재 공격력만큼 damage를 전달한다.
    public void TakeDamage(float damage)
    {
        // 이미 죽은 좀비는 더 이상 데미지를 받지 않도록 처리한다.
        if (IsDead) return;

        // 현재 체력에서 받은 데미지만큼 감소시키고, 0 아래로 내려가지 않도록 제한한다.
        _currentHp = Mathf.Max(0f, _currentHp - damage);

        // 체력바 갱신
        // 현재 체력 / 최대 체력 비율로 Slider 값을 계산한다.
        if (hpSlider != null)
            hpSlider.value = _currentHp / _maxHp; // 비율로 계산

        // 체력이 0 이하면 사망 처리
        // 사망 처리는 코루틴으로 실행하여 사망 애니메이션 후 제거되도록 한다.
        if (_currentHp <= 0f)
            StartCoroutine(Die());

        // 피격 사운드 재생
        PlayHitSound();
    }
    

    // ─────────────────────────────────────────────────────────────
    //  사망 
    // ─────────────────────────────────────────────────────────────

    // 좀비 사망 사운드를 재생하는 함수
    // PlayClipAtPoint를 사용하여 좀비 오브젝트가 삭제되어도 사운드가 끝까지 들리도록 한다.
    private void PlayDeathSound()
{
    if (deathClip != null)
    {
        AudioSource.PlayClipAtPoint(deathClip, transform.position, deathVolume);
    }
}

    // 좀비 사망 처리 코루틴
    // 사망 애니메이션 재생, 충돌/이동 비활성화, 보상 지급, 웨이브 카운트 보고, 오브젝트 제거를 담당한다.
    private IEnumerator Die()
    {
        // 사망 상태로 변경하여 추가 데미지나 중복 사망 처리를 막는다.
        IsDead = true;

        // 사망 효과음 재생
        PlayDeathSound();

        // 1. AI에게 먼저 사망 사실을 알려서 애니메이션을 틀고 에이전트를 멈추게 합니다.
        // ZombieAI에서 NavMeshAgent를 정지시키고 Dead 애니메이션 트리거를 실행한다.
        GetComponent<ZombieAI>()?.OnDead();

        // 2. 그 직후에 콜라이더와 에이전트를 안전하게 꺼줍니다.
        // 죽은 좀비가 플레이어나 총알과 계속 충돌하지 않도록 Collider를 비활성화한다.
        Collider zombieCollider = GetComponent<Collider>();
        if (zombieCollider != null)
        {
            zombieCollider.enabled = false;
        }

        // 사망 후 더 이상 NavMeshAgent가 이동 계산을 하지 않도록 비활성화한다.
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        // 3. 시체 위에 둥둥 떠 있는 체력바 Slider UI도 안 보이게 처리
        if (hpSlider != null)
        {
            hpSlider.gameObject.SetActive(false);
        }

        // 코인 지급
        // 현재 보유 코인과 ResultScene에 표시할 총 획득 코인이 함께 증가한다.
        DataManager.Instance?.AddCoin(_coinDrop);

        //킬 수 증가
        // ResultScene에서 표시할 총 처치 수를 증가시킨다.
        DataManager.Instance?.AddKill();

        // UIManager 코인 표시 갱신
        // 상점이나 HUD에 표시되는 현재 코인 수를 최신 값으로 갱신한다.
        UIManager.Instance?.UpdateCoin(DataManager.Instance.Coin);

        // WaveSpawner 에 킬 카운트 보고
        // 살아있는 좀비 목록에서 제거하고, 현재 웨이브 킬 카운트를 증가시킨다.
        WaveSpawner.Instance?.OnZombieDied(gameObject);

        // 사망 애니메이션 재생 후 오브젝트 제거
        // destroyDelay만큼 기다린 뒤 좀비 오브젝트를 삭제한다.
        yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);

    }
}