using UnityEngine;

namespace BootStrap
{
    [DefaultExecutionOrder(-100000)]
    public class DebugDisable : MonoBehaviour
    {
        private void Awake()
        {
#if !UNITY_EDITOR
        Debug.unityLogger.logEnabled = false;
#endif
        }
    }
}