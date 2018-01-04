using System;
using System.Threading;
using System.Windows.Forms;
using LiveScreen;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;

using DXDevice = SharpDX.Direct3D11.Device;
using SharpDX.DXGI;
using SharpDX.Windows;

namespace Akson
{
    namespace LiveScreen
    {
        class Program
        {

            /// <summary>
            /// The event raised when MediaEngine is ready to play the music.
            /// </summary>
            internal static readonly ManualResetEvent eventReadyToPlay = new ManualResetEvent(false);

            /// <summary>
            /// Set when the music is stopped.
            /// </summary>
            internal static bool isMusicStopped;

            /// <summary>
            /// The instance of MediaEngineEx
            /// </summary>
            internal static MediaEngineEx mediaEngineEx;

            /// <summary>
            /// Our dx11 device
            /// </summary>
            internal static DXDevice device;

            /// <summary>
            /// Our SwapChain
            /// </summary>
            public static SwapChain swapChain;

            /// <summary>
            /// DXGI Manager
            /// </summary>
            internal static DXGIDeviceManager dxgiManager;

            internal static MainForm mainForm;
            internal static OpenFileDialog openFileDialog;
            internal static IntPtr workerw;
            /// <summary>
            /// Defines the entry point of the application.
            /// </summary>
            /// <param name="args">The args.</param>
            [STAThread]
            static void Main(string[] args)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                mainForm = new MainForm();
                // Fetch the Progman window
                IntPtr progman = W32.FindWindow("Progman", null);

                IntPtr resultR = IntPtr.Zero;

                // Send 0x052C to Progman. This message directs Progman to spawn a 
                // WorkerW behind the desktop icons. If it is already there, nothing 
                // happens.
                W32.SendMessageTimeout(progman,
                    0x052C,
                    new IntPtr(0),
                    IntPtr.Zero,
                    W32.SendMessageTimeoutFlags.SMTO_NORMAL,
                    1000,
                    out resultR);

                workerw = IntPtr.Zero;

                // We enumerate all Windows, until we find one, that has the SHELLDLL_DefView 
                // as a child. 
                // If we found that window, we take its next sibling and assign it to workerw.
                W32.EnumWindows(new W32.EnumWindowsProc((tophandle, topparamhandle) =>
                {
                    IntPtr p = W32.FindWindowEx(tophandle,
                        IntPtr.Zero,
                        "SHELLDLL_DefView",
                        IntPtr.Zero);

                    if (p != IntPtr.Zero)
                    {
                            // Gets the WorkerW Window after the current one.
                            workerw = W32.FindWindowEx(IntPtr.Zero,
                            tophandle,
                            "WorkerW",
                            IntPtr.Zero);
                    }

                    return true;
                }), IntPtr.Zero);
                mainForm.Show();
                Application.Run();
            }

            /// <summary>
            /// Called when [playback callback].
            /// </summary>
            /// <param name="playEvent">The play event.</param>
            /// <param name="param1">The param1.</param>
            /// <param name="param2">The param2.</param>
            internal static void OnPlaybackCallback(MediaEngineEvent playEvent, long param1, int param2)
            {
                switch (playEvent)
                {
                    case MediaEngineEvent.CanPlay:
                        eventReadyToPlay.Set();
                        break;
                    case MediaEngineEvent.TimeUpdate:
                        break;
                    case MediaEngineEvent.Error:
                    case MediaEngineEvent.Abort:
                    case MediaEngineEvent.Ended:
                        isMusicStopped = true;
                        break;
                }
            }

            /// <summary>
            /// Creates device with necessary flags for video processing
            /// </summary>
            /// <param name="manager">DXGI Manager, used to create media engine</param>
            /// <returns>Device with video support</returns>
            internal static DXDevice CreateDeviceForVideo(out DXGIDeviceManager manager)
            {
                //Device need bgra and video support
                var device = new DXDevice(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport);

                //Add multi thread protection on device
                DeviceMultithread mt = device.QueryInterface<DeviceMultithread>();
                mt.SetMultithreadProtected(true);

                //Reset device
                manager = new DXGIDeviceManager();
                manager.ResetDevice(device);

                return device;
            }

            /// <summary>
            /// Creates swap chain ready to use for video output
            /// </summary>
            /// <param name="dxdevice">DirectX11 device</param>
            /// <param name="handle">RenderForm Handle</param>
            /// <returns>SwapChain</returns>
            internal static SwapChain CreateSwapChain(DXDevice dxdevice, IntPtr handle)
            {
                //Walk up device to retrieve Factory, necessary to create SwapChain
                var dxgidevice = dxdevice.QueryInterface<SharpDX.DXGI.Device>();
                var adapter = dxgidevice.Adapter.QueryInterface<Adapter>();
                var factory = adapter.GetParent<Factory1>();

                /*To be allowed to be used as video, texture must be of the same format (eg: bgra), and needs to be bindable are render target.
                 * you do not need to create render target view, only the flag*/
                SwapChainDescription sd = new SwapChainDescription()
                {
                    BufferCount = 1,
                    ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                    IsWindowed = true,
                    OutputHandle = handle,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.Discard,
                    Usage = Usage.RenderTargetOutput,
                    Flags = SwapChainFlags.None
                };

                return new SwapChain(factory, dxdevice, sd);
            }
        }

    }
}