using System;
using System.Windows.Forms;

namespace LogJoint.UI.Presenters.NewLogSourceDialog.Pages.FormatDetection
{
	public partial class AnyLogFormatUI : UserControl, IView
	{
		public AnyLogFormatUI()
		{
			InitializeComponent();
		}
		
		
		private void browseButton_Click(object sender, EventArgs e)
		{
            if (UI.MainForm.RequestFiles == null)
            {
                browseFileDialog.Filter = "*.*|*.*";

                if (browseFileDialog.ShowDialog() == DialogResult.OK)
                    filePathTextBox.Text = FileListUtils.MakeFileList(browseFileDialog.FileNames);
            }
            else
            {
                var files = UI.MainForm.RequestFiles();
                if (files == null)
                    return;
                filePathTextBox.Text = FileListUtils.MakeFileList(files);
            }
        }


		object IView.PageView
		{
			get { return this; }
		}

		string IView.InputValue
		{
			get { return filePathTextBox.Text; }
			set { filePathTextBox.Text = value; }
		}
	}
}
