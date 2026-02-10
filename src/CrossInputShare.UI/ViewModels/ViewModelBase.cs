using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace CrossInputShare.UI.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels implementing INotifyPropertyChanged
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
        private bool _isBusy;
        private string _title = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the ViewModel is busy
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Gets or sets the title of the ViewModel
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Sets a property value and raises PropertyChanged event
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, string propertyName = null)
        {
            return SetProperty(ref storage, value, propertyName);
        }
    }
}