using SimLinkHub.Models;
using SimLinkHub.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace SimLinkHub
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 1. Get the handle
            IntPtr handle = new WindowInteropHelper(this).Handle;

            // 2. Get the ViewModel
            var vm = DataContext as SimLinkHub.ViewModels.MainViewModel;

            if (vm != null)
            {
                // 3. Start the watcher and pass the handle
                vm.Initialize(handle);

                try
                {
                    HwndSource source = HwndSource.FromHwnd(handle);
                    source.AddHook(new HwndSourceHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                    {
                        if (msg == 0x0402) // WM_USER_SIMCONNECT
                        {
                            vm.ProcessSimConnectMessage();
                            handled = true;
                        }
                        return IntPtr.Zero;
                    }));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Hook Error: {ex.Message}");
                }
            }
        }
        private void Test0_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.TestFlaps(0);
        }
        private void Test50_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.TestFlaps(50);
        }
        private void Test100_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.TestFlaps(100);
        }
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SimInstrument inst)
            {
                // Get the percentage from the "Tag" we set in XAML
                double percent = double.Parse(btn.Tag.ToString());

                // Tell the ViewModel to run the test
                var vm = (MainViewModel)this.DataContext;
                vm.ManualTest(inst, percent);
            }
        }
    }
}