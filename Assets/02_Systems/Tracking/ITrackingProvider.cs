using UnityEngine;
using UnityEngine.XR;
using VRCore.Core;

namespace VRCore.Systems.Tracking
{
    public interface ITrackingProvider
    {
        // 성공 시 포즈 + 샘플 타임스탬프(초)를 반환
        bool TryGetPose(XRNode node, out PoseF pose, out double timestamp);
    }
}