using System;
using System.Drawing;
using System.Windows.Forms;
using LogJoint.UI.Presenters.MainForm;
using System.Runtime.InteropServices;
using LogJoint.UI.Presenters;
using System.Collections.Generic;

namespace LogJoint.UI
{
	public partial class MainForm : Form, IView, IWinFormsComponentsInitializer
	{
		IViewModel viewModel;
		List<Form> ownedForms = new List<Form>();
		bool forceClosing;

        internal static Func<string[]> RequestFiles { get; private set; }

        public static Form Create(Func<string[]> requestFiles)
        {
            RequestFiles = requestFiles;

            return Program.WireupDependenciesAndCreateMainForm();
        }

        public MainForm()
		{
			InitializeComponent();

			splitContainer_Menu_Workspace.SplitterDistance = UIUtils.Dpi.Scale(170, 120);
			splitContainer_Timeline_Log.SplitterDistance = UIUtils.Dpi.Scale(133, 120);

			menuTabControl.Selected += (s, e) =>
			{
				var t = menuTabControl.SelectedTab;
				if (t == null || string.IsNullOrEmpty(t.Name))
					return;
				viewModel.OnChangeTab(t.Name);
			};

			searchResultView.SearchResultsSplitContainer = splitContainer_Log_SearchResults;
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!forceClosing)
			{
				e.Cancel = true;
				viewModel.OnClosing();
			}
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			viewModel.OnLoad();
		}

		protected override bool ProcessTabKey(bool forward)
		{
			viewModel.OnTabPressed();
			return base.ProcessTabKey(forward);
		}

		private void MainForm_KeyDown(object se, KeyEventArgs e)
		{
			KeyCode key = KeyCode.Unknown;
			bool control = (e.KeyData & Keys.Control) != 0;
			bool shift = (e.KeyData & Keys.Shift) != 0;

			Keys keyCode = e.KeyData & Keys.KeyCode;
			if (keyCode == Keys.Escape)
				key = KeyCode.Escape;
			else if (keyCode == Keys.B && control)
				key = KeyCode.ToggleBookmarkShortcut;
			else if (keyCode == Keys.F && control)
				key = KeyCode.FindShortcut;
			else if (keyCode == Keys.K && control)
				key = KeyCode.ToggleBookmarkShortcut;
			else if (keyCode == Keys.F2 && !shift)
				key = KeyCode.NextBookmarkShortcut;
			else if (keyCode == Keys.F2 && shift)
				key = KeyCode.PrevBookmarkShortcut;
			else if (keyCode == Keys.H && control)
				key = KeyCode.HistoryShortcut;
			else if (keyCode == Keys.N && control)
				key = KeyCode.NewWindowShortcut;
			else if (keyCode == Keys.F3 && !shift)
				key = KeyCode.FindNextShortcut;
			else if (keyCode == Keys.F3 && shift)
				key = KeyCode.FindPrevShortcut;
			else if (keyCode == Keys.F6)
				key = KeyCode.FindCurrentTimeShortcut;

			if (key != KeyCode.Unknown)
			{
				viewModel.OnKeyPressed(key);
				e.Handled = true;
			}
		}

