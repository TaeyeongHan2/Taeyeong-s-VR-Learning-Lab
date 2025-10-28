using UnityEngine;
using UnityEngine.XR;
using VRCore.Core;
using Sirenix.OdinInspector;

namespace VRCore.Systems.Tracking
{
    [HideMonoScript]
    [InfoBox("OpenXR에서 Head / LeftHand / RightHand의 포즈를 읽어오는 Provider(바닥층). " +
             "반환 포즈는 XR Origin의 Tracking Origin(Local/Floor) 기준 로컬 포즈입니다.")]
    public class OpenXRTrackingProvider : MonoBehaviour, ITrackingProvider
    {
        [TitleGroup("Diagnostics"), ShowInInspector, ReadOnly]
        private bool DeviceValid => _lastDevice.isValid;

        [TitleGroup("Diagnostics"), ShowInInspector, ReadOnly]
        private XRNode _lastNode;

        [TitleGroup("Diagnostics"), ShowInInspector, ReadOnly]
        private InputDevice _lastDevice;

        [Button("Ping Head"), BoxGroup("Quick Test"), GUIColor(0.6f, 0.9f, 1f)]
        private void PingHead()
        {
            TryGetPose(XRNode.Head, out var pose, out var ts);
            _lastNode = XRNode.Head;
            Debug.Log($"[OpenXRTrackingProvider] Head → pos={pose.position}, rot={pose.rotation}, t={ts:F3}");
        }

        [Button("Ping Left"), BoxGroup("Quick Test")]
        private void PingLeft()
        {
            TryGetPose(XRNode.LeftHand, out var pose, out var ts);
            _lastNode = XRNode.LeftHand;
            Debug.Log($"[OpenXRTrackingProvider] Left → pos={pose.position}, rot={pose.rotation}, t={ts:F3}");
        }

        [Button("Ping Right"), BoxGroup("Quick Test")]
        private void PingRight()
        {
            TryGetPose(XRNode.RightHand, out var pose, out var ts);
            _lastNode = XRNode.RightHand;
            Debug.Log($"[OpenXRTrackingProvider] Right → pos={pose.position}, rot={pose.rotation}, t={ts:F3}");
        }

        public bool TryGetPose(XRNode node, out PoseF pose, out double timestamp)
        {
            var dev = InputDevices.GetDeviceAtXRNode(node);
            _lastDevice = dev;

            timestamp = Time.realtimeSinceStartupAsDouble;
            if (dev.isValid &&
                dev.TryGetFeatureValue(CommonUsages.devicePosition, out var p) &&
                dev.TryGetFeatureValue(CommonUsages.deviceRotation, out var r))
            {
                pose = new PoseF(p, r);
                return true;
            }
            pose = default;
            return false;
        }
    }
}
