using UnityEngine;
using Sirenix.OdinInspector;

namespace VRCore.Core
{
    /// 간단 포즈 컨테이너(이름 충돌 피하려고 UnityEngine.Pose 대신).
    [System.Serializable, InlineProperty]
    public struct PoseF
    {
        [HorizontalGroup, LabelWidth(64)]
        public Vector3 position;

        [HorizontalGroup, LabelWidth(64)]
        public Quaternion rotation;

        public PoseF(Vector3 p, Quaternion r){ position = p; rotation = r; }
    }

    /// 지터 저감용 EMA 필터(지연-안정 트레이드오프는 a로 조절)
    [System.Serializable]
    public class PoseEmaFilter
    {
        [PropertyRange(0f, 1f), LabelText("Pos α"), SuffixLabel("0..1", overlay: true)]
        public float APos { get; set; } = 0.15f; // 0..1 (클수록 반응 빠름)

        [PropertyRange(0f, 1f), LabelText("Rot α"), SuffixLabel("0..1", overlay: true)]
        public float ARot { get; set; } = 0.15f;

        [ShowInInspector, ReadOnly, LabelText("Initialized")]
        private bool _initialized;

        [ShowInInspector, ReadOnly, LabelText("State")]
        private PoseF _state;

        /// raw → EMA 1스텝
        [Button(25), GUIColor(0.6f, 0.8f, 1f)]
        public PoseF Step(PoseF raw)
        {
            if (!_initialized) { _state = raw; _initialized = true; return _state; }
            _state.position = Vector3.Lerp(_state.position, raw.position, Mathf.Clamp01(APos));
            _state.rotation = Quaternion.Slerp(_state.rotation, raw.rotation, Mathf.Clamp01(ARot));
            return _state;
        }
    }

    /// 1-스텝 속도 기반 위치 예측(lookahead 초 만큼 앞당김)
    [System.Serializable]
    public class PosePredictor
    {
        [ShowInInspector, ReadOnly] private Vector3 _prevPos;
        [ShowInInspector, ReadOnly] private float _prevTime;
        [ShowInInspector, ReadOnly, LabelText("Initialized")] private bool _initialized;

        [Button(25), GUIColor(0.9f, 0.95f, 0.6f)]
        public Vector3 Predict(Vector3 currentPos, float currentTime, float lookaheadSeconds)
        {
            if (!_initialized) { _prevPos = currentPos; _prevTime = currentTime; _initialized = true; return currentPos; }
            float dt = Mathf.Max(currentTime - _prevTime, 1e-4f);
            Vector3 vel = (currentPos - _prevPos) / dt;
            _prevPos = currentPos; _prevTime = currentTime;
            return currentPos + vel * Mathf.Max(lookaheadSeconds, 0f);
        }
    }
}
