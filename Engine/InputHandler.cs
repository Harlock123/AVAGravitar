using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;

namespace AVAGravitar.Engine;

/// <summary>
/// Keyboard, mouse and gamepad, boiled down to the cabinet's five controls: rotate left, rotate right,
/// thrust, fire, and shield (which is also the tractor beam).
/// </summary>
public class InputHandler : IDisposable
{
    private readonly HashSet<Key> _pressedKeys = new();
    private readonly ControllerManager _controller = new();

    private const float TriggerThreshold = 0.3f;
    private const float AimStick = 0.55f;

    private bool _leftMousePressed;
    private bool _rightMousePressed;
    private bool _middleMousePressed;
    private double _mouseWheelDelta;

    public void OnKeyDown(Key key)
    {
        _pressedKeys.Add(key);
        // A rotate key takes the ship back from the mouse.
        if (key is Key.Left or Key.Right or Key.A or Key.D)
            MouseAiming = false;
    }

    public void OnKeyUp(Key key) => _pressedKeys.Remove(key);

    public bool IsKeyPressed(Key key) => _pressedKeys.Contains(key);

    public void OnMouseDown(MouseButton button)
    {
        if (button == MouseButton.Left)
            _leftMousePressed = true;
        else if (button == MouseButton.Right)
            _rightMousePressed = true;
        else if (button == MouseButton.Middle)
            _middleMousePressed = true;
    }

    public void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
            _leftMousePressed = false;
        else if (button == MouseButton.Right)
            _rightMousePressed = false;
        else if (button == MouseButton.Middle)
            _middleMousePressed = false;
    }

    public void OnMouseWheel(double delta) => _mouseWheelDelta += delta;

    /// <summary>Where the mouse pointer is on the game's 1024×768 screen.</summary>
    public Point Pointer { get; private set; }

    /// <summary>Set once the mouse moves: the ship then turns to face the pointer.</summary>
    public bool MouseAiming { get; set; }

    public void OnPointer(Point screen)
    {
        if (Math.Abs(Pointer.X - screen.X) + Math.Abs(Pointer.Y - screen.Y) > 3) MouseAiming = true;
        Pointer = screen;
    }

    public void Update() => _controller.Update();

    public void PostUpdate() => _mouseWheelDelta = 0;

    public bool IsLeftMousePressed => _leftMousePressed;
    public double MouseWheelDelta => _mouseWheelDelta;
    public bool IsControllerConnected => _controller.IsConnected;

    // Controller-only menu inputs, for initials entry (the keyboard is handled key by key there).
    public bool PadUp => _controller.DPadUp || _controller.LeftStickY < -0.5f;
    public bool PadDown => _controller.DPadDown || _controller.LeftStickY > 0.5f;
    public bool PadLeft => _controller.DPadLeft || _controller.LeftStickX < -0.5f;
    public bool PadRight => _controller.DPadRight || _controller.LeftStickX > 0.5f;
    public bool PadAccept => _controller.ButtonX || _controller.ButtonB || _controller.ButtonA;
    public bool PadDone => _controller.ButtonStart;

    /// <summary>Any key, mouse button, wheel turn or controller input at all this frame.</summary>
    public bool IsAnyInput =>
        _pressedKeys.Count > 0 ||
        _leftMousePressed || _rightMousePressed || _middleMousePressed || _mouseWheelDelta != 0 ||
        Math.Abs(_controller.LeftStickX) > 0.2 || Math.Abs(_controller.LeftStickY) > 0.2 ||
        IsFiring || IsThrusting || IsShielding || IsPausePressed || IsBackPressed ||
        _controller.DPadLeft || _controller.DPadRight;

    /// <summary>−1 to turn anticlockwise, +1 clockwise.</summary>
    public double Turn
    {
        get
        {
            double t = 0;
            if (IsKeyPressed(Key.Left) || IsKeyPressed(Key.A) || _controller.DPadLeft) t -= 1;
            if (IsKeyPressed(Key.Right) || IsKeyPressed(Key.D) || _controller.DPadRight) t += 1;
            return t;
        }
    }

    /// <summary>A direction to turn the ship to, when the left stick is pushed well over.</summary>
    public double? StickAngle
    {
        get
        {
            double x = _controller.LeftStickX, y = _controller.LeftStickY;
            if (x * x + y * y < AimStick * AimStick) return null;
            return Math.Atan2(x, -y);
        }
    }

    public bool IsThrusting =>
        IsKeyPressed(Key.Up) || IsKeyPressed(Key.W) || _rightMousePressed ||
        _controller.ButtonA || _controller.RightTrigger > TriggerThreshold;

    public bool IsFiring =>
        IsKeyPressed(Key.Space) || IsKeyPressed(Key.LeftCtrl) || IsKeyPressed(Key.RightCtrl) || _leftMousePressed ||
        _controller.ButtonX || _controller.ButtonB || _controller.RightBumper;

    public bool IsShielding =>
        IsKeyPressed(Key.Down) || IsKeyPressed(Key.S) || IsKeyPressed(Key.LeftShift) || IsKeyPressed(Key.RightShift) ||
        _middleMousePressed || _controller.ButtonY || _controller.LeftBumper || _controller.LeftTrigger > TriggerThreshold;

    /// <summary>What starts a game from the title: fire, Enter, or the A button.</summary>
    public bool IsStartPressed => IsFiring || IsKeyPressed(Key.Enter) || _controller.ButtonA;

    public bool IsPausePressed => _controller.ButtonStart;

    public bool IsBackPressed => _controller.ButtonBack;

    public void Dispose() => _controller.Dispose();
}
