using UnityEngine;
using UnityEngine.XR;
using VRCore.Core;
using UnityEngine.InputSystem; // New Input System
using Sirenix.OdinInspector;

namespace VRCore.Systems.Tracking
{
    [HideMonoScript]
    [InfoBox("디바이스 Raw 포즈 → EMA 필터 → 1-스텝 예측, 그리고 샘플레이트/지터/지연을 계측하는 런타임 샘플러.")]
    public class TrackingSamplerPro : MonoBehaviour
    {
        // ── Runtime Root Group (부모 Foldout을 명시적으로 생성) ─────────────────────
        [FoldoutGroup("Runtime", Expanded = true)]
        [ShowInInspector, ReadOnly, LabelText("Runtime (live)")]
        [PropertyOrder(-1000)] // 맨 위로
        private string __RuntimeRoot => "샘플레이트/지터/지연 및 내부 오브젝트를 모아두는 섹션입니다.";
        
        // ── Source ────────────────────────────────────────────────────────────
        [TitleGroup("Source"), Required, SceneObjectsOnly]
        [Tooltip("포즈를 읽어올 바닥층 Provider. 비워두면 자동 탐색")]
        public OpenXRTrackingProvider provider;

        [TitleGroup("Source"), EnumToggleButtons, LabelWidth(50)]
        public XRNode node = XRNode.Head;

        // ── Filter / Predict ──────────────────────────────────────────────────
        [TitleGroup("Filter & Predict")]
        [PropertyRange(0f, 1f), LabelText("Pos α"), SuffixLabel("0..1", overlay: true)]
        public float posAlpha = 0.15f;

        [TitleGroup("Filter & Predict")]
        [PropertyRange(0f, 1f), LabelText("Rot α"), SuffixLabel("0..1", overlay: true)]
        public float rotAlpha = 0.15f;

        [TitleGroup("Filter & Predict")]
        [PropertyRange(0f, 0.05f), SuffixLabel("sec", overlay: true)]
        [Tooltip("예측 lookahead (권장 0~0.02s)")]
        public float predictLookahead = 0.0f;

        [TitleGroup("Filter & Predict")]
        [GUIColor(nameof(GetFilterColor))]
        public bool filterEnabled = true;

        // ── Debug/Draw ────────────────────────────────────────────────────────
        [TitleGroup("Debug")]
        [ToggleLeft] public bool drawGizmos = true;

        // ── Input (New Input System) ──────────────────────────────────────────
        [TitleGroup("Input (New Input System)")]
        [AssetsOnly, LabelText("Toggle Filter Action")]
        [Tooltip("비우면 F 키와 LeftHand Primary 버튼으로 기본 바인딩이 생성됩니다.")]
        [SerializeField] private InputActionReference toggleFilterActionRef;

        [TitleGroup("Input (New Input System)")]
        [AssetsOnly, LabelText("Cycle Predict Action")]
        [Tooltip("비우면 P 키와 LeftHand Secondary 버튼으로 기본 바인딩이 생성됩니다.")]
        [SerializeField] private InputActionReference cyclePredictActionRef;

        private InputAction _toggleFilterAction;
        private InputAction _cyclePredictAction;
        private bool _ownsToggle;
        private bool _ownsCycle;

        // ── Runtime State (읽기 전용: HUD/검증용) ─────────────────────────────
        [FoldoutGroup("Runtime/State"), ShowInInspector, ReadOnly] public PoseF Raw { get; private set; }
        [FoldoutGroup("Runtime/State"), ShowInInspector, ReadOnly] public PoseF Filtered { get; private set; }
        [FoldoutGroup("Runtime/State"), ShowInInspector, ReadOnly] public Vector3 PredictedPos { get; private set; }
        [FoldoutGroup("Runtime/State"), ShowInInspector, ReadOnly, SuffixLabel("Hz", overlay: true)] public float SampleRateHz { get; private set; }
        [FoldoutGroup("Runtime/State"), ShowInInspector, ReadOnly, SuffixLabel("m", overlay: true)] public float JitterRmsM { get; private set; }
        [FoldoutGroup("Runtime/State"), ShowInInspector, ReadOnly, SuffixLabel("ms", overlay: true)] public float LatencyMs { get; private set; }

        // 내부 필터/예측기
        [FoldoutGroup("Runtime/Objects"), ShowInInspector, ReadOnly] private PoseEmaFilter _filter;
        [FoldoutGroup("Runtime/Objects"), ShowInInspector, ReadOnly] private PosePredictor _predictor;

        Vector3 _prevPos; double _prevTime; bool _hasPrev;
        float _srEma; const float SR_A = 0.2f;

        // 지터 계산용
        const int N = 90; int _count;
        Vector3[] _buf = new Vector3[N];
        [FoldoutGroup("Runtime/Params"), SuffixLabel("m/s", overlay: true)]
        [Tooltip("정지 판정 임계속도(미만일 때만 지터 샘플링)")]
        [SerializeField] private float _stationarySpeedThresh = 0.02f;

        // ── Odin Buttons (빠른 테스트/토글) ──────────────────────────────────
        [ButtonGroup("Controls"), Button(ButtonSizes.Medium)]
        private void ToggleFilter() { filterEnabled = !filterEnabled; }

        [ButtonGroup("Controls"), Button(ButtonSizes.Medium)]
        private void CyclePredict()
        {
            predictLookahead = (predictLookahead < 0.005f) ? 0.010f :
                               (predictLookahead < 0.015f) ? 0.020f : 0.0f;
        }

