using UnityEngine;
using Luddite.AIBrain;
using Luddite.Combat;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.Enemies
{
    /// <summary>
    /// 프리미엄 구독봇(엘리트) = 챗봇 프리팹 + 이 컴포넌트 (GDD §5.1 — 별도 클래스를 만들지 않는다).
    /// 예측탄(§7.4)의 판단·조준·텔레그래프를 소유한다. 호스트 FSM(<see cref="ChatbotDrone"/>)이
    /// 조준 시작 시 <see cref="TryBeginPredictiveAim"/>으로 물어보고, 참이면 발사를 이 컴포넌트에 넘긴다.
    ///
    /// <para>
    /// 조준 공식 (§7.4): <c>predictedTarget = playerPosition + predictedSideDir × offset</c>.
    /// predictedSideDir의 LEFT/RIGHT는 <b>탄환 진행 방향 기준</b>이며, 그 규약의 원본은
    /// <see cref="Vec2.Left"/>(반시계 90°)다 — 학습(<c>ThreatEventTracker</c>)과 조준이
    /// 같은 축을 쓰지 않으면 예측이 통째로 뒤집힌다. 여기서도 반드시 <see cref="Vec2.Left"/>를 경유한다.
    /// </para>
    ///
    /// <para>
    /// AIBrain은 <b>읽기만</b> 한다 (규칙 7과 같은 정신) — 이 컴포넌트는 모델을 절대 수정하지 않는다.
    /// 마젠타는 예측탄·조준선·마커 전용 (🔴 §10.4). TODO(D5): 보스 P1이 이 컴포넌트를 재사용.
    /// </para>
    /// </summary>
    public class EliteModifier : MonoBehaviour
    {
        [SerializeField] private PredictiveShotConfigSO _config;

        [Tooltip("발사기. 비워 두면 자식에서 찾는다")]
        [SerializeField] private EnemyGun _gun;

        [Header("텔레그래프 (§7.4 1단계 — 프리팹의 비활성 자식을 배선)")]
        [Tooltip("예측 지점으로 뻗는 마젠타 조준선")]
        [SerializeField] private LineRenderer _aimLine;

        [Tooltip("예측 지점 원형 마커")]
        [SerializeField] private Transform _targetMarker;

        [Tooltip("예측탄에 붙일 발광 트레일 원본 (비활성). 발사 시 복제해 탄에 붙인다")]
        [SerializeField] private TrailRenderer _trailTemplate;

        [Tooltip("예측탄 색 — 🔴 §10.4: 마젠타는 AI가 나를 읽고 행하는 것 전용. 일반탄에 쓰지 말 것")]
        [SerializeField] private Color _predictiveColor = Color.magenta;

        private AIBrainRunner _brain;
        private Transform _player;
        private int _attackCount;
        private bool _aiming;
        private Vector2 _predictedTarget;

        /// <summary>1단계 텔레그래프 길이(초). 예측 공격일 때 FSM의 조준 시간을 이것으로 대체한다.</summary>
        public float TelegraphDuration => _config != null ? _config.TelegraphDuration : 0.35f;

        /// <summary>이번 런에서 이 개체가 예측탄을 쏜 횟수. 디버그·결과 화면 참고용.</summary>
        public int PredictiveShotsFired { get; private set; }

        private void Awake()
        {
            if (_gun == null) _gun = GetComponentInChildren<EnemyGun>();

            if (_config == null) Debug.LogError("[EliteModifier] PredictiveShotConfigSO 미지정", this);
            if (_gun == null) Debug.LogError("[EliteModifier] EnemyGun을 찾지 못함", this);

            HideTelegraph();
        }

        private void Start()
        {
            _brain = FindFirstObjectByType<AIBrainRunner>();
            if (_brain == null)
                Debug.LogWarning("[EliteModifier] AIBrainRunner 없음 — HIGH CONFIDENCE를 읽을 수 없어 예측탄 비활성", this);

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _player = player.transform;
        }

        /// <summary>
        /// 호스트 FSM이 조준 상태에 들어갈 때마다 호출한다 (= 공격 1회로 집계).
        /// 참을 반환하면 이번 공격은 예측 공격 — 텔레그래프가 켜지고, 발사는
        /// <see cref="FirePredictive"/>로 해야 한다.
        /// §7.4: HIGH CONFIDENCE 시 공격 N회당 1회 (N은 SO, 기본 2).
        /// </summary>
        public bool TryBeginPredictiveAim()
        {
            _attackCount++;

            if (_config == null || _gun == null || _brain == null || _player == null) return false;
            if (!_brain.IsHighConfidence) return false;
            if (_attackCount % _config.AttacksPerPredictive != 0) return false;

            _aiming = true;
            UpdatePredictiveAim();
            return true;
        }

        /// <summary>
        /// 텔레그래프 중 매 프레임 호출 — 예측 지점이 플레이어를 따라가며 갱신된다.
        /// "플레이어가 조준선을 읽고 반대로 속일 시간"이 §7.4 텔레그래프의 존재 이유다.
        /// </summary>
        public void UpdatePredictiveAim()
        {
            if (!_aiming || _player == null) return;

            Vector2 origin = transform.position;
            Vector2 playerPosition = _player.position;

            Vector2 travelDirection = playerPosition - origin;
            if (travelDirection.sqrMagnitude < 1e-6f) travelDirection = Vector2.right;
            travelDirection.Normalize();

            // LEFT/RIGHT 축은 반드시 Vec2.Left를 경유 — 학습과 조준의 규약 일치 (클래스 주석 참조)
            Vec2 left = new Vec2(travelDirection.x, travelDirection.y).Left;
            Vector2 sideDirection = new Vector2(left.X, left.Y);
            if (_brain.DominantDirection == DodgeDirection.Right) sideDirection = -sideDirection;

            _predictedTarget = playerPosition + sideDirection * _config.AimOffset;

            if (_aimLine != null)
            {
                if (!_aimLine.gameObject.activeSelf) _aimLine.gameObject.SetActive(true);
                _aimLine.positionCount = 2;
                _aimLine.SetPosition(0, origin);
                _aimLine.SetPosition(1, _predictedTarget);
            }

            if (_targetMarker != null)
            {
                if (!_targetMarker.gameObject.activeSelf) _targetMarker.gameObject.SetActive(true);
                _targetMarker.position = _predictedTarget;
            }
        }

        /// <summary>§7.4 2단계: 마젠타 탄 + 발광 트레일. 텔레그래프를 끄고 예측 지점을 향해 발사한다.</summary>
        public void FirePredictive()
        {
            if (!_aiming) return;

            UpdatePredictiveAim(); // 마지막 프레임 기준으로 예측 지점 확정

            Vector2 origin = transform.position;
            Vector2 direction = _predictedTarget - origin;
            direction = direction.sqrMagnitude > 1e-6f
                ? direction.normalized
                : (_player != null ? ((Vector2)_player.position - origin).normalized : Vector2.right);

            Projectile shot = _gun.Fire(direction, _predictiveColor, markPredictive: true);
            if (shot != null)
            {
                PredictiveShotsFired++;
                AttachTrail(shot);
            }

            EndAim();
        }

        /// <summary>조준이 중단됐을 때(사망 등) 텔레그래프 정리. 파괴 시에는 자식이라 함께 사라진다.</summary>
        public void CancelPredictiveAim() => EndAim();

        private void AttachTrail(Projectile shot)
        {
            if (_trailTemplate == null) return;

            TrailRenderer trail = Instantiate(_trailTemplate, shot.transform);
            trail.transform.localPosition = Vector3.zero;
            trail.Clear();
            trail.gameObject.SetActive(true);
        }

        private void EndAim()
        {
            _aiming = false;
            HideTelegraph();
        }

        private void HideTelegraph()
        {
            if (_aimLine != null) _aimLine.gameObject.SetActive(false);
            if (_targetMarker != null) _targetMarker.gameObject.SetActive(false);
        }
    }
}
