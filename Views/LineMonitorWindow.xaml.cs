using System.ComponentModel;
using System.Windows;

namespace IEC101MasterTester.Views
{
    public partial class LineMonitorWindow : Window
    {
        public LineMonitorWindow()
        {
            InitializeComponent();
        }

        public bool AllowClose { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!AllowClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnClosing(e);
        }
    }
}
