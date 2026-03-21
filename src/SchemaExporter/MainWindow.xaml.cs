using System.Windows;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model to bind to this window.</param>
    public MainWindow(ViewModel viewModel) {
        InitializeComponent();

        DataContext = viewModel;
    }
}