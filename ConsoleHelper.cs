using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

#nullable enable
namespace MST.Tools;

public static class ConsoleHelper
{
    static ConsoleHelper()
    {
        try
        {
            Initialize();
        }
        catch (Exception ex)
        {
#if DEBUG
            Console.Error.WriteLine($"[ConsoleHelper] Initialization failed: {ex.Message}");
#endif
            // Continue without throwing to allow usage in restricted console environments.
        }
    }

    // ===== Public Theme API =====

    /// <summary>Immutable RGB color (0..255).</summary>
    public readonly struct Rgb
    {
        public int R { get; }
        public int G { get; }
        public int B { get; }

        public Rgb(int r, int g, int b)
        {
            if (!InByte(r) || !InByte(g) || !InByte(b))
                throw new ArgumentOutOfRangeException(nameof(r), "RGB components must be between 0 and 255.");
            R = r;
            G = g;
            B = b;
        }

        public override string ToString() => $"{R},{G},{B}";
        private static bool InByte(int v) => (uint)v <= 255;
    }

    /// <summary>Immutable theme container.</summary>
    public readonly struct Theme
    {
        public Rgb Primary { get; }
        public Rgb Secondary { get; }
        public Rgb Accent { get; }

        public Theme(Rgb primary, Rgb secondary, Rgb accent)
        {
            Primary = primary;
            Secondary = secondary;
            Accent = accent;
        }

        public Theme With(Rgb? primary = null, Rgb? secondary = null, Rgb? accent = null) =>
            new Theme(primary ?? Primary, secondary ?? Secondary, accent ?? Accent);
    }

    // Defaults
    public static readonly Theme DefaultTheme = new Theme(
        primary: new Rgb(2, 219, 111),
        secondary: new Rgb(180, 180, 180),
        accent: new Rgb(255, 208, 71)
    );

    public static readonly Rgb DefaultErrorColor = new Rgb(219, 0, 0);

    private static readonly object _sync = new();

    private static Theme _currentTheme = DefaultTheme;

    /// <summary>Current theme used for printing.</summary>
    public static Theme CurrentTheme
    {
        get
        {
            lock (_sync) return _currentTheme;
        }
        private set
        {
            lock (_sync) _currentTheme = value;
        }
    }

    private static Rgb _errorColor = DefaultErrorColor;

    /// <summary>Current error color for error messages.</summary>
    public static Rgb ErrorColor
    {
        get
        {
            lock (_sync) return _errorColor;
        }
        private set
        {
            lock (_sync) _errorColor = value;
        }
    }

    /// <summary>Reset theme to default.</summary>
    public static void ResetTheme() => CurrentTheme = DefaultTheme;

    /// <summary>Reset error color to default.</summary>
    public static void ResetErrorColor() => ErrorColor = DefaultErrorColor;

    // ===== Symbols & Fallback =====

    public enum SymbolSet
    {
        Auto,
        Unicode,
        Ascii
    }

    private static volatile SymbolSet _symbolSet = SymbolSet.Auto;

    private static class Sym
    {
        public static string Check => _symbolSet == SymbolSet.Ascii ? "[OK]" : "✔";
        public static string Cross => _symbolSet == SymbolSet.Ascii ? "[X]" : "✖";
        public static string Arrow => _symbolSet == SymbolSet.Ascii ? "->" : "➜";

        public static string Line => _symbolSet == SymbolSet.Ascii
            ? "------------------------------"
            : "────────────────────────────────";
    }

    /// <summary>Override automatic symbol detection.</summary>
    public static void SetSymbolSet(SymbolSet mode)
    {
        lock (_sync) _symbolSet = mode;
    }

    private static void AutoDetectSymbols()
    {
        if (_symbolSet != SymbolSet.Auto) return;

        bool isWindows =
#if NET6_0_OR_GREATER
            OperatingSystem.IsWindows();
#else
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        bool isWindowsTerminal = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION"));
        lock (_sync)
        {
            if (_symbolSet == SymbolSet.Auto)
                _symbolSet = (isWindows && !isWindowsTerminal) ? SymbolSet.Ascii : SymbolSet.Unicode;
        }
    }

    // ===== ANSI Pieces =====
    private const string Reset = "\u001b[0m";
    private static string Fg(Rgb c) => $"\u001b[38;2;{c.R};{c.G};{c.B}m";
    private static string Bg(Rgb c) => $"\u001b[48;2;{c.R};{c.G};{c.B}m";
    private const string BoldOn = "\u001b[1m";

    // ===== Initialization for Windows ANSI =====

    private static volatile bool _ansiReady;

