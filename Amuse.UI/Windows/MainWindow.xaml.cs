using Amuse.UI.Commands;
using Amuse.UI.Models;
using Amuse.UI.Views;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Amuse.UI.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : BaseWindow
    {
        private INavigatable _selectedTabItem;
        private int _navigationMenuIndex = -1;
        private int _navigationImageSubmenuIndex = -1;
        private int _navigationVideoSubmenuIndex = 0;
        private int _navigationTextSubmenuIndex = 0;

        public MainWindow(AmuseSettings settings, ILogger<MainWindow> logger)
        {
            Logger = logger;
            Settings = settings;
            IsLogCommandEnabled = true;
            NavigateCommand = new AsyncRelayCommand<NavigationModel>(NavigateAsync);

            InitializeComponent();
            NavigationMenuIndex = 1;
            NavigationImageSubmenuIndex = 0;
        }

        public AsyncRelayCommand<NavigationModel> NavigateCommand { get; }


        public int NavigationMenuIndex
        {
            get { return _navigationMenuIndex; }
            set
            {
                _navigationMenuIndex = value;
                NotifyPropertyChanged();
                if (_navigationMenuIndex == (int)MenuId.Image)
                {
                    var previous = NavigationImageSubmenuIndex;
                    NavigationImageSubmenuIndex = -1;
                    NavigationImageSubmenuIndex = previous;
                }
                else if (_navigationMenuIndex == (int)MenuId.Video)
                {
                    var previous = NavigationVideoSubmenuIndex;
                    NavigationVideoSubmenuIndex = -1;
                    NavigationVideoSubmenuIndex = previous;
                }
                else if (_navigationMenuIndex == (int)MenuId.Text)
                {
                    var previous = NavigationTextSubmenuIndex;
                    NavigationTextSubmenuIndex = -1;
                    NavigationTextSubmenuIndex = previous;
                }
            }
        }

        public int NavigationImageSubmenuIndex
        {
            get { return _navigationImageSubmenuIndex; }
            set { _navigationImageSubmenuIndex = value; NotifyPropertyChanged(); }
        }

        public int NavigationVideoSubmenuIndex
        {
            get { return _navigationVideoSubmenuIndex; }
            set { _navigationVideoSubmenuIndex = value; NotifyPropertyChanged(); }
        }

        public int NavigationTextSubmenuIndex
        {
            get { return _navigationTextSubmenuIndex; }
            set { _navigationTextSubmenuIndex = value; NotifyPropertyChanged(); }
        }

        public INavigatable SelectedTabItem
        {
            get { return _selectedTabItem; }
            set { _selectedTabItem = value; NotifyPropertyChanged(); }
        }


        private async Task NavigateAsync(NavigationModel model)
        {
            NavigationMenuIndex = (int)model.Menu;
            if (model.Menu == MenuId.Image)
            {
                NavigationImageSubmenuIndex = (int)model.ImageSubmenu;
                await SelectedTabItem.NavigateAsync(model.Image);
            }
            else if (model.Menu == MenuId.Video)
            {
                NavigationVideoSubmenuIndex = (int)model.VideoSubmenu;
                if (model.Video == null && model.Image != null)
                    await SelectedTabItem.NavigateAsync(model.Image);
                else
                    await SelectedTabItem.NavigateAsync(model.Video);
            }
        }
    }
}
