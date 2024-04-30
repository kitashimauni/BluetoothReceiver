using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace BluetoothReceiver
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public INPUTUNION data;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        public const int INPUT_KEYBOARD = 1;
        public const int KEYEVENTF_KEYDOWN = 0x0000;
        public const int KEYEVENTF_KEYUP = 0x0002;

        public const int VK_MEDIA_PLAY_PAUSE = 0xB3;
        public const int VK_LEFT = 0x25;
        public const int VK_RIGHT = 0x27;

        private Button status_button;
        private string status_text;
        private bool connecting = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (connecting) {
                SystemSounds.Beep.Play();
                return; 
            }
            connecting = true;
            status_button = (Button)sender;
            status_button.Text = "接続待ち";
            Console.WriteLine("接続開始");
            Start_Connection();  
        }

        RfcommServiceProvider _provider;

        private async void Start_Connection()
        {
            _provider =
                await RfcommServiceProvider.CreateAsync(
                    RfcommServiceId.SerialPort);
            StreamSocketListener listener = new StreamSocketListener();
            listener.ConnectionReceived += OnConnectionReceivedAsync;
            await listener.BindServiceNameAsync(
                _provider.ServiceId.AsString(),
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);
            _provider.StartAdvertising(listener);
        }

        private async void OnConnectionReceivedAsync(StreamSocketListener listener, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            _provider.StopAdvertising();
            listener.Dispose();

            status_text = "接続中";
            Invoke(new OnStatusChanged(SetStatusText));
            Console.WriteLine("接続完了");

            var _socket = args.Socket;
            var inputStream = _socket.InputStream;
            var dataReader = new DataReader(inputStream);

            while (_socket != null)
            {
                if (inputStream != null)
                {
                    try
                    {
                        await dataReader.LoadAsync(1);
                        byte data = dataReader.ReadByte();
                        if(data == 0x0)
                        {
                            break;
                        }
                        else
                        {
                            SendKeyFromKeyCode(data);
                        }
                    }catch (Exception ex)
                    {
                        Debug.WriteLine($"An error occurred: {ex.Message}");
                        break;
                    }
                }
            }

            status_text = "接続";
            Invoke(new OnStatusChanged(SetStatusText));
            Console.WriteLine("接続終了");
            connecting = false;
        }

        private void SendKeyFromKeyCode(byte data)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].data.ki.wVk = data;
            inputs[0].data.ki.dwFlags = KEYEVENTF_KEYDOWN;
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].data.ki.wVk = data;
            inputs[1].data.ki.dwFlags = KEYEVENTF_KEYUP;
            var result = SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
            Console.WriteLine($"Send : {data}, status {(result == 2 ? "OK" : "Error")}");
        }

        delegate void OnStatusChanged();

        private void SetStatusText()
        {
            status_button.Text = status_text;
        }
    }
}
