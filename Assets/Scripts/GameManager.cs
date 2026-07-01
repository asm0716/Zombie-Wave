using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// 씬 전용 게임 매니저.
/// 웨이브 클리어 시 상점을 열고, 마지막 웨이브 후 상점 닫으면 다음 씬으로 이동.

// 각 Stage 씬에서 게임 흐름을 총괄하는 매니저 스크립트
// 웨이브 클리어, 상점 열기/닫기, 게임 오버, 다음 스테이지 이동, ResultScene 이동을 관리한다.
// DataManager, UIManager, WaveSpawner, PlayerController와 연결되어 전체 게임 진행을 제어한다.
public class GameManager : MonoBehaviour
{
    // 다른 스크립트에서 GameManager.Instance로 현재 씬의 게임 매니저에 접근할 수 있게 하는 싱글톤
    public static GameManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────────────────────────

    [Header("씬 이름")]
    // 현재 스테이지 클리어 후 이동할 다음 스테이지 씬 이름
    // Stage1에서는 Stage2Scene, Stage2에서는 Stage3Scene처럼 설정한다.
    [SerializeField] private string nextSceneName   = "Stage2Scene"; // 다음 스테이지 씬 이름

    // 마지막 스테이지 클리어 후 이동할 결과 화면 씬 이름
    [SerializeField] private string resultSceneName = "ResultScene"; // 결과 씬 이름

    [Header("웨이브 클리어 코인 보상")]
    // 인덱스 0 = 웨이브1 클리어, 1 = 웨이브2 클리어, 2 = 웨이브3(보스) 클리어
    // 웨이브를 클리어했을 때 지급되는 보상 코인 배열
    [SerializeField] private int[] waveRewardCoins = { 30, 50, 80 };

    [Header("씬 전환 딜레이")]
    // 마지막 웨이브 클리어 후 바로 씬을 넘기지 않고, 스테이지 클리어 UI를 보여줄 시간
    [SerializeField] private float sceneTransitionDelay = 3f; // 스테이지 클리어 후 다음 씬까지 대기 시간

    [Header("사운드")]
    // 웨이브 클리어음과 게임오버음을 재생하기 위한 AudioSource
    // 인스펙터에서 비워두면 Start에서 자동으로 찾거나 추가한다.
    [SerializeField] private AudioSource uiAudioSource;

    // 웨이브 클리어 시 재생할 효과음
    [SerializeField] private AudioClip waveClearClip;
    [SerializeField, Range(0f, 1f)] private float waveClearVolume = 0.8f;

    // 게임 오버 시 재생할 효과음
    [SerializeField] private AudioClip gameOverClip;
    [SerializeField, Range(0f, 1f)] private float gameOverVolume = 0.9f;

    // ─────────────────────────────────────────────────────────────
    //  상태
    // ─────────────────────────────────────────────────────────────

    // 현재 게임 오버 상태인지 확인하는 값
    // true가 되면 플레이어 조작, 좀비 AI, 무기 발사 등이 중단된다.
    public bool IsGameOver   { get; private set; } = false;

    // 현재 씬이 마지막 스테이지인지 확인하는 값
    // Stage3Scene일 경우 true가 되며, 마지막 웨이브 이후 ResultScene으로 이동한다.
    public bool IsLastStage  { get; private set; } = false; // Stage3 이면 true

    // 현재 클리어한 웨이브가 해당 스테이지의 마지막 웨이브인지 확인하는 값
    public bool IsLastWave   { get; private set; } = false; // 현재 클리어한 웨이브가 마지막인지

    // UI 표시를 담당하는 UIManager 참조
    private UIManager   _ui;

    // 좀비 웨이브 스폰을 담당하는 WaveSpawner 참조
    private WaveSpawner _spawner;

