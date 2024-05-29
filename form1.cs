using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace NezulaMacro
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;

        private Thread? clickThread;
        private bool isClicking = false;
        private float clickInterval = 100.0f;

        private Guna2TextBox? cpsTextBox;
        private Guna2CheckBox? toggleCheckBox;
        private Guna2Button? startButton;
        private Guna2Button? stopButton;
        private Guna2TextBox? keybindTextBox;

        private Keys toggleKey = Keys.None;
        private bool shiftPressed = false;

        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            this.Load += Form1_Load;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
        }

        private void InitializeCustomComponents()
        {
            this.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.ClientSize = new System.Drawing.Size(660, 260);
            this.Text = "AutoClicker";

            Guna2Panel leftPanel = new Guna2Panel
            {
                BorderColor = System.Drawing.Color.Gray,
                BorderThickness = 1,
                FillColor = System.Drawing.Color.FromArgb(50, 50, 50),
                Size = new System.Drawing.Size(300, 200),
                Location = new System.Drawing.Point(20, 20)
            };
            this.Controls.Add(leftPanel);

            toggleCheckBox = new Guna2CheckBox
            {
                Text = "Toggle",
                Location = new System.Drawing.Point(10, 10),
                ForeColor = System.Drawing.Color.White,
                CheckedState = { BorderColor = System.Drawing.Color.FromArgb(94, 148, 255) }
            };
            leftPanel.Controls.Add(toggleCheckBox);

            startButton = new Guna2Button
            {
                Text = "Start",
                Location = new System.Drawing.Point(10, 150),
                Size = new System.Drawing.Size(100, 30)
            };
            startButton.Click += (sender, e) => StartClicking();
            leftPanel.Controls.Add(startButton);

            stopButton = new Guna2Button
            {
                Text = "Stop",
                Location = new System.Drawing.Point(120, 150),
                Size = new System.Drawing.Size(100, 30)
            };
            stopButton.Click += (sender, e) => StopClicking();
            leftPanel.Controls.Add(stopButton);

            cpsTextBox = new Guna2TextBox
            {
                PlaceholderText = "Clicks Per Second",
                Location = new System.Drawing.Point(10, 50),
                Size = new System.Drawing.Size(100, 25),
                ForeColor = System.Drawing.Color.White,
                FillColor = System.Drawing.Color.FromArgb(50, 50, 50)
            };
            leftPanel.Controls.Add(cpsTextBox);

            keybindTextBox = new Guna2TextBox
            {
                PlaceholderText = "Keybind",
                Location = new System.Drawing.Point(10, 90),
                Size = new System.Drawing.Size(100, 25),
                ForeColor = System.Drawing.Color.White,
                FillColor = System.Drawing.Color.FromArgb(50, 50, 50),
                Text = toggleKey.ToString()
            };
            keybindTextBox.KeyDown += KeybindTextBox_KeyDown;
            leftPanel.Controls.Add(keybindTextBox);
        }

        private void KeybindTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            toggleKey = e.KeyCode;
            shiftPressed = e.Shift;
            keybindTextBox!.Text = (shiftPressed ? "Shift + " : "") + toggleKey.ToString();
            e.SuppressKeyPress = true;
        }

        private void StartClicking()
        {
            if (float.TryParse(cpsTextBox!.Text, out float cps) && cps > 0)
            {
                clickInterval = 1000.0f / cps;
            }

            if (!isClicking)
            {
                isClicking = true;
                clickThread = new Thread(ClickLoop);
                clickThread.IsBackground = true;
                clickThread.Start();
            }
        }

        private void StopClicking()
        {
            isClicking = false;
            clickThread?.Join();
        }

        private void ClickLoop()
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            while (isClicking)
            {
                stopwatch.Restart();
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                stopwatch.Stop();

                var elapsed = stopwatch.ElapsedMilliseconds;
                var sleepTime = (int)(clickInterval - elapsed);
                if (sleepTime > 0)
                {
                    Thread.Sleep(sleepTime);
                }
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule?.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    if (key == Keys.ShiftKey)
                    {
                        shiftPressed = true;
                    }
                    else if (key == toggleKey && shiftPressed == ModifierKeys.HasFlag(Keys.Shift))
                    {
                        if (isClicking)
                        {
                            StopClicking();
                        }
                        else
                        {
                            StartClicking();
                        }
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP && key == Keys.ShiftKey)
                {
                    shiftPressed = false;
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
            }
            base.OnFormClosing(e);
        }
    }
}
