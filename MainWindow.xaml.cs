using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AutoClicker
{
    public partial class MainWindow : Window
    {
        const int StartHotkeyId = 1;
        const int StopHotkeyId = 2;
        const double MinRate = 0.01;

        readonly Clicker clicker = new Clicker();
        readonly DispatcherTimer countTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

        IntPtr hwnd;
        Hotkey startKey = new Hotkey(ModifierKeys.None, Key.F6);
        Hotkey stopKey = new Hotkey(ModifierKeys.None, Key.F7);
        ToggleButton capturingChip;
        string hotkeyError;
        Drawing.Point? fixedPoint;
        bool picking;
        bool ready;
        double rate = 10;
        bool rateValid = true;

        public MainWindow()
        {
            InitializeComponent();
            countTimer.Tick += (s, e) => CountText.Text = clicker.ClickCount.ToString("N0");
            PreviewKeyDown += OnPreviewKeyDown;
            ready = true;
            ValidateRate();
            UpdateRunUi();
        }

        bool IsPerMinute => PerMinuteRadio.IsChecked == true;
        double MaxRate => IsPerMinute ? 60000 : 1000;

        // ---- Window chrome ----

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);
            ApplyWindowEffects();
            RegisterHotkeys();
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        void ApplyWindowEffects()
        {
            // Dark frame, rounded corners and a subtle border on Windows 11; ignored on older builds.
            int dark = 1, round = 2, border = 0x002E2926;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
            DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));
        }

        void PinButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = PinButton.IsChecked == true;
            PinButton.Content = Topmost ? "" : "";
            PinButton.Foreground = (Brush)FindResource(Topmost ? "Accent" : "Muted");
        }

        void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            clicker.Stop();
            UnregisterHotkeys();
            base.OnClosed(e);
        }

        // ---- Start / stop ----

        void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (clicker.IsRunning) StopClicking(); else StartClicking();
        }

        void StartClicking()
        {
            if (clicker.IsRunning || picking || !rateValid) return;
            if (FixedRadio.IsChecked == true && fixedPoint == null) return;

            var button = RightRadio.IsChecked == true ? Forms.MouseButtons.Right
                : MiddleRadio.IsChecked == true ? Forms.MouseButtons.Middle
                : Forms.MouseButtons.Left;
            Drawing.Point? target = FixedRadio.IsChecked == true ? fixedPoint : null;

            clicker.Start(ClickInterval(), button, DoubleRadio.IsChecked == true, target);
            CountText.Text = "0";
            countTimer.Start();
            UpdateRunUi();
        }

        void StopClicking()
        {
            if (!clicker.IsRunning) return;
            clicker.Stop();
            countTimer.Stop();
            CountText.Text = clicker.ClickCount.ToString("N0");
            UpdateRunUi();
        }

        void UpdateRunUi()
        {
            bool running = clicker.IsRunning;

            RunButton.Background = (Brush)FindResource(running ? "Danger" : "Accent");
            RunButton.Foreground = (Brush)FindResource(running ? "OnDanger" : "OnAccent");
            RunButton.IsEnabled = running || rateValid;
            RunGlyph.Text = running ? "" : "";
            RunLabel.Text = running ? "Stop clicking" : "Start clicking";
            RunKey.Text = (running ? stopKey : startKey).ToString();

            StatusText.Text = running ? "Clicking" : "Ready";
            StatusText.Foreground = (Brush)FindResource(running ? "Accent" : "Muted");
            StatusDot.Fill = (Brush)FindResource(running ? "Accent" : "Muted");
            if (running)
            {
                StatusDot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(650))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                });
            }
            else
            {
                StatusDot.BeginAnimation(OpacityProperty, null);
            }

            SettingsPanel.IsEnabled = !running;
            SettingsPanel.Opacity = running ? 0.45 : 1;
        }

        // ---- Speed ----

        TimeSpan ClickInterval() => TimeSpan.FromSeconds((IsPerMinute ? 60.0 : 1.0) / rate);

        static bool TryParseRate(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        static string FormatRate(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

        void ValidateRate()
        {
            if (!ready) return;
            rateValid = TryParseRate(RateBox.Text, out double value) && value >= MinRate && value <= MaxRate;
            if (rateValid) rate = value;

            RateBox.Tag = rateValid ? null : "invalid";
            if (rateValid)
            {
                double ms = ClickInterval().TotalMilliseconds;
                IntervalText.Text = "One click every " + (ms >= 1000 ? $"{ms / 1000:0.##} s" : $"{ms:0.##} ms");
                IntervalText.Foreground = (Brush)FindResource("Muted");
            }
            else
            {
                IntervalText.Text = $"Enter a number from {FormatRate(MinRate)} to {MaxRate:N0}.";
                IntervalText.Foreground = (Brush)FindResource("Danger");
            }
            UpdateRunUi();
        }

        void SetRate(double value)
        {
            value = Math.Round(Math.Max(MinRate, Math.Min(MaxRate, value)), 2);
            RateBox.Text = FormatRate(value);
            RateBox.CaretIndex = RateBox.Text.Length;
        }

        void StepRate(int direction)
        {
            double current = rateValid ? rate : 1;
            double step = direction > 0 ? (current >= 1 ? 1 : 0.1) : (current > 1 ? 1 : 0.1);
            SetRate(current + direction * step);
        }

        void RateBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ValidateRate();

        void RateBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != '.' && c != ',') e.Handled = true;
            }
        }

        void RateBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            StepRate(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }

        void RateBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (rateValid) RateBox.Text = FormatRate(rate);
        }

        void RateDown_Click(object sender, RoutedEventArgs e) => StepRate(-1);

        void RateUp_Click(object sender, RoutedEventArgs e) => StepRate(1);

        void Unit_Checked(object sender, RoutedEventArgs e)
        {
            if (!ready) return;
            // Keep the same real speed when switching units: 10/sec becomes 600/min and back.
            if (rateValid) SetRate(IsPerMinute ? rate * 60 : rate / 60);
            ValidateRate();
        }

        // ---- Target ----

        void Target_Checked(object sender, RoutedEventArgs e)
        {
            if (!ready) return;
            if (FixedRadio.IsChecked == true && fixedPoint == null)
            {
                PickPoint();
                if (fixedPoint == null)
                {
                    FollowRadio.IsChecked = true;
                    return;
                }
            }
            UpdateTargetUi();
        }

        void PickButton_Click(object sender, RoutedEventArgs e) => PickPoint();

        void PickPoint()
        {
            if (clicker.IsRunning || picking) return;
            picking = true;
            Hide();
            try
            {
                using (var overlay = new PointPickerOverlay())
                {
                    if (overlay.ShowDialog() == Forms.DialogResult.OK) fixedPoint = overlay.PickedPoint;
                }
            }
            finally
            {
                picking = false;
                Show();
                Activate();
            }
            UpdateTargetUi();
        }

        void UpdateTargetUi()
        {
            bool isFixed = FixedRadio.IsChecked == true;
            PickButton.Visibility = isFixed ? Visibility.Visible : Visibility.Collapsed;
            TargetText.Text = isFixed && fixedPoint is Drawing.Point p
                ? $"X {p.X}  ·  Y {p.Y}"
                : "Clicks wherever your cursor is.";
            TargetText.FontFamily = isFixed ? (FontFamily)FindResource("MonoFont") : FontFamily;
            TargetText.Foreground = (Brush)FindResource(isFixed ? "Text" : "Muted");
        }

        // ---- Hotkeys ----

        void RegisterHotkeys()
        {
            UnregisterHotkeys();
            hotkeyError = null;

            // While a chip is listening for a new key, leave the old ones free so the press reaches this window.
            if (capturingChip == null && hwnd != IntPtr.Zero)
            {
                var taken = new System.Collections.Generic.List<string>();
                if (!NativeRegister(StartHotkeyId, startKey)) taken.Add(startKey.ToString());
                if (!stopKey.Equals(startKey) && !NativeRegister(StopHotkeyId, stopKey)) taken.Add(stopKey.ToString());
                if (taken.Count > 0)
                {
                    hotkeyError = string.Join(" and ", taken) + (taken.Count > 1 ? " are" : " is") +
                        " already in use by another program (or another copy of Auto Clicker). Pick a different key.";
                }
            }

            HotkeyError.Text = hotkeyError ?? "";
            HotkeyError.Visibility = hotkeyError == null ? Visibility.Collapsed : Visibility.Visible;
        }

        bool NativeRegister(int id, Hotkey key) =>
            Native.RegisterHotKey(hwnd, id, key.NativeModifiers | Native.MOD_NOREPEAT, key.VirtualKey);

        void UnregisterHotkeys()
        {
            if (hwnd == IntPtr.Zero) return;
            Native.UnregisterHotKey(hwnd, StartHotkeyId);
            Native.UnregisterHotKey(hwnd, StopHotkeyId);
        }

        IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY)
            {
                handled = true;
                int id = wParam.ToInt32();
                if (id == StartHotkeyId && startKey.Equals(stopKey))
                {
                    if (clicker.IsRunning) StopClicking(); else StartClicking();
                }
                else if (id == StartHotkeyId)
                {
                    StartClicking();
                }
                else if (id == StopHotkeyId)
                {
                    StopClicking();
                }
            }
            return IntPtr.Zero;
        }

        void KeyChip_Checked(object sender, RoutedEventArgs e)
        {
            var chip = (ToggleButton)sender;
            var other = chip == StartKeyChip ? StopKeyChip : StartKeyChip;
            if (other.IsChecked == true) other.IsChecked = false;

            capturingChip = chip;
            chip.Content = "Press a key…";
            HotkeyHint.Text = "Press the new key (Ctrl, Alt and Shift combos work). Esc cancels.";
            HotkeyHint.Foreground = (Brush)FindResource("Muted");
            RegisterHotkeys();
        }

        void KeyChip_Unchecked(object sender, RoutedEventArgs e)
        {
            if (capturingChip != sender) return;
            EndCapture();
        }

        void EndCapture()
        {
            var chip = capturingChip;
            capturingChip = null;
            if (chip != null && chip.IsChecked == true) chip.IsChecked = false;

            StartKeyChip.Content = startKey.ToString();
            StopKeyChip.Content = stopKey.ToString();
            HotkeyHint.Text = "Click a key to change it. Use the same key for both to toggle.";
            HotkeyHint.Foreground = (Brush)FindResource("Muted");
            RegisterHotkeys();
            UpdateRunUi();
        }

        void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (capturingChip == null) return;
            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                EndCapture();
                return;
            }
            if (Hotkey.IsModifierKey(key) || key == Key.ImeProcessed || key == Key.DeadCharProcessed) return;

            var hotkey = new Hotkey(Keyboard.Modifiers, key);
            if (hotkey.Modifiers == ModifierKeys.None && hotkey.IsTypingKey)
            {
                HotkeyHint.Text = $"{hotkey} alone would block typing everywhere. Add Ctrl, Alt or Shift, or use an F-key.";
                HotkeyHint.Foreground = (Brush)FindResource("Danger");
                return;
            }

            if (capturingChip == StartKeyChip) startKey = hotkey; else stopKey = hotkey;
            EndCapture();
        }
    }
}
