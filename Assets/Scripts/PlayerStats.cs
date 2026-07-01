using UnityEngine;

/// <summary>
/// DataManager 의 강화 수치를 PlayerController / WeaponController 에
/// 실시간으로 반영하는 중간 허브.
/// UpgradeShopUI 에서 강화 완료 후 ApplyAll() 을 호출한다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(WeaponController))]
public class PlayerStats : MonoBehaviour
{
    private PlayerController _player;
    private WeaponController _weapon;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _weapon = GetComponent<WeaponController>();
    }

    /// <summary>
    /// 강화 후 모든 수치를 컨트롤러에 즉시 반영.
    /// UpgradeShopUI 의 각 구매 버튼 OnClick 에서 호출.
    /// </summary>
    public void ApplyAll()
    {
    _player.RefreshStats();   // 최대 체력 & 이동속도 갱신

    // 강화 후에도 현재 탄창/예비탄은 초기화하지 않고 유지
    _weapon.RefreshWeaponStatsKeepAmmo();

    UIManager.Instance?.RefreshAll(); // UI 동기화
    }
}