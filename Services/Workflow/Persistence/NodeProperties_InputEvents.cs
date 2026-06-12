using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreKeyPressEventNodeProperties(KeyPressEventNode keyPressNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("PressDelay", out var pdObj2) && double.TryParse(pdObj2?.ToString(), out var pd2))
                keyPressNode.PressDelay = pd2;
            else if (properties.TryGetValue("PressDelayMs", out var pdObj) && double.TryParse(pdObj?.ToString(), out var pd))
                keyPressNode.PressDelay = pd;

            if (properties.TryGetValue("DelayUnit", out var duObj))
                keyPressNode.DelayUnit = duObj?.ToString() ?? "ms";

            if (properties.TryGetValue("IsAsync", out var isAsyncObj) && bool.TryParse(isAsyncObj?.ToString(), out var isA))
                keyPressNode.IsAsync = isA;

            // Hold properties
            if (properties.TryGetValue("HoldDuration", out var hdObj) && double.TryParse(hdObj?.ToString(), out var hd))
                keyPressNode.HoldDuration = hd;
            if (properties.TryGetValue("HoldDurationUnit", out var hduObj))
                keyPressNode.HoldDurationUnit = hduObj?.ToString() ?? "ms";
            if (properties.TryGetValue("IsHoldAsync", out var isHAObj) && bool.TryParse(isHAObj?.ToString(), out var isHA))
                keyPressNode.IsHoldAsync = isHA;

            // Position properties
            if (properties.TryGetValue("ManualPosition_X", out var posX) && properties.TryGetValue("ManualPosition_Y", out var posY))
            {
                keyPressNode.ManualPosition = new Point(double.Parse(posX.ToString()!), double.Parse(posY.ToString()!));
            }
            if (properties.TryGetValue("HasManualPosition", out var hasPos))
                keyPressNode.HasManualPosition = bool.Parse(hasPos.ToString()!);

            // Coord source from other node
            if (properties.TryGetValue("CoordSourceNodeId", out var csni))
                keyPressNode.CoordSourceNodeId = csni?.ToString();
            if (properties.TryGetValue("CoordSourceOutputKey", out var csok))
                keyPressNode.CoordSourceOutputKey = csok?.ToString();

            // Click on position
            if (properties.TryGetValue("ClickOnPosition", out var cop))
                keyPressNode.ClickOnPosition = bool.Parse(cop.ToString()!);
            if (properties.TryGetValue("ClickDurationMs", out var cdm) && int.TryParse(cdm?.ToString(), out var cdmVal))
                keyPressNode.ClickDurationMs = cdmVal;

            // Target window
            if (properties.TryGetValue("TargetProcessName", out var tpn))
                keyPressNode.TargetProcessName = tpn?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TargetWindowTitle", out var twt))
                keyPressNode.TargetWindowTitle = twt?.ToString() ?? string.Empty;

            // Return to original screen
            if (properties.TryGetValue("ReturnToOriginalScreen", out var rtos))
                keyPressNode.ReturnToOriginalScreen = bool.Parse(rtos.ToString()!);

    }

    private static void RestoreHotkeyPressEventNodeProperties(HotkeyPressEventNode hotkeyPressNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("PressDelayMs", out var pdObj) && int.TryParse(pdObj?.ToString(), out var pd))
                hotkeyPressNode.PressDelayMs = pd;

            // Trigger mode
            if (properties.TryGetValue("TriggerMode", out var tmObj))
            {
                if (Enum.TryParse<Models.Enums.HotkeyTriggerModeEnum>(tmObj?.ToString(), out var tm))
                    hotkeyPressNode.TriggerMode = tm;
            }

            // Position properties
            if (properties.TryGetValue("ManualPosition_X", out var posX) && properties.TryGetValue("ManualPosition_Y", out var posY))
            {
                hotkeyPressNode.ManualPosition = new Point(double.Parse(posX.ToString()!), double.Parse(posY.ToString()!));
            }
            if (properties.TryGetValue("HasManualPosition", out var hasPos))
                hotkeyPressNode.HasManualPosition = bool.Parse(hasPos.ToString()!);

            // Coord source from other node
            if (properties.TryGetValue("CoordSourceNodeId", out var csni))
                hotkeyPressNode.CoordSourceNodeId = csni?.ToString();
            if (properties.TryGetValue("CoordSourceOutputKey", out var csok))
                hotkeyPressNode.CoordSourceOutputKey = csok?.ToString();

            // Click on position
            if (properties.TryGetValue("ClickOnPosition", out var cop))
                hotkeyPressNode.ClickOnPosition = bool.Parse(cop.ToString()!);
            if (properties.TryGetValue("ClickDurationMs", out var cdm) && int.TryParse(cdm?.ToString(), out var cdmVal))
                hotkeyPressNode.ClickDurationMs = cdmVal;

            // Target window
            if (properties.TryGetValue("TargetProcessName", out var tpn))
                hotkeyPressNode.TargetProcessName = tpn?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TargetWindowTitle", out var twt))
                hotkeyPressNode.TargetWindowTitle = twt?.ToString() ?? string.Empty;

            // Return to original screen
            if (properties.TryGetValue("ReturnToOriginalScreen", out var rtos))
                hotkeyPressNode.ReturnToOriginalScreen = bool.Parse(rtos.ToString()!);

    }

    private static void RestoreMouseEventNodeProperties(MouseEventNode mouseNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("MouseButton", out var btn))
                mouseNode.MouseButton = btn?.ToString() ?? "Left";

            if (properties.TryGetValue("RepeatCount", out var rep) && int.TryParse(rep?.ToString(), out var repVal))
                mouseNode.RepeatCount = repVal;

            if (properties.TryGetValue("HoldDuration", out var hold) && double.TryParse(hold?.ToString(), out var holdVal))
                mouseNode.HoldDuration = holdVal;

            if (properties.TryGetValue("ScrollSpeed", out var speed) && int.TryParse(speed?.ToString(), out var speedVal))
                mouseNode.ScrollSpeed = speedVal;

            // Position properties
            if (properties.TryGetValue("ManualPosition_X", out var posX) && properties.TryGetValue("ManualPosition_Y", out var posY))
            {
                mouseNode.ManualPosition = new Point(double.Parse(posX.ToString()!), double.Parse(posY.ToString()!));
            }
            if (properties.TryGetValue("HasManualPosition", out var hasPos))
                mouseNode.HasManualPosition = bool.Parse(hasPos.ToString()!);

            // Coord source from other node
            if (properties.TryGetValue("CoordSourceNodeId", out var csni))
                mouseNode.CoordSourceNodeId = csni?.ToString();
            if (properties.TryGetValue("CoordSourceOutputKey", out var csok))
                mouseNode.CoordSourceOutputKey = csok?.ToString();

            // Click on position
            if (properties.TryGetValue("ClickOnPosition", out var cop))
                mouseNode.ClickOnPosition = bool.Parse(cop.ToString()!);
            if (properties.TryGetValue("ClickDurationMs", out var cdm) && int.TryParse(cdm?.ToString(), out var cdmVal))
                mouseNode.ClickDurationMs = cdmVal;

            // Target window
            if (properties.TryGetValue("TargetProcessName", out var tpn))
                mouseNode.TargetProcessName = tpn?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TargetWindowTitle", out var twt))
                mouseNode.TargetWindowTitle = twt?.ToString() ?? string.Empty;

            // Return to original screen
            if (properties.TryGetValue("ReturnToOriginalScreen", out var rtos))
                mouseNode.ReturnToOriginalScreen = bool.Parse(rtos.ToString()!);

    }

    private static void RestoreScreenPositionPickerNodeProperties(ScreenPositionPickerNode pos, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("X_Pos", out var x) && properties.TryGetValue("Y_Pos", out var y))
            {
                pos.SelectedPosition = new Point(double.Parse(x.ToString()!), double.Parse(y.ToString()!));
            }
            if (properties.TryGetValue("HasPosition", out var hp))
                pos.HasPosition = bool.Parse(hp.ToString()!);
            if (properties.TryGetValue("CoordSourceNodeId", out var csni))
                pos.CoordSourceNodeId = csni?.ToString();
            if (properties.TryGetValue("CoordSourceOutputKey", out var csok))
                pos.CoordSourceOutputKey = csok?.ToString();
            if (properties.TryGetValue("MouseAction", out var ma) &&
                Enum.TryParse<ScreenPositionMouseAction>(ma?.ToString(), out var maVal))
                pos.MouseAction = maVal;
            if (properties.TryGetValue("ClickCount", out var cc) && int.TryParse(cc?.ToString(), out var ccVal))
                pos.ClickCount = ccVal;
            if (properties.TryGetValue("HoldDurationMs", out var hd) && int.TryParse(hd?.ToString(), out var hdVal))
                pos.HoldDurationMs = hdVal;
            if (properties.TryGetValue("ScrollCount", out var sc) && int.TryParse(sc?.ToString(), out var scVal))
                pos.ScrollCount = scVal;
            if (properties.TryGetValue("ScrollIntervalMs", out var si) && int.TryParse(si?.ToString(), out var siVal))
                pos.ScrollIntervalMs = siVal;

            // Target window
            if (properties.TryGetValue("TargetProcessName", out var tpn))
                pos.TargetProcessName = tpn?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TargetWindowTitle", out var twt))
                pos.TargetWindowTitle = twt?.ToString() ?? string.Empty;

            // Return to original screen
            if (properties.TryGetValue("ReturnToOriginalScreen", out var rtos))
                pos.ReturnToOriginalScreen = bool.Parse(rtos.ToString()!);
    }

    // -- GET (Serialize) --

    private static void GetKeyPressEventNodeProperties(KeyPressEventNode kp, Dictionary<string, object> dict)
    {
            if (kp.RepeatCount != 1)
                dict["RepeatCount"] = kp.RepeatCount;
            if (Math.Abs(kp.PressDelay - 100) > 0.000001)
                dict["PressDelay"] = kp.PressDelay;
            if (kp.DelayUnit != "ms")
                dict["DelayUnit"] = kp.DelayUnit;
            if (kp.IsAsync)
                dict["IsAsync"] = kp.IsAsync;

            // Hold properties
            if (Math.Abs(kp.HoldDuration - 0) > 0.000001)
                dict["HoldDuration"] = kp.HoldDuration;
            if (kp.HoldDurationUnit != "ms")
                dict["HoldDurationUnit"] = kp.HoldDurationUnit;
            if (kp.IsHoldAsync)
                dict["IsHoldAsync"] = kp.IsHoldAsync;

            // Position properties
            dict["ManualPosition_X"] = kp.ManualPosition.X;
            dict["ManualPosition_Y"] = kp.ManualPosition.Y;
            dict["HasManualPosition"] = kp.HasManualPosition;

            // Coord source from other node
            if (!string.IsNullOrEmpty(kp.CoordSourceNodeId))
                dict["CoordSourceNodeId"] = kp.CoordSourceNodeId;
            if (!string.IsNullOrEmpty(kp.CoordSourceOutputKey))
                dict["CoordSourceOutputKey"] = kp.CoordSourceOutputKey;

            // Click on position
            dict["ClickOnPosition"] = kp.ClickOnPosition;
            dict["ClickDurationMs"] = kp.ClickDurationMs;

            // Target window
            if (!string.IsNullOrEmpty(kp.TargetProcessName))
                dict["TargetProcessName"] = kp.TargetProcessName;
            if (!string.IsNullOrEmpty(kp.TargetWindowTitle))
                dict["TargetWindowTitle"] = kp.TargetWindowTitle;

            // Return to original screen
            dict["ReturnToOriginalScreen"] = kp.ReturnToOriginalScreen;

    }

    private static void GetHotkeyPressEventNodeProperties(HotkeyPressEventNode hk, Dictionary<string, object> dict)
    {
            if (hk.RepeatCount != 1)
                dict["RepeatCount"] = hk.RepeatCount;
            if (hk.PressDelayMs != 50)
                dict["PressDelayMs"] = hk.PressDelayMs;

            // Trigger mode
            if (hk.TriggerMode != Models.Enums.HotkeyTriggerModeEnum.Send)
                dict["TriggerMode"] = hk.TriggerMode.ToString();

            // Position properties
            dict["ManualPosition_X"] = hk.ManualPosition.X;
            dict["ManualPosition_Y"] = hk.ManualPosition.Y;
            dict["HasManualPosition"] = hk.HasManualPosition;

            // Coord source from other node
            if (!string.IsNullOrEmpty(hk.CoordSourceNodeId))
                dict["CoordSourceNodeId"] = hk.CoordSourceNodeId;
            if (!string.IsNullOrEmpty(hk.CoordSourceOutputKey))
                dict["CoordSourceOutputKey"] = hk.CoordSourceOutputKey;

            // Click on position
            dict["ClickOnPosition"] = hk.ClickOnPosition;
            dict["ClickDurationMs"] = hk.ClickDurationMs;

            // Target window
            if (!string.IsNullOrEmpty(hk.TargetProcessName))
                dict["TargetProcessName"] = hk.TargetProcessName;
            if (!string.IsNullOrEmpty(hk.TargetWindowTitle))
                dict["TargetWindowTitle"] = hk.TargetWindowTitle;

            // Return to original screen
            dict["ReturnToOriginalScreen"] = hk.ReturnToOriginalScreen;

    }

    private static void GetMouseEventNodeProperties(MouseEventNode mouseNode, Dictionary<string, object> dict)
    {
            dict["MouseButton"] = mouseNode.MouseButton;
            dict["RepeatCount"] = mouseNode.RepeatCount;
            dict["HoldDuration"] = mouseNode.HoldDuration;
            dict["ScrollSpeed"] = mouseNode.ScrollSpeed;

            // Position properties
            dict["ManualPosition_X"] = mouseNode.ManualPosition.X;
            dict["ManualPosition_Y"] = mouseNode.ManualPosition.Y;
            dict["HasManualPosition"] = mouseNode.HasManualPosition;

            // Coord source from other node
            if (!string.IsNullOrEmpty(mouseNode.CoordSourceNodeId))
                dict["CoordSourceNodeId"] = mouseNode.CoordSourceNodeId;
            if (!string.IsNullOrEmpty(mouseNode.CoordSourceOutputKey))
                dict["CoordSourceOutputKey"] = mouseNode.CoordSourceOutputKey;

            // Click on position
            dict["ClickOnPosition"] = mouseNode.ClickOnPosition;
            dict["ClickDurationMs"] = mouseNode.ClickDurationMs;

            // Target window
            if (!string.IsNullOrEmpty(mouseNode.TargetProcessName))
                dict["TargetProcessName"] = mouseNode.TargetProcessName;
            if (!string.IsNullOrEmpty(mouseNode.TargetWindowTitle))
                dict["TargetWindowTitle"] = mouseNode.TargetWindowTitle;

            // Return to original screen
            dict["ReturnToOriginalScreen"] = mouseNode.ReturnToOriginalScreen;

    }

    private static void GetScreenPositionPickerNodeProperties(ScreenPositionPickerNode pos, Dictionary<string, object> dict)
    {
            dict["X_Pos"] = pos.SelectedPosition.X;
            dict["Y_Pos"] = pos.SelectedPosition.Y;
            dict["HasPosition"] = pos.HasPosition;
            if (!string.IsNullOrEmpty(pos.CoordSourceNodeId))
                dict["CoordSourceNodeId"] = pos.CoordSourceNodeId;
            if (!string.IsNullOrEmpty(pos.CoordSourceOutputKey))
                dict["CoordSourceOutputKey"] = pos.CoordSourceOutputKey;
            dict["MouseAction"]     = pos.MouseAction.ToString();
            dict["ClickCount"]      = pos.ClickCount;
            dict["HoldDurationMs"]  = pos.HoldDurationMs;
            dict["ScrollCount"]     = pos.ScrollCount;
            dict["ScrollIntervalMs"] = pos.ScrollIntervalMs;

            // Target window
            if (!string.IsNullOrEmpty(pos.TargetProcessName))
                dict["TargetProcessName"] = pos.TargetProcessName;
            if (!string.IsNullOrEmpty(pos.TargetWindowTitle))
                dict["TargetWindowTitle"] = pos.TargetWindowTitle;

            // Return to original screen
            dict["ReturnToOriginalScreen"] = pos.ReturnToOriginalScreen;
    }

}
