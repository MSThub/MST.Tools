# MST.Tools

**MST.Tools** is a collection of useful console utilities for .NET CLI applications.

It provides features like themed output, colored text, structured menus, and input helpers.

---

## ✨ Features

* **Colorful output** with custom RGB themes
* **Menu helpers** for CLI apps
* **User input validation** (string, int, decimal)
* **Exit confirmation** prompts
* Works on **Windows**, **Linux**, and **macOS**
* Supports **.NET Standard 2.0** and above

---

## 📦 Installation

```bash
dotnet add package MST.Tools
```

---

## 🔧 Usage

You can import the namespace normally:

```csharp
using MST.Tools;

class Program
{
    static void Main()
    {
        ConsoleHelper.PrintSuccess("Operation completed successfully!");
        ConsoleHelper.PrintError("Something went wrong!");
    }
}
```

Or, if you don’t want to type `ConsoleHelper.` every time, you can use the static import:

```csharp
//use static import like this :
using static MST.Tools.ConsoleHelper;

class Program
{
    static void Main()
    {
        // and you can use methods without typing 'ConsoleHelper.' every time
        Ok("Data saved correctly.");
        Fail("Failed to connect to server.");
        Warn("Low disk space.");
        Info("Loading configuration...");
    }
}
```

---

## 🎨 Theming

You can fully customize colors with RGB values (`0-255`) or hex codes (`#RRGGBB`).

```csharp
using static MST.Tools.ConsoleHelper;

class Program
{
    static void Main()
    {
        //you can set theme colors in hex format or R,G,B format
        SetTheme("#2DC653", "#A0A0A0", "#FFD23F");
    }
}
```

Reset theme back to default:

```csharp
ResetTheme();
```

Set error color:

```csharp
SetErrorColor("255,0,0");
```

---

## 📋 UI Helpers

### Sections and Dividers

```csharp
Section("User Menu");
Divider();
```

### Menu Items

```csharp
MenuItem(1, "Start Application");
MenuItem(2, "Settings");
MenuItem(3, "Exit");
```

### Results and Items

```csharp
PrintResult("Username", "MST");
ShowItem(42, "Special Item");
```
### Customizing Outputs
you can override the default theme colors for UI elements on a per-call basis to draw attention to important information.

Here is a combined example showing how you can use these customizations together to build a richer UI:
```csharp
Section("System Diagnostics");
Info("Choose a system to check:");

// Highlight the "Network" option which requires attention
MenuItem(1, "CPU");
MenuItem(2, "Memory");
MenuItem(
    number: 3,
    text: "Network (High Latency Detected)",
    numberFg: "#FFB300", // Gold
    textFg: "255,215,0"  // Lighter Gold
);

// Show a result with custom colors to indicate a warning status
PrintResult(
    label: "Overall Status",
    result: "Action Required",
    labelFg: "#D3D3D3",   // Light Gray
    valueFg: "#FFA500"   // Orange
);

// List critical processes and highlight one that has stalled
Info("Running critical processes:");
ShowItem(2041, "system.core.service");
ShowItem(
    id: 3155,
    item: "data.backup.agent (Stalled)",
    idFg: "#FF6347",      // Tomato Red
    itemFg: "#FF8C00"     // Dark Orange
);
```

---

## ⌨️ Input Helpers

### Simple Input

```csharp
string name = GetInput("Enter your name:");
```

### Integer Input

```csharp
int age = GetIntInput("Enter your age:");
```

### Decimal Input

```csharp
decimal price = GetDecimalInput("Enter product price:");
```

### Customizing Input Prompts
By default, input prompts use the theme's Secondary color for the label (the prompt text) and the Primary color for the input symbol (➜). You can easily override these on a per-call basis using the labelFg and cursorFg parameters. This allows for creating visually distinct prompts for different questions without changing the global theme.

Here is a complete example demonstrating all parameters for GetIntInput:
```csharp
int specialNumber = GetIntInput(
    prompt: "Enter a special number between 40 and 50",
    min: 40,
    max: 50,
    labelFg: "#9370DB", // A custom purple color for the label
    cursorFg: "#FFD700"  // A custom gold color for the arrow
);

Ok($"Success! Your special number is: {specialNumber}");

// The same customization can be applied to GetInput and GetDecimalInput.
string reason = GetInput(
    prompt: "Why did you choose this number?",
    labelFg: "173, 216, 230" // Light Blue
);
```


### Confirm / Exit

```csharp
if (Confirm("Do you want to continue?"))
{
    Ok("Confirmed!");
}

if (ExitOption())
{
    Fail("Exiting program...");
}
```

---

## ✅ Example Output

```text
✔ Operation completed successfully!
✖ Something went wrong!
➜ Loading configuration...
```

---

## ⚙️ Advanced Configuration

If you're running the application in an environment that doesn't support Unicode symbols (like older Windows `cmd.exe`), you can force an ASCII-only symbol set.

```csharp
// Force ASCII symbols like "[OK]" and "[X]" instead of "✔" and "✖"
SetSymbolSet(SymbolSet.Ascii);

Ok("This will now be prefixed with [OK].");
```

---

## 📝 License

MIT License © 2025 MST.Tools
