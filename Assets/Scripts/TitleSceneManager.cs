using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬 전용 매니저.
/// [게임 시작] / [게임 방법] 버튼 처리.
/// </summary>
public class TitleSceneManager : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnHowTo;
    [SerializeField] private Button btnHowToClose;

    [Header("패널")]
    [SerializeField] private GameObject howToPlayPanel;

    [Header("씬 이름")]
    [SerializeField] private string firstStageName = "Stage1Scene";

    private void Awake()
    {
        // 버튼 리스너 등록
        btnStart?.onClick.AddListener(OnClickStart);
        btnHowTo?.onClick.AddListener(OnClickHowTo);
        btnHowToClose?.onClick.AddListener(OnClickHowToClose);

        // 게임 방법 패널 초기 숨김
        howToPlayPanel?.SetActive(false);

        // 타이틀 화면에서는 커서 보이게
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        // 모든 Awake가 끝난 뒤에도 DataManager가 없으면 예비로 생성
        // 단, 기본적으로는 TitleScene에 DataManager 오브젝트를 직접 두는 것을 권장
        if (DataManager.Instance == null)
        {
            GameObject dm = new GameObject("DataManager");
            dm.AddComponent<DataManager>();
        }
    }

    private void OnClickStart()
    {
        // 새 게임 시작: 데이터 초기화 + 플레이 시간 측정 시작
        DataManager.Instance?.StartNewRun();

        SceneManager.LoadScene(firstStageName);
    }

    private void OnClickHowTo()
    {
        howToPlayPanel?.SetActive(true);
    }

    private void OnClickHowToClose()
    {
        howToPlayPanel?.SetActive(false);
    }
}