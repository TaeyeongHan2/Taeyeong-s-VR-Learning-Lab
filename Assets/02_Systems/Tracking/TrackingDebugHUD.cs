using UnityEngine;
using TMPro;

namespace VRCore.Systems.Tracking
{
    public class TrackingDebugHUD : MonoBehaviour
    {
        public TrackingSamplerPro head, left, right;
        public TextMeshProUGUI text;

        void Update(){
            if (!text || !head || !left || !right) return;
            text.text =
                $@"<b>Tracking HUD</b>
FPS: {(1f/Time.smoothDeltaTime):0.0} Hz
Head - Hz:{head.SampleRateHz:0.0}  Jitter:{head.JitterRmsM*100f:0.0} cm  Lat:{head.LatencyMs:0.0} ms
Left - Hz:{left.SampleRateHz:0.0}  Jitter:{left.JitterRmsM*100f:0.0} cm  Lat:{left.LatencyMs:0.0} ms
Right- Hz:{right.SampleRateHz:0.0}  Jitter:{right.JitterRmsM*100f:0.0} cm  Lat:{right.LatencyMs:0.0} ms
Filter: {(head.filterEnabled ? "ON" : "OFF")}  |  Predict Δt: {head.predictLookahead*1000f:0} ms
[F]ilter toggle / [P]redict step";
            // 세 Sampler의 토글/파라미터를 동기화해도 좋다(옵션)
            left.filterEnabled = right.filterEnabled = head.filterEnabled;
            left.predictLookahead = right.predictLookahead = head.predictLookahead;
            left.posAlpha = right.posAlpha = head.posAlpha;
            left.rotAlpha = right.rotAlpha = head.rotAlpha;
        }
    }
}