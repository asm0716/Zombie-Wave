using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 스폰 시스템.
/// - 스폰된 모든 좀비를 처치해야 웨이브가 클리어되는 전멸전 방식.
/// - 웨이브 3(보스전): 보스가 등장하며, 동시에 일반 좀비들도 부하로 끊임없이 스폰됩니다.
/// </summary>
// 각 스테이지에서 좀비 웨이브를 생성하고 관리하는 스크립트
// 웨이브별로 스폰 주기, 한 번에 나오는 수, 총 스폰 수를 다르게 설정할 수 있다.
// Stage1, Stage2, Stage3에서 서로 다른 좀비 프리팹 배열을 연결해 난이도와 적 종류를 다르게 구성한다.
public class WaveSpawner : MonoBehaviour
{
    // 다른 스크립트(GameManager, UpgradeShopUI 등)에서 현재 웨이브 정보를 접근할 수 있도록 하는 싱글톤 인스턴스
    public static WaveSpawner Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────────────────────────
    [Header("프리팹")]
    [SerializeField] private GameObject zombiePrefab;   // 일반 좀비
    [SerializeField] private GameObject bossPrefab;     // 보스 좀비

    [Header("웨이브별 일반 좀비 프리팹")]
    // 웨이브마다 등장할 일반 좀비 프리팹 목록
    // 배열에 같은 프리팹을 여러 번 넣으면 해당 좀비의 등장 확률을 높일 수 있다.
    // 예: Basic, Basic, Tank를 넣으면 Basic이 Tank보다 더 자주 등장한다.
    [SerializeField] private GameObject[] wave1ZombiePrefabs;
    [SerializeField] private GameObject[] wave2ZombiePrefabs;
    [SerializeField] private GameObject[] wave3ZombiePrefabs;

    [Header("스폰 포인트")]
    // 좀비가 등장할 위치들
    // 씬 안에 배치한 빈 오브젝트들을 연결하고, 스폰 시 이 중 하나를 랜덤 선택한다.
    [SerializeField] private Transform[] spawnPoints;   // 맵에 배치한 빈 오브젝트들

    [Header("웨이브 1 설정")]
    // 첫 번째 웨이브 설정
    // 주로 기본 좀비 중심으로 구성하여 플레이어가 조작과 전투 흐름을 익히는 구간이다.
    [SerializeField] private float wave1Interval     = 3f;  // 스폰 주기(초)
    [SerializeField] private int   wave1SpawnCount   = 1;   // 한 번에 스폰 수
    [SerializeField] private int   wave1TotalCount   = 10;  // 이 웨이브에 '총 생성할' 좀비 수

    [Header("웨이브 2 설정")]
    // 두 번째 웨이브 설정
    // 스폰 주기가 짧아지거나 한 번에 나오는 수가 늘어나면서 난이도가 상승한다.
    [SerializeField] private float wave2Interval     = 2f;  // 스폰 주기(초)
    [SerializeField] private int   wave2SpawnCount   = 2;   // 한 번에 스폰 수
    [SerializeField] private int   wave2TotalCount   = 10;  // 이 웨이브에 '총 생성할' 좀비 수

    [Header("웨이브 3 (보스 + 부하 좀비)")]
    // 세 번째 웨이브 설정
    // 보스가 먼저 등장하고, 추가 일반 좀비가 계속 스폰되는 최종 웨이브 역할을 한다.
    [SerializeField] private float wave3Interval     = 2.5f; // 보스전 중 일반 좀비 스폰 주기
    [SerializeField] private int   wave3SpawnCount   = 1;    // 보스전 중 한 번에 나오는 일반 좀비 수
    [SerializeField] private int   wave3TotalCount   = 20;   // 보스 외에 추가로 나올 일반 좀비 총 수

    [Header("최적화")]
    // 필드에 동시에 존재할 수 있는 좀비 수 제한
    // 너무 많은 좀비가 한꺼번에 생성되어 프레임이 떨어지거나 난이도가 과도하게 상승하는 것을 방지한다.
    [SerializeField] private int   maxZombiesOnField = 15;  // 동시 존재 최대 좀비 수

    [Header("웨이브 시작 딜레이")]
    // 웨이브가 시작되기 전 잠깐의 대기 시간
    // 플레이어가 준비할 시간을 주거나 UI에 웨이브 정보를 보여주기 위해 사용한다.
    [SerializeField] private float waveStartDelay    = 3f;  // 웨이브 시작 전 대기

