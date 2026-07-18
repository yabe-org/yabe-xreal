using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardInput : MonoBehaviour
{
  bool keyboardInitialized;
  readonly HashSet<uint> pressedKeys = new();

  void OnDisable()
  {
    ReleasePressedKeys();
    if (!this.keyboardInitialized)
    {
      return;
    }
    NativePlugin.destroy_keyboard();
    this.keyboardInitialized = false;
  }

  void OnApplicationFocus(bool hasFocus)
  {
    if (!hasFocus)
    {
      ReleasePressedKeys();
    }
  }

  void ReleasePressedKeys()
  {
    if (this.keyboardInitialized && NativePlugin.is_compositor_running())
    {
      foreach (var key in this.pressedKeys)
      {
        NativePlugin.send_keyboard_key(key, false);
      }
    }
    this.pressedKeys.Clear();
  }

  void Update()
  {
    if (!NativePlugin.is_compositor_running())
    {
      this.pressedKeys.Clear();
      if (this.keyboardInitialized)
      {
        NativePlugin.destroy_keyboard();
        this.keyboardInitialized = false;
      }
      return;
    }
    if (!this.keyboardInitialized)
    {
      this.keyboardInitialized = NativePlugin.initialize_keyboard();
      if (!this.keyboardInitialized)
      {
        return;
      }
    }

    var keyboard = Keyboard.current;
    if (keyboard == null)
    {
      return;
    }
    foreach (var keyControl in keyboard.allKeys)
    {
      var key = ToEvdevKeyCode(keyControl.keyCode);
      if (key == 0)
      {
        continue;
      }
      if (keyControl.wasPressedThisFrame)
      {
        NativePlugin.send_keyboard_key(key, true);
        this.pressedKeys.Add(key);
      }
      else if (keyControl.wasReleasedThisFrame)
      {
        NativePlugin.send_keyboard_key(key, false);
        this.pressedKeys.Remove(key);
      }
    }
  }

  // Convert UnityEngine.InputSystem.Key to the Linux key code (linux/input-event-codes.h)
  static uint ToEvdevKeyCode(Key key) => key switch
  {
    Key.Escape => 1,
    Key.Digit1 => 2,
    Key.Digit2 => 3,
    Key.Digit3 => 4,
    Key.Digit4 => 5,
    Key.Digit5 => 6,
    Key.Digit6 => 7,
    Key.Digit7 => 8,
    Key.Digit8 => 9,
    Key.Digit9 => 10,
    Key.Digit0 => 11,
    Key.Minus => 12,
    Key.Equals => 13,
    Key.Backspace => 14,
    Key.Tab => 15,
    Key.Q => 16,
    Key.W => 17,
    Key.E => 18,
    Key.R => 19,
    Key.T => 20,
    Key.Y => 21,
    Key.U => 22,
    Key.I => 23,
    Key.O => 24,
    Key.P => 25,
    Key.LeftBracket => 26,
    Key.RightBracket => 27,
    Key.Enter => 28,
    Key.LeftCtrl => 29,
    Key.A => 30,
    Key.S => 31,
    Key.D => 32,
    Key.F => 33,
    Key.G => 34,
    Key.H => 35,
    Key.J => 36,
    Key.K => 37,
    Key.L => 38,
    Key.Semicolon => 39,
    Key.Quote => 40,
    Key.Backquote => 41,
    Key.LeftShift => 42,
    Key.Backslash => 43,
    Key.Z => 44,
    Key.X => 45,
    Key.C => 46,
    Key.V => 47,
    Key.B => 48,
    Key.N => 49,
    Key.M => 50,
    Key.Comma => 51,
    Key.Period => 52,
    Key.Slash => 53,
    Key.RightShift => 54,
    Key.NumpadMultiply => 55,
    Key.LeftAlt => 56,
    Key.Space => 57,
    Key.CapsLock => 58,
    Key.F1 => 59,
    Key.F2 => 60,
    Key.F3 => 61,
    Key.F4 => 62,
    Key.F5 => 63,
    Key.F6 => 64,
    Key.F7 => 65,
    Key.F8 => 66,
    Key.F9 => 67,
    Key.F10 => 68,
    Key.NumLock => 69,
    Key.ScrollLock => 70,
    Key.Numpad7 => 71,
    Key.Numpad8 => 72,
    Key.Numpad9 => 73,
    Key.NumpadMinus => 74,
    Key.Numpad4 => 75,
    Key.Numpad5 => 76,
    Key.Numpad6 => 77,
    Key.NumpadPlus => 78,
    Key.Numpad1 => 79,
    Key.Numpad2 => 80,
    Key.Numpad3 => 81,
    Key.Numpad0 => 82,
    Key.NumpadPeriod => 83,
    Key.F11 => 87,
    Key.F12 => 88,
    Key.NumpadEnter => 96,
    Key.RightCtrl => 97,
    Key.NumpadDivide => 98,
    Key.PrintScreen => 99,
    Key.RightAlt => 100,
    Key.Home => 102,
    Key.UpArrow => 103,
    Key.PageUp => 104,
    Key.LeftArrow => 105,
    Key.RightArrow => 106,
    Key.End => 107,
    Key.DownArrow => 108,
    Key.PageDown => 109,
    Key.Insert => 110,
    Key.Delete => 111,
    Key.NumpadEquals => 117,
    Key.Pause => 119,
    Key.LeftMeta => 125,
    Key.RightMeta => 126,
    Key.ContextMenu => 127,
    Key.MediaPlayPause => 164,
    Key.MediaRewind => 168,
    Key.F13 => 183,
    Key.F14 => 184,
    Key.F15 => 185,
    Key.F16 => 186,
    Key.F17 => 187,
    Key.F18 => 188,
    Key.F19 => 189,
    Key.F20 => 190,
    Key.F21 => 191,
    Key.F22 => 192,
    Key.F23 => 193,
    Key.F24 => 194,
    Key.MediaForward => 208,
    _ => 0,
  };

}
