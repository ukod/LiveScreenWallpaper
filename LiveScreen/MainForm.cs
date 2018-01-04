using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Akson.LiveScreen;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.MediaFoundation;
using SharpDX.Windows;

namespace LiveScreen
{
    public partial class MainForm : Form
    {
        private MediaEngine mediaEngine;
        RenderForm renderForm;

        public MainForm()
        {
            InitializeComponent();
            notifyIcon1.Visible = true;
            this.notifyIcon1.MouseDoubleClick += new MouseEventHandler(notifyIcon1_MouseDoubleClick);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            notifyIcon1.ContextMenuStrip = contextMenuStrip;
        }
        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            WindowState = FormWindowState.Normal;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == WindowState) Hide();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                WindowState = FormWindowState.Minimized;
            }
        }

        private void ChooseButton_Click(object sender, EventArgs e)
        {
            // Select a File to play
            Program.openFileDialog = new OpenFileDialog { Title = "Выберите видеофайл", Filter = "Media Files(*.WMV;*.MP4;*.AVI)|*.WMV;*.MP4;*.AVI" };
            var result = Program.openFileDialog.ShowDialog();
            if (result == DialogResult.Cancel)
            {
                return;
            }
            fileNameLabel.Text = Program.openFileDialog.FileNames[0];
        }

        private void RunVideo_Click(object sender, EventArgs e)
        {
            try
            {
                if (Program.openFileDialog == null)
                {
                    throw new NullReferenceException();
                }

                // Initialize MediaFoundation
                MediaManager.Startup();
                renderForm = new SharpDX.Windows.RenderForm();
                renderForm.WindowState = FormWindowState.Minimized;
                renderForm.ShowInTaskbar = false;

                Program.device = Program.CreateDeviceForVideo(out Program.dxgiManager);

                // Creates the MediaEngineClassFactory
                var mediaEngineFactory = new MediaEngineClassFactory();

                //Assign our dxgi manager, and set format to bgra
                MediaEngineAttributes attr = new MediaEngineAttributes();
                attr.VideoOutputFormat = (int)SharpDX.DXGI.Format.B8G8R8A8_UNorm;
                attr.DxgiManager = Program.dxgiManager;

                // Creates MediaEngine for AudioOnly 
                mediaEngine = new MediaEngine(mediaEngineFactory, attr, MediaEngineCreateFlags.None);

                // Register our PlayBackEvent
                mediaEngine.PlaybackEvent += Program.OnPlaybackCallback;

                // Query for MediaEngineEx interface
                Program.mediaEngineEx = mediaEngine.QueryInterface<MediaEngineEx>();

                // Opens the file
                var fileStream = Program.openFileDialog.OpenFile();

                // Create a ByteStream object from it
                var stream = new ByteStream(fileStream);

                // Creates an URL to the file
                var url = new Uri(Program.openFileDialog.FileName, UriKind.RelativeOrAbsolute);

                // Set the source stream
                Program.mediaEngineEx.SetSourceFromByteStream(stream, url.AbsoluteUri);

                // Wait for MediaEngine to be ready
                if (!Program.eventReadyToPlay.WaitOne(1000))
                {
                    Console.WriteLine("Unexpected error: Unable to play this file");
                }

                //Create our swapchain
                Program.swapChain = Program.CreateSwapChain(Program.device, Program.workerw);

                //Get DXGI surface to be used by our media engine
                var texture = Texture2D.FromSwapChain<Texture2D>(Program.swapChain, 0);
                var surface = texture.QueryInterface<SharpDX.DXGI.Surface>();

                //Get our video size
                int w, h;
                //mediaEngine.GetNativeVideoSize(out w, out h);
                // Play the music
                Program.mediaEngineEx.Play();
                Program.mediaEngineEx.Loop = true;
                Program.mediaEngineEx.Muted = true;
                //mediaEngineEx.Volume = 0.01;
                long ts;
                //Get display size
                int displayHeight = 0;
                int displayWidth = 0;
                SharpDX.DXGI.Factory dxgiFactory = new Factory();
                foreach (var dxgiAdapter in dxgiFactory.Adapters)
                {
                    foreach (var output in dxgiAdapter.Outputs)
                    {
                        foreach (var format in Enum.GetValues(typeof(Format)))
                        {
                            var displayModes = output.GetDisplayModeList((Format)format,
                                DisplayModeEnumerationFlags.Interlaced
                                | DisplayModeEnumerationFlags.Scaling);

                            foreach (var displayMode in displayModes)
                            {
                                //Assign last mode from list - max resolution
                                displayWidth = displayMode.Width;
                                displayHeight = displayMode.Height;
                                Rational displayRefresh = displayMode.RefreshRate;
                            }
                        }
                    }
                }

                RenderLoop.Run(renderForm, () =>
                {
                    //Transfer frame if a new one is available
                    if (mediaEngine.OnVideoStreamTick(out ts))
                    {
                        mediaEngine.TransferVideoFrame(surface, null,
                            new SharpDX.Rectangle(0, 0, displayWidth, displayHeight), null);
                    }
                    Program.swapChain.Present(1, SharpDX.DXGI.PresentFlags.None);
                }, true);
            }
            catch (NullReferenceException ex)
            {
                MessageBox.Show("Ошибка открытия файла: " + ex.Message, "Ошибка");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка");
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            WindowState = FormWindowState.Normal;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (mediaEngine != null)
                mediaEngine.Shutdown();
            if (Program.swapChain != null)
                Program.swapChain.Dispose();
            if (Program.device != null)
                Program.device.Dispose();
            if(renderForm != null)
                renderForm.Close();
            Application.Exit();
        }
    }
}
