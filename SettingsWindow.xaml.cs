using System.Windows;

namespace Glimule;

public partial class SettingsWindow : Window
{
    private bool _loading = true;

    public SettingsWindow()
    {
        InitializeComponent();
        CapsuleToggle.IsChecked = SettingsStore.Current.CapsuleInTextField;
        StartupToggle.IsChecked = SettingsStore.Current.RunAtStartup;
        DefaultScaleToggle.IsChecked = SettingsStore.Current.UseDefaultScale;
        ScaleSlider.Value = SettingsStore.Current.UseDefaultScale
            ? 100
            : Math.Clamp(SettingsStore.Current.BubbleScale, 50, 200);
        UpdateScaleLabel();
        Loaded += (_, _) => _loading = false;
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SettingsStore.Current.CapsuleInTextField = CapsuleToggle.IsChecked == true;
        SettingsStore.Current.RunAtStartup = StartupToggle.IsChecked == true;
        SettingsStore.Save();
    }

    private void DefaultScaleChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var useDefault = DefaultScaleToggle.IsChecked == true;
        SettingsStore.Current.UseDefaultScale = useDefault;
        if (useDefault)
        {
            _loading = true;
            ScaleSlider.Value = 100;
            SettingsStore.Current.BubbleScale = 100;
            _loading = false;
        }

        UpdateScaleLabel();
        SettingsStore.Save();
    }

    private void ScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var value = (int)Math.Round(ScaleSlider.Value);
        SettingsStore.Current.BubbleScale = value;
        SettingsStore.Current.UseDefaultScale = value == 100;
        _loading = true;
        DefaultScaleToggle.IsChecked = value == 100;
        _loading = false;
        UpdateScaleLabel();
        SettingsStore.Save();
    }

    private void UpdateScaleLabel()
    {
        var value = SettingsStore.Current.UseDefaultScale ? 100 : SettingsStore.Current.BubbleScale;
        ScaleLabel.Text = $"{value}%";
    }
}
