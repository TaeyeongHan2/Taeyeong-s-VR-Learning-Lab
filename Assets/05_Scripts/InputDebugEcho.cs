using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

public class InputDebugEcho : MonoBehaviour
{
    [Header("테스트할 액션(버튼 타입 권장)")]
    [SerializeField] private InputActionReference actionRef;

    [Header("XR 컨트롤러 버튼 폴링도 병행 체크")]
    [SerializeField] private bool pollXRButtons = true;
    [SerializeField] private string xrButtonName = "primaryButton"; // A/X (Oculus/Quest)

    private InputAction _action;

    private void OnEnable()
    {
        _action = actionRef != null ? actionRef.action : null;

        if (_action != null)
        {
            _action.performed += OnPerformed;
            _action.canceled  += OnCanceled;
            _action.Enable();
            Debug.Log($"[InputDebugEcho] Enabled action '{_action.name}'.");
        }
        else
        {
            Debug.LogWarning("[InputDebugEcho] actionRef가 비어있습니다.");
        }
    }

    private void OnDisable()
    {
        if (_action != null)
        {
            _action.performed -= OnPerformed;
            _action.canceled  -= OnCanceled;
            _action.Disable();
        }
    }

    private void OnPerformed(InputAction.CallbackContext ctx)
    {
        var ctrl = ctx.control;
        var dev  = ctrl?.device;
        object val = null;
        try { val = ctx.ReadValueAsObject(); } catch { }

        Debug.Log($"[Action] '{_action.name}' PERFORMED | value={val} | path={ctrl?.path} | device={dev?.layout} '{dev?.displayName}'");
    }
    private void OnCanceled(InputAction.CallbackContext ctx)
    {
        var ctrl = ctx.control;
        var dev  = ctrl?.device;
        Debug.Log($"[Action] '{_action.name}' CANCELED  | path={ctrl?.path} | device={dev?.layout} '{dev?.displayName}'");
    }

    private void Update()
    {
        if (!pollXRButtons) return;

        // 모든 XRController 장치에서 지정 버튼이 눌렸는지 직접 폴링
        foreach (var xr in InputSystem.devices.OfType<XRController>())
        {
            // 예: "primaryButton", "secondaryButton", "menu", "triggerPressed", "gripPressed" 등
            var btn = xr.TryGetChildControl<ButtonControl>(xrButtonName);
            if (btn != null && btn.wasPressedThisFrame)
            {
                Debug.Log($"[XR Poll] {xrButtonName} pressed on device='{xr.displayName}' layout='{xr.layout}' path='{btn.path}'");
            }
        }
    }
}
