﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Inscribe.Configuration.Tabs;
using Inscribe.ViewModels.Dialogs;
using Livet;
using Livet.Commands;
using Livet.Messaging;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;
using Inscribe.Filter.Filters.ScreenName;

namespace Inscribe.ViewModels.PartBlocks.MainBlock
{
    public class ColumnViewModel : ViewModel
    {
        public ColumnOwnerViewModel Parent { get; private set; }

        public ColumnViewModel(ColumnOwnerViewModel parent)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// このカラムがフォーカスを得た
        /// </summary>
        public event EventHandler GotFocus;
        protected void OnGetFocus()
        {
            var fchandler = GotFocus;
            if (fchandler != null)
                fchandler(this, EventArgs.Empty);
        }

        public event Action<TabViewModel> SelectedTabChanged;
        protected void OnSelectedTabChanged(TabViewModel selected)
        {
            var stc = this.SelectedTabChanged;
            if (stc != null)
                stc(selected);
        }

        private bool _isInDragDrop = false;
        /// <summary>
        /// ドラッグアンドドロップ処理中であることを示します。
        /// </summary>
        public bool IsInDragDrop
        {
            get { return this._isInDragDrop; }
            set
            {
                this._isInDragDrop = value;
                RaisePropertyChanged(() => IsInDragDrop);
            }
        }

        private ObservableCollection<TabViewModel> _tabItems = new ObservableCollection<TabViewModel>();
        public ObservableCollection<TabViewModel> TabItems { get { return this._tabItems; } }

        private TabViewModel _selectedTabViewModel = null;
        public TabViewModel SelectedTabViewModel
        {
            get { return this._selectedTabViewModel; }
            set
            {
                if (this._selectedTabViewModel != null)
                    this._selectedTabViewModel.IsSelected = false;
                this._selectedTabViewModel = value;
                if (this._selectedTabViewModel != null)
                    this._selectedTabViewModel.IsSelected = true;
                // 選択タブが変わったんだから、カレントカラムは自分でないとおかしい
                this.OnGetFocus();
                OnSelectedTabChanged(value);
                RaisePropertyChanged(() => SelectedTabViewModel);
            }
        }

        /// <summary>
        /// タブをこのカラムの末尾に追加します。
        /// </summary>
        public TabViewModel AddTab(TabProperty tabProperty = null)
        {
            var nvm = new TabViewModel(this, tabProperty);
            this.AddTab(nvm);
            return nvm;
        }

        /// <summary>
        /// タブをこのカラムの末尾に追加します。
        /// </summary>
        public void AddTab(TabViewModel tabViewModel)
        {
            InsertBefore(tabViewModel, null);
        }

        /// <summary>
        /// タブを指定したタブの前に追加します。
        /// </summary>
        /// <param name="insert">追加するタブ</param>
        /// <param name="beforeThis">このタブの前に追加、nullなら末尾に追加</param>
        public void InsertBefore(TabViewModel insert, TabViewModel beforeThis)
        {
            // インデックス取得
            int idx = -1;
            if (beforeThis != null)
                idx = this._tabItems.IndexOf(beforeThis);

            if (idx == -1)
                this._tabItems.Add(insert);
            else
                this._tabItems.Insert(idx, insert);

            // タブの保持カラムを更新し、イベントハンドラを連結する
            insert.SetTabOwner(this);
            insert.GotFocus += OnTabGotFocus;

            // 一つ目のタブであればそのタブを選択する
            if (this._tabItems.Count == 1)
                SelectedTabViewModel = insert;

            // タブ変更のコミット
            insert.InvalidateCache();
        }

        public void CloseTab(TabViewModel tabViewModel)
        {
            RemoveTab(tabViewModel);
            this.Parent.PushClosedTabStack(tabViewModel);
            this.Parent.GCColumn();
        }

