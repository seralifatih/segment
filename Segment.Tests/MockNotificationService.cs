using System.Collections.Generic;
using Segment.App.Services;

namespace Segment.Tests
{
    public class MockNotificationService : INotificationService
    {
        public List<DetectedChange> ToastCalls { get; } = new();
        public int CallCount => ToastCalls.Count;

        public void ShowToast(DetectedChange change)
        {
            ToastCalls.Add(change);
        }

        public void Reset()
        {
            ToastCalls.Clear();
        }
    }
}
