using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
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

        private void OpenHyperLink(object sender, RoutedEventArgs e)
        {
            Process.Start(((Hyperlink)sender).NavigateUri.OriginalString);
        }
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

        private MainModel()
        {

        }

        private IDisposable subscription;
        IDirectInputDevice8 joystick;

        private bool isMagnified = false;

        public string StatusText
        {
            get { return _status; }
            set
            {
                _status = value;
                this.OnPropertyChanged();
            }
        }
        private string _status;

        public int TargetWidth
        {
            get { return _tw; }
            set
            {
                _tw = Math.Max((int)Math.Ceiling(Geometry.ORIG_WIDTH * WidthScale), value);
                TargetMargin = TargetMargin;
                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(MagRate));
            }
        }
        private int _tw;
        public float WidthScale
        {
            get { return _ws; }
            set
            {
                _ws = value;
                TargetWidth = TargetWidth;
            }
        }
        private float _ws;
        public float MagRate
        {
            get
            {
                return TargetWidth / (Geometry.ORIG_WIDTH * WidthScale);
            }
        }

        public int TargetMargin
        {
            get { return _tm; }
            set
            {
                xOffset = 50 - (float)Geometry.ORIG_WIDTH / TargetWidth * Math.Max(0, value);
                _tm = (int)Math.Round((float)(50 - xOffset) * TargetWidth / Geometry.ORIG_WIDTH);
                this.OnPropertyChanged();
            }
        }
        private int _tm;
        public float xOffset
        {
            get { return _xOfs; }
            set
            { 
                _xOfs = value >= 0 ? value : 0;
                this.OnPropertyChanged();
            }
        }
        private float _xOfs;
        public float yOffset { get; set; }


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

        public void Magnify()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool success = MagSetFullscreenTransform(isMagnified ? 1.0f : MagRate, (int)xOffset, (int)yOffset);
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

        public MainViewModel()
        {
            model = MainModel.getInstance();

            // pass thru propertychanged event
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

            /*
            _ri = 0;
            _ws = ResolutionChoice[_ri].WidthScale;
            _tw = Geometry.ORIG_WIDTH;
            _tm = Geometry.ORIG_LEFT_MARGIN;
            */

            ResolutionIndex = 1;
            WidthScale = Resolution.RESOLUTION_CHOICES[_ri].WidthScale;
            TargetWidth = 639;
            TargetMargin = 80;

        }

        public string StatusText
        {
            get { return model.StatusText; }
        }

        public string VersionText { get; } = Assembly.GetEntryAssembly()?.GetName().Version.ToString();


        public int ResolutionIndex
        {
            get { return _ri; }
            set
            {
                _ri = value;
                WidthScale = Resolution.RESOLUTION_CHOICES[_ri].WidthScale;
            }
        }
        private int _ri;

        public float WidthScale
        {
            get { return model.WidthScale; }
            set { model.WidthScale = value; }
        }


        public int TargetWidth
        {
            get { return model.TargetWidth; }
            set { model.TargetWidth = value; }
        }


        public int TargetMargin
        {
            get { return model.TargetMargin; }
            set { model.TargetMargin = value; }
        }

        public float MagRate
        {
            get { return model.MagRate; }
        }

        public int xOffset
        {
            get { return (int)model.xOffset; }
        }
        public PlayStyle PlayStyle
        {
            get; set;
        }

    }

    public class Resolution
    {
        public const int HD_WIDTH = 1280;
        private int _x, _y;
        private string _abbr;
        private float _scale;
        public Resolution(int x, int y, string abbr)
        {
            _x = x;
            _y = y;
            _abbr = abbr;
            _scale = (float)y / HD_WIDTH;
        }
        public override string ToString()
        {
            return $"{_x}x{_y} ({_abbr})";
        }
        public float WidthScale { get { return _scale; } }

        public static readonly Resolution[] RESOLUTION_CHOICES = {
                new Resolution(720, 1280, "HD"),
                new Resolution(1080, 1920, "FHD"),
                new Resolution(2160, 3840, "4K")
        };
        public static Resolution[] GetResolutions()
        {
            return RESOLUTION_CHOICES;
        }
    }
    public class Geometry
    {
        public static int ORIG_WIDTH = 288;
        public static int ORIG_LEFT_MARGIN = 50;
    }

    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum PlayStyle
    {
        [Description("1P")]
        PLAYSTYLE_1P = 0,
        [Description("2P")]
        PLAYSTYLE_2P = 1,
        [Description("DP")]
        PLAYSTYLE_DP = 2
    }
    public class EnumDescriptionConverter: EnumConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value != null)
            {
                FieldInfo fi = value.GetType().GetField(value.ToString());
                if (fi != null)
                {
                    return fi.GetCustomAttributes<DescriptionAttribute>().First().Description;
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public EnumDescriptionConverter(Type type): base(type) { } 
    }
}
