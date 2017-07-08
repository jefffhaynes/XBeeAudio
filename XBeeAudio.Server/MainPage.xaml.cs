using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using XBee;
using XBee.Universal;

namespace XBeeAudio
{
    public sealed partial class MainPage : Page
    {
        private XBeeController _controller;
        private MediaCapture _capture;
        private XBeeNode _remoteNode;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await FindXBeeAsync();
            await InitializeRecordingAsync();
            InitializePlayback();
        }

        private async Task FindXBeeAsync()
        {
            var controllers = await XBeeController.FindControllersAsync(9600);

            if (controllers.Count == 0)
            {
                StatusText.Text = "No controllers.";
                return;
            }

            StatusText.Text = "Found controller \n";

            _controller = controllers.First();

            _controller.NodeDiscovered += (sender, args) =>
            {
                _remoteNode = args.Node;
                StatusText.Text += $"Discovered remote node: {args.Name}\n";
            };

            await _controller.DiscoverNetworkAsync(TimeSpan.FromSeconds(5));
        }

        private async Task InitializeRecordingAsync()
        {
            try
            {
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Audio
                };

                _capture = new MediaCapture();

                _capture.RecordLimitationExceeded += sender => throw new Exception("Record Limitation Exceeded ");

                _capture.Failed += (sender, errorEventArgs) => throw new Exception(string.Format("Code: {0}. {1}", errorEventArgs.Code, errorEventArgs.Message));

                await _capture.InitializeAsync(settings);

                StatusText.Text += "Recording initialized\n";
            }
            catch (Exception e)
            {
                StatusText.Text += $"Audio failed: {e.Message}";
            }
        }

        private void InitializePlayback()
        {
            if (_controller == null)
            {
                return;
            }

            var playback = new MediaElement();
            playback.SetSource(new XBeeStreamWrapper(_controller.Local.GetSerialStream()), "mp3");
            playback.Play();

            StatusText.Text += "Playback initialized\n";
        }

        private async void PushToTalkButton_OnPressed(object sender, RoutedEventArgs e)
        {
            await _capture.StartRecordToStreamAsync(MediaEncodingProfile.CreateMp3(AudioEncodingQuality.Low), 
                new XBeeStreamWrapper(_controller.Local.GetSerialStream()));
        }
    }
}
