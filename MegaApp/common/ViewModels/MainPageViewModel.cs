﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Phone.Shell;
using mega;
using MegaApp.Classes;
using MegaApp.Enums;
using MegaApp.MegaApi;
using MegaApp.Resources;
using MegaApp.Services;
using MegaApp.Views;

namespace MegaApp.ViewModels
{
    public class MainPageViewModel : BaseAppInfoAwareViewModel
    {
        public event EventHandler<CommandStatusArgs> CommandStatusChanged;
        private readonly MainPage _mainPage;

        public MainPageViewModel(MegaSDK megaSdk, AppInformation appInformation, MainPage mainPage)
            :base(megaSdk, appInformation)
        {
            _mainPage = mainPage;            
            UpgradeAccountCommand = new DelegateCommand(UpgradeAccount);
            CancelUpgradeAccountCommand = new DelegateCommand(CancelUpgradeAccount);

            InitializeModel();

            UpdateUserData();

            InitializeMenu(HamburgerMenuItemType.CloudDrive);
        }

        #region Commands
                
        public ICommand UpgradeAccountCommand { get; set; }
        public ICommand CancelUpgradeAccountCommand { get; set; }

        #endregion

        #region Events

        private void OnCommandStatusChanged(bool status)
        {
            if (CommandStatusChanged == null) return;

            CommandStatusChanged(this, new CommandStatusArgs(status));
        }

        #endregion

        #region Public Methods

        public void Initialize(GlobalListener globalListener)
        {
            AccountService.GetAccountDetailsFinish += OnGetAccountDetailsFinish;

            // Add folders to global listener to receive notifications
            globalListener.Folders.Add(this.CloudDrive);
            globalListener.Folders.Add(this.RubbishBin);
        }

        public void Deinitialize(GlobalListener globalListener)
        {
            AccountService.GetAccountDetailsFinish -= OnGetAccountDetailsFinish;

            // Add folders to global listener to receive notifications
            globalListener.Folders.Remove(this.CloudDrive);
            globalListener.Folders.Remove(this.RubbishBin);
        }

        public void SetCommandStatus(bool status)
        {
            OnCommandStatusChanged(status);
        }

        public void LoadFolders()
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                if (this.CloudDrive.FolderRootNode == null)
                    this.CloudDrive.FolderRootNode = NodeService.CreateNew(this.MegaSdk, this.AppInformation, this.MegaSdk.getRootNode(), ContainerType.CloudDrive);

                this.CloudDrive.LoadChildNodes();

                if (this.RubbishBin.FolderRootNode == null)
                    this.RubbishBin.FolderRootNode = NodeService.CreateNew(this.MegaSdk, this.AppInformation, this.MegaSdk.getRubbishNode(), ContainerType.RubbishBin);

