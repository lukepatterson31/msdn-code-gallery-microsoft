using DiagnosticsHelper;
using HttpTransportHelper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Networking.PushNotifications;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;



namespace Background
{
    // This class illustrates one way to setup a CCT enabled transport when 
    // a system event (such as network state change) occurs.
    public sealed class NetworkChangeTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            if (taskInstance == null)
            {
                Diag.DebugPrint("NetworkChangeTask: taskInstance was null");
                return;
            }

            // In this example, the channel name has been hardcoded to lookup the property bag
            // for any previous contexts. The channel name may be used in more sophisticated ways
            // in case an app has multiple controlchanneltrigger objects.
            string channelId = "channelOne";
            if (((IDictionary<string, object>)CoreApplication.Properties).ContainsKey(channelId))
            {
                try
                {
                    AppContext appContext = null;
                    lock (CoreApplication.Properties)
                    {
                        appContext = ((IDictionary<string, object>)CoreApplication.Properties)[channelId] as AppContext;
                    }
                    if (appContext != null && appContext.CommInstance != null)
                    {
                        CommModule commInstance = appContext.CommInstance;

                        // Clear any existing channels, sockets etc.
                        commInstance.Reset();

                        // Create CCT enabled transport.
                        commInstance.SetupTransport(commInstance.serverUri);
                    }
                }
                catch (Exception exp)
                {
                    Diag.DebugPrint("Registering with CCT failed with: " + exp.ToString());
                }
            }
            else
            {
                Diag.DebugPrint("Cannot find AppContext key channelOne");
            }

            Diag.DebugPrint("Systemtask - " + taskInstance.Task.Name + " finished.");
        }
    }

    public sealed class KATask : IBackgroundTask
    {

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            if (taskInstance == null)
            {
                Diag.DebugPrint("KATask: taskInstance was null");
                return;
            }

            Diag.DebugPrint("KATask " + taskInstance.Task.Name + " Starting...");

            // Use the ControlChannelTriggerEventDetails object to derive the context for this background task.
            // The context happens to be the channelId that apps can use to differentiate between
            // various instances of the channel..
            var channelEventArgs = (IControlChannelTriggerEventDetails)taskInstance.TriggerDetails;

            ControlChannelTrigger channel = channelEventArgs.ControlChannelTrigger;
            if (channel == null)
            {
                Diag.DebugPrint("Channel object may have been deleted.");
                return;
            }

            string channelId = channel.ControlChannelTriggerId;

            if (((IDictionary<string, object>)CoreApplication.Properties).ContainsKey(channelId))
            {
                try
                {
                    AppContext appContext = null;
                    lock (CoreApplication.Properties)
                    {
                        appContext = ((IDictionary<string, object>)CoreApplication.Properties)[channelId] as AppContext;
                    }
                    if (appContext != null && appContext.CommInstance != null)
                    {
                        CommModule commModule = appContext.CommInstance;
                        Task<bool> result;
                        lock (commModule)
                        {
                            result = commModule.SendKAMessage();
                        }
                        if (result != null && result.Result == true)
                        {
                            // Call FlushTransport on the channel object to ensure
                            // the packet is out of the process and on the wire before
                            // exiting the keepalive task.
                            commModule.channel.FlushTransport();
                        }
                        else
                        {
                            // Socket has not been set up. reconnect the transport and plug in to the controlchanneltrigger object.
                            commModule.Reset();

                            // Create CCT enabled transport.
                            commModule.SetupTransport(commModule.serverUri);
                        }
                    }
                }
                catch (Exception exp)
                {
                    Diag.DebugPrint("KA Task failed with: " + exp.ToString());
                }
            }
            else
            {
                Diag.DebugPrint("Cannot find AppContext key channelOne");
            }

            Diag.DebugPrint("KATask " + taskInstance.Task.Name + " finished.");
        }
    }

    public sealed class PushNotifyTask : IBackgroundTask
    {
        void InvokeSimpleToast(string messageReceived)
        {
            // GetTemplateContent returns a Windows.Data.Xml.Dom.XmlDocument object containing
            // the toast XML
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText02);

            // You can use the methods from the XML document to specify all of the
            // required parameters for the toast
            XmlNodeList stringElements = toastXml.GetElementsByTagName("text");
            stringElements.Item(0).AppendChild(toastXml.CreateTextNode("Push notification message:"));
            stringElements.Item(1).AppendChild(toastXml.CreateTextNode(messageReceived));

            // Audio tags are not included by default, so must be added to the
            // XML document
            string audioSrc = "ms-winsoundevent:Notification.IM";
            XmlElement audioElement = toastXml.CreateElement("audio");
            audioElement.SetAttribute("src", audioSrc);

            IXmlNode toastNode = toastXml.SelectSingleNode("/toast");
            toastNode.AppendChild(audioElement);

            // Create a toast from the Xml, then create a ToastNotifier object to show
            // the toast
            ToastNotification toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        public void Run(Windows.ApplicationModel.Background.IBackgroundTaskInstance taskInstance)
        {
            if (taskInstance == null)
            {
                Diag.DebugPrint("PushNotifyTask: taskInstance was null");
                return;
            }

            Diag.DebugPrint("PushNotifyTask " + taskInstance.Task.Name + " Starting...");

            // Use the ControlChannelTriggerEventDetails object to derive the context for this background task.
            // The context happens to be the channelId that apps can use to differentiate between
            // various instances of the channel..
            var channelEventArgs = taskInstance.TriggerDetails as IControlChannelTriggerEventDetails;

            ControlChannelTrigger channel = channelEventArgs.ControlChannelTrigger;
            if (channel == null)
            {
                Diag.DebugPrint("Channel object may have been deleted.");
                return;
            }

            string channelId = channel.ControlChannelTriggerId;

            if (((IDictionary<string, object>)CoreApplication.Properties).ContainsKey(channelId))
            {
                try
                {
                    string messageReceived = "PushNotification Received";
                    AppContext appContext = null;
                    lock (CoreApplication.Properties)
                    {
                        appContext = ((IDictionary<string, object>)CoreApplication.Properties)[channelId] as AppContext;
                    }
                    
                    // Read the HttpResponse from the  server
                    Task<HttpResponseMessage> httpResponseTask;
                    bool result = AppContext.messageQueue.TryDequeue(out httpResponseTask);
                    if (result)
                    {
                        messageReceived = appContext.CommInstance.ReadResponse(httpResponseTask);
                        Diag.DebugPrint("PushNotification Message: " + messageReceived);
                        InvokeSimpleToast(messageReceived);
                    }
                    else
                    {
                        Diag.DebugPrint("There was no message for this push notification: ");
                    }
                }
                catch (Exception exp)
                {
                    Diag.DebugPrint("PushNotifyTask failed with: " + exp.Message);
                }
            }
            else
            {
                Diag.DebugPrint("Cannot find AppContext key " + channelId);
            }

            Diag.DebugPrint("PushNotifyTask " + taskInstance.Task.Name + " finished.");
        }
    }


}