		private void optionsLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			viewModel.OnOptionsLinkClicked();
		}

		private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			viewModel.OnAboutMenuClicked();
		}

		private void reportIssueToolStripMenuItem_Click(object sender, EventArgs e)
		{
			viewModel.OnReportProblemMenuItemClicked();
		}

		private void configurationToolStripMenuItem_Click(object sender, EventArgs e)
		{
			viewModel.OnConfigurationMenuClicked();
		}

		private void MainForm_DragOver(object sender, DragEventArgs e)
		{
			if (viewModel.OnDragOver(e.Data))
				e.Effect = DragDropEffects.All;
		}

		private void MainForm_DragDrop(object sender, DragEventArgs e)
		{
			viewModel.OnDragDrop(e.Data, (ModifierKeys & Keys.Control) != 0);
		}

		private void rawViewToolStripButton_Click(object sender, EventArgs e)
		{
			viewModel.OnRawViewButtonClicked();
		}

		void IWinFormsComponentsInitializer.InitOwnedForm(Form form, bool takeOwnership)
		{
			if (takeOwnership)
				components.Add(form);
			ownedForms.Add(form);
			AddOwnedForm(form);
		}

		void IView.SetViewModel(IViewModel value)
		{
			this.viewModel = value;

			var updateAutoUpdateButton = Updaters.Create(
				() => viewModel.AutoUpdateButton,
				btn =>
				{
					restartAppToUpdatePicture.Visible = btn.state != AutoUpdateButtonState.Hidden;
					restartAppToUpdatePicture.Image =
						btn.state == AutoUpdateButtonState.ProgressIcon ?
						Properties.Resources.loader :
						Properties.Resources.RestartApp;
					toolTip1.SetToolTip(this.restartAppToUpdatePicture, btn.tooltip);
				}
			);

			var updateActiveTab = Updaters.Create(
				() => viewModel.ActiveTab,
				tab =>
				{
					var page = TabPageById(viewModel.VisibleTabs[tab].Id);
					if (page != null)
						menuTabControl.SelectedTab = page;
				}
			);

			viewModel.ChangeNotification.CreateSubscription(() =>
			{
				updateAutoUpdateButton();
				updateActiveTab();
			});
		}

		void IView.ShowOptionsMenu()
		{
			optionsContextMenu.Show(aboutLinkLabel,
				new Point(aboutLinkLabel.Width, aboutLinkLabel.Height), ToolStripDropDownDirection.BelowLeft);
		}

		IInputFocusState IView.CaptureInputFocusState()
		{
			return new InputFocusState(this);
		}

		void IView.ExecuteThreadPropertiesDialog(IThread thread, IPresentersFacade navHandler, IColorTheme theme)
		{
			using (UI.ThreadPropertiesForm f = new UI.ThreadPropertiesForm(thread, navHandler, theme))
			{
				f.ShowDialog();
			}
		}

		void IView.BeginSplittingSearchResults()
		{
			splitContainer_Log_SearchResults.BeginSplitting();
		}

		void IView.BeginSplittingTabsPanel()
		{
			splitContainer_Menu_Workspace.BeginSplitting();
		}

		void IView.EnableFormControls(bool enable)
		{
			splitContainer_Menu_Workspace.Enabled = enable;
			splitContainer_Menu_Workspace.ForeColor = !enable ? Color.Gray : Color.Black;
			Win32Native.EnableMenuItem(Win32Native.GetSystemMenu(this.Handle, false), Win32Native.SC_CLOSE,
				!enable ? Win32Native.MF_GRAYED : Win32Native.MF_ENABLED);
		}

		void IView.EnableOwnedForms(bool enable)
		{
			ownedForms.ForEach(f =>
			{
				if (!f.IsDisposed)
					f.UseWaitCursor = !enable;
			});
		}

		void IView.SetAnalyzingIndicationVisibility(bool value)
		{
			toolStripAnalyzingImage.Visible = value;
			toolStripAnalyzingLabel.Visible = value;
		}

		void IView.SetCaption(string value)
		{
			this.Text = value;
		}

		void IView.SetShareButtonState(bool visible, bool enabled, bool progress)
		{
			// on win there are no sharing buttons on main form
		}

		void IView.SetIssueReportingMenuAvailablity(bool value)
		{
			reportIssueToolStripMenuItem.Visible = value;
		}

		void IView.UpdateTaskbarProgress(int progressPercentage)
		{
			TaskbarProgress.SetValue(this.Handle, progressPercentage, 100);
		}

		void IView.SetTaskbarState(TaskbarState state)
		{
			TaskbarProgress.SetState(this.Handle,
				state == TaskbarState.Progress ? TaskbarProgress.TaskbarStates.Normal : TaskbarProgress.TaskbarStates.NoProgress);
		}

		void IView.ForceClose()
		{
			forceClosing = true;
			this.Close();
		}

		TabPage TabPageById(string tabId)
		{
			switch (tabId)
			{
				case TabIDs.Sources: return sourcesTabPage;
				case TabIDs.Threads: return threadsTabPage;
				case TabIDs.HighlightingFilteringRules: return highlightTabPage;
				case TabIDs.Bookmarks: return navigationTabPage;
				case TabIDs.Search: return searchTabPage;
				case TabIDs.Postprocessing: return postprocessingTabPage;
				default: return null;
			}
		}

		void restartAppToUpdatePicture_Click(object sender, System.EventArgs e)
		{
			this.viewModel.OnRestartPictureClicked();
		}

		class InputFocusState : IInputFocusState
		{
			public InputFocusState(MainForm form)
			{
				focusedControlBeforeWaitState = Control.FromHandle(Win32Native.GetFocus());

				if (focusedControlBeforeWaitState == null
				 && form.searchPanelView.searchTextBox.Focused)
				{
					// ComboBox's child EDIT returned by win32 GetFocus()
					// can not be found by Control.FromHandle().
					// Handle search box separately.
					focusedControlBeforeWaitState = form.searchPanelView.searchTextBox;
				}
			}

			void IInputFocusState.Restore()
			{
				if (focusedControlBeforeWaitState != null
				 && !focusedControlBeforeWaitState.IsDisposed
				 && focusedControlBeforeWaitState.Enabled
				 && focusedControlBeforeWaitState.CanFocus)
				{
					focusedControlBeforeWaitState.Focus();
				}
				focusedControlBeforeWaitState = null;
			}

			Control focusedControlBeforeWaitState;
		};

		static class Win32Native
		{
			public const int SC_CLOSE = 0xF060;
			public const int MF_GRAYED = 0x1;
			public const int MF_ENABLED = 0x0;

			[DllImport("user32.dll")]
			public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

			[DllImport("user32.dll")]
			public static extern int EnableMenuItem(IntPtr hMenu, int wIDEnableItem, int wEnable);

			[DllImport("user32.dll")]
			public static extern IntPtr GetFocus();

			[DllImport("user32.dll")]
			public static extern IntPtr GetParent(IntPtr hWnd);
		}
	}

}