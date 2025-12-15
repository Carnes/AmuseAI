using Amuse.UI.Commands;
using Amuse.UI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Amuse.UI.Helpers;

namespace Amuse.UI.Windows
{
    public class BaseWindow : Window, INotifyPropertyChanged, System.Windows.Forms.IWin32Window
    {
        private readonly WindowInteropHelper _interopHelper;

        public BaseWindow()
        {
            _interopHelper = new WindowInteropHelper(this);
            Title = $"Amuse {App.DisplayVersion}";
            TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();
            WindowCloseCommand = new AsyncRelayCommand(WindowClose);
            WindowRestoreCommand = new AsyncRelayCommand(WindowRestore);
            WindowMinimizeCommand = new AsyncRelayCommand(WindowMinimize);
            WindowMaximizeCommand = new AsyncRelayCommand(WindowMaximize);
        }

        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register(nameof(Settings), typeof(AmuseSettings), typeof(BaseWindow));

        public AmuseSettings Settings
        {
            get { return (AmuseSettings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        public ILogger Logger { get; init; }
        public nint Handle => _interopHelper.Handle;
        public AsyncRelayCommand WindowMinimizeCommand { get; }
        public AsyncRelayCommand WindowRestoreCommand { get; }
        public AsyncRelayCommand WindowMaximizeCommand { get; }
        public AsyncRelayCommand WindowCloseCommand { get; }
        public bool IsLogCommandEnabled { get; init; }

        public Task ShowAsync(WindowState state)
        {
            Opacity = 0.4;
            WindowState = state;
            Show();

            var tcs = new TaskCompletionSource();
            var fadeInAnimation = new DoubleAnimation
            {
                From = .4,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseOut }
            };

            if (Content is Grid content)
            {
                var marginAnimation = new ThicknessAnimation
                {
                    From = new Thickness(20),
                    Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                    FillBehavior = FillBehavior.Stop,
                    EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseOut }
                };
                content.BeginAnimation(Grid.MarginProperty, marginAnimation);
            }

            fadeInAnimation.Completed += (s, e) =>
            {
                Opacity = 1;
                tcs.SetResult();
            };
            BeginAnimation(OpacityProperty, fadeInAnimation);
            return tcs.Task;
        }


        public Task HideAsync()
        {
            var tcs = new TaskCompletionSource();
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseOut }
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                Opacity = 0;
                WindowState = WindowState.Normal;
                Hide();
                tcs.SetResult();
            };
            BeginAnimation(OpacityProperty, fadeOutAnimation);
            return tcs.Task;
        }


        public void AnimateWidth(double toWidth)
        {
            var widthAnimation = new DoubleAnimation
            {
                To = toWidth,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseOut }
            };
            widthAnimation.Completed += (s, e) => { Width = toWidth; };
            BeginAnimation(WidthProperty, widthAnimation);
        }


        protected Task WindowClose()
        {
            Close();
            return Task.CompletedTask;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _interopHelper.EnsureHandle();
            this.RegisterWindowDisplayMonitor();
        }


        protected virtual Task WindowRestore()
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
            return Task.CompletedTask;
        }


        protected virtual Task WindowMinimize()
        {
            WindowState = WindowState.Minimized;
            return Task.CompletedTask;
        }


        protected virtual Task WindowMaximize()
        {
            WindowState = WindowState.Maximized;
            return Task.CompletedTask;
        }


        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        #endregion
    }

}
