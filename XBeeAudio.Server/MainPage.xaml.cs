using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using XBee;
using XBee.Universal;

namespace XBeeAudio
{  
    // We are initializing a COM interface for use within the namespace
    // This interface allows access to memory at the byte level which we need to populate audio data that is generated
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]

    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public sealed partial class MainPage : Page
    {
        private XBeeController _controller;
        private XBeeNode _remoteNode;
        private AudioDeviceInputNode _deviceInputNode;
        private AudioDeviceOutputNode _deviceOutputNode;
        private AudioFrameInputNode _frameInputNode;
        private AudioFrameOutputNode _frameOutputNode;
        public double Theta = 0;


        private AudioGraph _graph;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            //_rootPage = MainPage.Current;
            //await FindXBeeAsync();
            await InitializeAudioAsync();
        }


        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _graph?.Dispose();
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

        private async Task InitializeAudioAsync()
        {
            // Create an AudioGraph with default settings
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
            settings.EncodingProperties = AudioEncodingProperties.CreatePcm(22050, 1, 16);

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                return;
            }

            _graph = result.Graph;

            // Create a device output node
            CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await _graph.CreateDeviceOutputNodeAsync();
            if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                return;
            }
            
            _deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

            CreateAudioDeviceInputNodeResult deviceInputNodeResult = await _graph.CreateDeviceInputNodeAsync(MediaCategory.Other);

            if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                return;
            }

            _deviceInputNode = deviceInputNodeResult.DeviceInputNode;

            // Create the FrameInputNode at the same format as the graph, except explicitly set mono.
            AudioEncodingProperties nodeEncodingProperties = _graph.EncodingProperties;
            
            nodeEncodingProperties.ChannelCount = 1;
            _frameInputNode = _graph.CreateFrameInputNode(nodeEncodingProperties);
            _frameInputNode.AddOutgoingConnection(_deviceOutputNode);


            _frameOutputNode = _graph.CreateFrameOutputNode(nodeEncodingProperties);
            _deviceInputNode.AddOutgoingConnection(_frameOutputNode);

            // Initialize the Frame Input Node in the stopped state
            _frameInputNode.Stop();

            // Hook up an event handler so we can start generating samples when needed
            // This event is triggered when the node is required to provide data
            _frameInputNode.QuantumStarted += node_QuantumStarted;

            _graph.QuantumProcessed += GraphOnQuantumProcessed;
           
            // Start the graph since we will only start/stop the frame input node
            _graph.Start();
        }

        private void GraphOnQuantumProcessed(AudioGraph audioGraph, object o)
        {
            ProcessFrameOutput(_frameOutputNode.GetFrame());
        }

        private void node_QuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
        {
            // GenerateAudioData can provide PCM audio data by directly synthesizing it or reading from a file.
            // Need to know how many samples are required. In this case, the node is running at the same rate as the rest of the graph
            // For minimum latency, only provide the required amount of samples. Extra samples will introduce additional latency.
            uint numSamplesNeeded = (uint)args.RequiredSamples;

            if (numSamplesNeeded != 0)
            {
                AudioFrame audioData = GenerateAudioData(numSamplesNeeded);
                _frameInputNode.AddFrame(audioData);
            }
        }

        private unsafe AudioFrame GenerateAudioData(uint samples)
        {
            // Buffer size is (number of samples) * (size of each sample)
            // We choose to generate single channel (mono) audio. For multi-channel, multiply by number of channels
            uint bufferSize = samples * sizeof(float);
            AudioFrame frame = new AudioFrame(bufferSize);

            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                byte* dataInBytes;
                uint capacityInBytes;

                // Get the buffer from the AudioFrame
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                // Cast to float since the data we are generating is float
                var dataInFloat = (float*)dataInBytes;

                float freq = 1000; // choosing to generate frequency of 1kHz
                float amplitude = 0.3f;
                int sampleRate = (int)_graph.EncodingProperties.SampleRate;
                double sampleIncrement = freq * (Math.PI * 2) / sampleRate;

                // Generate a 1kHz sine wave and populate the values in the memory buffer
                for (int i = 0; i < samples; i++)
                {
                    double sinValue = amplitude * Math.Sin(Theta);
                    dataInFloat[i] = (float)sinValue;
                    Theta += sampleIncrement;
                }
            }

            return frame;
        }

        private async void PushToTalkButton_OnPressed(object sender, RoutedEventArgs e)
        {
            _frameInputNode.Start();

            if (_remoteNode == null)
            {
                return;
            }
            
        }

        private unsafe void ProcessFrameOutput(AudioFrame frame)
        {
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                byte* dataInBytes;
                uint capacityInBytes;
                float* dataInFloat;

                // Get the buffer from the AudioFrame
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                dataInFloat = (float*)dataInBytes;
            }
        }
    }
}