                this.RubbishBin.LoadChildNodes();                
            }); 
        }

        public void FetchNodes()
        {
            if (this.CloudDrive != null)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() => this.CloudDrive.SetEmptyContentTemplate(true));
                this.CloudDrive.CancelLoad();
            }

            if (this.RubbishBin != null)
            {
                Deployment.Current.Dispatcher.BeginInvoke(() => this.RubbishBin.SetEmptyContentTemplate(true));
                this.RubbishBin.CancelLoad();
            }

            var fetchNodesRequestListener = new FetchNodesRequestListener(this);
            this.MegaSdk.fetchNodes(fetchNodesRequestListener);
        }

        public bool SpecialNavigation()
        {
            if(_mainPage != null)
                return _mainPage.SpecialNavigation();

            return false;
        }

        public void SetImportMode()
        {
            if(_mainPage != null)
                _mainPage.SetImportMode();
        }

        public void ChangeMenu(FolderViewModel currentFolderViewModel, IList iconButtons, IList menuItems)
        {
            switch (currentFolderViewModel.CurrentDisplayMode)
            {
                case DriveDisplayMode.CloudDrive:
                {
                    this.TranslateAppBarItems(
                        iconButtons.Cast<ApplicationBarIconButton>().ToList(),
                        menuItems.Cast<ApplicationBarMenuItem>().ToList(),
                        new[] { UiResources.Upload, UiResources.AddFolder, UiResources.UI_OpenLink},
                        new []{ UiResources.Refresh, UiResources.Sort, UiResources.MultiSelect});
                    break;
                }
                case DriveDisplayMode.CopyOrMoveItem:
                {
                    this.TranslateAppBarItems(
                        iconButtons.Cast<ApplicationBarIconButton>().ToList(),
                        menuItems.Cast<ApplicationBarMenuItem>().ToList(),
                        new[] { UiResources.AddFolder, UiResources.Copy, UiResources.Move, UiResources.Cancel },
                        null);
                    break;
                }
                case DriveDisplayMode.MultiSelect:
                {
                    this.TranslateAppBarItems(
                        iconButtons.Cast<ApplicationBarIconButton>().ToList(),
                        menuItems.Cast<ApplicationBarMenuItem>().ToList(),
                        new[] { UiResources.Download, String.Format("{0}/{1}", UiResources.Copy, UiResources.Move), UiResources.Remove },
                        new[] { UiResources.SelectAll, UiResources.DeselectAll, UiResources.Cancel });
                    break;
                }
                case DriveDisplayMode.RubbishBin:
                {
                    this.TranslateAppBarItems(
                        iconButtons.Cast<ApplicationBarIconButton>().ToList(),
                        menuItems.Cast<ApplicationBarMenuItem>().ToList(),
                        new[] { UiResources.ClearRubbishBin },
                        new[] { UiResources.Refresh, UiResources.Sort, UiResources.MultiSelect });
                    break;
                }
                case DriveDisplayMode.ImportItem:
                {
                    this.TranslateAppBarItems(
                        iconButtons.Cast<ApplicationBarIconButton>().ToList(),
                        menuItems.Cast<ApplicationBarMenuItem>().ToList(),
                        new[] { UiResources.AddFolder, UiResources.Import, UiResources.Cancel },
                        null);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException("currentFolderViewModel");
            }
        }

        public void GetAccountDetails()
        {
            AccountService.GetAccountDetails();
            UpdateUserData();
        }

        public void CleanRubbishBin()
        {
            if (this.RubbishBin.ChildNodes.Count < 1) return;

            var customMessageDialog = new CustomMessageDialog(
                UiResources.ClearRubbishBin,
                AppMessages.CleanRubbishBinQuestion,
                App.AppInformation,
                MessageDialogButtons.OkCancel,
                MessageDialogImage.RubbishBin);

            customMessageDialog.OkOrYesButtonTapped += (sender, args) =>
            {
                MegaSdk.cleanRubbishBin(new CleanRubbishBinRequestListener());
            };

            customMessageDialog.ShowDialog();            
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Get a random visibility
        /// </summary>
        /// <param name="PercentOfTimes">Argument with the "%" of times that the visibility should be true</param>
        private Visibility GetRandomVisibility(int PercentOfTimes)
        {            
            if (new Random().Next(100) < PercentOfTimes)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        /// <summary>
        /// Timer for the visibility of the border/dialog to ask user to upgrade when is a free account
        /// </summary>
        /// <param name="milliseconds">Argument with the milliseconds that the visibility will be true and then will change to false</param>
        private async void TimerGetProAccountVisibility(int milliseconds)
        {            
            await Task.Delay(milliseconds);
            Deployment.Current.Dispatcher.BeginInvoke(() => _mainPage.ChangeGetProAccountBorderVisibility(Visibility.Collapsed));
        }

        /// <summary>
        /// Timer for the visibility of the warning border/dialog to ask user to upgrade because is going out of space
        /// </summary>
        /// <param name="milliseconds">Argument with the milliseconds that the visibility will be true and then will change to false</param>
        private async void TimerWarningOutOfSpaceVisibility(int milliseconds)
        {            
            await Task.Delay(milliseconds);
            Deployment.Current.Dispatcher.BeginInvoke(() => _mainPage.ChangeWarningOutOfSpaceBorderVisibility(Visibility.Collapsed));
        }

        private void UpgradeAccount(object obj)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                _mainPage.ChangeGetProAccountBorderVisibility(Visibility.Collapsed);
                _mainPage.ChangeWarningOutOfSpaceBorderVisibility(Visibility.Collapsed);
            });

            var extraParams = new Dictionary<string, string>(1);
            extraParams.Add("Pivot", "1");
            NavigateService.NavigateTo(typeof(MyAccountPage), NavigationParameter.Normal, extraParams);
        }

        private void CancelUpgradeAccount(object obj)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                _mainPage.ChangeGetProAccountBorderVisibility(Visibility.Collapsed);
                _mainPage.ChangeWarningOutOfSpaceBorderVisibility(Visibility.Collapsed);
            });
        }

        private void InitializeModel()
        {
            this.CloudDrive = new FolderViewModel(this.MegaSdk, this.AppInformation, ContainerType.CloudDrive);
            this.RubbishBin = new FolderViewModel(this.MegaSdk, this.AppInformation, ContainerType.RubbishBin);
            
            // The Cloud Drive is always the first active folder on initalization
            this.ActiveFolderView = this.CloudDrive;
        }

        private void OnGetAccountDetailsFinish(object sender, EventArgs e)
        {
            int usedSpacePercent;
            if ((AccountService.AccountDetails.TotalSpace > 0) && (AccountService.AccountDetails.UsedSpace > 0))
                usedSpacePercent = (int)(AccountService.AccountDetails.UsedSpace * 100 / AccountService.AccountDetails.TotalSpace);
            else
                usedSpacePercent = 0;

            // If used space is less than 95% and is a free account, the 5% of the times show a message to upgrade the account
            if (usedSpacePercent <= 95)
            {
                if (AccountService.AccountDetails.AccountType == MAccountType.ACCOUNT_TYPE_FREE)
                {
                    Task.Run(() =>
                    {
                        Visibility visibility = GetRandomVisibility(5);
                        Deployment.Current.Dispatcher.BeginInvoke(() => _mainPage.ChangeGetProAccountBorderVisibility(visibility));

                        if (visibility == Visibility.Visible)
                            this.TimerGetProAccountVisibility(30000);
                    });
                }
            }
            // Else show a warning message indicating the user is running out of space
            else
            {
                Task.Run(() =>
                {
                    Deployment.Current.Dispatcher.BeginInvoke(() => _mainPage.ChangeWarningOutOfSpaceBorderVisibility(Visibility.Visible));
                    this.TimerWarningOutOfSpaceVisibility(15000);
                });
            }
        }
        
        #endregion

        #region Properties
        
        private FolderViewModel _cloudDrive;
        public FolderViewModel CloudDrive
        {
            get { return _cloudDrive; }
            private set { SetField(ref _cloudDrive, value); }
        }

        private FolderViewModel _rubbishBin;
        public FolderViewModel RubbishBin
        {
            get { return _rubbishBin; }
            private set { SetField(ref _rubbishBin, value); }
        }

        private FolderViewModel _activeFolderView;
        public FolderViewModel ActiveFolderView
        {
            get { return _activeFolderView; }
            set { SetField(ref _activeFolderView, value); }
        }

        /// <summary>
        /// Property needed to store the source folder in a move/copy action 
        /// </summary>
        private FolderViewModel _sourceFolderView;
        public FolderViewModel SourceFolderView
        {
            get { return _sourceFolderView; }
            set { SetField(ref _sourceFolderView, value); }
        }

        #endregion
    }
}
