﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using mega;
using MegaApp.Classes;
using MegaApp.Enums;
using MegaApp.Extensions;
using MegaApp.Models;
using MegaApp.Pages;
using MegaApp.Resources;
using MegaApp.Services;

namespace MegaApp.MegaApi
{
    class FetchNodesRequestListener : BaseRequestListener
    {
        private readonly MainPageViewModel _mainPageViewModel;
        private readonly ulong? _shortCutHandle;
        public FetchNodesRequestListener(MainPageViewModel mainPageViewModel, ulong? shortCutHandle = null)
        {
            this._mainPageViewModel = mainPageViewModel;
            this._shortCutHandle = shortCutHandle;
        }

        #region Base Properties

        protected override string ProgressMessage
        {
            get { return ProgressMessages.FetchingNodes; }
        }

        protected override bool ShowProgressMessage
        {
            get { return true; }
        }

        protected override string ErrorMessage
        {
            get { return AppMessages.FetchingNodesFailed; }
        }

        protected override string ErrorMessageTitle
        {
            get { return AppMessages.FetchingNodesFailed_Title; }
        }

        protected override bool ShowErrorMessage
        {
            get { return true; }
        }

        protected override string SuccessMessage
        {
            get { throw new NotImplementedException(); }
        }

        protected override string SuccessMessageTitle
        {
            get { throw new NotImplementedException(); }
        }

        protected override bool ShowSuccesMessage
        {
            get { return false; }
        }

        protected override bool NavigateOnSucces
        {
            get { return false; }
        }

        protected override bool ActionOnSucces
        {
            get { return true; }
        }

        protected override Type NavigateToPage
        {
            get { throw new NotImplementedException(); }
        }

        protected override NavigationParameter NavigationParameter
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region Override Methods

        protected override void OnSuccesAction(MegaSDK api, MRequest request)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                // Enable appbar buttons
                _mainPageViewModel.SetCommandStatus(true);
            });

            // If the user is trying to open a shortcut
            if (_shortCutHandle.HasValue)
            {
                bool shortCutError = false;

                MNode shortCutMegaNode = api.getNodeByHandle(_shortCutHandle.Value);
                if (shortCutMegaNode != null)
                {
                    // Looking for the absolute parent of the shortcut node to see the type
                    MNode parentNode;
                    MNode absoluteParentNode = shortCutMegaNode;
                    while ((parentNode = api.getParentNode(absoluteParentNode)) != null)
                        absoluteParentNode = parentNode;

                    if (absoluteParentNode.getType() == MNodeType.TYPE_ROOT)
                    {
                        var newRootNode = NodeService.CreateNew(api, _mainPageViewModel.AppInformation, shortCutMegaNode);
                        var autoResetEvent = new AutoResetEvent(false);
                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            _mainPageViewModel.ActiveFolderView.FolderRootNode = newRootNode;
                            autoResetEvent.Set();
                        });
                        autoResetEvent.WaitOne();
                    }
                    else shortCutError = true;
                }
                else shortCutError = true;

                if(shortCutError)
                {
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        new CustomMessageDialog(
                                AppMessages.ShortCutFailed_Title,
                                AppMessages.ShortCutFailed,
                                App.AppInformation,
                                MessageDialogButtons.Ok).ShowDialog();
                    });
                }
            }
            else
            {
                var cloudDriveRootNode = _mainPageViewModel.CloudDrive.FolderRootNode ??
                    NodeService.CreateNew(api, _mainPageViewModel.AppInformation, api.getRootNode());
                var rubbishBinRootNode = _mainPageViewModel.RubbishBin.FolderRootNode ??
                        NodeService.CreateNew(api, _mainPageViewModel.AppInformation, api.getRubbishNode());

                var autoResetEvent = new AutoResetEvent(false);
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    _mainPageViewModel.CloudDrive.FolderRootNode = cloudDriveRootNode;
                    _mainPageViewModel.RubbishBin.FolderRootNode = rubbishBinRootNode;
                    autoResetEvent.Set();
                });
                autoResetEvent.WaitOne();
            }
                        
            _mainPageViewModel.LoadFolders();

            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                // If is a newly activated account, navigates to the upgrade account page
                if (App.AppInformation.IsNewlyActivatedAccount)                                    
                    NavigateService.NavigateTo(typeof(MyAccountPage), NavigationParameter.Normal, new Dictionary<string, string> { { "Pivot", "1" } });
                // If is the first login, navigates to the camera upload service config page
                else if (SettingsService.LoadSetting<bool>(SettingsResources.CameraUploadsFirstInit, true))
                    NavigateService.NavigateTo(typeof(InitCameraUploadsPage), NavigationParameter.Normal);
                else if (App.AppInformation.IsStartedAsAutoUpload)                
                    NavigateService.NavigateTo(typeof(SettingsPage), NavigationParameter.AutoCameraUpload);
            });
        }

        public override void onRequestStart(MegaSDK api, MRequest request)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                // Disable appbar buttons
                _mainPageViewModel.SetCommandStatus(false);

                ProgressService.SetProgressIndicator(true,
                   String.Format(ProgressMessages.FetchingNodes, request.getTransferredBytes().ToStringAndSuffix()));
            });
        }

        public override void onRequestUpdate(MegaSDK api, MRequest request)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                ProgressService.ChangeProgressBarBackgroundColor((Color)Application.Current.Resources["PhoneChromeColor"]);
                ProgressService.SetProgressIndicator(true, String.Format(ProgressMessages.FetchingNodes,
                    request.getTransferredBytes().ToStringAndSuffix()));
            });
            
            if (AppMemoryController.IsThresholdExceeded(75UL.FromMBToBytes()))
            {
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    new CustomMessageDialog(
                            AppMessages.MemoryLimitError_Title,
                            AppMessages.MemoryLimitError,
                            App.AppInformation,
                            MessageDialogButtons.Ok).ShowDialog();
                    Application.Current.Terminate();
                });

            }
        }

        #endregion
    }
}
