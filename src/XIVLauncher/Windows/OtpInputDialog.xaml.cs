using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Serilog;
using XIVLauncher.Common.Http;
using XIVLauncher.Common.Util;
using XIVLauncher.Windows.ViewModel;

using System.ComponentModel;
using MaterialDesignThemes.Wpf;
using XIVLauncher.Accounts;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for OtpInputDialog.xaml
    /// </summary>
    public partial class OtpInputDialog : Window
    {
        public event Action<string> OnResult;

        private readonly Brush _otpInputPromptDefaultBrush;

        private OtpInputDialogViewModel ViewModel => DataContext as OtpInputDialogViewModel;

        private OtpListener _otpListener;
        private bool _ignoreCurrentOtp;

        public OtpInputDialog()
        {
            InitializeComponent();

            _otpInputPromptDefaultBrush = OtpInputPrompt.Foreground;

            this.DataContext = new OtpInputDialogViewModel();

            MouseMove += OtpInputDialog_OnMouseMove;
            Activated += (_, _) => OtpTextBox.Focus();
            GotFocus += (_, _) => OtpTextBox.Focus();
            Topmost = App.Settings.OtpAlwaysOnTopEnabled;
        }

        public new bool? ShowDialog()
        {
            OtpTextBox.Focus();

            if (App.Settings.OtpServerEnabled)
            {
                _otpListener = new OtpListener("legacy-" + AppUtil.GetAssemblyVersion());
                _otpListener.OnOtpReceived += TryAcceptOtp;

                try
                {
                    // Start Listen
                    Task.Run(() => _otpListener.Start());
                    Log.Debug("OTP server started...");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not start OTP HTTP listener.");
                }
            }

            return base.ShowDialog();
        }

        public void Reset()
        {
            OtpInputPrompt.Text = ViewModel.OtpInputPromptLoc;
            OtpInputPrompt.Foreground = _otpInputPromptDefaultBrush;
            OtpTextBox.Text = "";
            OtpTextBox.Focus();
        }

        public void IgnoreCurrentResult(string reason)
        {
            OtpInputPrompt.Text = reason;
            OtpInputPrompt.Foreground = Brushes.Red;
            _ignoreCurrentOtp = true;
        }

        public void TryAcceptOtp(string otp)
        {
            if (!TryApplyTotp(otp, out otp))
                return;

            if (otp.Length != 6)
            {
                Log.Error("Malformed OTP: {Otp}", otp);

                Dispatcher.Invoke(() =>
                {
                    OtpInputPrompt.Text = ViewModel.OtpInputPromptBadLoc;
                    OtpInputPrompt.Foreground = Brushes.Red;
                    Storyboard myStoryboard = (Storyboard)OtpInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), OtpInputPrompt);
                    myStoryboard.Begin();
                    OtpTextBox.Focus();
                });

                return;
            }

            _ignoreCurrentOtp = false;
            OnResult?.Invoke(otp);

            Dispatcher.Invoke(() =>
            {
                if (_ignoreCurrentOtp)
                {
                    Storyboard myStoryboard = (Storyboard)OtpInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), OtpInputPrompt);
                    myStoryboard.Begin();
                    OtpTextBox.Focus();
                }
                else
                {
                    _otpListener?.Stop();
                    DialogResult = true;
                    Hide();
                }
            });
        }

        private void Cancel()
        {
            OnResult?.Invoke(null);
            _otpListener?.Stop();
            DialogResult = false;
            Hide();
        }

        private void OtpInputDialog_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void OtpTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void OtpTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void OtpTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cancel();
            }
            else if (e.Key == Key.Enter)
            {
                TryAcceptOtp(this.OtpTextBox.Text);
            }
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            TryAcceptOtp(this.OtpTextBox.Text);
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void PasteButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.OtpTextBox.Text = Clipboard.GetText();
            TryAcceptOtp(this.OtpTextBox.Text);
        }

        public void OpenShortcutInfo_MouseUp(object sender, RoutedEventArgs e)
        {
            PlatformHelpers.OpenBrowser($"https://goatcorp.github.io/faq/mobile_otp");
        }

        public static string AskForOtp(Action<OtpInputDialog, string> onOtpResult, Window parentWindow, string userName = null, bool doingAutoLogin = false)
        {
            if (Dispatcher.CurrentDispatcher != parentWindow.Dispatcher)
                return parentWindow.Dispatcher.Invoke(() => AskForOtp(onOtpResult, parentWindow, userName, doingAutoLogin));

            var dialog = new OtpInputDialog();
            if (parentWindow.IsVisible)
            {
                dialog.Owner = parentWindow;
                dialog.ShowInTaskbar = false;
            }

            string result = null;
            dialog.OnResult += otp => onOtpResult(dialog, result = otp);

            dialog.InitializeTotp(userName);
            if (doingAutoLogin && dialog.TrySilentAccept())
                return result;

            return dialog.ShowDialog() == true ? result : null;
        }

        #region Automatic TOTP

        private string _totpUserName;
        private string _savedTotpUri;
        private Totp _totp;
        private DispatcherTimer _totpHintTimer;

        private void InitializeTotp(string userName)
        {
            _totpUserName = userName;

            if (userName == null)
            {
                TotpUriPasswordBox.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                _savedTotpUri = XivAccount.GetTotpUri(userName);
            }
            catch (Win32Exception ex)
            {
                Log.Error(ex, "Could not read TOTP secret");
            }

            // Triggers TotpUriPasswordBox_OnPasswordChanged, which parses the URI
            TotpUriPasswordBox.Password = _savedTotpUri ?? string.Empty;

            _totpHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _totpHintTimer.Tick += (_, _) => UpdateTotpHint();

            // The dialog is hidden rather than closed when done, so tie the timer to visibility
            IsVisibleChanged += (_, _) =>
            {
                if (IsVisible)
                    _totpHintTimer.Start();
                else
                    _totpHintTimer.Stop();
            };
        }

        /// <summary>
        /// Like a saved password, submit the computed code without showing the dialog.
        /// A failed login disables auto-login, so the next attempt will show the dialog.
        /// </summary>
        /// <returns>False if no TOTP secret is available, or the code was rejected by the result handler.</returns>
        private bool TrySilentAccept()
        {
            if (_totp == null)
                return false;

            _ignoreCurrentOtp = false;
            OnResult?.Invoke(_totp.Compute(DateTimeOffset.UtcNow));

            if (_ignoreCurrentOtp)
                return false;

            Log.Information("Submitted TOTP automatically");
            Close();
            return true;
        }

        /// <summary>
        /// Validate and persist the entered TOTP URI, and fill in the computed code if none was entered.
        /// </summary>
        /// <returns>False if the entered URI is invalid.</returns>
        private bool TryApplyTotp(string otp, out string result)
        {
            result = otp;

            if (_totpUserName == null)
                return true;

            var resolved = otp;
            var valid = Dispatcher.Invoke(() =>
            {
                var totpUri = TotpUriPasswordBox.Password.Trim();

                if (totpUri.Length > 0 && _totp == null)
                {
                    OtpInputPrompt.Text = ViewModel.OtpInputPromptBadTotpUriLoc;
                    OtpInputPrompt.Foreground = Brushes.Red;
                    Storyboard myStoryboard = (Storyboard)OtpInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), OtpInputPrompt);
                    myStoryboard.Begin();
                    TotpUriPasswordBox.Focus();
                    return false;
                }

                if (totpUri != (_savedTotpUri ?? string.Empty))
                {
                    try
                    {
                        XivAccount.SetTotpUri(_totpUserName, totpUri);
                        _savedTotpUri = totpUri;
                    }
                    catch (Win32Exception ex)
                    {
                        Log.Error(ex, "Could not save TOTP secret");
                    }
                }

                if (string.IsNullOrEmpty(resolved) && _totp != null)
                    resolved = _totp.Compute(DateTimeOffset.UtcNow);

                return true;
            });

            result = resolved;
            return valid;
        }

        private void UpdateTotpHint()
        {
            HintAssist.SetHint(OtpTextBox, _totp?.Compute(DateTimeOffset.UtcNow) ?? string.Empty);
        }

        private void TotpUriPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            Totp.TryParse(TotpUriPasswordBox.Password, out _totp);
            UpdateTotpHint();
        }

        private void TotpUriPasswordBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            // Stop the window's GotFocus handler from moving focus back to OtpTextBox
            e.Handled = true;
        }

        private void TotpUriPasswordBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cancel();
            }
            else if (e.Key == Key.Enter)
            {
                TryAcceptOtp(this.OtpTextBox.Text);
            }
        }

        #endregion
    }
}
