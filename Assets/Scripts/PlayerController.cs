using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────────────────────────

    [Header("이동")]
    [SerializeField] private float gravity = -9.81f; // 중력 가속도

    [Header("마우스 감도")]
    [SerializeField] private float mouseSensitivity = 200f; // 높을수록 시선이 빠르게 돌아감

    [Header("카메라")]
    [SerializeField] private Transform cameraTransform; // 인스펙터에서 Main Camera 연결

    [Header("체력")]
    [SerializeField] private float invincibleTime = 0.5f; // 피격 후 무적 시간 (연속 피격 방지)
    [Header("피격 피드백")]
    [SerializeField] private DamageFeedback damageFeedback;

    // ─────────────────────────────────────────────────────────────
    //  컴포넌트 캐시
    // ─────────────────────────────────────────────────────────────

    private CharacterController _cc; // 이동 처리용 컴포넌트

    // ─────────────────────────────────────────────────────────────
    //  런타임 상태
    // ─────────────────────────────────────────────────────────────

    private float   _xRotation   = 0f;    // 카메라 상하 회전값 (누적)
    private float   _currentHp;           // 현재 체력
    private Vector3 _velocity;            // 중력 적용용 속도 벡터
    private float   _lastHitTime = -999f; // 마지막 피격 시간 (무적 판정용)
    private bool    _isDead      = false; // 사망 여부
    private bool    _canMove     = true;  // 상점 오픈 시 조작을 막을 제어용 스위치

    // ─────────────────────────────────────────────────────────────
    //  이벤트
    // ─────────────────────────────────────────────────────────────

    // 체력이 변할 때마다 UIManager 에 알려주는 이벤트 (현재HP, 최대HP)
    public event System.Action<float, float> OnHpChanged;

    // ─────────────────────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // DataManager 에서 현재 강화 레벨에 맞는 최대 체력으로 초기화
        _currentHp = DataManager.Instance.CurrentMaxHp;
        OnHpChanged?.Invoke(_currentHp, DataManager.Instance.CurrentMaxHp);

        // 게임 시작 시 마우스 커서 숨기고 화면 중앙에 고정
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (damageFeedback == null)
    {
    damageFeedback = GetComponent<DamageFeedback>();
    }
    }

    // ─────────────────────────────────────────────────────────────
    //  매 프레임
    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_isDead) return; // 사망 시 모든 조작 차단
        if (!_canMove) return; // 상점 등 조작 불가 상태면 키보드/마우스 입력 무시!

        HandleMouseLook();
        HandleMovement();
    }

    // ─────────────────────────────────────────────────────────────
    //  마우스 시선 처리
    // ─────────────────────────────────────────────────────────────

    private void HandleMouseLook()
    {
        // 마우스 이동량 읽기 (Time.deltaTime 으로 프레임 독립적으로 만들기)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 상하 회전값 누적 후 -90 ~ 90 도로 제한 (고개가 뒤집히는 것 방지)
        _xRotation -= mouseY;
        _xRotation  = Mathf.Clamp(_xRotation, -90f, 90f);

        // 카메라만 상하로 회전 (localRotation 으로 부모 기준)
        cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // 플레이어 몸체 전체를 좌우로 회전
        transform.Rotate(Vector3.up * mouseX);
    }

    // ─────────────────────────────────────────────────────────────
    //  이동 처리
    // ─────────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        // DataManager 에서 현재 강화된 이동속도 가져오기
        float speed = DataManager.Instance.CurrentMoveSpeed;

        // W/A/S/D 입력값 읽기
        float h = Input.GetAxis("Horizontal"); // A/D
        float v = Input.GetAxis("Vertical");   // W/S

        // 바라보는 방향 기준으로 이동 벡터 계산
        Vector3 move = transform.right * h + transform.forward * v;
        _cc.Move(move * speed * Time.deltaTime);

        // 땅에 닿아 있을 때 중력 초기화 (계속 아래로 가속되는 것 방지)
        if (_cc.isGrounded && _velocity.y < 0)
            _velocity.y = -2f;

        // 중력 가속도 누적
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────
    //  외부 제어용 함수
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// GameManager 등 외부에서 플레이어의 조작 및 마우스 커서 상태를 제어합니다.
    /// </summary>
    public void SetControllable(bool controllable)
    {
        _canMove = controllable;

        if (!controllable)
        {
            // 상점이 열려서 조작이 불가능할 때: 마우스 해제 및 커서 보이기
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
        else
        {
            // 상점이 닫혀서 다시 복구될 때: 마우스 화면 중앙 고정 및 가리기
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  체력 관련
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 좀비가 공격할 때 호출. 무적 시간 중이면 피해 무시.
    /// </summary>
    public void TakeDamage(float damage)
    {
    if (_isDead) return;

    // 마지막 피격으로부터 무적 시간이 지나지 않았으면 무시
    if (Time.time - _lastHitTime < invincibleTime) return;

    _lastHitTime = Time.time;
    _currentHp = Mathf.Max(0f, _currentHp - damage); // 0 아래로 내려가지 않게

    // 피격 화면 빨개짐 + 카메라 흔들림
    if (damageFeedback != null)
    {
        damageFeedback.PlayFeedback();
    }

    OnHpChanged?.Invoke(_currentHp, DataManager.Instance.CurrentMaxHp);

    if (_currentHp <= 0f) Die();
    }


    // 체력 회복 (추후 회복 아이템 등에서 사용 가능).

    public void Heal(float amount)
    {
        float maxHp = DataManager.Instance.CurrentMaxHp;
        _currentHp  = Mathf.Min(maxHp, _currentHp + amount); // 최대 체력 초과 불가
        OnHpChanged?.Invoke(_currentHp, maxHp);
    }


    // 체력 강화 후 UpgradeShopUI → PlayerStats.ApplyAll() 에서 호출.
    // 현재 체력 비율을 유지한 채로 최대 체력을 재적용.

    public void RefreshStats()
    {
        float maxHp = DataManager.Instance.CurrentMaxHp;

        // 강화 전 체력 비율 계산 후 새 최대 체력에 적용
        float ratio = _currentHp / DataManager.Instance.CurrentMaxHp;
        _currentHp  = maxHp * ratio;
        OnHpChanged?.Invoke(_currentHp, maxHp);
    }

    // ─────────────────────────────────────────────────────────────
    //  사망
    // ─────────────────────────────────────────────────────────────

    private void Die()
    {
        _isDead = true;
        GameManager.Instance?.OnGameOver(); // GameManager 에 게임 오버 알림
    }
}