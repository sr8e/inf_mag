using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using Vortice.DirectInput;

namespace inf_mag
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("Magnification.dll")]
        static extern bool MagInitialize();

        [DllImport("Magnification.dll")]
        static extern bool MagUninitialize();

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MagInitialize();
        }

        protected override void OnClosed(EventArgs e)
        {
            MagUninitialize();
        }


        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
        }


        public class NotifyObject : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }



        }
        public class MainModel : NotifyObject
        {
            [DllImport("Magnification.dll")]
            static extern bool MagSetFullscreenTransform(float magLevel, int xOffset, int yOffset);
            public static MainModel instance;

            public static MainModel getInstance()
            {
                if (instance == null)
                {
                    instance = new MainModel();
                }
                return instance;
            }


            private IDisposable subscription;
            IDirectInputDevice8 joystick;
            private string _status;
            public string StatusText
            {
                get { return _status; }
                set
                {
                    _status = value;
                    this.OnPropertyChanged();
                }
            }
            public float MagRate { get; set; }
            public int xOffset { get; set; }
            public int yOffset { get; set; }
            private bool isMagnified = false;

            private MainModel()
            {

            }
            public void InitializeDirectInput()
            {
                // setup directinput...
                IDirectInput8 dinput = DInput.DirectInput8Create();

                IList<DeviceInstance> devices = dinput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly);

                if (devices.Count == 0)
                {
                    StatusText = "Joystick device not found...";
                }
                else
                {
                    StatusText = $"{devices.Count} devices found";
                }

                // todo: list all devices and choice
                Guid guid = devices.First().InstanceGuid;
                joystick = dinput.CreateDevice(guid);

                /*
                Console.WriteLine(device.Capabilities);
                IList<DeviceObjectInstance> obj = device.GetObjects();
                foreach (DeviceObjectInstance objinstance in obj)
                {
                    Console.WriteLine(objinstance.Name);
                }
                */

                joystick.SetCooperativeLevel((IntPtr)joystick, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                joystick.SetDataFormat<RawJoystickState>();
                joystick.Properties.BufferSize = 1024;
                joystick.Acquire();

                IObservable<long> interval = Observable.Interval(TimeSpan.FromMilliseconds(10));
                subscription = interval.Subscribe((_) =>
                {
                    JoystickUpdate[] updates = joystick.GetBufferedJoystickData();
                    foreach (JoystickUpdate u in updates)
                    {
                        if (u.Offset == JoystickOffset.Buttons10 && u.Value > 0)
                        {
                            Magnify();
                        }
                    }

                });
            }
            public void InitProps(float mag, int x, int y)
            {
                MagRate = mag;
                xOffset = x;
                yOffset = y;
            }

            public void Magnify()
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    bool success = MagSetFullscreenTransform(isMagnified ? 1.0f : MagRate, xOffset, yOffset);
                    if (!success)
                    {
                        StatusText = "Failed to Magnify Display.";
                    }
                    isMagnified ^= success;
                    Console.WriteLine($"x{MagRate}, {success}, {isMagnified}");
                });
            }
        }

        public class MainViewModel : NotifyObject
        {
            public MainModel model;

            private float _mag = 1.0f;
            private int x = 0, y = 0;

            public MainViewModel()
            {
                model = MainModel.getInstance();
                model.PropertyChanged += new PropertyChangedEventHandler((object sender, PropertyChangedEventArgs e) =>
                {
                    PropertyInfo pInfo = this.GetType().GetProperty(e.PropertyName);
                    if (pInfo == null)
                    {
                        return;
                    }
                    //pInfo.SetValue(this, model.GetType().GetProperty(e.PropertyName).GetValue(model));
                    this.OnPropertyChanged(e.PropertyName);
                });
                model.InitializeDirectInput();
                model.InitProps(MagRate, x, y);
            }

            public string StatusText
            {
                get { return model.StatusText; }
            }
            public string VersionText { get; } = Assembly.GetEntryAssembly()?.GetName().Version.ToString();
            public float MagRate
            {
                get { return _mag; }
                set
                {
                    _mag = value > 1.0f ? value : 1.0f;
                    this.OnPropertyChanged();
                    model.MagRate = MagRate;
                }
            }
        }
    }
}
