using System.Collections;
using UnityEngine;

// 이 스크립트가 붙은 오브젝트에는 반드시 PlayerController 컴포넌트가 있어야 함을 보장한다.
// 무기 사용은 플레이어 조작과 함께 동작하기 때문에 PlayerController와 같은 오브젝트에서 사용한다.
[RequireComponent(typeof(PlayerController))]
public class WeaponController : MonoBehaviour
{
    [Header("사격")]
    // 총알이 도달할 수 있는 최대 사거리
    [SerializeField] private float     fireRange  = 100f;

    // 총알 Raycast가 맞출 수 있는 대상 Layer
    // Zombie Layer를 연결하여 좀비만 피격 판정이 나도록 사용한다.
    [SerializeField] private LayerMask zombieLayer;

    // FPS 시점 기준으로 Ray를 발사할 카메라
    // 비어 있으면 Start에서 Main Camera를 자동으로 찾는다.
    [SerializeField] private Camera    fpsCam;

    [Header("연사 설정")]
    // 연사 속도 조절값
    // fireRate가 작을수록 더 빠르게 연속 발사된다.
    [SerializeField] private float     fireRate   = 0.12f; // 다음 총알이 나갈 때까지의 대기 시간(초). 작을수록 연사가 빨라짐
    private float _lastFireTime = -99f;                     // 마지막으로 총을 쏜 시점을 체크하기 위한 변수

    [Header("재장전")]
    // 재장전에 걸리는 전체 시간
    [SerializeField] private float reloadTime = 1.8f;

    // 게임 시작 시 가지고 있는 예비 탄약 수
    [SerializeField] private int   startTotalAmmo = 90; // 게임 시작할 때 들고 있는 기본 예비 총알
    
    [Header("재장전 모션")]
    // 재장전 중 움직일 총 모델 Transform
    // 총 전체를 아래로 내리고 기울이는 모션에 사용한다.
    [SerializeField] private Transform weaponModel;

    // 재장전 시 총 모델이 이동할 위치 오프셋
    [SerializeField] private Vector3 reloadPositionOffset = new Vector3(0.15f, -0.25f, -0.1f);

    // 재장전 시 총 모델이 회전할 각도 오프셋
    [SerializeField] private Vector3 reloadRotationOffset = new Vector3(25f, -15f, 10f);

    // 총이 아래로 내려가는 시간과 다시 올라오는 시간
    [SerializeField] private float reloadDownTime = 0.25f;
    [SerializeField] private float reloadUpTime = 0.25f;

    [Header("탄창 재장전 모션")]
    // 재장전 시 따로 움직일 탄창 모델 Transform
    [SerializeField] private Transform magazineModel;

    // 탄창이 빠질 때 이동할 위치 오프셋
    [SerializeField] private Vector3 magazineOutOffset = new Vector3(0f, -0.35f, 0f);

    // 탄창이 빠질 때 회전할 각도 오프셋
    [SerializeField] private Vector3 magazineRotationOffset = new Vector3(0f, 0f, 0f);

    // 탄창이 빠지는 시간, 빠진 상태로 대기하는 시간, 다시 들어가는 시간
    [SerializeField] private float magazineOutTime = 0.2f;
    [SerializeField] private float magazineWaitTime = 0.25f;
    [SerializeField] private float magazineInTime = 0.2f;

    [Header("사운드")]
    // 총 발사음과 재장전음을 재생할 AudioSource
    [SerializeField] private AudioSource weaponAudioSource;

    // 총 발사 사운드와 재장전 사운드 클립
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip reloadClip;
    
    // 총 발사음과 재장전음의 볼륨 조절값
    [SerializeField, Range(0f, 1f)] private float shootVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float reloadVolume = 0.8f;
    


// 탄창의 기본 위치와 회전값 저장용 변수
// 재장전 모션 후 원래 위치로 되돌리기 위해 사용한다.
private Vector3 _magazineDefaultLocalPosition;
private Quaternion _magazineDefaultLocalRotation;

// 총 모델의 기본 위치와 회전값 저장용 변수
// 재장전 모션 후 총을 원래 자세로 복귀시키기 위해 사용한다.
private Vector3 _weaponDefaultLocalPosition;
private Quaternion _weaponDefaultLocalRotation;

