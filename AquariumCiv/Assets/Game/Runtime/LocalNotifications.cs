using System;
using System.Collections;
using UnityEngine;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using Unity.Notifications;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace AquariumCiv.Game
{
    /// <summary>The phone's local notifications, all <see cref="EraNotifier"/> needs of them. <see cref="Schedule"/> may run off the main
    /// thread; the rest are main thread only.</summary>
    public interface ILocalNotifications
    {
        void Prepare();
        /// <summary>Asks the player's permission, the one call that shows a system dialog. A coroutine: the dialog takes as long as it takes.</summary>
        IEnumerator Ask(Action<bool> answered);
        void Schedule(int id, TimeSpan after, string title, string body);
        void CancelAll();
    }

    /// <summary>The Editor and anything that isn't a phone: the schedule is only logged.</summary>
    public class LoggedNotifications : ILocalNotifications
    {
        public void Prepare() { }

        public IEnumerator Ask(Action<bool> answered)
        {
            answered(true);
            yield break;
        }

        public void Schedule(int id, TimeSpan after, string title, string body) =>
            Debug.Log($"[Notify] would fire in {after.TotalHours:0.00} h: {title}: {body}");

        public void CancelAll() { }
    }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
    /// <summary>Unity's Mobile Notifications package (#97), through its unified API. Sound and alert, no badge: a dot never counts.</summary>
    public class PhoneNotifications : ILocalNotifications
    {
        const string Channel = "era";
        int mainThread;

        public void Prepare()
        {
            mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var args = NotificationCenterArgs.Default;
            args.PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Sound;
            args.AndroidChannelId = Channel;
            NotificationCenter.Initialize(args);
#if UNITY_ANDROID
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = Channel, Name = "When your people are ready", Description = "One note when an era is ready to arrive.", Importance = Importance.Default,
            });
#endif
        }

        public IEnumerator Ask(Action<bool> answered)
        {
            var request = NotificationCenter.RequestPermission();
            yield return request;
            answered(request.Status == NotificationsPermissionStatus.Granted);
        }

        public void Schedule(int id, TimeSpan after, string title, string body)
        {
            var note = new Notification { Identifier = id, Title = title, Text = body };
#if UNITY_ANDROID
            // A worker thread isn't known to the JVM until it says so.
            bool attach = System.Threading.Thread.CurrentThread.ManagedThreadId != mainThread;
            if (attach) AndroidJNI.AttachCurrentThread();
            try { NotificationCenter.ScheduleNotification(note, new NotificationIntervalSchedule(after)); }
            finally { if (attach) AndroidJNI.DetachCurrentThread(); }
#else
            NotificationCenter.ScheduleNotification(note, new NotificationIntervalSchedule(after));
#endif
        }

        public void CancelAll()
        {
            NotificationCenter.CancelAllScheduledNotifications();
            NotificationCenter.CancelAllDeliveredNotifications();
        }
    }
#endif
}
