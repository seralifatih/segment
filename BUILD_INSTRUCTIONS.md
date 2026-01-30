# Segment Application - Build & Installer Instructions

This guide explains how to create a production-ready installer for the Segment WPF application.

## 📋 Prerequisites

1. **Windows 10/11** with PowerShell or Command Prompt
2. **.NET 8 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
3. **Inno Setup 6.x** - [Download here](https://jrsoftware.org/isinfo.php)
   - During installation, make sure to install the Inno Setup Compiler

## 🔨 Step-by-Step Build Process

### Step 1: Build the Self-Contained Executable

1. Open **Command Prompt** or **PowerShell**
2. Navigate to the project root directory:
   ```cmd
   cd C:\Users\fatih\Desktop\Programming\Segment
   ```

3. Run the build script:
   ```cmd
   build_release.bat
   ```

4. **What this does:**
   - Cleans any previous builds
   - Publishes a self-contained, single-file executable for Windows x64
   - Outputs to the `Publish` folder
   - The resulting `Segment.exe` includes all dependencies (no .NET runtime needed on target machines)

5. **Expected output:**
   ```
   ✓ Publish\Segment.exe (~80-120 MB depending on dependencies)
   ✓ Any JSON configuration files (if present)
   ```

### Step 2: Create the Installer

1. **Open Inno Setup Compiler**
   - Start Menu → Inno Setup → Inno Setup Compiler

2. **Open the script:**
   - File → Open → Navigate to `C:\Users\fatih\Desktop\Programming\Segment\setup.iss`

3. **Compile the installer:**
   - Build → Compile (or press `Ctrl+F9`)
   - Alternatively: Right-click `setup.iss` in Windows Explorer → Compile

4. **Wait for compilation:**
   - The compiler will show progress in the console window
   - Takes 10-30 seconds depending on file size

5. **Output:**
   - The installer will be created in the same directory: `SegmentSetup_v1.0.exe`
   - Size: ~80-120 MB (contains the full application)

## 📦 Installer Features

The generated installer includes:

### ✅ Installation Options
- **Default location:** `C:\Program Files\Segment`
- **Requires admin privileges** (to install in Program Files)
- **64-bit only** (optimized for modern Windows)

### ✅ Optional Tasks
- **Desktop shortcut** (unchecked by default)
- **Start with Windows** (checked by default - runs in system tray)

### ✅ Smart Features
- **Auto-detection:** Checks if Segment is running before install/uninstall
- **Auto-close:** Offers to close the app if running
- **Registry entry:** Adds startup registry key if "Start with Windows" is selected
- **Clean uninstall:** Removes all files and registry entries

### ✅ Start Menu Integration
- Creates program group: `Start Menu → Segment`
- Includes uninstaller shortcut

## 🎯 Distribution

Once you have `SegmentSetup_v1.0.exe`, you can:

1. **Share directly** - Users just run the .exe (no dependencies needed)
2. **Upload to GitHub Releases** - Tag a release and attach the installer
3. **Host on your website** - Direct download link
4. **Sign the installer** (optional but recommended for professional deployment)

## 🔄 Updating the Version

To release a new version:

1. **Update `setup.iss`:**
   ```ini
   #define MyAppVersion "1.1"  ; Change version number
   ```

2. **Rebuild:**
   ```cmd
   build_release.bat
   ```

3. **Recompile installer** in Inno Setup

4. **Output:** `SegmentSetup_v1.1.exe`

## 🎨 Adding a Custom Icon (Optional)

Currently, the installer uses the default Windows icon. To add a custom icon:

1. **Create or obtain a .ico file** (16x16, 32x32, 48x48, 256x256 recommended)
   - You can use your procedural tray icon as a base
   - Use tools like IcoFX, GIMP, or online converters

2. **Save as:** `segment.ico` in the project root

3. **Edit `setup.iss`:**
   ```ini
   ; Uncomment this line:
   SetupIconFile=segment.ico
   ```

4. **Recompile** the installer

## 🐛 Troubleshooting

### Error: "dotnet: command not found"
- ✓ Install .NET 8 SDK from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)
- ✓ Restart your terminal after installation

### Error: "Source file not found" in Inno Setup
- ✓ Make sure you ran `build_release.bat` first
- ✓ Check that `Publish\Segment.exe` exists

### Installer won't compile
- ✓ Make sure Inno Setup 6.x is installed (not an older version)
- ✓ Check for syntax errors in `setup.iss` (the compiler will highlight them)

### Application won't start after installation
- ✓ Check Windows Event Viewer for crash logs
- ✓ Check `%AppData%\SegmentApp\crash_log.txt` for errors
- ✓ Make sure target machine is Windows 10 1607 or later

## 📝 Notes

- **Single File Deployment:** The entire application (including .NET runtime) is bundled into one executable
- **No Installation Required:** Users can also run `Publish\Segment.exe` directly without installing
- **Crash Logging:** The app logs crashes to `%AppData%\SegmentApp\crash_log.txt`
- **Self-Contained:** No .NET runtime needs to be installed on user machines

## 🔐 Code Signing (Recommended for Production)

For professional distribution, consider signing your installer:

1. Obtain a code signing certificate (from DigiCert, Sectigo, etc.)
2. Add to `setup.iss`:
   ```ini
   SignTool=signtool /f "path\to\certificate.pfx" /p "password" /tr http://timestamp.digicert.com /td sha256 /fd sha256 $f
   SignedUninstaller=yes
   ```

## 📞 Support

For issues or questions:
- Check crash logs: `%AppData%\SegmentApp\crash_log.txt`
- Review build output for errors
- Consult Inno Setup documentation: [jrsoftware.org/ishelp](https://jrsoftware.org/ishelp/)

---

**Last Updated:** January 2026  
**Version:** 1.0