        public void RemoveTab(TabViewModel tabViewModel)
        {
            if (this._tabItems.Remove(tabViewModel))
            {
                // イベントハンドラを解除
                tabViewModel.GotFocus -= OnTabGotFocus;
                if (this._tabItems.Count > 0)
                    SelectedTabViewModel = this._tabItems[0];
                else
                    SelectedTabViewModel = null;
            }
        }

        private void OnTabGotFocus(object o, EventArgs e)
        {
            OnGetFocus();
        }

        public bool Contains(TabViewModel tabViewModel)
        {
            return this._tabItems.Contains(tabViewModel);
        }

        #region DragDropStartCommand
        DelegateCommand _DragDropStartCommand;

        public DelegateCommand DragDropStartCommand
        {
            get
            {
                if (_DragDropStartCommand == null)
                    _DragDropStartCommand = new DelegateCommand(DragDropStart);
                return _DragDropStartCommand;
            }
        }

        private void DragDropStart()
        {
            this.Parent.Columns.ForEach(c => c.IsInDragDrop = true);
        }
        #endregion

        #region OnDropCommand
        DelegateCommand<DragEventArgs> _OnDropCommand;

        public DelegateCommand<DragEventArgs> OnDropCommand
        {
            get
            {
                if (_OnDropCommand == null)
                    _OnDropCommand = new DelegateCommand<DragEventArgs>(OnDrop);
                return _OnDropCommand;
            }
        }

        private void OnDrop(DragEventArgs parameter)
        {
            var data = parameter.Data.GetData(typeof(TabViewModel)) as TabViewModel;
            if (data != null)
            {
                var odo = parameter.OriginalSource as FrameworkElement;
                var tvm = odo != null ? odo.DataContext as TabViewModel : null;
                if (tvm == data) return;
                Parent.Columns.ForEach(cvm => cvm.RemoveTab(data));
                this.InsertBefore(data, tvm);
                this.SelectedTabViewModel = data;
                return;
            }
            var td = parameter.Data.GetData(typeof(TweetViewModel)) as TweetViewModel;
            if (td != null)
            {
                var odo = parameter.OriginalSource as FrameworkElement;
                var tvm = odo != null ? odo.DataContext as TabViewModel : null;
                if (tvm != null)
                {
                    tvm.TabProperty.TweetSources = tvm.TabProperty.TweetSources.Concat(new[] { new FilterUser("^" + td.Status.User.ScreenName + "$") }).ToArray();
                    tvm.InvalidateCache();
                }
                else
                {
                    this.AddTab(new TabProperty() { Name = "@" + td.Status.User.ScreenName, TweetSources = new[] { new FilterUser(td.Status.User.ScreenName) } });
                }
                return;
            }
        }
        #endregion

        #region OnDropLeftColumnCommand
        DelegateCommand<DragEventArgs> _OnDropLeftColumnCommand;

        public DelegateCommand<DragEventArgs> OnDropLeftColumnCommand
        {
            get
            {
                if (_OnDropLeftColumnCommand == null)
                    _OnDropLeftColumnCommand = new DelegateCommand<DragEventArgs>(OnDropLeftColumn);
                return _OnDropLeftColumnCommand;
            }
        }

        private void OnDropLeftColumn(DragEventArgs parameter)
        {
            var data = parameter.Data.GetData(typeof(TabViewModel)) as TabViewModel;
            if (data == null) return;
            var idx = this.Parent.ColumnIndexOf(this);
            var target = this.Parent.CreateColumn(idx);
            this.Parent.Columns.ForEach(cvm => cvm.RemoveTab(data));
            target.AddTab(data);
        }
        #endregion

        #region OnDropRightColumnCommand
        DelegateCommand<DragEventArgs> _OnDropRightColumnCommand;

        public DelegateCommand<DragEventArgs> OnDropRightColumnCommand
        {
            get
            {
                if (_OnDropRightColumnCommand == null)
                    _OnDropRightColumnCommand = new DelegateCommand<DragEventArgs>(OnDropRightColumn);
                return _OnDropRightColumnCommand;
            }
        }

