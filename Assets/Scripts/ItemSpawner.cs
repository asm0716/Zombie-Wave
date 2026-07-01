using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // [추가] 내비메쉬(NavMesh) 기능을 사용하기 위해 필수 포함

/// <summary>
/// 플레이어 주변 랜덤 위치(NavMesh 유효 바닥)에 힐팩/총알팩을 일정 주기로 스폰합니다.
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    [Header("프리팹")]
    [SerializeField] private GameObject healPackPrefab; // 힐팩 프리팹
    [SerializeField] private GameObject ammoPackPrefab; // 총알팩 프리팹

    [Header("스폰 설정")]
    [SerializeField] private float healSpawnInterval = 15f; // 힐팩 스폰 주기(초)
    [SerializeField] private float ammoSpawnInterval = 10f; // 총알팩 스폰 주기(초)

    [Header("스폰 범위")]
    [SerializeField] private float minSpawnRadius = 5f;  // 플레이어로부터 최소 거리
    [SerializeField] private float maxSpawnRadius = 10f; // 플레이어로부터 최대 거리

    [Header("동시 최대 개수")]
    [SerializeField] private int maxHealPacks = 2; // 맵에 동시 존재 최대 힐팩 수
    [SerializeField] private int maxAmmoPacks = 2; // 맵에 동시 존재 최대 총알팩 수

    // 현재 맵에 있는 아이템 목록
    private List<GameObject> _healPacks = new();
    private List<GameObject> _ammoPacks = new();

    private Transform _player;

    private void Start()
    {
        // 플레이어 찾기
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) 
        {
            _player = playerGO.transform;
        }
        else
        {
            Debug.LogError("ItemSpawner: 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다!");
        }

        // 스폰 코루틴 시작
        StartCoroutine(HealSpawnLoop());
        StartCoroutine(AmmoSpawnLoop());
    }

    // ─────────────────────────────────────────────────────────────
    //  힐팩 스폰 루프
    // ─────────────────────────────────────────────────────────────
    private IEnumerator HealSpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(healSpawnInterval);

            // 게임 오버면 스폰 중단
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;

            // 리스트에서 이미 파괴된(먹은) 아이템 정리
            _healPacks.RemoveAll(h => h == null);
            
            // 최대 개수 미만일 때만 스폰
            if (_healPacks.Count < maxHealPacks)
            {
                GameObject heal = SpawnItem(healPackPrefab);
                if (heal != null) _healPacks.Add(heal);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  총알팩 스폰 루프
    // ─────────────────────────────────────────────────────────────
    private IEnumerator AmmoSpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(ammoSpawnInterval);

            // 게임 오버면 스폰 중단
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;

            // 리스트에서 이미 파괴된(먹은) 아이템 정리
            _ammoPacks.RemoveAll(a => a == null);
            
            // 최대 개수 미만일 때만 스폰
            if (_ammoPacks.Count < maxAmmoPacks)
            {
                GameObject ammo = SpawnItem(ammoPackPrefab);
                if (ammo != null) _ammoPacks.Add(ammo);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  랜덤 위치에 아이템 스폰
    // ─────────────────────────────────────────────────────────────
    private GameObject SpawnItem(GameObject prefab)
    {
        if (prefab == null || _player == null) return null;

        // NavMesh를 기반으로 계산된 안전한 바닥 위치 가져오기
        Vector3 spawnPos = GetRandomSpawnPosition();

        // 계산된 위치에 아이템 스폰
        return Instantiate(prefab, spawnPos, Quaternion.identity);
    }

    /// <summary>
    /// 플레이어 주변의 NavMesh(걸어다닐 수 있는 바닥) 위에서 랜덤 좌표를 계산합니다.
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        Vector3 finalPosition = _player.position;
        bool positionFound = false;

        // 건물 안, 땅속, 허공 스폰을 방지하기 위해 최대 10번 올바른 바닥 위치를 탐색합니다.
        for (int i = 0; i < 10; i++)
        {
            // 1. 플레이어를 기준으로 원형의 랜덤 방향 및 거리 계산
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomRadius = Random.Range(minSpawnRadius, maxSpawnRadius);

            Vector3 offset = new Vector3(randomDir.x, 0f, randomDir.y) * randomRadius;
            Vector3 targetPos = _player.position + offset;

            // 2. [핵심] 가상으로 지정한 위치(targetPos)에서 가장 가까운 '진짜 NavMesh 바닥'을 찾음
            // maxSpawnRadius 거리 안에서 실제 좀비/플레이어가 다닐 수 있는 파란색 NavMesh 구역을 탐색합니다.
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, maxSpawnRadius, NavMesh.AllAreas))
            {
                finalPosition = hit.position;
                positionFound = true;
                break; // 올바른 위치를 찾았으므로 루프 탈출
            }
        }

        // 만약 유효한 NavMesh 바닥을 찾지 못했다면 백업용으로 플레이어 위치로 설정 (에러 방지)
        if (!positionFound)
        {
            finalPosition = _player.position;
        }

        // 아이템이 맵 바닥에 절반쯤 파묻혀 소환되는 것을 방지하기 위해 위로 살짝 띄워줍니다.
        finalPosition.y += 0.8f;

        return finalPosition;
    }
}