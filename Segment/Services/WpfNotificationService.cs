using System;
using System.Windows;
using Segment.App.Views;

namespace Segment.App.Services
{
    public class WpfNotificationService : INotificationService
    {
        public void ShowToast(DetectedChange change)
        {
            if (System.Windows.Application.Current == null) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var toast = new NotificationToast(change);
                toast.Show();
            });
        }
    }
}
