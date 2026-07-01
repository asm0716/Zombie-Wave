using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 플레이어가 좀비에게 피격당했을 때의 시각적/청각적 피드백을 담당하는 스크립트
// 화면을 붉게 깜빡이게 하고, 카메라를 흔들며, 피격 사운드를 재생한다.
// PlayerController 또는 체력 관련 스크립트에서 데미지를 받을 때 PlayFeedback()을 호출하는 방식으로 사용한다.
public class DamageFeedback : MonoBehaviour
{
    [Header("Red Screen Flash (화면 빨간색 깜빡임 피드백)")]
    [SerializeField] private Image damageOverlay;       // 화면을 덮을 투명/빨간색 UI 이미지
    [SerializeField] private float flashMaxAlpha = 0.45f; // 피격 시 순간적으로 올라갈 최대 불투명도 (0~1)
    [SerializeField] private float flashFadeTime = 0.25f; // 빨간 화면이 서서히 사라지는 데 걸리는 시간

    [Header("Camera Shake (카메라 흔들림 피드백)")]
    [SerializeField] private Transform cameraTransform;   // 흔들 제어할 카메라의 Transform
    [SerializeField] private float shakeDuration = 0.18f; // 카메라가 흔들리는 총 시간
    [SerializeField] private float shakeMagnitude = 0.08f;// 카메라가 흔들리는 강도(범위)
    
    [Header("사운드 피드백")]
    [SerializeField] private AudioSource audioSource;     // 소리를 재생할 오디오 소스 컴포넌트
    [SerializeField] private AudioClip playerHitClip;     // 플레이어 피격 효과음 파일
    [SerializeField, Range(0f, 1f)] private float playerHitVolume = 0.8f; // 효과음 볼륨 (0은 무음, 1은 최대)

    // 실행 중인 코루틴을 기억하고 제어하기 위한 변수 (중복 실행 방지용)
    // 플레이어가 짧은 시간 안에 여러 번 맞을 수 있으므로, 이전 효과를 중지하고 새 효과를 실행하기 위해 사용한다.
    private Coroutine _flashCoroutine;
    private Coroutine _shakeCoroutine;

    // 카메라가 흔들린 후 원래 위치로 돌아오기 위해 초기 위치를 저장할 변수
    // 흔들림 효과가 끝난 뒤 카메라 위치가 어긋나지 않도록 기준 위치를 저장한다.
    private Vector3 _cameraDefaultLocalPosition;

    private void Awake()
    {
        // 1. 카메라 컴포넌트 자동 할당 (인스펙터에서 비워두면 Main Camera를 자동으로 찾음)
        // 인스펙터에서 직접 연결하지 않아도 MainCamera 태그가 붙은 카메라를 자동으로 참조한다.
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // 2. 카메라의 최초 시작 위치(기본 위치) 기억하기
        // 카메라 흔들림 효과가 끝난 뒤 이 위치로 복귀시킨다.
        if (cameraTransform != null)
        {
            _cameraDefaultLocalPosition = cameraTransform.localPosition;
        }

        // 3. 게임 시작 시 화면 진흙/빨간색 이펙트 UI를 완전히 투명(알파값 0)하게 초기화
        // 피격 전에는 화면 효과가 보이면 안 되므로 Alpha 값을 0으로 설정한다.
        if (damageOverlay != null)
        {
            Color color = damageOverlay.color;
            color.a = 0f;
            damageOverlay.color = color;
        }

        // 4. 오디오 소스 컴포넌트 체크 및 자동 추가
        // 인스펙터에서 AudioSource를 넣지 않아도 현재 오브젝트에서 찾아서 사용한다.
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>(); // 오브젝트에 이미 있는지 확인
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>(); // 없으면 컴포넌트를 새로 붙여줌
        }

