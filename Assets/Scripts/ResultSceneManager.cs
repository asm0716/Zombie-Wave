using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// 게임을 모두 클리어한 뒤 이동하는 ResultScene을 관리하는 스크립트
// DataManager에 저장된 플레이 시간, 총 처치 수, 총 획득 코인을 UI에 표시하고,
// Main Menu / Retry 버튼을 통해 타이틀 씬 또는 Stage1Scene으로 이동하는 기능을 담당한다.
public class ResultSceneManager : MonoBehaviour
{
    [Header("Result Texts")]
    // ResultScene 화면에 표시할 결과 텍스트들
    // 각각 플레이 시간, 총 처치 수, 총 획득 코인을 출력하는 TextMeshPro UI이다.
    [SerializeField] private TMP_Text textPlayTime;
    [SerializeField] private TMP_Text textTotalKills;
    [SerializeField] private TMP_Text textTotalCoins;

    [Header("Scene Names")]
    // 버튼 클릭 시 이동할 씬 이름
    // Unity Build Settings에 등록된 씬 이름과 정확히 일치해야 한다.
    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private string firstStageSceneName = "Stage1Scene";

    private void Start()
    {
        // ResultScene은 UI 버튼을 눌러야 하므로 마우스 잠금을 해제하고 커서를 보이게 한다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 씬이 시작되면 DataManager에 저장된 최종 결과 데이터를 화면에 표시한다.
        ShowResult();
    }

    // DataManager에 저장되어 있는 최종 플레이 기록을 ResultScene UI에 반영하는 함수
    private void ShowResult()
    {
        // 게임 진행 중 유지되던 DataManager 싱글톤을 가져온다.
        DataManager dm = DataManager.Instance;

        // DataManager가 존재하지 않으면 결과 데이터를 표시할 수 없으므로 경고를 출력하고 종료한다.
        if (dm == null)
        {
            Debug.LogWarning("ResultSceneManager: DataManager가 없습니다.");
            return;
        }

        // 플레이 시간 표시
        // DataManager의 PlayTimeText는 분:초 형식으로 변환된 문자열이다.
        if (textPlayTime != null)
            textPlayTime.text = $"Play Time {dm.PlayTimeText}";

        // 총 처치 수 표시
        // 플레이 중 좀비가 죽을 때마다 DataManager의 TotalKills가 증가한다.
        if (textTotalKills != null)
            textTotalKills.text = $"kill : {dm.TotalKills}";

        // 총 획득 코인 표시
        // 현재 남은 코인이 아니라, 플레이 전체에서 획득한 누적 코인을 표시한다.
        // N0 포맷을 사용하여 1000 단위에 쉼표가 들어가도록 한다.
        if (textTotalCoins != null)
            textTotalCoins.text = $"Coin : {dm.TotalEarnedCoin:N0}";
    }

    // ResultScene의 Main Menu 버튼에서 호출되는 함수
    // 게임 데이터를 초기화한 뒤 타이틀 씬으로 이동한다.
    public void OnClickMainMenu()
    {
        DataManager.Instance?.ResetAllData();
        SceneManager.LoadScene(titleSceneName);
    }

    // ResultScene의 Retry 버튼에서 호출되는 함수
    // 새로운 플레이 기록을 시작하고 Stage1Scene으로 이동한다.
    // StartNewRun()을 사용하여 코인, 강화 레벨, 처치 수, 획득 코인, 플레이 시간을 새로 초기화한다.
    public void OnClickRetry()
    {
        DataManager.Instance?.StartNewRun();
        SceneManager.LoadScene(firstStageSceneName);
    }
}