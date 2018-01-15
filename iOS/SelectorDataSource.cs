using System;
using System.Collections.Generic;
using System.ComponentModel;

using CoreGraphics;
using Foundation;
using Q42.HueApi;
using UIKit;

namespace Bloons.iOS
{
    public class SelectorDataSource : UITableViewSource
    {
        string titleProperty;
        string identifierProperty;
        bool allowMultiselection = true;
        protected List<object> selectedOptions;
        protected List<object> selectorOptions;

        public SelectorDataSource(List<object> selectorOptions, string identifierProperty, string titleProperty, List<object> savedOptions = null, bool allowMultiselection = true)
        {
            this.selectorOptions = selectorOptions;
            this.titleProperty = titleProperty;
            this.identifierProperty = identifierProperty;
            this.allowMultiselection = allowMultiselection;

            if (savedOptions == null)
            {
                selectedOptions = selectorOptions;
            }
            else
            {
                selectedOptions = savedOptions;
            }
        }

        public List<object> SelectedOptions
        {
            get
            {
                return selectedOptions;
            }
        }

        public override string TitleForHeader(UITableView tableView, nint section)
        {
            return string.Empty;
        }

        public override nint RowsInSection(UITableView tableview, nint section)
        {
            return selectorOptions.Count;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            UISwitch cellSwitch = new UISwitch();
            UIView backgroundView = new UIView();
            UITableViewCell tableViewCell = tableView.DequeueReusableCell(GetOptionPropertyValue(selectorOptions[indexPath.Row], identifierProperty).ToString());

            // If there are no cells to reuse, create a new one.
            if (tableViewCell == null)
            {
                tableViewCell = new UITableViewCell(UITableViewCellStyle.Default, GetOptionPropertyValue(selectorOptions[indexPath.Row], identifierProperty).ToString());
            }

            cellSwitch.On = (selectedOptions.Find(o => GetOptionPropertyValue(o, identifierProperty) == GetOptionPropertyValue(selectorOptions[indexPath.Row], identifierProperty)) == null ? false : true);
            cellSwitch.Frame = new CGRect(tableView.Frame.Width - cellSwitch.Frame.Width - 15, 7, cellSwitch.Frame.Width, cellSwitch.Frame.Height);
            cellSwitch.HorizontalAlignment = UIControlContentHorizontalAlignment.Right;
            cellSwitch.VerticalAlignment = UIControlContentVerticalAlignment.Bottom;
            cellSwitch.ValueChanged += (sender, e) => SwitchValueChanged(sender, e);

            tableViewCell.BackgroundColor = (indexPath.Row % 2 == 0 ? new UIColor(0.96f, 0.98f, 1, 0.5f) : new UIColor(1, 1, 1, 0));
            tableViewCell.Frame = new CGRect(0, 0, tableView.Frame.Width, tableView.Frame.Height);
            tableViewCell.TextLabel.Tag = int.Parse(GetOptionPropertyValue(selectorOptions[indexPath.Row], identifierProperty).ToString());
            tableViewCell.TextLabel.Text = (GetOptionPropertyValue(selectorOptions[indexPath.Row], titleProperty).ToString().Length > 20 ?
                                            GetOptionPropertyValue(selectorOptions[indexPath.Row], titleProperty).ToString().Substring(0, 17) + "..." :
                                            GetOptionPropertyValue(selectorOptions[indexPath.Row], titleProperty).ToString());

            tableViewCell.Add(cellSwitch);

            return tableViewCell;
        }

        object GetOptionPropertyValue(object optionObject, string optionProperty)
        {
            return optionObject.GetType().GetProperty(optionProperty).GetValue(optionObject);
        }

        void SwitchValueChanged(object sender, EventArgs e)
        {
            UISwitch cellSwitch = (UISwitch)sender;
            UITableViewCell tableViewCell = (UITableViewCell)cellSwitch.Superview;
            UITableView tableView = (UITableView)tableViewCell.Superview.Superview;
            int lightBulbId = int.Parse(tableViewCell.ReuseIdentifier);

            if (allowMultiselection == true)
            {
                if (cellSwitch.On == false)
                {
                    selectedOptions.RemoveAll(o => GetOptionPropertyValue(o, identifierProperty).ToString() == lightBulbId.ToString());
                }
                else if (selectedOptions.FindIndex(o => GetOptionPropertyValue(o, identifierProperty).ToString() == lightBulbId.ToString()) == -1)
                {
                    selectedOptions.Add(selectorOptions.Find(o => GetOptionPropertyValue(o, identifierProperty).ToString() == lightBulbId.ToString()));
                }
            }
            else
            {
                selectedOptions.Clear();
                selectedOptions.Add(selectorOptions.Find(o => GetOptionPropertyValue(o, identifierProperty).ToString() == lightBulbId.ToString()));
                tableView.ReloadRows(tableView.IndexPathsForVisibleRows, UITableViewRowAnimation.Automatic);
            }
        }
    }
}