        private void OnDropRightColumn(DragEventArgs parameter)
        {
            var data = parameter.Data.GetData(typeof(TabViewModel)) as TabViewModel;
            if (data == null) return;
            this.Parent.Columns.ForEach(c => c.IsInDragDrop = false);
            var idx = this.Parent.ColumnIndexOf(this);
            var target = this.Parent.CreateColumn(idx + 1);
            this.Parent.Columns.ForEach(cvm => cvm.RemoveTab(data));
            target.AddTab(data);
        }
        #endregion

        #region DragDropFinishCommand
        DelegateCommand _DragDropFinishCommand;

        public DelegateCommand DragDropFinishCommand
        {
            get
            {
                if (_DragDropFinishCommand == null)
                    _DragDropFinishCommand = new DelegateCommand(DragDropFinish);
                return _DragDropFinishCommand;
            }
        }

        private void DragDropFinish()
        {
            this.Parent.Columns.ForEach(c => c.IsInDragDrop = false);
            this.Parent.GCColumn();
        }
        #endregion

        #region GetFocusCommand
        DelegateCommand _GetFocusCommand;

        public DelegateCommand GetFocusCommand
        {
            get
            {
                if (_GetFocusCommand == null)
                    _GetFocusCommand = new DelegateCommand(GetFocus);
                return _GetFocusCommand;
            }
        }

        private void GetFocus()
        {
            OnGetFocus();
        }
        #endregion

        #region EditTabCommand
        DelegateCommand<TabViewModel> _EditTabCommand;

        public DelegateCommand<TabViewModel> EditTabCommand
        {
            get
            {
                if (_EditTabCommand == null)
                    _EditTabCommand = new DelegateCommand<TabViewModel>(EditTab);
                return _EditTabCommand;
            }
        }

        private void EditTab(TabViewModel parameter)
        {
            parameter.TabProperty = ShowTabEditor(parameter.TabProperty);
            parameter.InvalidateCache();
        }
        #endregion

        #region AddNewTabCommand
        DelegateCommand _AddNewTabCommand;

        public DelegateCommand AddNewTabCommand
        {
            get
            {
                if (_AddNewTabCommand == null)
                    _AddNewTabCommand = new DelegateCommand(AddNewTab);
                return _AddNewTabCommand;
            }
        }

        private void AddNewTab()
        {
            var property = ShowTabEditor();
            this.AddTab(property);
        }
        #endregion

        #region RebirthTabCommand
        DelegateCommand _RebirthTabCommand;

        public DelegateCommand RebirthTabCommand
        {
            get
            {
                if (_RebirthTabCommand == null)
                    _RebirthTabCommand = new DelegateCommand(RebirthTab, CanRebirthTab);
                return _RebirthTabCommand;
            }
        }

        private bool CanRebirthTab()
        {
            return this.Parent.IsExistedClosedTab();
        }

        private void RebirthTab()
        {
            this.AddTab(this.Parent.PopClosedTab());
        }
        #endregion
      

        #region CloseCommand
        DelegateCommand<TabViewModel> _CloseCommand;

        public DelegateCommand<TabViewModel> CloseCommand
        {
            get
            {
                if (_CloseCommand == null)
                    _CloseCommand = new DelegateCommand<TabViewModel>(Close);
                return _CloseCommand;
            }
        }

        private void Close(TabViewModel parameter)
        {
            this.CloseTab(parameter);
        }
        #endregion

        private TabProperty ShowTabEditor(TabProperty property = null)
        {
            if(property == null)
                property = new TabProperty();
            var vm = new TabEditorViewModel(property);
            this.Messenger.Raise(new TransitionMessage(vm, "EditTab"));
            property.TweetSources = vm.FilterEditorViewModel.RootFilters;
            return property;
        }

        internal void SetFocus()
        {
            this.Messenger.Raise(new InteractionMessage("SetFocus"));
        }
    }
}