    // ─────────────────────────────────────────────────────────────
    //  런타임 상태
    // ─────────────────────────────────────────────────────────────
    // 현재 진행 중인 웨이브 번호
    // 내부적으로는 0부터 시작하지만, UI에는 CurrentWave + 1로 표시한다.
    public int  CurrentWave    { get; private set; } = 0; // 0-based

    // 현재 웨이브에서 처치한 좀비 수
    // ZombieHealth에서 좀비 사망 시 OnZombieDied를 호출하여 증가한다.
    public int  KillCount      { get; private set; } = 0; // 현재 웨이브에서 죽인 수

    // 현재 스폰 루프가 진행 중인지 나타내는 값
    public bool IsSpawning     { get; private set; } = false;

    // 상점이 열렸거나 웨이브 진행을 일시정지해야 할 때 사용하는 값
    private bool _isPaused     = false;

    // 이번 웨이브에서 지금까지 실제로 생성한 좀비 수
    // waveTotalCount와 비교하여 목표 스폰 수를 채웠는지 판단한다.
    private int  _zombiesSpawnedInThisWave = 0; // 이번 웨이브에 지금까지 스폰된 총 좀비 수

    // 현재 필드에 살아있는 좀비 목록
    // 웨이브 클리어 조건을 확인하기 위해 살아있는 좀비들을 추적한다.
    private readonly List<GameObject> _aliveZombies = new();

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        // WaveSpawner 역시 씬 안에서 하나만 사용되도록 싱글톤 형태로 관리한다.
        // 중복으로 존재하면 웨이브 진행이나 스폰이 꼬일 수 있으므로 기존 인스턴스가 있으면 제거한다.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 씬이 시작되면 첫 번째 웨이브부터 시작한다.
        // 0은 내부 기준 웨이브 1을 의미한다.
        StartCoroutine(StartWave(0));
    }

    // ─────────────────────────────────────────────────────────────
    //  웨이브 시작
    // ─────────────────────────────────────────────────────────────
    public void StartNextWave()
    {
        // 현재 웨이브 다음 번호를 계산한다.
        int next = CurrentWave + 1;

        // 총 3개의 웨이브만 사용하므로, 웨이브 3 이후에는 더 이상 시작하지 않는다.
        if (next >= 3) return; // 웨이브 3 이후는 없음

        // 다음 웨이브를 코루틴으로 시작한다.
        StartCoroutine(StartWave(next));
    }

    private IEnumerator StartWave(int waveIndex)
    {
        // 새 웨이브가 시작될 때 관련 상태값들을 초기화한다.
        CurrentWave = waveIndex;
        KillCount   = 0;
        _zombiesSpawnedInThisWave = 0;
        IsSpawning  = false;

        // 혹시 남아있을지 모르는 데드 좀비 리스트 청소
        // 이전 웨이브나 씬 전환 과정에서 남은 참조가 있으면 전멸 판정이 꼬일 수 있으므로 초기화한다.
        _aliveZombies.Clear();

        // 현재 웨이브 번호를 UI에 표시한다.
        // 내부 값은 0부터 시작하므로 플레이어에게는 1을 더해 보여준다.
        UIManager.Instance?.UpdateWave(CurrentWave + 1); // 1-based 표시

        // 웨이브 시작 전 대기 시간
        // 플레이어가 숨을 돌리거나 화면에 웨이브 정보를 표시할 시간을 준다.
        yield return new WaitForSeconds(waveStartDelay);

        // 모든 웨이브는 공통된 루프를 사용합니다.
        // 웨이브별 값만 다르게 적용하고, 실제 스폰/전멸 체크는 SpawnLoop에서 처리한다.
        StartCoroutine(SpawnLoop(waveIndex));
    }

    // ─────────────────────────────────────────────────────────────
    //  공통 웨이브 스폰 루프 (전멸전 방식)
    // ─────────────────────────────────────────────────────────────
    private IEnumerator SpawnLoop(int waveIndex)
    {
        // 스폰 루프가 시작되었음을 표시한다.
        IsSpawning = true;

        // 1. 웨이브별 스폰 세팅 데이터 분기
        // 기본값은 웨이브 1 설정으로 두고, waveIndex에 따라 웨이브 2 또는 3 값으로 교체한다.
        float spawnInterval = wave1Interval;
        int   spawnCount    = wave1SpawnCount;
        int   totalGoal     = wave1TotalCount;

        if (waveIndex == 1)
        {
            // 웨이브 2 설정 적용
            spawnInterval = wave2Interval;
            spawnCount    = wave2SpawnCount;
            totalGoal     = wave2TotalCount;
        }
        else if (waveIndex == 2)
        {
            // 웨이브 3 설정 적용
            spawnInterval = wave3Interval;
            spawnCount    = wave3SpawnCount;
            totalGoal     = wave3TotalCount;

            // [웨이브 3 특권] 루프 시작하자마자 보스를 즉시 먼저 스폰!
            // 일반 좀비 스폰 루프와 별개로 보스가 먼저 등장하도록 한다.
            SpawnZombie(true, waveIndex);
        }

        // 2. 총 목표 소환 개수를 다 채울 때까지 스폰 굴리기
        // _zombiesSpawnedInThisWave가 totalGoal에 도달할 때까지 반복한다.
        while (_zombiesSpawnedInThisWave < totalGoal)
        {
            // 상점이 열렸거나 일시정지 상태이면 스폰을 멈추고 다음 프레임까지 대기한다.
            if (_isPaused)
            {
                yield return null;
                continue;
            }

            // 파괴된 좀비가 리스트에 남아있을 수 있으므로 주기적으로 정리한다.
            CleanupDeadZombies();

            // 필드에 자리가 있고, 아직 더 소환해야 할 좀비가 남아있다면 스폰
            // 동시에 너무 많은 좀비가 존재하지 않도록 maxZombiesOnField로 제한한다.
            if (_aliveZombies.Count < maxZombiesOnField)
            {
                // 이번 웨이브에서 남은 스폰 수
                int remainingToSpawn = totalGoal - _zombiesSpawnedInThisWave;

                // 현재 필드 제한상 추가로 생성 가능한 수
                int maxCanSpawn = maxZombiesOnField - _aliveZombies.Count;

                // 한 번에 생성할 수, 남은 목표 수, 필드 여유 수 중 가장 작은 값을 실제 스폰 수로 사용한다.
                int toSpawn = Mathf.Min(spawnCount, remainingToSpawn, maxCanSpawn);

                for (int i = 0; i < toSpawn; i++)
                {
                    // 일반 좀비 스폰
                    // waveIndex에 따라 wave1/2/3 전용 프리팹 배열에서 랜덤으로 선택된다.
                    SpawnZombie(false, waveIndex); // 일반 좀비 스폰
                }
            }

            // 다음 스폰까지 정해진 시간만큼 대기한다.
            yield return new WaitForSeconds(spawnInterval);
        }

        // 3. [핵심] 정해진 좀비를 다 채워서 스폰은 종료됨!
        // 더 이상 새로운 좀비는 생성하지 않지만, 이미 필드에 있는 좀비는 계속 남아있을 수 있다.
        IsSpawning = false;

        // 4. [핵심] 스폰은 끝났지만, 맵에 남은 좀비가 단 1마리라도 있다면 다 죽을 때까지 대기!
        // 전멸전 방식이기 때문에 목표 수를 모두 스폰했다고 바로 클리어하지 않는다.
        // 살아있는 좀비가 0마리가 될 때까지 계속 확인한다.
        while (true)
        {
            CleanupDeadZombies();
            if (_aliveZombies.Count == 0) break; // 필드에 좀비가 아예 없으면 루프 탈출!
            yield return new WaitForSeconds(0.5f); // 0.5초마다 체크하며 대기
        }

        // 5. 완벽하게 전멸시켰으므로 웨이브 클리어 처리!
        // GameManager가 상점 표시, 코인 보상, 다음 웨이브 또는 다음 스테이지 이동을 담당한다.
        GameManager.Instance?.OnWaveClear(waveIndex);
    }

    // 현재 필드에 있는 좀비 전부 제거 (필요할 때만 호출)
    // 웨이브 클리어 처리나 씬 전환 전 남은 좀비를 정리하기 위해 사용한다.
    public void ClearAllZombies()
    {
        foreach (var z in _aliveZombies)
            if (z != null) Destroy(z);
        _aliveZombies.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    //  스폰 헬퍼
    // ─────────────────────────────────────────────────────────────
    private void SpawnZombie(bool isBoss, int waveIndex)
    {
    // 스폰 포인트가 설정되어 있지 않으면 좀비를 생성할 위치가 없으므로 종료한다.
    if (spawnPoints == null || spawnPoints.Length == 0) return;

    // 랜덤 스폰 포인트 선택
    // 등록된 스폰 포인트 중 하나를 무작위로 골라 좀비를 생성한다.
    Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];

    // 보스 스폰이면 bossPrefab을 사용하고, 일반 좀비면 웨이브별 랜덤 프리팹을 가져온다.
    GameObject prefab = isBoss ? bossPrefab : GetRandomZombiePrefabForWave(waveIndex);
    if (prefab == null) return;

    // 선택된 위치와 회전값으로 좀비 프리팹을 실제 씬에 생성한다.
    GameObject go = Instantiate(prefab, sp.position, sp.rotation);

    // 보스 플래그 세팅
    // ZombieAI에 보스 여부를 전달하여 체력, 이동속도, 공격력 등의 보스 설정을 적용할 수 있게 한다.
    ZombieAI ai = go.GetComponent<ZombieAI>();
    if (ai != null)
    {
        ai.SetBoss(isBoss);
    }

    // 생성된 좀비를 살아있는 좀비 목록에 등록하여 전멸 여부를 추적한다.
    _aliveZombies.Add(go);

    // 일반 좀비든 보스든 스폰 카운트를 올려서 기획 개수를 맞춥니다.
    // 이번 웨이브에서 총 몇 마리를 생성했는지 계산하는 데 사용한다.
    _zombiesSpawnedInThisWave++;
    }

    // ─────────────────────────────────────────────────────────────
    //  좀비 사망 콜백 (ZombieHealth 가 호출)
    // ─────────────────────────────────────────────────────────────
    public void OnZombieDied(GameObject zombie)
    {
        // 죽은 좀비가 살아있는 목록에 있으면 제거한다.
        // 리스트에서 제거되어야 웨이브 클리어 조건인 _aliveZombies.Count == 0이 정상적으로 작동한다.
        if (_aliveZombies.Contains(zombie))
        {
            _aliveZombies.Remove(zombie);
        }

        // 현재 웨이브에서 처치한 좀비 수를 증가시킨다.
        KillCount++;
    }

    // ─────────────────────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────────────────────
    private void CleanupDeadZombies()
    {
        // 이미 파괴되어서 null이 된 좀비 오브젝트들을 리스트에서 완전히 제거
        // Destroy된 오브젝트가 리스트에 남아있으면 웨이브 클리어 판정이 늦어질 수 있다.
        _aliveZombies.RemoveAll(z => z == null);
    }

    // GameManager 가 호출
    // 상점이 열렸을 때는 스폰을 멈추고, 상점이 닫히면 다시 스폰을 재개하는 데 사용한다.
    public void PauseSpawning(bool pause) => _isPaused = pause;

    private GameObject GetRandomZombiePrefabForWave(int waveIndex)
    {
    // 현재 웨이브 번호에 따라 사용할 좀비 프리팹 배열을 선택한다.
    GameObject[] prefabs = null;

    if (waveIndex == 0)
    {
        prefabs = wave1ZombiePrefabs;
    }
    else if (waveIndex == 1)
    {
        prefabs = wave2ZombiePrefabs;
    }
    else if (waveIndex == 2)
    {
        prefabs = wave3ZombiePrefabs;
    }

    // 웨이브별 배열이 비어 있거나 설정되지 않은 경우 기본 zombiePrefab을 사용한다.
    // 프리팹 배열 설정 누락으로 스폰이 완전히 멈추는 것을 방지하기 위한 예비 처리이다.
    if (prefabs == null || prefabs.Length == 0)
    {
        return zombiePrefab;
    }

    // 배열 안에 null이 섞여 있을 수 있으므로 최대 20번까지 유효한 프리팹을 뽑아본다.
    for (int i = 0; i < 20; i++)
    {
        // 웨이브별 프리팹 배열에서 랜덤으로 하나 선택한다.
        // 같은 프리팹을 배열에 여러 번 넣으면 그 프리팹의 등장 확률이 올라간다.
        GameObject selected = prefabs[Random.Range(0, prefabs.Length)];

        if (selected != null)
        {
            return selected;
        }
    }

    // 여러 번 시도해도 유효한 프리팹을 찾지 못하면 기본 좀비 프리팹을 반환한다.
    return zombiePrefab;
    }
}