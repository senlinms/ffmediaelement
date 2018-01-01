﻿namespace Unosquare.FFME.MacOS
{
    using System;
    using System.Threading.Tasks;
    using AppKit;
    using Foundation;
    using Unosquare.FFME.Core;
    using Unosquare.FFME.MacOS.Core;
    using Unosquare.FFME.MacOS.Rendering;

    public class MediaElement
    {
        private MediaElementCore mediaElementCore;

        #region Constructors

        static MediaElement()
        {
            // Platform specific implementation
            Platform.SetDllDirectory = NativeMethods.SetDllDirectory;
            Platform.CopyMemory = NativeMethods.CopyMemory;
            Platform.FillMemory = NativeMethods.FillMemory;
            Platform.CreateTimer = (priority) =>
            {
                return new CustomDispatcherTimer();
            };
            Platform.UIInvoke = (priority, action) =>
            {
                NSRunLoop.Main.BeginInvokeOnMainThread(action.Invoke);
            };
            Platform.UIEnqueueInvoke = (priority, action, args) =>
            {
                NSRunLoop.Main.BeginInvokeOnMainThread(() =>
                {
                    action.DynamicInvoke(args);
                });
            };
            Platform.CreateRenderer = (mediaType, m) =>
            {
                if (mediaType == MediaType.Audio) return new AudioRenderer(m);
                else if (mediaType == MediaType.Video) return new VideoRenderer(m);
                else if (mediaType == MediaType.Subtitle) return new SubtitleRenderer(m);

                throw new ArgumentException($"No suitable renderer for Media Type '{mediaType}'");
            };

            // Simply forward the calls
            MediaElementCore.FFmpegMessageLogged += (o, e) =>
            {
                if (e.MessageType == MediaLogMessageType.Trace) return;
                Console.WriteLine($"{e.MessageType,10} - {e.Message}");
            };
        }

        public MediaElement(NSImageView imageView)
        {
            this.ImageView = imageView;
            this.mediaElementCore = new MediaElementCore(this, false);

            // RoutedEvent event bindings
            mediaElementCore.MediaOpening += (s, e) => { };
            mediaElementCore.MediaClosed += (s, e) => { };
            mediaElementCore.MediaOpened += (s, e) => { };
            mediaElementCore.MediaFailed += (s, e) => { };
            mediaElementCore.MediaEnded += (s, e) => { };
            mediaElementCore.BufferingStarted += (s, e) => { };
            mediaElementCore.BufferingEnded += (s, e) => { };
            mediaElementCore.SeekingStarted += (s, e) => { };
            mediaElementCore.SeekingEnded += (s, e) => { };

            // Non-RoutedEvent event bindings
            mediaElementCore.MessageLogged += (s, e) =>
            {
                // TODO: This is incomplete
                if (e.MessageType == MediaLogMessageType.Trace) return;
                Console.WriteLine($"{e.MessageType,10} - {e.Message}");
            };
            mediaElementCore.PositionChanged += (s, e) => { };

            // INotifyPropertyChanged PropertyChanged Event binding
            mediaElementCore.PropertyChanged += (s, e) => { };
        }

        #endregion

        #region Properties

        public NSImageView ImageView { get; }


        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Settng this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get => MediaElementCore.FFmpegDirectory;
            set => MediaElementCore.FFmpegDirectory = value;
        }

        #endregion

        #region Public methods

        public async Task Open(Uri uri)
        {
            await mediaElementCore.Open(uri);
        }

        #endregion
    }
}