        // 5. 오디오 소스의 기본 세팅 (게임 켜지자마자 재생 안 됨, 2D 사운드로 설정)
        // 플레이어 피격음은 위치에 따라 작아지는 소리보다 화면 전체에서 들리는 2D 효과음이 자연스럽다.
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 0f = 완전한 2D 사운드 (화면 전체에 균일하게 들림)
    }

    /// <summary>
    /// 외부(예: PlayerHealth 스크립트 등)에서 플레이어가 맞았을 때 호출하는 메인 함수
    /// 화면 피격 효과, 카메라 흔들림, 피격 사운드를 한 번에 실행한다.
    /// </summary>
    public void PlayFeedback()
    {
        // --- 1. 화면 깜빡임 연출 시작 ---
        if (damageOverlay != null)
        {
            // 이미 깜빡이는 중이었다면 이전 연출을 강제로 중지 (중첩 에러 방지)
            // 피격이 연속으로 들어와도 효과가 꼬이지 않고 다시 처음부터 재생되도록 처리한다.
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);

            _flashCoroutine = StartCoroutine(FlashRedScreen());
        }

        // --- 2. 카메라 흔들림 연출 시작 ---
        if (cameraTransform != null)
        { // <--- 아까 지워졌던 중괄호 복구 완료
            // 이미 흔들리는 중이었다면 이전 연출을 강제로 중지하고 새로 흔듦
            // 연속 피격 시 흔들림 값이 누적되어 카메라 위치가 틀어지는 것을 방지한다.
            if (_shakeCoroutine != null)
                StopCoroutine(_shakeCoroutine);

            _shakeCoroutine = StartCoroutine(ShakeCamera());
        } // <--- 아까 지워졌던 중괄호 복구 완료

        // --- 3. 피격 사운드 재생 ---
        if (audioSource != null && playerHitClip != null)
        {
            // PlayOneShot은 소리가 겹쳐서 나도 끊기지 않고 자연스럽게 중첩 재생됨
            // 기존에 재생 중인 소리를 멈추지 않고 피격 효과음을 한 번 재생한다.
            audioSource.PlayOneShot(playerHitClip, playerHitVolume);
        }
    }

    /// <summary>
    /// 빨간 화면이 순간적으로 나타났다가 서서히 사라지게 하는 코루틴
    /// UI Image의 Alpha 값을 조절하여 피격 시 화면이 붉게 번쩍이는 효과를 만든다.
    /// </summary>
    private IEnumerator FlashRedScreen()
    {
        Color color = damageOverlay.color;

        // 즉시 최대 불투명도(flashMaxAlpha)로 설정하여 화면을 붉게 만듦
        color.a = flashMaxAlpha;
        damageOverlay.color = color;

        float timer = 0f;

        // 정해진 시간(flashFadeTime) 동안 서서히 투명도를 0으로 낮춤
        while (timer < flashFadeTime)
        {
            timer += Time.deltaTime;
            float t = timer / flashFadeTime; // 0에서 1까지 변하는 비율값

            // Lerp를 이용해 최대치에서 0f까지 부드럽게 보간
            // 시간이 지날수록 빨간 화면이 자연스럽게 사라지도록 만든다.
            color.a = Mathf.Lerp(flashMaxAlpha, 0f, t);
            damageOverlay.color = color;

            yield return null; // 다음 프레임까지 대기
        }

        // 확실하게 투명도를 0으로 만들어 연출 종료
        // 반복 피격이나 프레임 오차로 인해 잔상이 남는 것을 방지한다.
        color.a = 0f;
        damageOverlay.color = color;
    }

    /// <summary>
    /// 지정된 시간 동안 카메라를 무작위로 흔드는 코루틴
    /// 피격 시 충격감을 주기 위해 카메라의 localPosition을 짧게 흔들어준다.
    /// </summary>
    private IEnumerator ShakeCamera()
    {
        float timer = 0f;

        // 정해진 시간(shakeDuration) 동안 매 프레임 위치를 흔듦
        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;

            // -1부터 1 사이의 무작위 값에 흔들림 강도(Magnitude)를 곱해 좌표 구함
            // x, y 방향으로 랜덤한 작은 움직임을 만들어 충격 효과를 표현한다.
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            // 원래 기본 위치에서 구한 무작위 값만큼 카메라를 이동시킴
            // 기본 위치를 기준으로 흔들기 때문에 카메라가 계속 밀려나지 않는다.
            cameraTransform.localPosition = _cameraDefaultLocalPosition + new Vector3(x, y, 0f);

            yield return null; // 다음 프레임까지 대기
        }

        // 흔들림이 끝나면 카메라를 반드시 원래 조용했던 기본 위치로 원상복구
        // 카메라 위치가 흔들린 상태로 남는 문제를 방지한다.
        cameraTransform.localPosition = _cameraDefaultLocalPosition;
    }
}