        [ButtonGroup("Controls"), Button(ButtonSizes.Medium), GUIColor(0.9f, 0.8f, 0.6f)]
        private void ResetMetrics()
        {
            SampleRateHz = 0f; JitterRmsM = 0f; LatencyMs = 0f;
            _srEma = 0f; _count = 0;
        }

        // ── MonoBehaviour ─────────────────────────────────────────────────────
        private void Awake()
        {
            // provider 자동 할당 (인스펙터에서 비워둔 경우)
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            if (!provider)
                provider = Object.FindFirstObjectByType<OpenXRTrackingProvider>(FindObjectsInactive.Exclude);
            if (!provider)
                provider = Object.FindAnyObjectByType<OpenXRTrackingProvider>(FindObjectsInactive.Include);
#else
            if (!provider)
                provider = FindObjectOfType<OpenXRTrackingProvider>(); // 구버전 호환
#endif
            _filter = new PoseEmaFilter { APos = posAlpha, ARot = rotAlpha };
            _predictor = new PosePredictor();
        }

        private void OnEnable()
        {
            // 액션 준비 & 구독
            SetupOrCreateAction(ref _toggleFilterAction, toggleFilterActionRef,
                "<Keyboard>/f", "<XRController>{LeftHand}/primaryButton", out _ownsToggle);
            _toggleFilterAction.performed += OnToggleFilter;
            _toggleFilterAction.Enable();

            SetupOrCreateAction(ref _cyclePredictAction, cyclePredictActionRef,
                "<Keyboard>/p", "<XRController>{LeftHand}/secondaryButton", out _ownsCycle);
            _cyclePredictAction.performed += OnCyclePredict;
            _cyclePredictAction.Enable();
        }

        private void OnDisable()
        {
            // 구독 해제 & 정리
            if (_toggleFilterAction != null)
            {
                _toggleFilterAction.performed -= OnToggleFilter;
                _toggleFilterAction.Disable();
                if (_ownsToggle) _toggleFilterAction.Dispose();
                _toggleFilterAction = null;
            }
            if (_cyclePredictAction != null)
            {
                _cyclePredictAction.performed -= OnCyclePredict;
                _cyclePredictAction.Disable();
                if (_ownsCycle) _cyclePredictAction.Dispose();
                _cyclePredictAction = null;
            }
        }

        private void Update()
        {
            if (provider && provider.TryGetPose(node, out var raw, out var t))
            {
                Raw = raw;

                // 샘플레이트 EMA
                if (_hasPrev)
                {
                    var dt = Mathf.Max((float)(t - _prevTime), 1e-4f);
                    var instHz = 1f / dt;
                    _srEma = Mathf.Lerp(_srEma <= 0 ? instHz : _srEma, instHz, SR_A);
                    SampleRateHz = _srEma;
                }
                _prevTime = t; _hasPrev = true;

                // 필터/예측
                _filter.APos = posAlpha; _filter.ARot = rotAlpha;
                var fx = filterEnabled ? _filter.Step(raw) : raw;
                Filtered = fx;

                // provider timestamp를 사용해 예측(동기성↑)
                PredictedPos = _predictor.Predict(fx.position, (float)t, predictLookahead);

                // 지연 추정: |raw-filtered| / speed
                var v = (fx.position - _prevPos) / Mathf.Max(Time.deltaTime, 1e-4f);
                var speed = v.magnitude;
                var lagSec = speed > 0.001f ? (Raw.position - Filtered.position).magnitude / speed : 0f;
                LatencyMs = Mathf.Clamp(lagSec * 1000f, 0f, 100f);
                _prevPos = fx.position;

                // 지터: 정지일 때만 버퍼 축적 → 표준편차
                if (speed < _stationarySpeedThresh)
                {
                    _buf[_count % N] = Raw.position;
                    _count++;
                    int use = Mathf.Min(_count, N);
                    if (use > 5)
                    {
                        Vector3 mean = Vector3.zero;
                        for (int i = 0; i < use; i++) mean += _buf[i];
                        mean /= use;

                        float acc = 0f;
                        for (int i = 0; i < use; i++)
                        {
                            var d = (_buf[i] - mean).magnitude;
                            acc += d * d;
                        }
                        JitterRmsM = Mathf.Sqrt(acc / use);
                    }
                }
            }
        }

        // ── Input System helpers ───────────────────────────────────────────────
        private void SetupOrCreateAction(ref InputAction target, InputActionReference reference,
                                         string defaultBinding1, string defaultBinding2, out bool owns)
        {
            owns = false;
            if (reference != null && reference.action != null)
            {
                target = reference.action;
                return;
            }

            // 에셋이 비어있으면 코드로 기본 액션 생성(에디터/런타임 모두 사용 가능)
            target = new InputAction(type: InputActionType.Button);
            target.AddBinding(defaultBinding1); // Keyboard 기본
            target.AddBinding(defaultBinding2); // XR 컨트롤러 예시(Left primary/secondary)
            owns = true;
        }

        private void OnToggleFilter(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            filterEnabled = !filterEnabled;
        }

        private void OnCyclePredict(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            CyclePredict();
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            Gizmos.color = Color.gray;  Gizmos.DrawWireSphere(Raw.position, 0.02f);
            Gizmos.color = Color.green; Gizmos.DrawWireSphere(Filtered.position, 0.03f);
            Gizmos.color = Color.cyan;  Gizmos.DrawWireSphere(PredictedPos, 0.035f);
        }

        // ── Odin helpers ──────────────────────────────────────────────────────
        private Color GetFilterColor() => filterEnabled ? new Color(0.75f, 1f, 0.75f) : new Color(1f, 0.75f, 0.75f);
    }
}
