using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CrossInputShare.UI.ViewModels;

namespace CrossInputShare.UI.Controls
{
    public sealed partial class CreateSessionDialog : ContentDialog
    {
        public MainViewModel ViewModel { get; }

        public CreateSessionDialog(MainViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.DataContext = ViewModel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // The ViewModel's CreateSessionCommand will handle the actual creation
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Cancel - nothing to do
        }
    }
}