    [Header("사격 이펙트 (선택)")]
    // 총구 화염 효과
    // 발사할 때 재생되어 총을 쏘는 시각적 피드백을 준다.
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("타격 이펙트")]
    // 좀비를 맞췄을 때 생성할 피격 이펙트 프리팹
    [SerializeField] private GameObject bloodHitEffectPrefab;

    // 생성된 피격 이펙트가 자동으로 삭제되기까지의 시간
    [SerializeField] private float bloodEffectDestroyTime = 1.5f;

    // 현재 탄창에 들어있는 총알 수
    private int  _currentAmmo; // 현재 탄창에 든 총알

    // 예비 탄약 수
    private int  _totalAmmo;   // 전체 예비 총알

    // 재장전 중인지 확인하는 상태값
    // 재장전 중에는 발사나 중복 재장전을 막는다.
    private bool _isReloading = false;

    // 플레이어가 죽었는지 확인하는 상태값
    // 사망 후에는 사격을 막기 위해 사용한다.
    private bool _isDead      = false;

    // UI 스크립트가 (현재탄창, 예비총알)을 받아가도록 이벤트 변경
    // 탄약 수가 바뀔 때마다 UIManager 등이 구독해서 화면에 표시할 수 있다.
    public event System.Action<int, int> OnAmmoChanged;

    private void Start()
    {
    // 게임 시작 시 현재 탄창은 DataManager의 현재 최대 탄창 수로 설정한다.
    // 업그레이드에 따라 시작 탄창 크기가 달라질 수 있다.
    _currentAmmo = DataManager.Instance.CurrentMaxAmmo;

    // 예비 탄약은 설정한 기본값으로 시작한다.
    _totalAmmo = startTotalAmmo;

    // fpsCam이 비어 있으면 Main Camera를 자동으로 사용한다.
    fpsCam ??= Camera.main;

    // 총 모델의 시작 위치와 회전값 저장
    // 재장전 모션 이후 원래 상태로 복귀할 때 기준으로 사용한다.
    if (weaponModel != null)
    {
        _weaponDefaultLocalPosition = weaponModel.localPosition;
        _weaponDefaultLocalRotation = weaponModel.localRotation;
    }

    // 탄창 모델의 시작 위치와 회전값 저장
    // 탄창이 빠졌다 들어오는 모션의 기준값으로 사용한다.
    if (magazineModel != null)
    {
        _magazineDefaultLocalPosition = magazineModel.localPosition;
        _magazineDefaultLocalRotation = magazineModel.localRotation;
    }

    // 시작 시 현재 탄약 상태를 UI에 알린다.
    OnAmmoChanged?.Invoke(_currentAmmo, _totalAmmo);

    // AudioSource가 인스펙터에 연결되지 않은 경우 현재 오브젝트에서 찾는다.
    if (weaponAudioSource == null)
    {
    weaponAudioSource = GetComponent<AudioSource>();
    }

    // 현재 오브젝트에 AudioSource가 없으면 새로 추가한다.
    if (weaponAudioSource == null)
    {
    weaponAudioSource = gameObject.AddComponent<AudioSource>();
    }

    // 무기 사운드는 시작하자마자 자동 재생되지 않도록 설정한다.
    // 2D 사운드로 설정하여 플레이어에게 항상 일정하게 들리도록 한다.
    weaponAudioSource.playOnAwake = false;
    weaponAudioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        // 마우스가 중앙에 고정되지 않고 풀려있다면(None 상태 = 상점이 열린 상태), 화면 바깥쪽을 눌러도 사격을 차단 ──
        // 상점이나 UI 화면이 열려 있을 때는 마우스 클릭이 사격으로 처리되지 않도록 한다.
        if (Cursor.lockState == CursorLockMode.None) return;

        // 플레이어가 죽었거나 게임 오버 상태라면 무기 조작을 막는다.
        if (_isDead || GameManager.Instance.IsGameOver) return;

        // 재장전 중에는 사격과 추가 재장전을 막는다.
        if (_isReloading) return;

        // 마우스를 꾹 누르고 있고 && 마지막으로 쏜 지 연사 주기(fireRate)만큼 시간이 흘렀을 때만 발사!
        // GetButton을 사용하므로 마우스를 누르고 있으면 연속 사격이 가능하다.
        if (Input.GetButton("Fire1") && Time.time >= _lastFireTime + fireRate)
        {
            _lastFireTime = Time.time; // 마지막 발사 시간 갱신
            TryShoot();
        }

        // R 키를 누르면 재장전 코루틴을 실행한다.
        if (Input.GetKeyDown(KeyCode.R))
            StartCoroutine(Reload());
    }

    // 총 발사 사운드를 재생하는 함수
    // 실제 발사 성공 시 호출된다.
    private void PlayShootSound()
    {
        if (weaponAudioSource != null && shootClip != null)
    {
        weaponAudioSource.PlayOneShot(shootClip, shootVolume);
    }
    
    }

    // 실제 사격 처리를 담당하는 함수
    // 탄약 감소, 총구 이펙트, 발사음, Raycast 피격 판정을 한 번에 처리한다.
    private void TryShoot()
    {
        // 현재 탄창이 비어 있으면 발사하지 않고, 예비 탄약이 있을 경우 자동 재장전을 시도한다.
        if (_currentAmmo <= 0)
        {
            // 탄창이 비었고, 예비 총알이 있다면 자동 재장전
            if (_totalAmmo > 0)
                StartCoroutine(Reload());
            return;
        }

        // 발사 시 현재 탄창에서 총알 1발 감소
        _currentAmmo--;

        // 탄약 UI 갱신을 위해 현재 탄창과 예비 탄약 수를 이벤트로 전달한다.
        OnAmmoChanged?.Invoke(_currentAmmo, _totalAmmo);

        // 총구 화염 이펙트 재생
        muzzleFlash?.Play();

        // 발사 사운드는 명중 여부와 상관없이 실제 총알이 나갈 때 재생된다.
        PlayShootSound();

        // FPS 카메라 위치와 바라보는 방향을 기준으로 Ray를 생성한다.
        Ray ray = new Ray(fpsCam.transform.position, fpsCam.transform.forward);
        RaycastHit hit;

        // 지정한 사거리와 좀비 Layer를 기준으로 Raycast를 쏴서 좀비 피격 여부를 확인한다.
        if (Physics.Raycast(ray, out hit, fireRange, zombieLayer))
        
    {
    // 좀비 타격 시 피 이펙트 생성
    // 맞은 위치보다 약간 앞쪽에 생성하여 표면 안쪽에 묻히지 않게 한다.
    if (bloodHitEffectPrefab != null)
    {
        Vector3 effectPosition = hit.point + hit.normal * 0.03f;
        Quaternion effectRotation = Quaternion.LookRotation(-fpsCam.transform.forward);

        GameObject effect = Instantiate(bloodHitEffectPrefab, effectPosition, effectRotation);
        Destroy(effect, bloodEffectDestroyTime);
    }

    // 맞은 Collider의 부모에서 ZombieHealth를 찾아 데미지를 적용한다.
    // 좀비 모델의 자식 Collider를 맞춰도 부모 좀비 체력에 데미지가 들어가도록 GetComponentInParent를 사용한다.
    ZombieHealth zh = hit.collider.GetComponentInParent<ZombieHealth>();
    zh?.TakeDamage(DataManager.Instance.CurrentDamage);
    }
    }

    // 재장전 사운드를 재생하는 함수
    private void PlayReloadSound()
    {
        if (weaponAudioSource != null && reloadClip != null)
    {
        weaponAudioSource.PlayOneShot(reloadClip, reloadVolume);
    }
    }

    // 재장전 전체 흐름을 담당하는 코루틴
    // 재장전 중복 방지, 총 모션, 탄창 모션, 실제 탄약 보충을 순서대로 처리한다.
    private IEnumerator Reload()
{
    // 이미 재장전 중이면 중복 실행하지 않는다.
    if (_isReloading) yield break;

    // 현재 업그레이드 상태에 따른 최대 탄창 수를 가져온다.
    int maxMagazine = DataManager.Instance.CurrentMaxAmmo;

    // 이미 탄창이 가득 찼거나 예비 탄약이 없으면 재장전하지 않는다.
    if (_currentAmmo == maxMagazine || _totalAmmo <= 0) yield break;

    // 재장전 상태 시작
    _isReloading = true;

    // 재장전 사운드 재생
    PlayReloadSound();

    // 1. 총 전체를 아래로 내리고 살짝 기울임
    // weaponModel이 연결되어 있으면 총 전체가 내려가는 재장전 모션을 실행한다.
    if (weaponModel != null)
    {
        Vector3 targetPos = _weaponDefaultLocalPosition + reloadPositionOffset;
        Quaternion targetRot = _weaponDefaultLocalRotation * Quaternion.Euler(reloadRotationOffset);

        float timer = 0f;
        while (timer < reloadDownTime)
        {
            timer += Time.deltaTime;
            float t = timer / reloadDownTime;

            // 기본 위치/회전에서 목표 위치/회전으로 부드럽게 이동
            weaponModel.localPosition = Vector3.Lerp(_weaponDefaultLocalPosition, targetPos, t);
            weaponModel.localRotation = Quaternion.Slerp(_weaponDefaultLocalRotation, targetRot, t);

            yield return null;
        }

        // 보간 후 정확히 목표 위치와 회전으로 고정
        weaponModel.localPosition = targetPos;
        weaponModel.localRotation = targetRot;
    }

    // 2. 탄창만 빠졌다가 다시 들어가는 모션
    // 실제 탄약을 채우기 전에 탄창 분리/삽입 연출을 먼저 실행한다.
    yield return StartCoroutine(PlayMagazineReloadMotion());

    // 3. 남은 재장전 시간 보정
    // 전체 reloadTime에서 이미 사용한 모션 시간을 제외하고 남은 시간만큼 추가 대기한다.
    float usedMotionTime = reloadDownTime + magazineOutTime + magazineWaitTime + magazineInTime + reloadUpTime;
    float waitTime = Mathf.Max(0f, reloadTime - usedMotionTime);
    yield return new WaitForSeconds(waitTime);

    // 4. 실제 탄창 채우기
    // 필요한 탄약 수만큼 예비 탄약에서 현재 탄창으로 옮긴다.
    int ammoNeeded = maxMagazine - _currentAmmo;

    if (_totalAmmo >= ammoNeeded)
    {
        _currentAmmo += ammoNeeded;
        _totalAmmo -= ammoNeeded;
    }
    else
    {
        // 예비 탄약이 부족하면 남은 예비 탄약 전부를 현재 탄창에 넣는다.
        _currentAmmo += _totalAmmo;
        _totalAmmo = 0;
    }

    // 재장전 후 변경된 탄약 수를 UI에 전달한다.
    OnAmmoChanged?.Invoke(_currentAmmo, _totalAmmo);

    // 5. 총 전체 원래 위치로 복귀
    // 재장전 모션을 끝내고 총 모델을 원래 위치와 회전으로 되돌린다.
    if (weaponModel != null)
    {
        Vector3 startPos = weaponModel.localPosition;
        Quaternion startRot = weaponModel.localRotation;

        float timer = 0f;
        while (timer < reloadUpTime)
        {
            timer += Time.deltaTime;
            float t = timer / reloadUpTime;

            weaponModel.localPosition = Vector3.Lerp(startPos, _weaponDefaultLocalPosition, t);
            weaponModel.localRotation = Quaternion.Slerp(startRot, _weaponDefaultLocalRotation, t);

            yield return null;
        }

        // 복귀 후 위치와 회전값을 정확히 기본값으로 고정한다.
        weaponModel.localPosition = _weaponDefaultLocalPosition;
        weaponModel.localRotation = _weaponDefaultLocalRotation;
    }

    // 재장전 상태 종료
    _isReloading = false;
}

    // 탄창이 빠졌다가 다시 들어가는 세부 재장전 모션
    private IEnumerator PlayMagazineReloadMotion()
    {
    // 탄창 모델이 연결되어 있지 않으면 탄창 모션은 생략한다.
    if (magazineModel == null) yield break;

    // 탄창이 빠져나갈 목표 위치와 회전값 계산
    Vector3 magOutPos = _magazineDefaultLocalPosition + magazineOutOffset;
    Quaternion magOutRot = _magazineDefaultLocalRotation * Quaternion.Euler(magazineRotationOffset);

    // 탄창 빠짐
    // 기본 위치에서 빠져나간 위치까지 부드럽게 이동시킨다.
    float timer = 0f;
    while (timer < magazineOutTime)
    {
        timer += Time.deltaTime;
        float t = timer / magazineOutTime;

        magazineModel.localPosition = Vector3.Lerp(_magazineDefaultLocalPosition, magOutPos, t);
        magazineModel.localRotation = Quaternion.Slerp(_magazineDefaultLocalRotation, magOutRot, t);

        yield return null;
    }

    // 탄창을 정확히 빠진 위치와 회전으로 고정한다.
    magazineModel.localPosition = magOutPos;
    magazineModel.localRotation = magOutRot;

    // 탄창 빠진 상태로 잠깐 대기
    yield return new WaitForSeconds(magazineWaitTime);

    // 탄창 다시 삽입
    // 빠진 위치에서 원래 위치로 부드럽게 되돌린다.
    timer = 0f;
    while (timer < magazineInTime)
    {
        timer += Time.deltaTime;
        float t = timer / magazineInTime;

        magazineModel.localPosition = Vector3.Lerp(magOutPos, _magazineDefaultLocalPosition, t);
        magazineModel.localRotation = Quaternion.Slerp(magOutRot, _magazineDefaultLocalRotation, t);

        yield return null;
    }

    // 탄창을 원래 위치와 회전값으로 정확히 복구한다.
    magazineModel.localPosition = _magazineDefaultLocalPosition;
    magazineModel.localRotation = _magazineDefaultLocalRotation;
    }

    // 총알팩 픽업 시 호출. 이제 탄창이 아니라 '예비 총알'에 추가됨
    // 아이템을 먹었을 때 현재 탄창이 아니라 예비 탄약 수를 증가시킨다.
    public void AddAmmo(int amount)
    {
        _totalAmmo += amount;
        
        // 필요하다면 예비 총알의 소지 한도(예: 최대 999발)를 정하고 싶다면 아래 주석을 풀면 됨.
        // _totalAmmo = Mathf.Min(_totalAmmo, 999);

        // 탄약 수 변경을 UI에 반영한다.
        OnAmmoChanged?.Invoke(_currentAmmo, _totalAmmo);
    }

    // 무기 탄약 상태를 전체 초기화하는 함수
    // 게임 시작 또는 전체 리셋 상황에서 현재 탄창과 예비 탄약을 기본값으로 되돌린다.
    public void RefreshAmmo()
    {
        _currentAmmo = DataManager.Instance.CurrentMaxAmmo;
        _totalAmmo = startTotalAmmo; // 전체 리프레시 시 예비 총알도 리셋
        OnAmmoChanged?.Invoke(_currentAmmo, _totalAmmo);
    }

    // 플레이어 사망 여부를 외부에서 설정하는 함수
    // 사망 상태가 되면 Update에서 사격 처리가 막힌다.
    public void SetDead(bool dead) => _isDead = dead;

    // 업그레이드 적용 후 탄약을 초기화하지 않고 무기 스탯만 갱신하는 함수
    // 웨이브 클리어 후 탄창 업그레이드를 구매해도 현재 탄약 수가 초기화되지 않도록 사용한다.
    public void RefreshWeaponStatsKeepAmmo()
    {
    // 강화 후에도 현재 탄창/예비탄은 초기화하지 않는다.
    int maxMagazine = DataManager.Instance.CurrentMaxAmmo;

    // 혹시 현재 탄창 수가 새 최대 탄창보다 많아지는 경우만 방지
    _currentAmmo = Mathf.Min(_currentAmmo, maxMagazine);

    // 현재 탄약 상태를 UI에 다시 전달한다.
    OnAmmoChanged?.Invoke(_currentAmmo, _totalAmmo);
    }
}