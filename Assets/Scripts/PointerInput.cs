using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using static UnityEngine.InputSystem.EnhancedTouch.Touch;
using static UnityEngine.InputSystem.TouchPhase;

public class PointerInput : MonoBehaviour
{
  Coroutine mouseCaptureCoroutine;

  void OnEnable()
  {
    EnhancedTouchSupport.Enable();
    RequestMouseCapture();
  }

  void OnDisable()
  {
    if (this.mouseCaptureCoroutine != null)
    {
      StopCoroutine(this.mouseCaptureCoroutine);
      this.mouseCaptureCoroutine = null;
    }
    Android.SetMouseCapture(false);
    EnhancedTouchSupport.Disable();
  }

  void OnApplicationFocus(bool hasFocus)
  {
    if (!isActiveAndEnabled)
    {
      return;
    }
    if (!hasFocus)
    {
      Android.SetMouseCapture(false);
      return;
    }
    RequestMouseCapture();
  }

  void RequestMouseCapture()
  {
    if (this.mouseCaptureCoroutine != null)
    {
      StopCoroutine(this.mouseCaptureCoroutine);
    }
    this.mouseCaptureCoroutine = StartCoroutine(CaptureMouseAfterFocus());
  }

  IEnumerator CaptureMouseAfterFocus()
  {
    yield return null;
    this.mouseCaptureCoroutine = null;
    if (!isActiveAndEnabled || !Application.isFocused)
    {
      yield break;
    }
    Android.SetMouseCapture(true);
  }

  void Update()
  {
    HandleTap();
    HandleMouse();
  }

  const float MouseMovementDeltaMultiplier = 0.002F;
  const float MouseScrollDeltaMultiplier = 1.0F;
  void HandleMouse()
  {
    var mouse = Mouse.current;
    if (mouse == null)
    {
      return;
    }

    var movement = mouse.delta.ReadValue() * MouseMovementDeltaMultiplier;
    if (movement != Vector2.zero)
    {
      NativePlugin.yabe_compositor_move_pointer(movement.x, movement.y);
    }

    var scroll = mouse.scroll.ReadValue() * MouseScrollDeltaMultiplier;
    if (movement == Vector2.zero && scroll.y != 0.0F)
    {
      NativePlugin.yabe_compositor_axis(0, scroll.y);
    }
    if (movement == Vector2.zero && scroll.x != 0.0F)
    {
      NativePlugin.yabe_compositor_axis(1, scroll.x);
    }

    HandleMouseButton(mouse.leftButton, NativePlugin.PointerButtonLeft);
    HandleMouseButton(mouse.rightButton, NativePlugin.PointerButtonRight);
    HandleMouseButton(mouse.middleButton, NativePlugin.PointerButtonMiddle);
  }

  void HandleMouseButton(ButtonControl button, uint code)
  {
    if (button.wasPressedThisFrame)
    {
      NativePlugin.yabe_compositor_button(code, true);
      return;
    }
    if (button.wasReleasedThisFrame)
    {
      NativePlugin.yabe_compositor_button(code, false);
    }
  }

  enum TapStatus
  {
    Idle,
    Pending, // until TapThresholdSec passed from `Began` received
    LongTap,
    MoveOnly, // down event is not sent at the begenning
    Drag,
    Scroll,
  }
  TapStatus tapState = TapStatus.Idle;
  const float TapMovementDeltaMultiplier = 0.0008F;
  const float TapScrollDeltaMultiplier = 0.006F;
  const float TapThresholdSec = 0.2F;
  float passedSecFromBegan = 0.0F;
  void HandleTap()
  {
    IEnumerator SendMouseUpAfterDelay(uint button, float delay)
    {
      yield return new WaitForSeconds(delay);
      NativePlugin.yabe_compositor_button(button, false);
    }

    var touches = activeTouches;
    var phase = Ended;
    var deltaPosition = Vector2.zero;
    if (touches.Count > 0)
    {
      var touch = touches[0];
      phase = touch.phase;
      deltaPosition = touch.delta;
      if (phase == Moved && deltaPosition.sqrMagnitude <= 2.0F)
      {
        phase = Stationary; // discard small movement
      }
    }
    if (phase == Canceled)
    {
      phase = Ended;
    }

    switch (this.tapState)
    {
      case TapStatus.Idle:
        if (phase == Began)
        {
          this.tapState = TapStatus.Pending;
          this.passedSecFromBegan = 0.0F;
        }
        if (phase == Moved)
        {
          this.tapState = TapStatus.MoveOnly;
        }
        break;

      case TapStatus.Pending:
        if (touches.Count >= 2)
        {
          this.tapState = TapStatus.Scroll;
          break;
        }
        switch (phase)
        {
          case Moved:
            this.tapState = TapStatus.MoveOnly;
            break;
          case Stationary:
            this.passedSecFromBegan += Time.deltaTime;
            if (this.passedSecFromBegan > TapThresholdSec) // Long tap
            {
              this.tapState = TapStatus.LongTap;
              Android.Vibrate();
            }
            break;
          case Ended: // Tap duration is less than TapThreshold
            NativePlugin.yabe_compositor_button(NativePlugin.PointerButtonLeft, true);
            StartCoroutine(SendMouseUpAfterDelay(
              NativePlugin.PointerButtonLeft,
              this.passedSecFromBegan));
            this.tapState = TapStatus.Idle;
            break;
        }
        break;

      case TapStatus.LongTap:
        if (phase == Moved)
        {
          NativePlugin.yabe_compositor_button(NativePlugin.PointerButtonLeft, true);
          this.tapState = TapStatus.Drag;
        }
        else if (phase == Ended)
        {
          NativePlugin.yabe_compositor_button(NativePlugin.PointerButtonRight, true);
          StartCoroutine(SendMouseUpAfterDelay(NativePlugin.PointerButtonRight, 0.2F));
          this.tapState = TapStatus.Idle;
        }
        else if (touches.Count >= 2)
        {
          this.tapState = TapStatus.Scroll;
        }
        break;

      case TapStatus.MoveOnly:
        if (phase == Moved)
        {
          var v = deltaPosition * TapMovementDeltaMultiplier;
          NativePlugin.yabe_compositor_move_pointer(v.x, v.y);
        }
        else if (phase == Ended)
        {
          this.tapState = TapStatus.Idle;
        }
        else if (touches.Count >= 2)
        {
          this.tapState = TapStatus.Scroll;
        }
        break;
      case TapStatus.Drag:
        if (phase == Moved)
        {
          var v = deltaPosition * TapMovementDeltaMultiplier;
          NativePlugin.yabe_compositor_move_pointer(v.x, v.y);
        }
        if (phase == Ended)
        {
          NativePlugin.yabe_compositor_button(NativePlugin.PointerButtonLeft, false);
          this.tapState = TapStatus.Idle;
        }
        break;

      case TapStatus.Scroll:
        if (phase == Moved)
        {
          NativePlugin.yabe_compositor_axis(
            0,
            deltaPosition.sqrMagnitude * TapScrollDeltaMultiplier * (deltaPosition.y < 0 ? -1.0F : 1.0F));
        }
        else if (touches.Count < 2)
        {
          this.tapState = TapStatus.Idle;
        }
        break;
    }
  }
}
