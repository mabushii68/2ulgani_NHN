using UnityEngine;
using Luddite.Combat;

namespace Luddite.Enemies
{
    /// <summary>
    /// 보스 P2 구역 선점 장판 (GDD §9 P2-③): <c>favoriteQuadrant</c>에
    /// <b>2초 텔레그래프 → 3초 지속, 데미지 8/초</b>. 프리팹이 아니라 <see cref="Spawn"/>으로
    /// 런타임 생성한다 — 방 위치·프로필에 따라 매번 좌표가 달라 씬 상주물이 아니다.
    ///
    /// <para>🔴 §10.4: 장판은 "AI가 나를 읽고 행하는 것"이므로 <b>마젠타 전용</b>.
    /// 스프라이트는 흰 원(자체 제작 Placeholder)을 틴트한다 — 컬러 원본 위 곱셈 틴트가
    /// 마젠타를 못 내는 문제(SYSTEMS §15.6-1)를 원천 회피.</para>
    ///
    /// <para>장판 데미지는 접촉·비투사체이므로 <c>ProjectileHitPlayer</c>를 발행하지 않는다 —
    /// §7.1 학습의 원시 단위는 탄환이다 (표본 오염 없음).</para>
    /// </summary>
    public class BossZoneHazard : MonoBehaviour
    {
        private const float DAMAGE_TICK_PERIOD = 1f;   // 1초당 1회 × dps — i-frame 0.5s보다 길어 매 틱 온전히 들어간다

        private static readonly Color TELEGRAPH_COLOR = new Color(1f, 0f, 1f, 0.18f);
        private static readonly Color ACTIVE_COLOR = new Color(1f, 0f, 1f, 0.45f);

        private SpriteRenderer _renderer;
        private Transform _player;
        private IDamageable _playerDamageable;

        private float _radius;
        private float _telegraphDuration;
        private float _activeDuration;
        private float _damagePerSecond;
        private float _elapsed;
        private float _damageTimer;

        /// <summary>지금 텔레그래프 단계인지 (아직 데미지 없음). 스모크 테스트용.</summary>
        public bool IsTelegraphing => _elapsed < _telegraphDuration;

        /// <summary>장판 1개를 런타임 생성. <paramref name="sprite"/>는 흰 원(틴트 전제).</summary>
        public static BossZoneHazard Spawn(Vector2 position, Sprite sprite,
            float radius, float telegraphDuration, float activeDuration, float damagePerSecond)
        {
            GameObject zone = new GameObject("BossZoneHazard");
            zone.transform.position = position;

            SpriteRenderer renderer = zone.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = TELEGRAPH_COLOR;
            ApplyFloorSorting(renderer);

            // 흰 원 스프라이트의 월드 지름 → 목표 지름(radius*2)으로 스케일
            float spriteDiameter = sprite != null ? Mathf.Max(sprite.bounds.size.x, 1e-3f) : 1f;
            zone.transform.localScale = Vector3.one * (radius * 2f / spriteDiameter);

            BossZoneHazard hazard = zone.AddComponent<BossZoneHazard>();
            hazard._radius = radius;
            hazard._telegraphDuration = telegraphDuration;
            hazard._activeDuration = activeDuration;
            hazard._damagePerSecond = damagePerSecond;
            return hazard;
        }

        /// <summary>
        /// 바닥 연출은 Units(캐릭터) 아래 <c>Shadow</c> 레이어에 깐다 (MAP_SPEC §3).
        /// 레이어가 없으면(폴백 씬 등) Units 최하 순서로 — Default는 Ground 밑이라 바닥에 깔려 안 보인다.
        /// </summary>
        private static void ApplyFloorSorting(SpriteRenderer renderer)
        {
            int shadowId = SortingLayer.NameToID("Shadow");
            if (shadowId != 0)
            {
                renderer.sortingLayerID = shadowId;
                renderer.sortingOrder = 5;
                return;
            }

            int unitsId = SortingLayer.NameToID("Units");
            if (unitsId != 0)
            {
                renderer.sortingLayerID = unitsId;
                renderer.sortingOrder = -10;
                return;
            }

            Debug.LogWarning("[BossZoneHazard] Shadow/Units Sorting Layer 없음 — Default 사용 (바닥 밑에 깔릴 수 있음)");
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _player = player.transform;
                _playerDamageable = player.GetComponent<IDamageable>();
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;   // scaled — 인터벌·히트스톱에서 자연히 정지

            if (_elapsed >= _telegraphDuration + _activeDuration)
            {
                Destroy(gameObject);
                return;
            }

            if (IsTelegraphing)
            {
                // 텔레그래프: 알파 펄스 — "여기가 곧 위험해진다"를 읽을 시간 (§9 = 2초)
                if (_renderer != null)
                {
                    Color color = TELEGRAPH_COLOR;
                    color.a += 0.10f * Mathf.PingPong(_elapsed * 4f, 1f);
                    _renderer.color = color;
                }
                return;
            }

            if (_renderer != null) _renderer.color = ACTIVE_COLOR;
            TickDamage();
        }

        private void TickDamage()
        {
            if (_player == null || _playerDamageable == null) return;

            _damageTimer += Time.deltaTime;
            if (_damageTimer < DAMAGE_TICK_PERIOD) return;
            _damageTimer -= DAMAGE_TICK_PERIOD;

            float distance = Vector2.Distance(_player.position, transform.position);
            if (distance > _radius) return;
            if (!_playerDamageable.CanBeDamaged) return;

            // 방향은 장판 중심 → 플레이어 (플레이어 넉백 없음 계약이라 시각 정보일 뿐)
            Vector2 direction = ((Vector2)_player.position - (Vector2)transform.position).normalized;
            _playerDamageable.TakeDamage(_damagePerSecond * DAMAGE_TICK_PERIOD, direction);
        }
    }
}
