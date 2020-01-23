using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Interactivity;
using System.Windows.Media;
using WPFNotification.Core.Configuration;
using WPFNotification.Core.Interactivity;

namespace WPFNotification.Core
{
    public static class NotifyBox
    {
        #region Constructors

        /// <summary>
        /// Initialises static members of the <see cref="NotifyBox" /> class.
        /// </summary>
        static NotifyBox()
        {
            notificationWindows = new List<WindowInfo>();
            notificationWindowsCount = 0;
        }

        #endregion

        #region Private Classes

        /// <summary>
        /// Window metadata.
        /// </summary>
        private sealed class WindowInfo
        {
            public int ID { get; set; }

            /// <summary>
            /// Gets or sets the display duration.
            /// </summary>
            /// <value>
            /// The display duration.
            /// </value>
            public TimeSpan DisplayDuration { get; set; }

            /// <summary>
            /// Gets or sets the window.
            /// </summary>
            /// <value>
            /// The window.
            /// </value>
            public Window Window { get; set; }
        }

        #endregion

        #region Fields

        /// <summary>
        /// Max number of notifications window
        /// </summary>
        private const int MAX_NOTIFICATIONS = 1;

        /// <summary>
        /// Number of notification windows
        /// </summary>
        private static int notificationWindowsCount;

        /// <summary>
        /// The margin between notification windows.
        /// </summary>
        private const double Margin = 5;

        /// <summary>
        /// list of notifications window.
        /// </summary>
        private static readonly List<WindowInfo> notificationWindows;

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Shows the specified notification.
        /// </summary>
        /// <param name="content">The notification content.</param>
        /// <param name="configuration">The notification configuration object.</param>
        public static void Show(object content, NotificationConfiguration configuration)
        {
            var notificationTemplate = (DataTemplate)Application.Current.Resources[configuration.TemplateName];

            var window = new Window
            {
                Title = "",
                Width = configuration.Width.Value,
                Height = configuration.Height.Value,
                Content = content,
                ShowActivated = false,
                AllowsTransparency = true,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Topmost = true,
                Background = Brushes.Transparent,
                UseLayoutRounding = true,
                ContentTemplate = notificationTemplate
            };

            Show(window, configuration.DisplayDuration, configuration.NotificationFlowDirection);
        }

        /// <summary>
        /// Shows the specified notification.
        /// </summary>
        /// <param name="content">The notification content.</param>
        public static void Show(object content)
        {
            Show(content, NotificationConfiguration.DefaultConfiguration);
        }

        /// <summary>
        /// Shows the specified window as a notification.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <param name="displayDuration">The display duration.</param>
        public static void Show(Window window, TimeSpan displayDuration, NotificationFlowDirection notificationFlowDirection)
        {
            var behaviors = Interaction.GetBehaviors(window);
            behaviors.Add(new FadeBehavior());
            behaviors.Add(new SlideBehavior());
            SetWindowDirection(window, notificationFlowDirection);
            notificationWindowsCount += 1;

            var windowInfo = new WindowInfo
            {
                ID = notificationWindowsCount,
                DisplayDuration = displayDuration,
                Window = window
            };

            windowInfo.Window.Closed += Window_Closed;

            if (notificationWindows.Count + 1 > MAX_NOTIFICATIONS)
            {
                var noti = notificationWindows.FirstOrDefault();

                if (noti != null)
                {
                    var closeBehaviors = Interaction.GetBehaviors(noti.Window);
                    var fadeBehavior = closeBehaviors.OfType<FadeBehavior>().First();
                    fadeBehavior.FadeOut();

                    var slideBehavior = closeBehaviors.OfType<SlideBehavior>().First();
                    slideBehavior.SlideOut();

                    notificationWindows.Remove(noti);
                    noti.Window.Close();
                }
            }

            notificationWindows.Add(windowInfo);

            Observable
                .Timer(displayDuration)
                .ObserveOnDispatcher()
                .Subscribe(x => OnTimerElapsed(windowInfo));

            window.Show();
        }

        /// <summary>
        /// Remove all notifications from notification list and buffer list.
        /// </summary>
        public static void ClearNotifications()
        {
            notificationWindows.Clear();

            notificationWindowsCount = 0;
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Called when the timer has elapsed. Removes any stale notifications.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Timers.ElapsedEventArgs" /> instance containing the event data.</param>
        private static void OnTimerElapsed(WindowInfo windowInfo)
        {
            if (notificationWindows.Count > 0 && !notificationWindows.Any(i => i.ID == windowInfo.ID))
            {
                return;
            }

            var now = DateTime.Now;

            if (windowInfo.Window.IsMouseOver)
            {
                Observable
                    .Timer(windowInfo.DisplayDuration)
                    .ObserveOnDispatcher()
                    .Subscribe(x => OnTimerElapsed(windowInfo));
            }
            else
            {
                var behaviors = Interaction.GetBehaviors(windowInfo.Window);
                var fadeBehavior = behaviors.OfType<FadeBehavior>().First();
                fadeBehavior.FadeOut();

                var slideBehavior = behaviors.OfType<SlideBehavior>().First();
                slideBehavior.SlideOut();

                EventHandler eventHandler = null;

                eventHandler = (sender2, e2) =>
                {
                    fadeBehavior.FadeOutCompleted -= eventHandler;
                    notificationWindows.Remove(windowInfo);
                    windowInfo.Window.Close();
                };

                fadeBehavior.FadeOutCompleted += eventHandler;
            }
        }

        /// <summary>
        /// Called when the window is about to close.
        /// Remove the notification window from notification windows list and add one from the buffer list.
        /// </summary>
        private static void Window_Closed(object sender, EventArgs e)
        {
            var window = (Window)sender;
        }

        /// <summary>
        /// Display the notification window in specified direction of the screen
        /// </summary>
        /// <param name="window"> The window object</param>
        /// <param name="notificationFlowDirection"> Direction in which new notifications will appear.</param>
        private static void SetWindowDirection(Window window, NotificationFlowDirection notificationFlowDirection)
        {
            var workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            var transform = PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformFromDevice;
            var corner = transform.Transform(new Point(workingArea.Right, workingArea.Bottom));

            switch (notificationFlowDirection)
            {
                case NotificationFlowDirection.RightBottom:
                    window.Left = corner.X - window.Width - window.Margin.Right - Margin;
                    window.Top = corner.Y - window.Height - window.Margin.Top;
                    break;
                case NotificationFlowDirection.LeftBottom:
                    window.Left = 0;
                    window.Top = corner.Y - window.Height - window.Margin.Top;
                    break;
                case NotificationFlowDirection.LeftUp:
                    window.Left = 0;
                    window.Top = 0;
                    break;
                case NotificationFlowDirection.RightUp:
                    window.Left = corner.X - window.Width - window.Margin.Right - Margin;
                    window.Top = 0;
                    break;
                default:
                    window.Left = corner.X - window.Width - window.Margin.Right - Margin;
                    window.Top = corner.Y - window.Height - window.Margin.Top;
                    break;
            }
        }

        #endregion
    }
}