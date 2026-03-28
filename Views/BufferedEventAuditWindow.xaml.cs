using System.ComponentModel;
using System.Windows;

namespace IEC101MasterTester.Views
{
    public partial class BufferedEventAuditWindow : Window
    {
        public BufferedEventAuditWindow()
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
                AllowClose = false;
                return;
            }

            base.OnClosing(e);
        }
    }
}
