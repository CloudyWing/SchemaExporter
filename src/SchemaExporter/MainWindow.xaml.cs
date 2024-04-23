using System.Windows;

namespace CloudyWing.SchemaExporter {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow(ViewModel viewModel) {
            InitializeComponent();

            DataContext = viewModel;
        }
    }
}