    /// <summary>Initializes console encodings and ANSI support. Idempotent and thread-safe.</summary>
    public static void Initialize()
    {
        if (_ansiReady) return;
        lock (_sync)
        {
            if (_ansiReady) return;
            try
            {
                Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

#if NET6_0_OR_GREATER
                if (OperatingSystem.IsWindows())
#else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
                {
                    EnableWindowsAnsi();
                }

                AutoDetectSymbols();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine($"[ConsoleHelper] Init failed: {ex.Message}");
#endif
                // Continue without throwing (best-effort for limited environments).
            }

            _ansiReady = true;
        }
    }

    // ===== Color Parsing =====

    /// <summary>Parses "#RRGGBB", "RRGGBB", "#RGB", "RGB", or "r,g,b".</summary>
    public static bool TryParseColor(string? input, out Rgb color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();

        // "r,g,b"
        if (input.IndexOf(',') >= 0)
        {
            var parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

            if (parts.Length == 3 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) &&
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) &&
                InByte(r) && InByte(g) && InByte(b))
            {
                color = new Rgb(r, g, b);
                return true;
            }

            return false;
        }

        // Hex forms
        var hex = input.StartsWith("#", StringComparison.Ordinal) ? input.Substring(1) : input;

        if (hex.Length == 3) // #RGB
        {
            int r, g, b;
            if (TryHex(hex[0], out r) && TryHex(hex[1], out g) && TryHex(hex[2], out b))
            {
                color = new Rgb(r * 17, g * 17, b * 17);
                return true;
            }

            return false;
        }
        else if (hex.Length == 6) // #RRGGBB
        {
            byte r, g, b;
            if (byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
                byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
                byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
            {
                color = new Rgb(r, g, b);
                return true;
            }
        }

        return false;

        static bool InByte(int v) => v >= 0 && v <= 255;

        static bool TryHex(char c, out int val)
        {
            val = 0;
            if (c is >= '0' and <= '9')
            {
                val = c - '0';
                return true;
            }

            c = char.ToLowerInvariant(c);
            if (c is >= 'a' and <= 'f')
            {
                val = 10 + (c - 'a');
                return true;
            }

            return false;
        }
    }

    private static Rgb? ParseOpt(string? color) => TryParseColor(color, out var c) ? c : null;

    private static Rgb Resolve(string? colorOrNull, Rgb fallback) =>
        TryParseColor(colorOrNull, out var c) ? c : fallback;

    private static string rgb(Rgb c) => c.ToString();

    // ===== Theme Setters =====

    /// <summary>Set theme colors; any null keeps the previous value. Throws if invalid color strings are passed.</summary>
    public static void SetTheme(string? primary = null, string? secondary = null, string? accent = null)
    {
        var current = CurrentTheme;
        var p = primary is null
            ? current.Primary
            : (TryParseColor(primary, out var cp) ? cp : throw new ArgumentException("Invalid primary color."));
        var s = secondary is null
            ? current.Secondary
            : (TryParseColor(secondary, out var cs) ? cs : throw new ArgumentException("Invalid secondary color."));
        var a = accent is null
            ? current.Accent
            : (TryParseColor(accent, out var ca) ? ca : throw new ArgumentException("Invalid accent color."));
        CurrentTheme = new Theme(p, s, a);
    }

    /// <summary>Set error color. Throws if invalid.</summary>
    public static void SetErrorColor(string color)
    {
        if (!TryParseColor(color, out var e)) throw new ArgumentException("Invalid error color format.");
        ErrorColor = e;
    }

    // ===== High-level Printing API =====

    /// <summary>Writes a message with optional colors and bold, without newline.</summary>
    public static void Write(string message, string? fg = null, string? bg = null, bool bold = false)
    {
        WriteInternal(message, ParseOpt(fg), ParseOpt(bg), bold, newline: false);
    }

    /// <summary>Writes a message with optional colors and bold, with newline.</summary>
    public static void WriteLine(string message, string? fg = null, string? bg = null, bool bold = false)
    {
        WriteInternal(message, ParseOpt(fg), ParseOpt(bg), bold, newline: true);
    }

    /// <summary>Writes a success message (defaults to Primary color).</summary>
    public static void PrintSuccess(string message, string? fg = null, string? bg = null, bool? bold = null)
    {
        var theme = CurrentTheme;
        var useFg = Resolve(fg, theme.Primary);
        var useBg = ParseOpt(bg);
        var useBold = bold ?? true;
        WriteLine($"{Sym.Check} {message}", rgb(useFg), useBg?.ToString(), useBold);
    }

    /// <summary>Writes an info message (defaults to Secondary color).</summary>
    public static void PrintInfo(string message, string? fg = null, string? bg = null, bool? bold = null)
    {
        var theme = CurrentTheme;
        var useFg = Resolve(fg, theme.Secondary);
        var useBg = ParseOpt(bg);
        var useBold = bold ?? false;
        WriteLine(message, rgb(useFg), useBg?.ToString(), useBold);
    }

    /// <summary>Writes a warning message (defaults to Accent color).</summary>
    public static void PrintWarning(string message, string? fg = null, string? bg = null, bool? bold = null)
    {
        var theme = CurrentTheme;
        var useFg = Resolve(fg, theme.Accent);
        var useBg = ParseOpt(bg);
        var useBold = bold ?? true;
        WriteLine(message, rgb(useFg), useBg?.ToString(), useBold);
    }

    /// <summary>Writes an error message (defaults to Error color).</summary>
    public static void PrintError(string message, string? fg = null, string? bg = null, bool? bold = null)
    {
        var err = ErrorColor;
        var useFg = Resolve(fg, err);
        var useBg = ParseOpt(bg);
        var useBold = bold ?? true;
        WriteLine($"{Sym.Cross} {message}", rgb(useFg), useBg?.ToString(), useBold);
    }

    // Sugar aliases
    public static void Ok(string msg, string? fg = null, string? bg = null, bool? bold = null) =>
        PrintSuccess(msg, fg, bg, bold);

    public static void Info(string msg, string? fg = null, string? bg = null, bool? bold = null) =>
        PrintInfo(msg, fg, bg, bold);

    public static void Warn(string msg, string? fg = null, string? bg = null, bool? bold = null) =>
        PrintWarning(msg, fg, bg, bold);

    public static void Fail(string msg, string? fg = null, string? bg = null, bool? bold = null) =>
        PrintError(msg, fg, bg, bold);

    // ===== UI/UX Helpers =====

    /// <summary>Section header with accent/bold title and a divider line on both sides.</summary>
    public static void Section(string title, string? lineFg = null, string? titleFg = null, bool? bold = null)
    {
        var theme = CurrentTheme;
        var lineColor = Resolve(lineFg, theme.Accent);
        var titleColor = Resolve(titleFg, theme.Accent);
        var useBold = bold ?? true;

        Console.Write($"{Fg(lineColor)}{Sym.Line} {Reset}");
        if (useBold) Console.Write(BoldOn);
        Console.Write($"{Fg(titleColor)}{title}{Reset}");
        Console.WriteLine($"{Fg(lineColor)} {Sym.Line}{Reset}");
    }

    /// <summary>Prints a divider line (accent color by default).</summary>
    public static void Divider(string? fg = null)
    {
        var theme = CurrentTheme;
        var lineColor = Resolve(fg, theme.Accent);
        Console.WriteLine($"{Fg(lineColor)}{Sym.Line}{Reset}");
    }

    /// <summary>Menu item where number is primary color and text is secondary.</summary>
    public static void MenuItem(string number, string text, int padWidth = 0, string? numberFg = null,
        string? textFg = null)
    {
        if (padWidth > 0) number = number.PadLeft(padWidth);
        var theme = CurrentTheme;
        var nf = Resolve(numberFg, theme.Primary);
        var tf = Resolve(textFg, theme.Secondary);
        Console.WriteLine($"{Fg(nf)}+[{number}]{Reset} {Fg(tf)}{text}{Reset}");
    }

    public static void MenuItem(int number, string text, int padWidth = 0, string? numberFg = null,
        string? textFg = null) =>
        MenuItem(number.ToString(CultureInfo.InvariantCulture), text, padWidth, numberFg, textFg);

    /// <summary>Label (secondary) then arrow/value (primary), bolded value.</summary>
    public static void PrintResult(string label, string result, string? labelFg = null, string? valueFg = null)
    {
        var theme = CurrentTheme;
        var lf = Resolve(labelFg, theme.Secondary);
        var vf = Resolve(valueFg, theme.Primary);
        Console.WriteLine(
            $"{Fg(vf)}+{Reset} " +
            $"{Fg(lf)}{label}{Reset} " +
            $"{Fg(vf)}--> {Reset}" +
            $"{BoldOn}{Fg(vf)}{result}{Reset}"
        );
    }

    /// <summary>Shows an item id (primary) and text (secondary).</summary>
    public static void ShowItem(int id, string item, string? idFg = null, string? itemFg = null)
    {
        var theme = CurrentTheme;
        var idc = Resolve(idFg, theme.Primary);
        var itc = Resolve(itemFg, theme.Secondary);
        Console.WriteLine(
            $"{Fg(idc)}+{Reset} " +
            $"{BoldOn}{Fg(idc)}{id}{Reset} " +
            $"{Fg(idc)}--> {Reset}" +
            $"{Fg(itc)}{item}{Reset}"
        );
    }

    /// <summary>Waits for any key with a secondary-colored prompt.</summary>
    public static void WaitForUser(string? fg = null)
    {
        var theme = CurrentTheme;
        var useFg = Resolve(fg, theme.Secondary);
        Write("Press any key to continue...", rgb(useFg));
        Console.ReadKey(intercept: true);
        Console.WriteLine();
    }

    /// <summary>Reads a non-empty input; typing 'Exit()' and confirming throws OperationCanceledException.</summary>
    public static string GetInput(string prompt, string? labelFg = null, string? cursorFg = null)
    {
        while (true)
        {
            var theme = CurrentTheme;
            var lf = Resolve(labelFg, theme.Secondary);
            var cf = Resolve(cursorFg, theme.Primary);

            Console.Write($"{Fg(lf)}{prompt} {Reset}");
            Console.Write($"{BoldOn}{Fg(cf)}{Sym.Arrow} {Reset}");

            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                PrintError("Invalid input. Please enter a non-empty input.");
                continue;
            }

            if (input.Equals("Exit()", StringComparison.OrdinalIgnoreCase))
            {
                if (Confirm("Are you sure you want to cancel? (yes/no)"))
                    throw new OperationCanceledException("User cancelled via Exit().");
                else
                    continue;
            }

            return input!;
        }
    }

    /// <summary>Reads an integer with optional min/max constraints.</summary>
    public static int GetIntInput(string prompt, int? min = null, int? max = null, string? labelFg = null,
        string? cursorFg = null)
    {
        while (true)
        {
            var input = GetInput(prompt, labelFg, cursorFg);
            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ||
                int.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, out n))
            {
                if ((!min.HasValue || n >= min.Value) && (!max.HasValue || n <= max.Value))
                    return n;
            }

            PrintError("Invalid number.");
            if (min.HasValue || max.HasValue)
                PrintInfo(
                    $"Allowed range: {min?.ToString(CultureInfo.InvariantCulture) ?? "-∞"} to {max?.ToString(CultureInfo.InvariantCulture) ?? "∞"}");
        }
    }

    /// <summary>Reads a decimal number using Invariant or Current culture.</summary>
    public static decimal GetDecimalInput(string prompt, string? labelFg = null, string? cursorFg = null)
    {
        while (true)
        {
            var input = GetInput(prompt, labelFg, cursorFg);

            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ||
                decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out result))
                return result;

            PrintError("Invalid input! Please enter a valid decimal number.");
        }
    }

    /// <summary>Asks a yes/no question and returns true for yes.</summary>
    public static bool Confirm(string question = "Are you sure you want to exit? (yes/no)", string? questionFg = null)
    {
        while (true)
        {
            var theme = CurrentTheme;
            var qf = Resolve(questionFg, theme.Accent);
            Console.WriteLine($"{BoldOn}{Fg(qf)}{question}{Reset}");
            var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
            switch (confirmation)
            {
                case "y":
                case "yes":
                    Ok("Confirmed.");
                    return true;
                case "n":
                case "no":
                    Info("Cancelled.");
                    return false;
                default:
                    PrintError("Enter a valid confirmation (yes/no)");
                    break;
            }
        }
    }

    /// <summary>
    /// Displays an exit confirmation prompt; returns true if user confirmed exit.
    /// </summary>
    public static bool ExitOption(string question = "Are you sure you want to exit? (yes/no)",
        string? questionFg = null, bool showFarewell = true)
    {
        while (true)
        {
            var theme = CurrentTheme;
            var qf = Resolve(questionFg, theme.Accent);
            Console.WriteLine($"{BoldOn}{Fg(qf)}{question}{Reset}");
            var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
            switch (confirmation)
            {
                case "y":
                case "yes":
                    if (showFarewell) Ok("Goodbye!");
                    return true;

                case "n":
                case "no":
                    Info("Exit cancelled.");
                    return false;

                default:
                    PrintError("Enter a valid confirmation (yes/no)");
                    break;
            }
        }
    }

    // ===== Internals =====

    private static void WriteInternal(string message, Rgb? fg, Rgb? bg, bool bold, bool newline)
    {
        if (bold) Console.Write(BoldOn);
        if (fg is { } f) Console.Write(Fg(f));
        if (bg is { } b) Console.Write(Bg(b));

        if (newline) Console.WriteLine(message);
        else Console.Write(message);

        Console.Write(Reset); // always reset to avoid style leakage
    }

    // Windows ANSI enabling
    private static void EnableWindowsAnsi()
    {
        const int STD_OUTPUT_HANDLE = -11;
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle == IntPtr.Zero) return;
        if (!GetConsoleMode(handle, out var mode)) return;

        mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        _ = SetConsoleMode(handle, mode);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}