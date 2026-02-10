using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CrossInputShare.UI.Controls
{
    public sealed partial class VerificationDialog : ContentDialog
    {
        public VerificationViewModel ViewModel { get; }

        public VerificationDialog(string localFingerprint, string remoteFingerprint)
        {
            this.InitializeComponent();
            ViewModel = new VerificationViewModel(localFingerprint, remoteFingerprint);
            this.DataContext = ViewModel;
            
            // Update primary button based on verification status
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.IsVerified))
                {
                    IsPrimaryButtonEnabled = ViewModel.IsVerified;
                }
            };
        }

        private void CopyLocalFingerprint_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement clipboard copy
            // Clipboard.SetText(ViewModel.LocalFingerprint);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Verification successful
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Cancel verification
        }
    }

    public partial class VerificationViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _localFingerprint;

        [ObservableProperty]
        private string _remoteFingerprint;

        [ObservableProperty]
        private string _enteredFingerprint = string.Empty;

        [ObservableProperty]
        private bool _isVerified;

        [ObservableProperty]
        private bool _showVerificationStatus;

        [ObservableProperty]
        private string _verificationStatusMessage = string.Empty;

        [ObservableProperty]
        private Symbol _verificationStatusIcon = Symbol.Important;

        [ObservableProperty]
        private string _verificationStatusColor = "#FFF44336"; // Red

        public VerificationViewModel(string localFingerprint, string remoteFingerprint)
        {
            LocalFingerprint = localFingerprint;
            RemoteFingerprint = remoteFingerprint;
        }

        partial void OnEnteredFingerprintChanged(string value)
        {
            VerifyFingerprints();
        }

        private void VerifyFingerprints()
        {
            if (string.IsNullOrWhiteSpace(EnteredFingerprint))
            {
                ShowVerificationStatus = false;
                IsVerified = false;
                return;
            }

            // Simple comparison (in real app, this would be more sophisticated)
            bool matches = EnteredFingerprint.Trim().Equals(RemoteFingerprint, System.StringComparison.OrdinalIgnoreCase);

            IsVerified = matches;
            ShowVerificationStatus = true;

            if (matches)
            {
                VerificationStatusMessage = "Fingerprints match! Safe to connect.";
                VerificationStatusIcon = Symbol.Accept;
                VerificationStatusColor = "#4CAF50"; // Green
            }
            else
            {
                VerificationStatusMessage = "Fingerprints do not match! Do not connect.";
                VerificationStatusIcon = Symbol.Important;
                VerificationStatusColor = "#F44336"; // Red
            }
        }
    }
}