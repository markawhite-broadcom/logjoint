﻿using System;
using MonoMac.Foundation;
using MonoMac.AppKit;
using LogJoint.UI.Presenters.SearchPanel;

namespace LogJoint.UI
{
	public partial class SearchPanelControlAdapter: NSViewController, IView
	{
		IViewEvents viewEvents;
	
		public SearchPanelControlAdapter()
		{
			NSBundle.LoadNib ("SearchPanelControl", this);
		}

		#region IView implementation

		void IView.SetPresenter(IViewEvents viewEvents)
		{
			this.viewEvents = viewEvents;
		}

		void IView.SetSearchHistoryListEntries(object[] entries)
		{
			// todo
		}

		ViewCheckableControl IView.GetCheckableControlsState()
		{
			int ret = 0;

			if (searchAllRadioButton.State == NSCellStateValue.On)
				ret |= (int)ViewCheckableControl.SearchAllOccurences;
			else if (quickSearchRadioButton.State == NSCellStateValue.On)
				ret |= (int)ViewCheckableControl.QuickSearch;
			
			if (matchCaseCheckbox.State == NSCellStateValue.On)
				ret |= (int)ViewCheckableControl.MatchCase;
			if (wholeWordCheckbox.State == NSCellStateValue.On)
				ret |= (int)ViewCheckableControl.WholeWord;
			if (regexCheckbox.State == NSCellStateValue.On)
				ret |= (int)ViewCheckableControl.RegExp;
			
			return (ViewCheckableControl)ret;
		}

		void IView.SetCheckableControlsState(ViewCheckableControl affectedControls, ViewCheckableControl checkedControls)
		{
			// todo
		}

		void IView.EnableCheckableControls(ViewCheckableControl affectedControls, ViewCheckableControl enabledControls)
		{
			// todo
		}

		string IView.GetSearchTextBoxText()
		{
			return searchTextField.StringValue;
		}

		void IView.SetSearchTextBoxText(string value)
		{
			searchTextField.StringValue = value;
		}

		void IView.ShowErrorInSearchTemplateMessageBox()
		{
			// todo
		}

		void IView.FocusSearchTextBox()
		{
			searchTextField.BecomeFirstResponder();
		}

		#endregion

		partial void searchTextBoxEnterPressed (NSObject sender)
		{
			if (searchTextField.StringValue != "")
				viewEvents.OnSearchTextBoxEnterPressed();
		}

		partial void OnSearchModeChanged (NSObject sender)
		{
		}
	}
}

