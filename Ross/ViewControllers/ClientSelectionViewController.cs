﻿using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Ross.DataSources;
using Toggl.Ross.Theme;

namespace Toggl.Ross.ViewControllers
{
    public class ClientSelectionViewController : UITableViewController
    {
        private const float CellSpacing = 4f;
        private readonly WorkspaceModel workspace;

        public ClientSelectionViewController (WorkspaceModel workspace) : base (UITableViewStyle.Plain)
        {
            this.workspace = workspace;
            Title = "ClientTitle".Tr ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            View.Apply (Style.Screen);
            EdgesForExtendedLayout = UIRectEdge.None;
            new Source (this).Attach ();
        }

        public Action<ClientModel> ClientSelected { get; set; }

        private class Source : PlainDataViewSource<ClientModel>
        {
            private readonly static NSString ClientCellId = new NSString ("ClientCellId");
            private readonly ClientSelectionViewController controller;

            public Source (ClientSelectionViewController controller)
                : base (controller.TableView, GetClientView (controller.workspace))
            {
                this.controller = controller;
            }

            private static IDataView<ClientModel> GetClientView (WorkspaceModel model)
            {
                return model.Clients.Where (m => m.DeletedAt == null).ToView ();
            }

            public override void Attach ()
            {
                base.Attach ();

                controller.TableView.RegisterClassForCellReuse (typeof(ClientCell), ClientCellId);
                controller.TableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
            }

            public override float EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
            {
                return 60f;
            }

            public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                return EstimatedHeight (tableView, indexPath);
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = (ClientCell)tableView.DequeueReusableCell (ClientCellId, indexPath);
                cell.Bind (GetRow (indexPath));
                return cell;
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                var client = GetRow (indexPath);
                var cb = controller.ClientSelected;
                if (client != null && cb != null) {
                    cb (client);
                }
            }
        }

        private class ClientCell : UITableViewCell
        {
            private readonly UILabel nameLabel;
            private ClientModel model;

            public ClientCell (IntPtr handle) : base (handle)
            {
                this.Apply (Style.Screen);
                ContentView.Add (nameLabel = new UILabel ().Apply (Style.ClientList.NameLabel));
                BackgroundView = new UIView ().Apply (Style.ClientList.RowBackground);
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();

                var contentFrame = new RectangleF (0, CellSpacing / 2, Frame.Width, Frame.Height - CellSpacing);
                SelectedBackgroundView.Frame = BackgroundView.Frame = ContentView.Frame = contentFrame;

                contentFrame.X = 15f;
                contentFrame.Y = 0;
                contentFrame.Width -= 15f;

                nameLabel.Frame = contentFrame;
            }

            public void Bind (ClientModel model)
            {
                this.model = model;

                if (String.IsNullOrWhiteSpace (model.Name)) {
                    nameLabel.Text = "ClientNoNameClient".Tr ();
                } else {
                    nameLabel.Text = model.Name;
                }
            }
        }
    }
}