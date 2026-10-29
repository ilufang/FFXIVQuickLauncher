using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class OtpInputDialogViewModel
    {
        public OtpInputDialogViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            OtpInputPromptLoc = Loc.Localize("OtpInputPrompt", "Please enter your OTP key.");
            CancelWithShortcutLoc = Loc.Localize("CancelWithShortcut", "_Cancel");
            OkLoc = Loc.Localize("OK", "OK");
            OtpOneClickHintLoc = Loc.Localize("OtpOneClickHint", "Or use the app!\r\nClick here to learn more!");
            OtpInputPromptBadLoc = Loc.Localize("OtpInputPromptBad", "Enter a valid OTP key.\nIt is 6 digits long.");
            PasteButtonLoc = Loc.Localize("PasteButton", "Click here to paste from the clipboard.");
            OtpTotpUriHintLoc = Loc.Localize("OtpTotpUriHint", "otpauth://totp/ URL (optional)");
            OtpTotpUriTooltipLoc = Loc.Localize("OtpTotpUriTooltip", "Saved in Windows Credential Manager for this account. Codes are filled in automatically and entered silently during auto-login.\nClear to forget.");
            OtpInputPromptBadTotpUriLoc = Loc.Localize("OtpInputPromptBadTotpUri", "Enter a valid otpauth://totp/ URL.");
        }

        public string OtpInputPromptLoc { get; private set; }
        public string CancelWithShortcutLoc { get; private set; }
        public string OkLoc { get; private set; }
        public string OtpOneClickHintLoc { get; private set; }
        public string OtpInputPromptBadLoc { get; private set; }
        public string PasteButtonLoc { get; private set; }
        public string OtpTotpUriHintLoc { get; private set; }
        public string OtpTotpUriTooltipLoc { get; private set; }
        public string OtpInputPromptBadTotpUriLoc { get; private set; }
    }
}
