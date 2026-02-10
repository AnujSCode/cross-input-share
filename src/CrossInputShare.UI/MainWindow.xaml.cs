using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CrossInputShare.UI.ViewModels;
using Microsoft.UI.Xaml.Media;

namespace CrossInputShare.UI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            
            // Set up ViewModel
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
            
            // Set window title
            this.Title = "Cross Input Share";
            
            // Set up system tray integration (would be implemented in real app)
            InitializeSystemTray();
        }

        private void InitializeSystemTray()
        {
            // TODO: Implement system tray integration
            // This would create a system tray icon with context menu
        }

        private void CopySessionCode_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement clipboard copy
            // Clipboard.SetText(ViewModel.SessionCode);
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            // Update ViewModel when toggles are changed
            if (sender is ToggleSwitch toggleSwitch)
            {
                // The binding should handle this, but we can add additional logic here
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                // Handle navigation
                switch (item.Tag.ToString())
                {
                    case "dashboard":
                        // Already on dashboard
                        break;
                    case "devices":
                        // Navigate to devices page
                        break;
                    case "settings":
                        // Navigate to settings page
                        break;
                }
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Clean up system tray on window close
            // TODO: Clean up system tray icon
        }
    }
}