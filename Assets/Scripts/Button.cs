using UnityEngine;
using UnityEngine.EventSystems;

// Title 화면에서 버튼에 마우스를 올렸을 때 버튼이 살짝 커지는 효과를 담당하는 스크립트
// IPointerEnterHandler와 IPointerExitHandler를 사용하여 마우스가 UI 버튼 위에 올라갔을 때와 벗어났을 때를 감지한다.
public class HoverButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 버튼의 원래 크기를 저장하는 변수
    // 마우스가 버튼에서 벗어났을 때 다시 원래 크기로 되돌리기 위해 사용한다.
    private Vector3 originalScale;

    private void Start()
    {
        // 게임이 시작될 때 현재 버튼의 크기를 저장한다.
        // 이후 마우스 오버 효과가 끝났을 때 이 값으로 복구한다.
        originalScale = transform.localScale;
    }

    // 마우스 커서가 버튼 위에 올라왔을 때 자동으로 호출되는 함수
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 저장해둔 원래 크기보다 1.1배 크게 만들어 버튼이 강조되는 효과를 준다.
        transform.localScale = originalScale * 1.1f;
    }

    // 마우스 커서가 버튼 영역 밖으로 나갔을 때 자동으로 호출되는 함수
    public void OnPointerExit(PointerEventData eventData)
    {
        // 버튼 크기를 처음 저장해둔 원래 크기로 되돌린다.
        transform.localScale = originalScale;
    }
}