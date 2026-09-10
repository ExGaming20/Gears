using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using OpenTK.Mathematics;

public static class Input
{
    public static GameWindow? CurrentWindow { get; set; }

    private static KeyboardState _currentKeyboard;
    private static MouseState _currentMouse;

    // Flags to track whether we have a valid state
    private static bool _hasValidKeyboard;
    private static bool _hasValidMouse;

    // Current values
    public static bool IsMouseLocked;

    public static void Update()
    {
        var window = CurrentWindow;
        if (window != null && window.IsFocused)
        {
            // Valid states from the window
            _currentKeyboard = window.KeyboardState;
            _currentMouse = window.MouseState;
            _hasValidKeyboard = true;
            _hasValidMouse = true;
        }
        else
        {
            // No valid states this frame – keep previous values but mark as invalid
            _hasValidKeyboard = false;
            _hasValidMouse = false;
            // Do NOT assign default(_currentKeyboard/_currentMouse)
        }
    }

    // -----------------------------------------------------------------
    // Keyboard
    // -----------------------------------------------------------------
    public static bool GetKey(KeyCode key)
    {
        return _hasValidKeyboard && GetKeyState(key, _currentKeyboard);
    }

    public static bool GetKeyDown(KeyCode key)
    {
        return _currentKeyboard.IsKeyDown(MapKeyCode(key));
    }

    public static bool GetKeyUp(KeyCode key)
    {
        return !_currentKeyboard.IsKeyDown(MapKeyCode(key));
    }

    public static bool GetKeyPressed(KeyCode key)
    {
        return _currentKeyboard.IsKeyPressed(MapKeyCode(key));
    }

    public static bool GetKeyReleased(KeyCode key)
    {
        return _currentKeyboard.IsKeyReleased(MapKeyCode(key));
    }

    private static bool GetKeyState(KeyCode key, KeyboardState state)
    {
        Keys openTkKey = MapKeyCode(key);

        return openTkKey != Keys.Unknown && state.IsKeyDown(openTkKey);
    }

    // -----------------------------------------------------------------
    // Mouse buttons
    // -----------------------------------------------------------------
    public static bool GetMouseButton(MouseButtons button)
    {
        return _hasValidMouse && _currentMouse.IsButtonDown(MapMouseButton(button));
    }

    public static bool GetMouseButtonPressed(MouseButtons button)
    {
        return _hasValidMouse && _currentMouse.IsButtonPressed(MapMouseButton(button));
    }

    public static bool GetMouseButtonDown(MouseButtons button)
    {
        return _hasValidMouse && _currentMouse.IsButtonDown(MapMouseButton(button));
    }

    public static bool GetMouseButtonUp(MouseButtons button)
    {
        return _hasValidMouse && !_currentMouse.IsButtonDown(MapMouseButton(button));
    }

    public static bool GetMouseButtonReleased(MouseButtons button)
    {
        return _hasValidMouse && _currentMouse.IsButtonReleased(MapMouseButton(button));
    }

    private static bool GetMouseButtonState(MouseButtons button, MouseState state)
    {
        // Direct cast works because our enum uses the same underlying values as OpenTK's MouseButton.
        return state.IsButtonDown((OpenTK.Windowing.GraphicsLibraryFramework.MouseButton)button);
    }

    // -----------------------------------------------------------------
    // Mouse buttons enum/mapping
    // -----------------------------------------------------------------
    public enum MouseButtons
    {
        Left = 0,
        Right = 1,
        Middle = 2,
        Button4 = 3,
        Button5 = 4,
        Button6 = 5,
        Button7 = 6,
        Button8 = 7
    }

    private static OpenTK.Windowing.GraphicsLibraryFramework.MouseButton MapMouseButton(MouseButtons button)
    {
        return button switch
        {
            MouseButtons.Left => OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left,
            MouseButtons.Right => OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right,
            MouseButtons.Middle => OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Middle,
            MouseButtons.Button4 => OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button4,
            MouseButtons.Button5 => OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button5,
            MouseButtons.Button6 => OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button6,
            MouseButtons.Button7 => OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button7,
            MouseButtons.Button8 => OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Button8,
            _ => throw new ArgumentOutOfRangeException(nameof(button), $"Unsupported mouse button: {button}")
        };
    }

