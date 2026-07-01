using UnityEngine;

/// <summary>
/// 힐팩/총알팩 공통 픽업 스크립트.
/// 플레이어가 닿으면 아이템 효과 적용 후 제거.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    // 아이템 종류
    public enum ItemType { Heal, Ammo }

    [Header("아이템 설정")]
    [SerializeField] private ItemType itemType  = ItemType.Heal;
    [SerializeField] private float    healAmount = 20f; // 힐팩 회복량
    [SerializeField] private int      ammoAmount = 30;  // 총알팩 지급량

    [Header("자동 제거")]
    [SerializeField] private float autoDestroyTime = 15f; // 줍지 않으면 자동 제거(초)

    [Header("회전 효과")]
    [SerializeField] private float rotateSpeed = 90f; // 초당 회전 속도
    [SerializeField] private float floatSpeed  = 1f;  // 위아래 움직임 속도
    [SerializeField] private float floatHeight = 0.3f; // 위아래 움직임 높이

    [Header("사운드")]
    [SerializeField] private AudioClip pickupClip;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.8f;

    private Vector3 _startPos;

    private void Start()
    {
        Destroy(gameObject, autoDestroyTime);
        _startPos = transform.position; // 시작 위치 저장
    }

    private void Update()
    {
        // 빙글빙글 회전
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);

        // 위아래 둥실둥실
        float newY = _startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어만 반응
        if (!other.CompareTag("Player")) return;

        if (itemType == ItemType.Heal)
        {
            // 체력 회복
            PlayerController pc = other.GetComponent<PlayerController>();
            pc?.Heal(healAmount);
        }
        else if (itemType == ItemType.Ammo)
        {
            // 총알 지급
            WeaponController wc = other.GetComponent<WeaponController>();
            wc?.AddAmmo(ammoAmount);
        }

        // 픽업 후 제거
        if (pickupClip != null)
    {
        AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);
    }
        Destroy(gameObject);
    }
}