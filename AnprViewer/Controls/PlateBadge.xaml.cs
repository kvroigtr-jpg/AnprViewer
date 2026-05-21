using System.Windows;
using System.Windows.Controls;

namespace AnprViewer.Controls;

public partial class PlateBadge : UserControl
{
    public static readonly DependencyProperty PlateProperty =
        DependencyProperty.Register(nameof(Plate), typeof(string), typeof(PlateBadge),
            new PropertyMetadata("—"));

    public string Plate
    {
        get => (string)GetValue(PlateProperty);
        set => SetValue(PlateProperty, value);
    }

    public PlateBadge() => InitializeComponent();
}
