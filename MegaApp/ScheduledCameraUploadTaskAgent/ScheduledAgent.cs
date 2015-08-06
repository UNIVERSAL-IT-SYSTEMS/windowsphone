﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Windows.Storage;
using mega;
using MegaApp.MegaApi;
using Microsoft.Phone.Info;
using Microsoft.Phone.Scheduler;
using Microsoft.Xna.Framework.Media;

namespace ScheduledCameraUploadTaskAgent
{
    public class ScheduledAgent : ScheduledTaskAgent
    {
        private static MegaSDK MegaSdk { get; set; }
        /// <remarks>
        /// ScheduledAgent constructor, initializes the UnhandledException handler
        /// </remarks>
        static ScheduledAgent()
        {
            // Subscribe to the managed exception handler
            Deployment.Current.Dispatcher.BeginInvoke(delegate
            {
                Application.Current.UnhandledException += UnhandledException;
            });
        }

        /// Code to execute on Unhandled Exceptions
        private static void UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                Debugger.Break();
            }
        }

        /// <summary>
        /// Agent that runs a scheduled task
        /// </summary>
        /// <param name="task">
        /// The invoked task
        /// </param>
        /// <remarks>
        /// This method is called when a periodic or resource intensive task is invoked
        /// </remarks>
        protected override void OnInvoke(ScheduledTask task)
        {
            InitializeSdk();
        }

        private void InitializeSdk()
        {
            // Initialize MegaSDK 
            MegaSdk = new MegaSDK("Z5dGhQhL", String.Format("{0}/{1}/{2}",
                GetBackgroundAgentUserAgent(), DeviceStatus.DeviceManufacturer, DeviceStatus.DeviceName),
                ApplicationData.Current.LocalFolder.Path, new MegaRandomNumberProvider());

            FastLogin();
        }

        private void FastLogin()
        {
            var fastLoginListener = new MegaRequestListener();
            fastLoginListener.RequestFinished += (sender, args) =>
            {
                if (!args.Succeeded)
                {
                    this.Abort();
                }
                else
                {
                    FetchNodes();
                }
            };

            MegaSdk.fastLogin(SettingsService.LoadSettingFromFile<string>("{85DBF3E5-51E8-40BB-968C-8857B4FC6EF4}"),
                fastLoginListener);
        }

        private void FetchNodes()
        {
            var fetchNodesListener = new MegaRequestListener();
            fetchNodesListener.RequestFinished += (sender, args) =>
            {
                if (!args.Succeeded)
                {
                    this.Abort();
                }
                else
                {
                    Upload();
                }
            };

            MegaSdk.fetchNodes(fetchNodesListener);
        }
        
        private async void Upload()
        {
            var lastUploadDate = SettingsService.LoadSetting("LastUploadDate", DateTime.MinValue);

            using (var mediaLibrary = new MediaLibrary())
            {
                foreach (var picture in mediaLibrary.Pictures)
                {
                    if(picture.Date <= lastUploadDate) continue;

                    var imageStream = picture.GetImage();
                    imageStream.Position = 0;

                    string newFilePath = Path.Combine(
                        Path.Combine(ApplicationData.Current.LocalFolder.Path, "uploads\\"), picture.Name);

                    using (var fs = new FileStream(newFilePath, FileMode.Create))
                    {
                        await imageStream.CopyToAsync(fs);
                        await fs.FlushAsync();
                        fs.Close();
                    }

                    string fingerprint = MegaSdk.getFileFingerprint(newFilePath);

                    var mNode = MegaSdk.getNodeByFingerprint(fingerprint);

                    if (mNode == null)
                    {
                        lastUploadDate = picture.Date;
                        var transferListener = new MegaTransferListener();
                        transferListener.TransferFinished += (sender, args) =>
                        {
                            if(args.Succeeded)
                                SettingsService.SaveSetting("LastUploadDate", lastUploadDate);
                            File.Delete(newFilePath);
                            Upload();
                        };

                        MegaSdk.startUpload(newFilePath, await GetCameraUploadsNode(), transferListener);
                        break;
                    }
                    
                    File.Delete(newFilePath);
                }

            }

        }
       
        private async Task<MNode> GetCameraUploadsNode()
        {
            var rootNode = MegaSdk.getRootNode();
            if (rootNode == null) return null;

            var cameraUploadNode = FindCameraUploadNode(rootNode);

            if(cameraUploadNode == null)
                MegaSdk.createFolder("Camera Uploads", rootNode);

            await Task.Delay(5000);

            return FindCameraUploadNode(rootNode); ;
        }

        private MNode FindCameraUploadNode(MNode rootNode)
        {
            var childs = MegaSdk.getChildren(rootNode);

            for (int x = 0; x < childs.size(); x++)
            {
                var node = childs.get(x);
                if (node.getType() != MNodeType.TYPE_FOLDER) continue;
                if (!node.getName().ToLower().Equals("camera uploads")) continue;
                return node;
            }

            return null;
        }

        private static string GetBackgroundAgentUserAgent()
        {
            return String.Format("MEGAWindowsPhoneBackgroundAgent/{0}", "1.0.0.0");
        }
    }
}