    // ─────────────────────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // 한 씬 안에 GameManager가 중복으로 존재하지 않도록 싱글톤 처리
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Stage3 씬이면 마지막 스테이지로 표시
        // 마지막 웨이브 클리어 후 ResultScene 으로 이동
        // 현재 활성화된 씬 이름이 Stage3Scene이면 다음 이동 씬을 ResultScene으로 변경한다.
        if (SceneManager.GetActiveScene().name == "Stage3Scene")
        {
            IsLastStage   = true;
            nextSceneName = resultSceneName;
        }
    }

    private void Start()
    {
        // 씬에 존재하는 UIManager와 WaveSpawner 싱글톤을 가져와 캐싱한다.
        _ui      = UIManager.Instance;
        _spawner = WaveSpawner.Instance;

        // 게임 시작 시 마우스 잠금
        // FPS 게임 플레이 중에는 마우스가 화면 중앙에 고정되어야 하므로 커서를 숨기고 잠근다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        // 인스펙터에서 uiAudioSource가 연결되지 않은 경우 현재 오브젝트에서 AudioSource를 찾는다.
        if (uiAudioSource == null)
    {
        uiAudioSource = GetComponent<AudioSource>();
    }

    // 현재 오브젝트에 AudioSource가 없다면 새로 추가한다.
    if (uiAudioSource == null)
    {
        uiAudioSource = gameObject.AddComponent<AudioSource>();
    }

    // UI/상태 효과음은 시작하자마자 재생되지 않도록 설정하고,
    // 2D 사운드로 설정하여 위치와 관계없이 일정하게 들리게 한다.
    uiAudioSource.playOnAwake = false;
    uiAudioSource.spatialBlend = 0f;
    }

    // ─────────────────────────────────────────────────────────────
    //  웨이브 클리어 (WaveSpawner 가 호출)
    // ─────────────────────────────────────────────────────────────

    /// WaveSpawner 에서 웨이브 클리어 시 호출.
    /// waveIndex: 0 = 웨이브1, 1 = 웨이브2, 2 = 웨이브3(마지막)
    
    // 웨이브 클리어 효과음을 재생하는 함수
    // OnWaveClear에서 웨이브 클리어 처리가 끝날 때 호출된다.
    private void PlayWaveClearSound()
    {
    if (uiAudioSource != null && waveClearClip != null)
    {
        uiAudioSource.PlayOneShot(waveClearClip, waveClearVolume);
    }
    }

    // 웨이브 클리어 시 실행되는 핵심 처리 함수
    // 보상 지급, 스폰 정지, 플레이어 조작 정지, 업그레이드 상점 표시를 담당한다.
    public void OnWaveClear(int waveIndex)
    {
         // 남은 좀비 전부 제거
         // 웨이브가 클리어된 시점에 혹시 남아있는 좀비 오브젝트를 정리한다.
        _spawner?.ClearAllZombies();
        
        // 웨이브 클리어 코인 지급
        // waveIndex가 배열 범위를 벗어나지 않도록 Clamp 처리 후 보상 코인을 지급한다.
        int rewardIndex = Mathf.Clamp(waveIndex, 0, waveRewardCoins.Length - 1);
        DataManager.Instance.AddCoin(waveRewardCoins[rewardIndex]);

        // 마지막 웨이브(웨이브3) 클리어 여부 판단
        // 현재 스테이지의 마지막 웨이브를 클리어했다면 상점 닫기 후 다음 씬으로 이동하게 된다.
        IsLastWave = (waveIndex == 2);

        // 스폰 일시 정지
        // 상점이 열려 있는 동안 좀비가 추가로 나오지 않도록 한다.
        _spawner?.PauseSpawning(true);

        // 상점이 열렸으므로 플레이어를 멈추고 마우스 커서를 풀어줍니다.
        // 상점 UI 조작 중 플레이어가 움직이거나 총을 쏘지 않도록 조작을 막는다.
        PlayerController player = FindObjectOfType<PlayerController>();
        player?.SetControllable(false);

        // 상점 열기 (마지막 웨이브든 아니든 항상 상점 먼저 열림)
        // 마지막 웨이브를 클리어해도 바로 씬 전환하지 않고, 업그레이드 상점을 먼저 보여준다.
        _ui?.ShowUpgradeShop(true);

        // UI 코인 갱신
        // 웨이브 보상을 받은 뒤 현재 코인 표시를 최신 상태로 갱신한다.
        _ui?.UpdateCoin(DataManager.Instance.Coin);

        // 웨이브 클리어 효과음 재생
        PlayWaveClearSound();
    }

    // ─────────────────────────────────────────────────────────────
    //  상점 닫기 (UpgradeShopUI 의 닫기 버튼에서 호출)
    // ─────────────────────────────────────────────────────────────

    /// 상점 닫기 버튼 클릭 시 호출.
    /// 마지막 웨이브였으면 씬 전환, 아니면 다음 웨이브 시작.
    public void OnShopClosed()
    {
        // 상점 닫기
        // 업그레이드 UI를 비활성화한다.
        _ui?.ShowUpgradeShop(false);

        // 어떤 웨이브든 상점이 닫혔으므로 플레이어 조작을 다시 원래대로 풀어줌
        // 다음 웨이브 진행 또는 씬 전환 전 플레이어 조작을 다시 활성화한다.
        PlayerController player = FindObjectOfType<PlayerController>();
        player?.SetControllable(true);

        if (IsLastWave)
        {
            // 마지막 웨이브 클리어 후 상점 닫으면 스테이지 클리어 화면 표시 후 씬 전환
            // Stage1/Stage2에서는 다음 스테이지로, Stage3에서는 ResultScene으로 이동한다.
            _ui?.ShowStageClear(true);
            StartCoroutine(LoadNextStage());
        }
        else
        {
            // 일반 웨이브 클리어 후 상점 닫으면 다음 웨이브 시작
            // 스폰을 다시 허용하고 WaveSpawner에서 다음 웨이브를 시작한다.
            _spawner?.PauseSpawning(false);
            _spawner?.StartNextWave();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  게임 오버 (PlayerController 의 Die() 에서 호출)
    // ─────────────────────────────────────────────────────────────

    /// 플레이어 사망 시 호출. 게임 오버 처리.
    public void OnGameOver()
    {
        // 이미 게임 오버 처리된 상태라면 중복 실행을 막는다.
        if (IsGameOver) return;
        IsGameOver = true;

        // 마우스 커서 해제
        // 게임오버 UI 버튼을 클릭할 수 있도록 커서를 보이게 한다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // 기존 루프 브금 찾아서 멈추기 및 게임오버 사운드 재생
        // 현재 씬에 있는 모든 AudioSource 중 loop가 켜진 배경음을 찾아 정지시킨다.
        // 이후 게임오버 효과음을 한 번 재생한다.
        foreach (AudioSource src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) { if (src.loop) src.Stop(); }
        if (uiAudioSource != null && gameOverClip != null) uiAudioSource.PlayOneShot(gameOverClip, gameOverVolume);

        // 모든 데이터 초기화 (강화 레벨, 코인 등)
        // 사망 시 로그라이크 규칙에 따라 강화와 코인을 초기화한다.
        DataManager.Instance.ResetAllData();

        // 게임 오버 패널 표시
        // Retry 또는 Main Menu 버튼을 누를 수 있는 UI를 보여준다.
        _ui?.ShowGameOver(true);

    }

    // ─────────────────────────────────────────────────────────────
    //  씬 전환
    // ─────────────────────────────────────────────────────────────

    /// 스테이지 클리어 후 딜레이 뒤 다음 씬으로 이동.
    private IEnumerator LoadNextStage()
    {
        // [주의] 다음 씬으로 완전히 넘어가기 직전에는 마우스 락을 풀어주어야 에러가 안 남
        // 스테이지 클리어 UI 또는 다음 씬의 UI 조작을 위해 커서를 해제한다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // sceneTransitionDelay 초 대기 (스테이지 클리어 화면 표시 시간)
        // 플레이어에게 Stage Clear 화면을 잠시 보여준 뒤 씬을 이동한다.
        yield return new WaitForSeconds(sceneTransitionDelay);

        // 다음 씬으로 이동
        // Stage1/2에서는 다음 Stage로, Stage3에서는 Awake에서 설정한 ResultScene으로 이동한다.
        SceneManager.LoadScene(nextSceneName);
    }

    // ─────────────────────────────────────────────────────────────
    //  게임 오버 UI 버튼 연동용 함수
    // ─────────────────────────────────────────────────────────────

    /// 게임 오버 패널의 Retry 버튼 클릭 시 호출.
    /// 데이터 완벽 초기화 후 무조건 Stage1Scene으로 이동합니다.
    public void RestartFromStage1()
    {
        // 새로운 플레이를 시작하기 위해 DataManager의 기록과 타이머를 초기화한다.
        DataManager.Instance?.StartNewRun();

        // 첫 번째 스테이지로 이동하여 처음부터 다시 플레이한다.
        SceneManager.LoadScene("Stage1Scene");
    }

    /// 게임 오버 패널의 Main Menu 버튼 클릭 시 호출.
    /// 데이터 완벽 초기화 후 타이틀 씬으로 이동합니다.
    public void GoToTitle()
    {
        // 타이틀로 돌아가기 전 모든 플레이 데이터를 초기화한다.
        DataManager.Instance?.ResetAllData();

        // 타이틀 씬으로 이동한다.
        SceneManager.LoadScene("TitleScene");
    }
}