    // -----------------------------------------------------------------
    // Mouse scroll delta – also protect against invalid state
    // -----------------------------------------------------------------
    public static float GetMouseScrollDeltaY() => _hasValidMouse ? (float)_currentMouse.Scroll.Y : 0f;
    public static float GetMouseScrollDeltaX() => _hasValidMouse ? (float)_currentMouse.Scroll.X : 0f;

    // -----------------------------------------------------------------
    // Mouse delta – also protect against invalid state
    // -----------------------------------------------------------------
    public static Vector2 GetMouseDelta()
    {
        if (!_hasValidMouse)
            return Vector2.Zero;

        return new Vector2(_currentMouse.Delta.X, _currentMouse.Delta.Y);
    }

    // -----------------------------------------------------------------
    // Cursor locking (unchanged)
    // -----------------------------------------------------------------
    public static void SetCursorState(MouseStates state)
    {
        var window = CurrentWindow;

        if (window != null)
        {
            IsMouseLocked = state == MouseStates.Grabbed;

            window.CursorState = (OpenTK.Windowing.Common.CursorState)state;
        }
    }

    // -----------------------------------------------------------------
    // CursorState enum
    // -----------------------------------------------------------------
    public enum MouseStates
    {
        Normal = CursorState.Normal,
        Hidden = CursorState.Hidden,
        Grabbed = CursorState.Grabbed
    }

    // -----------------------------------------------------------------
    // Mouse
    // -----------------------------------------------------------------
    public static Vector2 GetMousePosition()
    {
        if (!_hasValidMouse)
        {
            return Vector2.Zero;
        }

        var window = CurrentWindow;

        if (window == null)
        {
            return Vector2.Zero;
        }

        return new Vector2((float)_currentMouse.X, (float)_currentMouse.Y);
    }

    // -----------------------------------------------------------------
    // GetAxis
    // -----------------------------------------------------------------
    public static float GetAxis(string axisName)
    {
        if (!_hasValidMouse) return 0f;
        var window = CurrentWindow;
        if (window == null) return 0f;
        return axisName switch
        {
            "Mouse X" => (float)_currentMouse.X,
            "Mouse Y" => (float)_currentMouse.Y,
            "Horizontal" => (GetKey(KeyCode.A) ? -1f : 0f) + (GetKey(KeyCode.D) ? 1f : 0f),
            "Vertical" => (GetKey(KeyCode.S) ? -1f : 0f) + (GetKey(KeyCode.W) ? 1f : 0f),
            _ => 0f
        };
    }

    // -----------------------------------------------------------------
    // KeyCode mapping
    // -----------------------------------------------------------------
    public enum KeyCode
    {
        // ----- Modifiers & special keys -----
        None,
        Backspace,
        Delete,
        Tab,
        Clear,
        Return,
        Pause,
        Escape,
        Space,

        // ----- Navigation keys -----
        UpArrow,
        DownArrow,
        RightArrow,
        LeftArrow,
        Insert,
        Home,
        End,
        PageUp,
        PageDown,

        // ----- Function keys -----
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,

        // ----- Alphanumeric keys (top row) -----
        Alpha0, Alpha1, Alpha2, Alpha3, Alpha4,
        Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,

        // ----- Letter keys (A–Z) -----
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

        // ----- Symbols on US keyboard (physical keys) -----
        Exclaim,       // !
        DoubleQuote,   // "
        Hash,          // #
        Dollar,        // $
        Percent,       // %
        Ampersand,     // &
        Quote,         // '
        LeftParen,     // (
        RightParen,    // )
        Asterisk,      // *
        Plus,          // +
        Comma,         // ,
        Minus,         // -
        Period,        // .
        Slash,         // /
        Colon,         // :
        Semicolon,     // ;
        Less,          // <
        Equals,        // =
        Greater,       // >
        Question,      // ?
        At,            // @
        LeftBracket,   // [
        Backslash,     // \
        RightBracket,  // ]
        Caret,         // ^
        Underscore,    // _
        BackQuote,     // `
        LeftCurlyBracket,  // {
        Pipe,               // |
        RightCurlyBracket,  // }
        Tilde,              // ~

