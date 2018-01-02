﻿namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public partial class MediaEngine
    {
        #region Property Backing

        private readonly ObservableCollection<KeyValuePair<string, string>> m_MetadataBase = new ObservableCollection<KeyValuePair<string, string>>();
        private bool m_HasMediaEnded = false;
        private double m_BufferingProgress = 0;
        private double m_DownloadProgress = 0;
        private string m_VideoSmtpeTimecode = string.Empty;
        private string m_VideoHardwareDecoder = string.Empty;
        private bool m_IsBuffering = false;
        private MediaEngineState m_CoreMediaState = MediaEngineState.Close;
        private bool m_IsOpening = false;
        private AtomicBoolean m_IsSeeking = new AtomicBoolean(false);

        #endregion

        #region Notification Properties

        /// <summary>
        /// Provides key-value pairs of the metadata contained in the media.
        /// Returns null when media has not been loaded.
        /// </summary>
        public ObservableCollection<KeyValuePair<string, string>> Metadata => m_MetadataBase;

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat => Container?.MediaFormatName;

        /// <summary>
        /// Gets the duration of a single frame step.
        /// If there is a video component with a framerate, this propery returns the length of a frame.
        /// If there is no video component it simply returns a tenth of a second.
        /// </summary>
        public TimeSpan FrameStepDuration
        {
            get
            {
                if (IsOpen == false) { return TimeSpan.Zero; }

                if (HasVideo)
                {
                    if (VideoFrameLength > 0)
                        return TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerMillisecond * VideoFrameLength * 1000d, 0));
                }

                return TimeSpan.FromSeconds(0.1d);
            }
        }

        /// <summary> 
        /// Returns whether the given media has audio. 
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public bool HasAudio => Container?.Components.HasAudio ?? false;

        /// <summary> 
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo => Container?.Components.HasVideo ?? false;

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec => Container?.Components?.Video?.CodecName;

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int VideoBitrate => Container?.Components?.Video?.Bitrate ?? 0;

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary> 
        public int NaturalVideoWidth => Container?.Components?.Video?.FrameWidth ?? 0;

        /// <summary> 
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight => Container?.Components.Video?.FrameHeight ?? 0;

        /// <summary>
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate => Container?.Components.Video?.BaseFrameRate ?? 0;

        /// <summary>
        /// Gets the duration in seconds of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameLength => 1d / (Container?.Components?.Video?.BaseFrameRate ?? 0);

        /// <summary>
        /// Gets the name of the video hardware decoder in use.
        /// Enabling hardware acceleration does not guarantee decoding will be performed in hardware.
        /// When hardware decoding of frames is in use this will return the name of the HW accelerator.
        /// Otherwise it will return an empty string.
        /// </summary>
        public string VideoHardwareDecoder
        {
            get => m_VideoHardwareDecoder;
            internal set => SetProperty(ref m_VideoHardwareDecoder, value);
        }

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec => Container?.Components?.Audio?.CodecName;

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitrate => Container?.Components?.Audio?.Bitrate ?? 0;

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels => Container?.Components?.Audio?.Channels ?? 0;

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate => Container?.Components?.Audio?.SampleRate ?? 0;

        /// <summary>
        /// Gets the audio bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitsPerSample => Container?.Components?.Audio?.BitsPerSample ?? 0;

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public TimeSpan? NaturalDuration => Container?.MediaDuration;

        /*
        public Duration NaturalDuration
        {
            get
            {
                return Container == null
                  ? Duration.Automatic
                  : (Container.MediaDuration == TimeSpan.MinValue
                    ? Duration.Forever
                    : (Container.MediaDuration < TimeSpan.Zero ? default(Duration) : new Duration(Container.MediaDuration)));
            }
        }
        */

        /// <summary>
        /// Returns whether the currently loaded media can be paused.
        /// This is only valid after the MediaOpened event has fired.
        /// Note that this property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause => IsOpen ? !IsLiveStream : false;

        /// <summary>
        /// Returns whether the currently loaded media is live or realtime
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsLiveStream => IsOpen ? Container.IsStreamRealtime && Container.MediaDuration == TimeSpan.MinValue : false;

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        public bool IsPositionUpdating
        {
            get => m_IsPositionUpdating.Value;
            set => m_IsPositionUpdating.Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether the currently loaded media can be seeked.
        /// </summary>
        public bool IsSeekable => Container?.IsStreamSeekable ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPlaying => MediaState == MediaEngineState.Play;

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        public bool HasMediaEnded
        {
            get => m_HasMediaEnded;
            internal set => SetProperty(ref m_HasMediaEnded, value);
        }

        /// <summary>
        /// Get a value indicating whether the media is buffering.
        /// </summary>
        public bool IsBuffering
        {
            get => m_IsBuffering;
            private set => SetProperty(ref m_IsBuffering, value);
        }

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking
        {
            get => m_IsSeeking.Value == true;

            internal set
            {
                if (m_IsSeeking.Value == value) return;
                m_IsSeeking.Value = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Returns the current video SMTPE timecode if available.
        /// If not available, this property returns an empty string.
        /// </summary>
        public string VideoSmtpeTimecode
        {
            get => m_VideoSmtpeTimecode;
            internal set => SetProperty(ref m_VideoSmtpeTimecode, value);
        }

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress
        {
            get => m_BufferingProgress;
            private set => SetProperty(ref m_BufferingProgress, value);
        }

        /// <summary>
        /// The wait packet buffer length.
        /// It is adjusted to 1 second if bitrate information is available.
        /// Otherwise, it's simply 512KB
        /// </summary>
        public int BufferCacheLength
        {
            get
            {
                if (Container == null || (HasVideo && VideoBitrate <= 0) || (HasAudio && AudioBitrate <= 0))
                {
                    return 512 * 1024; // 512 kilobytes
                }
                else
                {
                    var byteRate = (VideoBitrate + AudioBitrate) / 8;
                    return (Container?.IsStreamRealtime ?? false) ?
                        byteRate / 2 : byteRate;
                }
            }
        }

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double DownloadProgress
        {
            get => m_DownloadProgress;
            private set => SetProperty(ref m_DownloadProgress, value);
        }

        /// <summary>
        /// Gets the maximum packet buffer length, according to the bitrate (if available).
        /// If it's a realtime stream it will return 30 times the buffer cache length.
        /// Otherwise, it will return  4 times of the buffer cache length.
        /// </summary>
        public int DownloadCacheLength => (Container?.IsStreamRealtime ?? false) ?
            BufferCacheLength * 30 : BufferCacheLength * 4;

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening
        {
            get => m_IsOpening;
            internal set => SetProperty(ref m_IsOpening, value);
        }

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen => (IsOpening == false) && (Container?.IsOpen ?? false);

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaEngineState MediaState
        {
            get => m_CoreMediaState;

            internal set
            {
                SetProperty(ref m_CoreMediaState, value);
                OnPropertyChanged(nameof(IsPlaying));
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the metada property.
        /// </summary>
        internal void UpdateMetadaProperty()
        {
            m_MetadataBase.Clear();
            if (Container?.Metadata != null)
            {
                foreach (var kvp in Container.Metadata)
                    m_MetadataBase.Add(kvp);
            }

            OnPropertyChanged(nameof(Metadata));
        }

        /// <summary>
        /// Updates the media properties notifying that there are new values to be read from all of them.
        /// Call this method only when necessary because it creates a lot of events.
        /// </summary>
        internal void NotifyPropertyChanges()
        {
            UpdateMetadaProperty();

            OnPropertyChanged(nameof(IsOpen));
            OnPropertyChanged(nameof(MediaFormat));
            OnPropertyChanged(nameof(HasAudio));
            OnPropertyChanged(nameof(HasVideo));
            OnPropertyChanged(nameof(VideoCodec));
            OnPropertyChanged(nameof(VideoBitrate));
            OnPropertyChanged(nameof(NaturalVideoWidth));
            OnPropertyChanged(nameof(NaturalVideoHeight));
            OnPropertyChanged(nameof(VideoFrameRate));
            OnPropertyChanged(nameof(VideoFrameLength));
            OnPropertyChanged(nameof(VideoHardwareDecoder));
            OnPropertyChanged(nameof(AudioCodec));
            OnPropertyChanged(nameof(AudioBitrate));
            OnPropertyChanged(nameof(AudioChannels));
            OnPropertyChanged(nameof(AudioSampleRate));
            OnPropertyChanged(nameof(AudioBitsPerSample));
            OnPropertyChanged(nameof(NaturalDuration));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(IsLiveStream));
            OnPropertyChanged(nameof(IsSeekable));
            OnPropertyChanged(nameof(BufferCacheLength));
            OnPropertyChanged(nameof(DownloadCacheLength));
            OnPropertyChanged(nameof(FrameStepDuration));
        }

        /// <summary>
        /// Resets the dependency properies.
        /// </summary>
        internal void ResetDependencyProperies()
        {
            Volume = Defaults.DefaultVolume;
            Balance = Defaults.DefaultBalance;
            SpeedRatio = Defaults.DefaultSpeedRatio;
            IsMuted = false;
            DownloadProgress = 0;
            BufferingProgress = 0;
            VideoSmtpeTimecode = string.Empty;
            VideoHardwareDecoder = string.Empty;
            IsBuffering = false;
            IsMuted = false;
            HasMediaEnded = false;
            UpdatePosition(TimeSpan.Zero);
        }

        #endregion
    }
}
