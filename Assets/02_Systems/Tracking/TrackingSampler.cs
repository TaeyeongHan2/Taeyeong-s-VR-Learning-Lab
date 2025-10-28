using UnityEngine;
using UnityEngine.XR; // InputDevices, CommonUsages
using VRCore.Core;

namespace VRCore.Systems.Tracking
{
    /// XRNode(HMD/LeftHand/RightHand) → Raw/Filtered/Predicted 포즈 시각화
    public class TrackingSampler : MonoBehaviour
    {
        public XRNode node = XRNode.Head;    // LeftHand, RightHand 선택 가능
        [Range(0f,1f)] public float posSmoothing = 0.15f;
        [Range(0f,1f)] public float rotSmoothing = 0.15f;
        [Tooltip("미래 예측(초). 0~0.02 권장")] public float predictLookahead = 0.0f;
        public bool drawGizmos = true;

        PoseEmaFilter _filter;
        PosePredictor _predictor;

        PoseF _raw, _filtered;
        Vector3 _predictedPos;
        bool _hasPose;

        void Awake(){
            _filter = new PoseEmaFilter { APos = posSmoothing, ARot = rotSmoothing };
            _predictor = new PosePredictor();
        }

        void Update(){
            // 슬라이더 변경 반영
            _filter.APos = posSmoothing;
            _filter.ARot = rotSmoothing;

            var dev = InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid
                && dev.TryGetFeatureValue(CommonUsages.devicePosition, out var p)
                && dev.TryGetFeatureValue(CommonUsages.deviceRotation, out var r))
            {
                _raw = new PoseF(p, r);
                _filtered = _filter.Step(_raw);
                _predictedPos = _predictor.Predict(_filtered.position, Time.unscaledTime, predictLookahead);
                _hasPose = true;
            }
            else _hasPose = false;
        }

        void OnDrawGizmos(){
            if (!drawGizmos || !_hasPose) return;
            Gizmos.color = Color.gray;  Gizmos.DrawWireSphere(_raw.position,      0.02f); // Raw
            Gizmos.color = Color.green; Gizmos.DrawWireSphere(_filtered.position, 0.03f); // Filtered
            Gizmos.color = Color.cyan;  Gizmos.DrawWireSphere(_predictedPos,      0.035f);// Pred
        }
    }
}