        // ----- Keypad keys -----
        Keypad0, Keypad1, Keypad2, Keypad3, Keypad4,
        Keypad5, Keypad6, Keypad7, Keypad8, Keypad9,
        KeypadPeriod,
        KeypadDivide,
        KeypadMultiply,
        KeypadMinus,
        KeypadPlus,
        KeypadEnter,
        KeypadEquals,

        // ----- Lock & modifier keys -----
        Numlock,
        CapsLock,
        ScrollLock,
        RightShift,
        LeftShift,
        RightControl,
        LeftControl,
        RightAlt,
        LeftAlt,
        LeftMeta,      // Windows / Command key (left)
        RightMeta,     // Windows / Command key (right)
        AltGr,
        Help,
        Print,
        SysReq,
        Break,
        Menu,

        // ----- Mouse buttons (clicks) -----
        Mouse0, Mouse1, Mouse2, Mouse3, Mouse4, Mouse5, Mouse6
    }

    private static Keys MapKeyCode(KeyCode key)
    {
        return key switch
        {
            // Letters
            KeyCode.A => Keys.A,
            KeyCode.B => Keys.B,
            KeyCode.C => Keys.C,
            KeyCode.D => Keys.D,
            KeyCode.E => Keys.E,
            KeyCode.F => Keys.F,
            KeyCode.G => Keys.G,
            KeyCode.H => Keys.H,
            KeyCode.I => Keys.I,
            KeyCode.J => Keys.J,
            KeyCode.K => Keys.K,
            KeyCode.L => Keys.L,
            KeyCode.M => Keys.M,
            KeyCode.N => Keys.N,
            KeyCode.O => Keys.O,
            KeyCode.P => Keys.P,
            KeyCode.Q => Keys.Q,
            KeyCode.R => Keys.R,
            KeyCode.S => Keys.S,
            KeyCode.T => Keys.T,
            KeyCode.U => Keys.U,
            KeyCode.V => Keys.V,
            KeyCode.W => Keys.W,
            KeyCode.X => Keys.X,
            KeyCode.Y => Keys.Y,
            KeyCode.Z => Keys.Z,

            // Digits
            KeyCode.Alpha0 => Keys.D0,
            KeyCode.Alpha1 => Keys.D1,
            KeyCode.Alpha2 => Keys.D2,
            KeyCode.Alpha3 => Keys.D3,
            KeyCode.Alpha4 => Keys.D4,
            KeyCode.Alpha5 => Keys.D5,
            KeyCode.Alpha6 => Keys.D6,
            KeyCode.Alpha7 => Keys.D7,
            KeyCode.Alpha8 => Keys.D8,
            KeyCode.Alpha9 => Keys.D9,

            // Punctuation
            KeyCode.BackQuote => Keys.GraveAccent,
            KeyCode.Minus => Keys.Minus,
            KeyCode.Equals => Keys.Equal,
            KeyCode.LeftBracket => Keys.LeftBracket,
            KeyCode.RightBracket => Keys.RightBracket,
            KeyCode.Backslash => Keys.Backslash,
            KeyCode.Semicolon => Keys.Semicolon,
            KeyCode.Quote => Keys.Apostrophe,        // Unity uses 'Quote' for apostrophe
            KeyCode.Comma => Keys.Comma,
            KeyCode.Period => Keys.Period,
            KeyCode.Slash => Keys.Slash,

            // Modifiers
            KeyCode.LeftShift => Keys.LeftShift,
            KeyCode.RightShift => Keys.RightShift,
            KeyCode.LeftControl => Keys.LeftControl,
            KeyCode.RightControl => Keys.RightControl,
            KeyCode.LeftAlt => Keys.LeftAlt,
            KeyCode.RightAlt => Keys.RightAlt,
            KeyCode.LeftMeta => Keys.LeftSuper,   // LeftSuper = Left Windows / Command
            KeyCode.RightMeta => Keys.RightSuper,
            KeyCode.Menu => Keys.Menu,

            // Navigation & editing
            KeyCode.Space => Keys.Space,
            KeyCode.Escape => Keys.Escape,
            KeyCode.Tab => Keys.Tab,
            KeyCode.Return => Keys.Enter,
            KeyCode.Backspace => Keys.Backspace,
            KeyCode.Insert => Keys.Insert,
            KeyCode.Delete => Keys.Delete,
            KeyCode.Home => Keys.Home,
            KeyCode.End => Keys.End,
            KeyCode.PageUp => Keys.PageUp,
            KeyCode.PageDown => Keys.PageDown,
            KeyCode.UpArrow => Keys.Up,
            KeyCode.DownArrow => Keys.Down,
            KeyCode.LeftArrow => Keys.Left,
            KeyCode.RightArrow => Keys.Right,

            // Keypad
            KeyCode.Keypad0 => Keys.KeyPad0,
            KeyCode.Keypad1 => Keys.KeyPad1,
            KeyCode.Keypad2 => Keys.KeyPad2,
            KeyCode.Keypad3 => Keys.KeyPad3,
            KeyCode.Keypad4 => Keys.KeyPad4,
            KeyCode.Keypad5 => Keys.KeyPad5,
            KeyCode.Keypad6 => Keys.KeyPad6,
            KeyCode.Keypad7 => Keys.KeyPad7,
            KeyCode.Keypad8 => Keys.KeyPad8,
            KeyCode.Keypad9 => Keys.KeyPad9,
            KeyCode.KeypadPeriod => Keys.KeyPadDecimal,
            KeyCode.KeypadDivide => Keys.KeyPadDivide,
            KeyCode.KeypadMultiply => Keys.KeyPadMultiply,
            KeyCode.KeypadMinus => Keys.KeyPadSubtract,
            KeyCode.KeypadPlus => Keys.KeyPadAdd,
            KeyCode.KeypadEnter => Keys.KeyPadEnter,
            KeyCode.KeypadEquals => Keys.KeyPadEqual,

            // Function keys
            KeyCode.F1 => Keys.F1,
            KeyCode.F2 => Keys.F2,
            KeyCode.F3 => Keys.F3,
            KeyCode.F4 => Keys.F4,
            KeyCode.F5 => Keys.F5,
            KeyCode.F6 => Keys.F6,
            KeyCode.F7 => Keys.F7,
            KeyCode.F8 => Keys.F8,
            KeyCode.F9 => Keys.F9,
            KeyCode.F10 => Keys.F10,
            KeyCode.F11 => Keys.F11,
            KeyCode.F12 => Keys.F12,
            KeyCode.F13 => Keys.F13,
            KeyCode.F14 => Keys.F14,
            KeyCode.F15 => Keys.F15,
            KeyCode.F16 => Keys.F16,
            KeyCode.F17 => Keys.F17,
            KeyCode.F18 => Keys.F18,
            KeyCode.F19 => Keys.F19,
            KeyCode.F20 => Keys.F20,
            KeyCode.F21 => Keys.F21,
            KeyCode.F22 => Keys.F22,
            KeyCode.F23 => Keys.F23,
            KeyCode.F24 => Keys.F24,

            // Lock keys
            KeyCode.CapsLock => Keys.CapsLock,
            KeyCode.Numlock => Keys.NumLock,        // Note: Unity uses "Numlock" (lowercase l)
            KeyCode.ScrollLock => Keys.ScrollLock,

            // System keys
            KeyCode.Print => Keys.PrintScreen,      // Unity uses "Print" not "PrintScreen"
            KeyCode.Pause => Keys.Pause,

            // Catch any unmapped keys
            _ => Keys.Unknown
        };
    }
}